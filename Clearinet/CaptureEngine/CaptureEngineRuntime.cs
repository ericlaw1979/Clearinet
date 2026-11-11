using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Clearinet.CaptureEngine
{
    internal sealed class CaptureEngineRuntime : IDisposable
    {
        private readonly object _syncRoot = new object();
        private readonly CaptureEngineOptions _options;
        private readonly Func<ICaptureEngineListener> _listenerFactory;
        private ICaptureEngineListener _listener;

        internal CaptureEngineRuntime(CaptureEngineOptions options)
            : this(options, null)
        {
        }

        internal CaptureEngineRuntime(CaptureEngineOptions options, Func<ICaptureEngineListener> listenerFactory)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _listenerFactory = listenerFactory;
        }

        public bool IsRunning { get; private set; }

        public void Start()
        {
            lock (_syncRoot)
            {
                if (IsRunning) return;

                if (_listener == null)
                {
                    _listener = _listenerFactory != null
                        ? _listenerFactory()
                        : new TcpCaptureEngineListener(_options, new MapSocketToProcess());
                }

                _listener.Start();
                IsRunning = true;
                CApp.Log.Log($"CaptureEngine started at {_options.ListenAddress}:{_options.ListenPort}.");
            }
        }

        public void Stop()
        {
            lock (_syncRoot)
            {
                if (!IsRunning) return;
                _listener?.Stop();
                IsRunning = false;
                CApp.Log.Log("CaptureEngine stopped.");
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                try
                {
                    Stop();
                }
                finally
                {
                    _listener?.Dispose();
                    _listener = null;
                }
            }
        }
    }

    internal sealed class TcpCaptureEngineListener : ICaptureEngineListener
    {
        private const int MaxHeaderBytes = 64 * 1024;                   // TODO: Configurable max.

        private readonly CaptureEngineOptions _options;
        private readonly MapSocketToProcess _mapper;
        private readonly ICertificateProvider _certificateProvider;
        private readonly ISocketPool _socketPool;
        private readonly ConcurrentDictionary<Guid, ClientPipe> _clientPipeReuseQueue =
            new ConcurrentDictionary<Guid, ClientPipe>();
        private readonly object _syncRoot = new object();
        private GigWorker.gig _socketPoolJanitorGig;
        private CancellationTokenSource _cts;
        private TcpListener _listener;
        private Task _acceptLoop;

        internal TcpCaptureEngineListener(CaptureEngineOptions options, MapSocketToProcess mapper)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _certificateProvider = new CertEnrollCertificateProvider();
            _socketPool = new SocketPool(
                TimeSpan.FromMilliseconds(CaptureEnginePrefs.GetSocketPoolIdleMs()),
                TimeSpan.FromMilliseconds(CaptureEnginePrefs.GetSocketPoolMaxAgeMs()));
        }

        public bool IsRunning { get; private set; }

        public void Start()
        {
            lock (_syncRoot)
            {
                if (IsRunning) return;

                _cts = new CancellationTokenSource();

                // ISSUE: we need to respect options for IPv6 and AllowRemote here.

                _listener = new TcpListener(_options.ListenAddress, _options.ListenPort);
                if (_listener.Server.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    _listener.Server.DualMode = _options.ListenAddress.Equals(IPAddress.IPv6Any);
                }
                _listener.Start();
                EnsureSocketPoolJanitor();

                IsRunning = true;
                _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
            }
        }

        public void Stop()
        {
            lock (_syncRoot)
            {
                if (!IsRunning) return;
                IsRunning = false;

                try
                {
                    _cts.Cancel();
                    _listener.Stop();
                    _acceptLoop?.Wait(TimeSpan.FromSeconds(2));             // TODO: Configurable timeout. 
                    if (_socketPoolJanitorGig != null)
                    {
                        CApp.gwHoneyDew.cancelGig(_socketPoolJanitorGig);
                        _socketPoolJanitorGig = null;
                    }
                    foreach (ClientPipe clientPipe in _clientPipeReuseQueue.Values)
                    {
                        clientPipe.Dispose();
                    }
                    _clientPipeReuseQueue.Clear();
                    _socketPool?.Dispose();
                }
                catch (Exception eX)
                {
                    CApp.Log.Log($"! CaptureEngine listener stop exception: {eX.Message}");
                }
                finally
                {
                    _cts?.Dispose();
                    _cts = null;
                    _listener = null;
                    _acceptLoop = null;
                }
            }
        }

        private void EnsureSocketPoolJanitor()
        {
            if (_socketPoolJanitorGig != null) return;
            uint interval = (uint)Math.Max(5000,
                Math.Min(CaptureEnginePrefs.GetSocketPoolIdleMs(), CaptureEnginePrefs.GetSocketPoolMaxAgeMs()) / 2);
            _socketPoolJanitorGig = CApp.gwHoneyDew.createGig("CaptureEngine.SocketPoolJanitor",
                () => _socketPool.PurgeExpired(DateTime.UtcNow), interval);
        }
        public void Dispose()
        {
            Stop();
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client = null;
                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    _ = Task.Run(() => HandleClientAsync(client, token), token);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception eX)
                {
                    CApp.Log.Log($"! CaptureEngine accept failed: {eX.Message}");
                    client?.Dispose();
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            using (client)
            {
                client.NoDelay = true;
                IPEndPoint remote = client.Client.RemoteEndPoint as IPEndPoint;
                string processIdentity = remote == null ? string.Empty : _mapper.MapSocketToOriginatingProcess(remote);
                var clientPipe = new ClientPipe(client.Client, client.GetStream(), processIdentity);
                using (clientPipe)
                {
                    bool waitForNextRequest = false;
                    while (!token.IsCancellationRequested)
                    {
                        Exchange exchange = null;
                        try
                        {
                            if (waitForNextRequest && !_clientPipeReuseQueue.TryAdd(clientPipe.Id, clientPipe))
                                throw new InvalidOperationException("Client pipe was already registered for reuse.");

                            (HTTPRequestHeaders requestHeaders, byte[] requestBody) requestData;
                            try
                            {
                                requestData = waitForNextRequest
                                    ? await ReadNextClientRequestAsync(clientPipe, token).ConfigureAwait(false)
                                    : await clientPipe.Reader.ReadRequestAsync(token).ConfigureAwait(false);
                            }
                            finally
                            {
                                if (waitForNextRequest) _clientPipeReuseQueue.TryRemove(clientPipe.Id, out _);
                            }

                            waitForNextRequest = false;
                            if (requestData.requestHeaders == null) return;

                            var context = new CaptureConnectionContext(clientPipe.Socket)
                            {
                                LocalProcessDescriptor = processIdentity
                            };
                            exchange = BuildExchange(requestData.requestHeaders, requestData.requestBody, context);
                            context.Exchange = exchange;
                            UpdateUIForExchange(exchange);

                            if (ExchangeTargetsReflector(exchange))
                            {
                                await CreateReflectorPage(exchange, clientPipe.Stream, token).ConfigureAwait(false);
                                return;
                            }

                            if (exchange.HTTPMethodIs("CONNECT"))
                            {
                                Stream tunnelStream = clientPipe.Reader.DetachUnreadStream();
                                if (ShouldDecryptTunnel(exchange))
                                {
                                    exchange["x-connect-decrypt-eligible"] = "true";
                                    await HandleMitmConnectTunnelAsync(exchange, client, tunnelStream, clientPipe, token).ConfigureAwait(false);
                                }
                                else
                                {
                                    await HandleBlindConnectTunnelAsync(exchange, client, tunnelStream, token).ConfigureAwait(false);
                                }
                                return;
                            }

                            waitForNextRequest = await ForwardUsingServerPipeAsync(exchange, clientPipe, token).ConfigureAwait(false);
                            if (!waitForNextRequest || !clientPipe.IsConnected) return;
                        }
                        catch (Exception eX)
                        {
                            _clientPipeReuseQueue.TryRemove(clientPipe.Id, out _);
                            await HandleCaptureFailureAsync(exchange, clientPipe.Stream, eX, token).ConfigureAwait(false);
                            return;
                        }
                    }
                }
            }
        }

        private async Task<(HTTPRequestHeaders requestHeaders, byte[] requestBody)> ReadNextClientRequestAsync(
            ClientPipe clientPipe, CancellationToken token)
        {
            Task<(HTTPRequestHeaders requestHeaders, byte[] requestBody)> requestTask = clientPipe.Reader.ReadRequestAsync(token);
            Task timeoutTask = Task.Delay(TimeSpan.FromMinutes(1), token);
            Task completed = await Task.WhenAny(requestTask, timeoutTask).ConfigureAwait(false);
            if (completed == requestTask) return await requestTask.ConfigureAwait(false);

            clientPipe.Dispose();
            try
            {
                await requestTask.ConfigureAwait(false);
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (OperationCanceledException) { }
            return (null, Array.Empty<byte>());
        }

        private async Task<bool> ForwardUsingServerPipeAsync(Exchange exchange, ClientPipe clientPipe, CancellationToken token)
        {
            if (!TryResolveRequestTarget(exchange.RequestHeaders, out string host, out int port, out bool isTls))
                throw new InvalidDataException("The request does not contain a valid HTTP server authority.");

            string processIdentity = clientPipe.ProcessIdentity;
            var key = new ServerConnectionKey(host, port, isTls);
            ServerPipe serverPipe = null;
            bool returnServerPipe = false;
            try
            {
                if (_socketPool.TryRent(key, processIdentity, clientPipe.Id, out serverPipe))
                {
                    exchange.BitFlags |= ExchangeFlags.ServerPipeReused;
                    exchange["x-serverpool-reused"] = "true";
                }
                else
                {
                    serverPipe = await ConnectServerPipeAsync(host, port, isTls, processIdentity, clientPipe.Id,
                        CaptureEnginePrefs.GetServerPipeBindingPolicy()).ConfigureAwait(false);
                }

                exchange.Timers.ServerConnected = DateTime.Now;
                HTTPRequestHeaders upstreamRequest = exchange.RequestHeaders.Clone();
                PrepareRequestForServer(upstreamRequest, exchange.RequestBody ?? Array.Empty<byte>());
                await WriteHttpRequestAsync(serverPipe.Stream, upstreamRequest,
                    exchange.RequestBody ?? Array.Empty<byte>(), token).ConfigureAwait(false);

                var response = await serverPipe.Reader.ReadResponseAsync(upstreamRequest.HTTPMethod, token).ConfigureAwait(false);
                if (response.responseHeaders == null) throw new IOException("The server closed without returning an HTTP response.");

                exchange.ResponseHeaders = response.responseHeaders;
                exchange.ResponseBody = response.responseBody;
                exchange.Timers.ServerDoneResponse = DateTime.Now;

                returnServerPipe = response.reusable && ShouldKeepAlive(upstreamRequest, response.responseHeaders) && serverPipe.IsConnected;
                if (!response.reusable && !exchange.ResponseHeaders.Exists("Connection"))
                    exchange.ResponseHeaders["Connection"] = "close";

                exchange.state = ExchangeState.AutoTamperResponseBefore;
                byte[] clientBody = exchange.ResponseBody ?? Array.Empty<byte>();
                NormalizeResponseLength(exchange.RequestHeaders, exchange.ResponseHeaders, clientBody);
                exchange.Timers.ClientBeginResponse = DateTime.Now;
                await WriteHttpResponseAsync(clientPipe.Stream, exchange.ResponseHeaders,
                    clientBody, token).ConfigureAwait(false);
                exchange.Timers.ClientDoneResponse = DateTime.Now;
                exchange.state = ExchangeState.Done;
                UpdateUIForExchange(exchange);
                return response.reusable && ShouldKeepAlive(exchange.RequestHeaders, exchange.ResponseHeaders) && clientPipe.IsConnected;
            }
            finally
            {
                if (serverPipe != null)
                {
                    if (returnServerPipe)
                    {
                        _socketPool.Return(key, serverPipe, DateTime.UtcNow);
                        exchange["x-serverpool-returned"] = "true";
                    }
                    else
                    {
                        serverPipe.Dispose();
                    }
                }
            }
        }

        private static bool TryResolveRequestTarget(HTTPRequestHeaders headers, out string host, out int port, out bool isTls)
        {
            host = null;
            port = 0;
            isTls = false;
            if (headers == null) return false;

            string scheme = headers.UriScheme;
            string path = headers.RequestPath ?? string.Empty;
            Uri target;
            bool absoluteForm = path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            if (absoluteForm)
            {
                if (!Uri.TryCreate(path, UriKind.Absolute, out target)) return false;
                scheme = target.Scheme;
                headers.RequestPath = string.IsNullOrEmpty(target.PathAndQuery) ? "/" : target.PathAndQuery;
            }
            else
            {
                if (path.Contains("://")) return false;
                string authority = headers["Host"];
                if (!Uri.TryCreate(scheme + "://" + authority + "/", UriKind.Absolute, out target)) return false;
            }

            if (!target.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                !target.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.IsNullOrWhiteSpace(target.Host) || target.UserInfo.Length != 0) return false;

            host = target.DnsSafeHost;
            port = target.Port;
            isTls = target.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
            headers.UriScheme = target.Scheme;
            headers["Host"] = target.Authority;
            if (string.IsNullOrEmpty(headers.RequestPath)) headers.RequestPath = "/";
            return port > 0 && port <= IPEndPoint.MaxPort;
        }

        /// <summary>
        /// Remove any headers the client (likely) added targeting me, the proxy.
        /// 
        /// Technically, a "Connection" header is a hop-by-hop header, and should not be
        /// forwarded to the next hop. But I don't think we used to remove those.
        /// 
        /// </summary>
        private static void PrepareRequestForServer(HTTPRequestHeaders headers, byte[] body)
        {
            // ISSUE: We should only remove proxy-authorization if there's not an upstream
            // gateway that asked for it. And we probably want to convert a Proxy-Connection
            // header to a Connection header.
            headers.Remove("Proxy-Authorization", "Proxy-Connection");


            /*
            bool closeRequested = HasHeaderToken(headers, "Connection", "close") ||
                HasHeaderToken(headers, "Proxy-Connection", "close");
            var connectionOptions = new List<string>();
            foreach (HTTPHeaderItem header in headers)
            {
                if (!header.Name.Equals("Connection", StringComparison.OrdinalIgnoreCase) &&
                    !header.Name.Equals("Proxy-Connection", StringComparison.OrdinalIgnoreCase)) continue;
                string[] options = header.Value.Split(',');
                for (int i = 0; i < options.Length; i++) connectionOptions.Add(options[i].Trim());
            }*/

            /*
            for (int i = 0; i < connectionOptions.Count; i++)
            {
                string option = connectionOptions[i];
                if (option.Length > 0 && !option.Equals("Connection", StringComparison.OrdinalIgnoreCase))
                    headers.Remove(option);
            }
            headers.Remove("Connection");
            if (closeRequested) headers["Connection"] = "close";
            else headers["Connection"] = "keep-alive";
            if (body.Length > 0 && !headers.Exists("Content-Length") && !headers.Exists("Transfer-Encoding"))
                headers["Content-Length"] = body.Length.ToString(CultureInfo.InvariantCulture);
            */
        }

        private static void PrepareResponseForClient(HTTPResponseHeaders headers)
        {
            // If we got a 401 from the server, ensure the client knows that we
            // are smart enough to bind their requesting socket to the server to
            // avoid reusing it for a different client request that might not have the same credentials.
            if (401 == headers.StatusCode)
            {
                if (headers.ExistsAndContains("WWW-Authenticate", "Negotiate") ||
                   headers.ExistsAndContains("WWW-Authenticate", "NTLM"))
                {
                    headers.Add("Proxy-Support", "Session-Based-Authentication");
                }
                return;
            }

            if (407 == headers.StatusCode)
            {
                // SECURITY: TODO: If we got a 407 from upstream on an unencrypted
                // connection, then we should not forward it to the client because
                // it's likely to think that WE are the proxy asking for credentials.
                //
                // I can't recall whether we should allow these to flow through if
                // the upstream connection is over HTTPS.
            }


            /*
            bool closeRequested = HasHeaderToken(headers, "Connection", "close") ||
                HasHeaderToken(headers, "Proxy-Connection", "close");
            var connectionOptions = new List<string>();
            foreach (HTTPHeaderItem header in headers)
            {
                if (!header.Name.Equals("Connection", StringComparison.OrdinalIgnoreCase) &&
                    !header.Name.Equals("Proxy-Connection", StringComparison.OrdinalIgnoreCase)) continue;
                string[] options = header.Value.Split(',');
                for (int i = 0; i < options.Length; i++) connectionOptions.Add(options[i].Trim());
            }*/

            /*
            for (int i = 0; i < connectionOptions.Count; i++)
            {
                string option = connectionOptions[i];
                if (option.Length > 0 && !option.Equals("Connection", StringComparison.OrdinalIgnoreCase))
                    headers.Remove(option);
            }
            headers.Remove("Connection");
            if (closeRequested) headers["Connection"] = "close";
            else headers["Connection"] = "keep-alive";
            if (body.Length > 0 && !headers.Exists("Content-Length") && !headers.Exists("Transfer-Encoding"))
                headers["Content-Length"] = body.Length.ToString(CultureInfo.InvariantCulture);
            */
        }

        private async Task<ServerPipe> ConnectServerPipeAsync(string host, int port, bool isTls,
            string processIdentity, Guid clientPipeId, ServerPipeBindingPolicy bindingPolicy)
        {
            Socket socket = await ConnectSocketAsync(host, port).ConfigureAwait(false);
            NetworkStream networkStream = new NetworkStream(socket, false);
            Stream serverStream = networkStream;
            try
            {
                if (isTls)
                {
                    var tls = new SslStream(networkStream, false,
                        (sender, certificate, chain, errors) => callbackValidateServerCertificate(host, sender, certificate, chain, errors));
                    serverStream = tls;
                    await tls.AuthenticateAsClientAsync(host, null, SslProtocols.Tls12 | SslProtocols.Tls13, false).ConfigureAwait(false);
                }
                return new ServerPipe(socket, serverStream, bindingPolicy, processIdentity, clientPipeId);
            }
            catch
            {
                serverStream.Dispose();
                socket.Dispose();
                throw;
            }
        }

        private static async Task HandleCaptureFailureAsync(Exchange exchange, Stream clientStream, Exception eX, CancellationToken token)
        {
            CApp.Log.Log($"! CaptureEngine client handling error: {eX.GetType().Name}: {eX.Message}");

            if (exchange == null)
            {
                return;
            }

            int statusCode = 502;
            string statusText = "Capture Engine Failure";
            if (eX is AuthenticationException)
            {
                statusCode = 525;
                statusText = "TLS Handshake Failed";
            }
            else if (eX is InvalidDataException || eX is FormatException)
            {
                statusCode = 400;
                statusText = "Protocol Parse Failure";
            }
            else if (eX is SocketException)
            {
                statusCode = 504;
                statusText = "Upstream Connection Failed";
            }

            exchange.ResponseHeaders = new HTTPResponseHeaders(statusCode, statusText, new[]
            {
                "Content-Type: text/plain; charset=utf-8",
                "Connection: close",
                "X-CaptureEngine-Error: " + eX.GetType().Name
            });
            exchange.utilSetResponseBody("Capture engine error: " + eX.Message);
            exchange.BitFlags |= ExchangeFlags.ResponseGeneratedByClearinet;

            try
            {
                if (clientStream != null && clientStream.CanWrite)
                {
                    await WriteHttpResponseAsync(clientStream, exchange.ResponseHeaders, exchange.ResponseBody ?? Array.Empty<byte>(), token).ConfigureAwait(false);
                }
            }
            catch
            {
                // Best effort response generation.
            }

            exchange.state = ExchangeState.Aborted;
            UpdateUIForExchange(exchange);
        }

        private static async Task<(HTTPRequestHeaders requestHeaders, byte[] requestBody)> ReadHttpRequestAsync(Stream stream, CancellationToken token)
        {
            byte[] readBuffer = new byte[4096];
            using (MemoryStream ms = new MemoryStream())
            {
                while (ms.Length < MaxHeaderBytes)
                {
                    int bytesRead = await stream.ReadAsync(readBuffer, 0, readBuffer.Length, token).ConfigureAwait(false);
                    if (bytesRead <= 0) break;

                    ms.Write(readBuffer, 0, bytesRead);
                    byte[] current = ms.ToArray();
                    var (ixBody, endedWith) = Parser.FindHTTPBodyStart(current, MaxHeaderBytes);
                    if (endedWith == Parser.HeadersEndedWith.CRLFCRLF ||
                        endedWith == Parser.HeadersEndedWith.LFCRLF ||
                        endedWith == Parser.HeadersEndedWith.LFLF)
                    {
                        byte[] headersOnly = new byte[ixBody];
                        Buffer.BlockCopy(current, 0, headersOnly, 0, ixBody);

                        int initialBodyLength = current.Length - ixBody;
                        byte[] bodySoFar = initialBodyLength > 0 ? new byte[initialBodyLength] : Array.Empty<byte>();
                        if (initialBodyLength > 0)
                        {
                            Buffer.BlockCopy(current, ixBody, bodySoFar, 0, initialBodyLength);
                        }

                        string requestHeaderText = CONFIG.encodingOfHeaders.GetString(headersOnly);
                        var (method, path, version, headerLines, origin) = Parser.ParseRequestHeaders(requestHeaderText);
                        HTTPRequestHeaders parsedHeaders = new HTTPRequestHeaders(path, headerLines)
                        {
                            HTTPMethod = method,
                            HTTPVersion = version
                        };

                        if (origin.HasText() && origin.Contains(":"))
                        {
                            parsedHeaders.UriScheme = origin.TrimAfter(":");
                        }

                        if (parsedHeaders.ExistsAndContains("Transfer-Encoding", "chunked"))
                        {
                            bodySoFar = await ReadChunkedBodyToEndAsync(stream, bodySoFar, parsedHeaders, token).ConfigureAwait(false);
                        }
                        else if (int.TryParse(parsedHeaders["Content-Length"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int contentLength) &&
                                 contentLength > bodySoFar.Length)
                        {
                            bodySoFar = await ReadRemainingBodyAsync(stream, bodySoFar, contentLength, token).ConfigureAwait(false);
                        }
                        else if (int.TryParse(parsedHeaders["Content-Length"], NumberStyles.Integer, CultureInfo.InvariantCulture, out contentLength) &&
                                 contentLength >= 0 && bodySoFar.Length > contentLength)
                        {
                            byte[] trimmed = new byte[contentLength];
                            Buffer.BlockCopy(bodySoFar, 0, trimmed, 0, contentLength);
                            bodySoFar = trimmed;
                        }

                        return (parsedHeaders, bodySoFar);
                    }

                    if (endedWith == Parser.HeadersEndedWith.Oversized)
                    {
                        throw new InvalidDataException("Request headers exceeded maximum allowed size.");
                    }
                }

                return (null, Array.Empty<byte>());
            }
        }

        private static async Task<byte[]> ReadRemainingBodyAsync(Stream stream, byte[] bodySoFar, int expectedLength, CancellationToken token)
        {
            using (MemoryStream body = new MemoryStream(expectedLength))
            {
                if (bodySoFar.Length > 0)
                {
                    body.Write(bodySoFar, 0, bodySoFar.Length);
                }

                byte[] buffer = new byte[4096];
                while (body.Length < expectedLength)
                {
                    int toRead = (int)Math.Min(buffer.Length, expectedLength - body.Length);
                    int read = await stream.ReadAsync(buffer, 0, toRead, token).ConfigureAwait(false);
                    if (read <= 0) break;
                    body.Write(buffer, 0, read);
                }

                return body.ToArray();
            }
        }

        private static async Task<byte[]> ReadChunkedBodyToEndAsync(Stream stream, byte[] bodySoFar, HTTPRequestHeaders headers, CancellationToken token)
        {
            using (MemoryStream buffered = new MemoryStream())
            {
                if (bodySoFar.Length > 0)
                {
                    buffered.Write(bodySoFar, 0, bodySoFar.Length);
                }

                OffsetCursor cursor = new OffsetCursor();
                while (true)
                {
                    string sizeLine = await ReadAsciiLineAsync(stream, buffered, cursor, token).ConfigureAwait(false);
                    if (sizeLine == null)
                    {
                        throw new InvalidDataException("Unexpected EOF reading chunk size.");
                    }

                    string sizeToken = sizeLine.Trim();
                    int ixExt = sizeToken.IndexOf(';');
                    if (ixExt >= 0)
                    {
                        sizeToken = sizeToken.Substring(0, ixExt);
                    }

                    if (!int.TryParse(sizeToken, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int chunkSize) || chunkSize < 0)
                    {
                        throw new InvalidDataException("Invalid chunk size line: " + sizeLine);
                    }

                    if (chunkSize == 0)
                    {
                        await ReadAndApplyTrailerHeadersAsync(stream, buffered, cursor, headers, token).ConfigureAwait(false);
                        break;
                    }

                    await ReadExactlyAsync(stream, buffered, cursor, chunkSize, token).ConfigureAwait(false);
                    await ConsumeChunkTerminatorAsync(stream, buffered, cursor, token).ConfigureAwait(false);
                }

                byte[] rawChunkedBody = new byte[cursor.Value];
                Buffer.BlockCopy(buffered.GetBuffer(), 0, rawChunkedBody, 0, cursor.Value);
                return rawChunkedBody;
            }
        }

        private static async Task ReadAndApplyTrailerHeadersAsync(Stream stream, MemoryStream buffered, OffsetCursor cursor,
            HTTPRequestHeaders headers, CancellationToken token)
        {
            while (true)
            {
                string trailerLine = await ReadAsciiLineAsync(stream, buffered, cursor, token).ConfigureAwait(false);
                if (trailerLine == null) throw new InvalidDataException("Unexpected EOF reading chunked trailer headers.");
                if (trailerLine.Length == 0) return;

                int ixColon = trailerLine.IndexOf(':');
                if (ixColon <= 0) continue;
                headers.Add(trailerLine.Substring(0, ixColon), trailerLine.Substring(ixColon + 1).TrimStart(' ', '\t'));
            }
        }

        private static async Task ConsumeChunkTerminatorAsync(Stream stream, MemoryStream buffered, OffsetCursor cursor, CancellationToken token)
        {
            int b1 = await ReadByteAsync(stream, buffered, cursor, token).ConfigureAwait(false);
            if (b1 < 0) throw new InvalidDataException("Unexpected EOF after chunk payload.");

            if (b1 == '\r')
            {
                int b2 = await ReadByteAsync(stream, buffered, cursor, token).ConfigureAwait(false);
                if (b2 != '\n') throw new InvalidDataException("Invalid chunk payload terminator.");
                return;
            }

            if (b1 == '\n')
            {
                return;
            }

            throw new InvalidDataException("Invalid chunk payload terminator.");
        }

        private static async Task ReadExactlyAsync(Stream stream, MemoryStream buffered, OffsetCursor cursor,
            int byteCount, CancellationToken token)
        {
            for (int i = 0; i < byteCount; i++)
            {
                int b = await ReadByteAsync(stream, buffered, cursor, token).ConfigureAwait(false);
                if (b < 0) throw new InvalidDataException("Unexpected EOF inside chunk payload.");
            }
        }

        private static async Task<string> ReadAsciiLineAsync(Stream stream, MemoryStream buffered, OffsetCursor cursor, CancellationToken token)
        {
            using (MemoryStream line = new MemoryStream())
            {
                while (true)
                {
                    int b = await ReadByteAsync(stream, buffered, cursor, token).ConfigureAwait(false);
                    if (b < 0)
                    {
                        if (line.Length == 0) return null;
                        break;
                    }

                    if (b == '\n') break;
                    if (b != '\r') line.WriteByte((byte)b);
                }

                return Encoding.ASCII.GetString(line.ToArray());
            }
        }

        private static async Task<int> ReadByteAsync(Stream stream, MemoryStream buffered, OffsetCursor cursor, CancellationToken token)
        {
            if (cursor.Value >= buffered.Length)
            {
                byte[] temp = new byte[4096];
                int read = await stream.ReadAsync(temp, 0, temp.Length, token).ConfigureAwait(false);
                if (read <= 0) return -1;
                buffered.Position = buffered.Length;
                buffered.Write(temp, 0, read);
            }

            byte[] data = buffered.GetBuffer();
            return data[cursor.Value++];
        }

        /// <summary>
        /// This is basically an Integer that we're calling a cursor.
        /// NB: It's not clear what value this provides. 
        /// 
        /// </summary>
        private sealed class OffsetCursor
        {
            internal int Value;
        }

        private static Exchange BuildExchange(HTTPRequestHeaders headers, byte[] requestBodyBytes, CaptureConnectionContext context)
        {
            if (headers == null) throw new ArgumentNullException(nameof(headers));

            string method = headers.HTTPMethod ?? string.Empty;

            Exchange xc = new Exchange(headers, requestBodyBytes ?? Array.Empty<byte>());
            xc.EnsureID();
            xc.Timers.ClientConnected = DateTime.Now;
            xc.Timers.ClientBeginRequest = DateTime.Now;
            xc.Timers.ProxyGotRequestHeaders = DateTime.Now;
            xc.Timers.ClientDoneRequest = DateTime.Now;

            if (context?.ClientSocket?.RemoteEndPoint is IPEndPoint clientRemote)
            {
                xc["x-clientip"] = clientRemote.Address.ToString();
                xc["x-clientport"] = clientRemote.Port.ToString();
            }

            if (context?.LocalProcessDescriptor.HasText() == true)
            {
                xc["x-processinfo"] = context.LocalProcessDescriptor;
            }

            // By default a connect tunnel will be blind, but we might decide
            // to enable TLS decryption later.
            if (method.OICEquals("CONNECT"))
            {
                xc.SetBitFlag(ExchangeFlags.IsBlindTunnel);
            }

            xc.state = ExchangeState.ReadingRequestBody;
            return xc;
        }

        // Update the UI asynchronously, but don't block the capture engine thread on it.
        // NB: It's not clear that this is the right way to do this.
        private static void UpdateUIForExchange(Exchange exchange)
        {
            CApp.UIInvokeAsync(() =>
            {
                CApp.UI.UpdateViewItem(exchange);
            });
        }

        /// <summary>
        /// Does the client's request target Clearinet's reflector?
        /// </summary>
        private static bool ExchangeTargetsReflector(Exchange xc)
        {
            if (xc == null) return false;
            if (xc.port == CONFIG.ListenPort)
            {
                if (xc.hostname.OICEqualsAny("localhost", "127.0.0.1", "::1")) return true;
            }
            return xc.hostname.OICEqualsAny(
                "localhost.clearinet", "ipv4.clearinet", "iv6.clearinet",
                "localhost.fiddler", "ipv4.fiddler", "ipv6.fiddler");
        }

        private static bool ShouldDecryptTunnel(Exchange xc)
        {
            if (xc.BitFlags.HasFlag(ExchangeFlags.Ignored)) return false;
            if (!CaptureEnginePrefs.ShouldDecryptHttps()) return false;
            if (xc["x-no-decrypt"] != null) return false;
            return true;
        }

        private async Task HandleMitmConnectTunnelAsync(Exchange exchange, TcpClient client, Stream clientStream,
            ClientPipe clientPipe, CancellationToken token)
        {
            if (exchange == null) throw new ArgumentNullException(nameof(exchange));
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (clientStream == null) throw new ArgumentNullException(nameof(clientStream));

            // Validate that we got a valid CONNECT request.
            if (!TryGetConnectTarget(exchange, out string host, out int port))
            {
                exchange.ResponseHeaders = new HTTPResponseHeaders(400, "Invalid CONNECT Target", new[]
                {
                    "Content-Type: text/plain; charset=utf-8",
                    "Connection: close"
                });
                exchange.utilSetResponseBody("CONNECT target must be in host:port form.");
                exchange.BitFlags |= ExchangeFlags.ResponseGeneratedByClearinet;
                await WriteExchangeResponseToClientAsync(exchange, clientStream, token).ConfigureAwait(false);
                exchange.state = ExchangeState.Done;
                UpdateUIForExchange(exchange);
                return;
            }

            _certificateProvider.EnsureRootTrust();
            X509Certificate2 leaf = _certificateProvider.GetCertificateForHost(host);

            Guid clientPipeId = clientPipe.Id;
            string processIdentity = clientPipe.ProcessIdentity;
            var upstreamKey = new ServerConnectionKey(host, port, true);   // Issue: We need to also account for an upstream gateway proxy.
            ServerPipe upstreamPipe = null;
            bool returnUpstreamPipe = false;
            bool keepAliveAllowed = true;
            try
            {
                if (_socketPool.TryRent(upstreamKey, processIdentity, clientPipeId, out upstreamPipe))
                {
                    exchange.BitFlags |= ExchangeFlags.ServerPipeReused;
                }
                else
                {
                    upstreamPipe = await ConnectServerPipeAsync(host, port, true, processIdentity, clientPipeId,
                        CaptureEnginePrefs.GetServerPipeBindingPolicy()).ConfigureAwait(false);
                }

                exchange.Timers.ServerConnected = DateTime.Now;
                exchange.ResponseHeaders = new HTTPResponseHeaders(200, "Decrypting Connection Established", new[]
                {
                    "Connection: keep-alive"
                });
                exchange.Timers.ClientBeginResponse = DateTime.Now;
                byte[] established = exchange.ResponseHeaders.ToByteArray();
                await clientStream.WriteAsync(established, 0, established.Length, token).ConfigureAwait(false);
                await clientStream.FlushAsync(token).ConfigureAwait(false);

                var clientTls = new SslStream(clientStream, false);
                clientTls.AuthenticateAsServer(leaf, false, SslProtocols.Tls12 | SslProtocols.Tls13, false); // TODO: Configure protocols 
                clientPipe.SetStream(clientTls);
                {
                    var clientReader = clientPipe.Reader;
                    while (!token.IsCancellationRequested)
                    {
                        var tunneledRequest = await clientReader.ReadRequestAsync(token).ConfigureAwait(false);
                        if (tunneledRequest.requestHeaders == null) break;

                        tunneledRequest.requestHeaders.UriScheme = "https";
                        if (!tunneledRequest.requestHeaders.Exists("Host"))
                            tunneledRequest.requestHeaders["Host"] = port == 443 ? host : host + ":" + port;

                        var innerContext = new CaptureConnectionContext(client.Client) { LocalProcessDescriptor = processIdentity };
                        Exchange inner = BuildExchange(tunneledRequest.requestHeaders, tunneledRequest.requestBody, innerContext);
                        inner.SetBitFlag(ExchangeFlags.IsDecryptingTunnel);
                        inner.SetBitFlag(ExchangeFlags.IsHTTPS);
                        inner.state = ExchangeState.AutoTamperRequestBefore;

                        HTTPRequestHeaders upstreamRequest = inner.RequestHeaders.Clone();
                        PrepareRequestForServer(upstreamRequest, inner.RequestBody ?? Array.Empty<byte>());
                        await WriteHttpRequestAsync(upstreamPipe.Stream, upstreamRequest,
                            inner.RequestBody ?? Array.Empty<byte>(), token).ConfigureAwait(false);

                        var upstreamResponse = await upstreamPipe.Reader.ReadResponseAsync(upstreamRequest.HTTPMethod, token).ConfigureAwait(false);
                        if (upstreamResponse.responseHeaders == null) break;

                        inner.ResponseHeaders = upstreamResponse.responseHeaders;
                        inner.ResponseBody = upstreamResponse.responseBody;
                        inner.state = ExchangeState.AutoTamperResponseBefore;
                        if (!upstreamResponse.reusable && !inner.ResponseHeaders.Exists("Connection"))
                            inner.ResponseHeaders["Connection"] = "close";
                        NormalizeResponseLength(inner.RequestHeaders, inner.ResponseHeaders, inner.ResponseBody ?? Array.Empty<byte>());
                        await WriteHttpResponseAsync(clientTls, inner.ResponseHeaders, inner.ResponseBody ?? Array.Empty<byte>(), token).ConfigureAwait(false);

                        inner.Timers.ClientDoneResponse = DateTime.Now;
                        inner.state = ExchangeState.Done;
                        UpdateUIForExchange(inner);
                        keepAliveAllowed = upstreamResponse.reusable &&
                            ShouldKeepAlive(upstreamRequest, inner.ResponseHeaders) && upstreamPipe.IsConnected;
                        if (!keepAliveAllowed || !ShouldKeepAlive(inner.RequestHeaders, inner.ResponseHeaders)) break;
                    }
                }

                returnUpstreamPipe = keepAliveAllowed && upstreamPipe.IsConnected;
            }
            finally
            {
                if (upstreamPipe != null)
                {
                    if (returnUpstreamPipe)
                    {
                        _socketPool.Return(upstreamKey, upstreamPipe, DateTime.UtcNow);
                        exchange["x-serverpool-returned"] = "true";
                    }
                    else
                    {
                        upstreamPipe.Dispose();
                    }
                }
            }

            exchange.Timers.ClientDoneResponse = DateTime.Now;
            exchange.state = ExchangeState.Done;
            UpdateUIForExchange(exchange);
        }

        private bool callbackValidateServerCertificate(string targetHost, object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // https://github.com/microsoft/referencesource/blob/main/System/net/System/Net/SecureProtocols/_SslState.cs

            if (sslPolicyErrors == SslPolicyErrors.None) return true;
            if (CApp.Prefs.GetBoolPref("proxy.unsafe.ignore_cert_errors"))
            {
                CApp.Log.Log($"! Ignoring SSL validation error for connection to {targetHost}: {sslPolicyErrors}");
                return true;
            }
            CApp.Log.Log($"Blocking connection to {targetHost} due to SSL validation error: {sslPolicyErrors}");
            return false;
        }

        private static async Task HandleBlindConnectTunnelAsync(Exchange exchange, TcpClient client, Stream clientStream, CancellationToken token)
        {
            if (exchange == null) throw new ArgumentNullException(nameof(exchange));
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (clientStream == null) throw new ArgumentNullException(nameof(clientStream));

            if (!TryGetConnectTarget(exchange, out string host, out int port))
            {
                exchange.ResponseHeaders = new HTTPResponseHeaders(400, "Invalid CONNECT Target", new[]
                {
                    "Content-Type: text/plain; charset=utf-8",
                    "Connection: close"
                });
                exchange.utilSetResponseBody("CONNECT target must be in host:port form.");
                exchange.BitFlags |= ExchangeFlags.ResponseGeneratedByClearinet;
                await WriteExchangeResponseToClientAsync(exchange, clientStream, token).ConfigureAwait(false);
                exchange.state = ExchangeState.Done;
                UpdateUIForExchange(exchange);
                return;
            }

            using (TcpClient upstream = new TcpClient())
            {
                upstream.NoDelay = true;

                await upstream.ConnectAsync(host, port).ConfigureAwait(false);
                exchange.Timers.ServerConnected = DateTime.Now;

                exchange.ResponseHeaders = new HTTPResponseHeaders(200, "Blind Connection Established", new[]
                {
                    "Connection: keep-alive"
                });

                exchange.Timers.ClientBeginResponse = DateTime.Now;
                await clientStream.WriteAsync(exchange.ResponseHeaders.ToByteArray(), 0, exchange.ResponseHeaders.ToByteArray().Length, token).ConfigureAwait(false);
                await clientStream.FlushAsync(token).ConfigureAwait(false);

                using (NetworkStream upstreamStream = upstream.GetStream())
                {
                    using (CancellationTokenSource relayCts = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        Task<long> relayClientToServer = PumpStreamAsync(clientStream, upstreamStream, relayCts.Token);
                        Task<long> relayServerToClient = PumpStreamAsync(upstreamStream, clientStream, relayCts.Token);

                        await Task.WhenAny(relayClientToServer, relayServerToClient).ConfigureAwait(false);
                        relayCts.Cancel();

                        long clientToClearinetBytes = 0;
                        long serverToClearinetBytes = 0;

                        try { clientToClearinetBytes = await relayClientToServer.ConfigureAwait(false); } catch { }
                        try { serverToClearinetBytes = await relayServerToClient.ConfigureAwait(false); } catch { }

                        exchange["Tunnel-ByteCount-FromClient"] = clientToClearinetBytes.ToString("N0", CultureInfo.InvariantCulture);
                        exchange["Tunnel-ByteCount-FromServer"] = serverToClearinetBytes.ToString("N0", CultureInfo.InvariantCulture);
                    }
                }
            }

            exchange.Timers.ServerDoneResponse = DateTime.Now;
            exchange.Timers.ClientDoneResponse = DateTime.Now;
            exchange.state = ExchangeState.Done;
            UpdateUIForExchange(exchange);
        }

        private static async Task WriteExchangeResponseToClientAsync(Exchange exchange, Stream clientStream, CancellationToken token)
        {
            exchange.Timers.ClientBeginResponse = DateTime.Now;

            byte[] headers = exchange.ResponseHeaders?.ToByteArray() ?? Array.Empty<byte>();
            if (headers.Length > 0)
            {
                await clientStream.WriteAsync(headers, 0, headers.Length, token).ConfigureAwait(false);
            }

            byte[] body = exchange.ResponseBody ?? Array.Empty<byte>();
            if (body.Length > 0)
            {
                await clientStream.WriteAsync(body, 0, body.Length, token).ConfigureAwait(false);
            }

            await clientStream.FlushAsync(token).ConfigureAwait(false);
            exchange.Timers.ClientDoneResponse = DateTime.Now;
        }

        private static async Task WriteHttpRequestAsync(Stream stream, HTTPRequestHeaders headers, byte[] body, CancellationToken token)
        {
            byte[] headerBytes = headers.ToByteArray(false);
            // TODO: Set the correct timer.
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length, token).ConfigureAwait(false);

            if (body != null && body.Length > 0)
            {
                // TODO: Set the correct timer.
                await stream.WriteAsync(body, 0, body.Length, token).ConfigureAwait(false);
            }
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        private static async Task WriteHttpResponseAsync(Stream stream, HTTPResponseHeaders headers, byte[] body, CancellationToken token)
        {
            //if (body != null && body.Length > 0 && !headers.Exists("Content-Length") && !headers.Exists("Transfer-Encoding"))
            //    headers["Content-Length"] = body.Length.ToString(CultureInfo.InvariantCulture);

            byte[] headerBytes = headers.ToByteArray();
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length, token).ConfigureAwait(false);
            // TODO: Set the correct timer.

            if (body != null && body.Length > 0)
            {
                // TODO: Set the correct timer.
                await stream.WriteAsync(body, 0, body.Length, token).ConfigureAwait(false);
            }
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        private static async Task<(HTTPResponseHeaders responseHeaders, byte[] responseBody)> ReadHttpResponseAsync(Stream stream, CancellationToken token)
        {
            byte[] readBuffer = new byte[4096];
            using (MemoryStream ms = new MemoryStream())
            {
                while (ms.Length < MaxHeaderBytes)
                {
                    int bytesRead = await stream.ReadAsync(readBuffer, 0, readBuffer.Length, token).ConfigureAwait(false);
                    if (bytesRead <= 0) break;

                    ms.Write(readBuffer, 0, bytesRead);
                    byte[] current = ms.ToArray();
                    var (ixBody, endedWith) = Parser.FindHTTPBodyStart(current, MaxHeaderBytes);
                    if (endedWith == Parser.HeadersEndedWith.CRLFCRLF ||
                        endedWith == Parser.HeadersEndedWith.LFCRLF ||
                        endedWith == Parser.HeadersEndedWith.LFLF)
                    {
                        byte[] headersOnly = new byte[ixBody];
                        Buffer.BlockCopy(current, 0, headersOnly, 0, ixBody);

                        int initialBodyLength = current.Length - ixBody;
                        byte[] bodySoFar = initialBodyLength > 0 ? new byte[initialBodyLength] : Array.Empty<byte>();
                        if (initialBodyLength > 0)
                        {
                            Buffer.BlockCopy(current, ixBody, bodySoFar, 0, initialBodyLength);
                        }

                        string responseHeaderText = CONFIG.encodingOfHeaders.GetString(headersOnly);
                        var (version, statusCode, statusText, headerLines) = Parser.ParseResponseHeaders(responseHeaderText);
                        HTTPResponseHeaders responseHeaders = new HTTPResponseHeaders(statusCode, statusText, headerLines)
                        {
                            HTTPVersion = version
                        };

                        if (responseHeaders.ExistsAndContains("Transfer-Encoding", "chunked"))
                        {
                            bodySoFar = await ReadChunkedBodyToEndAsync(stream, bodySoFar,
                                BuildDummyRequestHeadersForTrailers(), token).ConfigureAwait(false);
                        }
                        else if (int.TryParse(responseHeaders["Content-Length"], NumberStyles.Integer,
                                     CultureInfo.InvariantCulture, out int contentLength) && contentLength > bodySoFar.Length)
                        {
                            bodySoFar = await ReadRemainingBodyAsync(stream, bodySoFar, contentLength, token).ConfigureAwait(false);
                        }
                        else if (int.TryParse(responseHeaders["Content-Length"], NumberStyles.Integer,
                                     CultureInfo.InvariantCulture, out contentLength) && contentLength >= 0 && bodySoFar.Length > contentLength)
                        {
                            byte[] trimmed = new byte[contentLength];
                            Buffer.BlockCopy(bodySoFar, 0, trimmed, 0, contentLength);
                            bodySoFar = trimmed;
                        }

                        return (responseHeaders, bodySoFar);
                    }

                    if (endedWith == Parser.HeadersEndedWith.Oversized)
                    {
                        throw new InvalidDataException("Response headers exceeded maximum allowed size.");
                    }
                }
            }

            return (null, Array.Empty<byte>());
        }

        private static HTTPRequestHeaders BuildDummyRequestHeadersForTrailers()
        {
            return new HTTPRequestHeaders("/", null) { HTTPMethod = "GET", HTTPVersion = "HTTP/1.1" };
        }

        private static void NormalizeResponseLength(HTTPRequestHeaders requestHeaders, HTTPResponseHeaders responseHeaders, byte[] body)
        {
            if (requestHeaders.HTTPMethod.OICEquals("HEAD") ||
                (responseHeaders.StatusCode >= 100 && responseHeaders.StatusCode < 200) ||
                responseHeaders.StatusCode == 204 || responseHeaders.StatusCode == 304 ||
                responseHeaders.Exists("Transfer-Encoding")) return;

            responseHeaders["Content-Length"] = (body ?? Array.Empty<byte>()).Length.ToString(CultureInfo.InvariantCulture);
        }

        private static bool ShouldKeepAlive(HTTPRequestHeaders requestHeaders, HTTPResponseHeaders responseHeaders)
        {
            if (requestHeaders == null || responseHeaders == null) return false;
            if (HasHeaderWithToken(requestHeaders, "Connection", "close") ||
                HasHeaderWithToken(requestHeaders, "Proxy-Connection", "close") ||
                HasHeaderWithToken(responseHeaders, "Connection", "close")) return false;

            bool requestHttp10 = (requestHeaders.HTTPVersion ?? string.Empty).OICContains("HTTP/1.0");
            bool responseHttp10 = (responseHeaders.HTTPVersion ?? string.Empty).OICContains("HTTP/1.0");
            bool requestPersistent = !requestHttp10 ||
                HasHeaderWithToken(requestHeaders, "Connection", "keep-alive") ||
                HasHeaderWithToken(requestHeaders, "Proxy-Connection", "keep-alive");
            bool responsePersistent = !responseHttp10 || HasHeaderWithToken(responseHeaders, "Connection", "keep-alive");
            return requestPersistent && responsePersistent;
        }

        // Perf: This probably allocates more often than it needs to.
        private static bool HasHeaderWithToken(IEnumerable<HTTPHeaderItem> headers, string headerName, string sought)
        {
            foreach (HTTPHeaderItem header in headers)
            {
                if (!header.Name.OICEquals(headerName)) continue;
                string[] tokens = header.Value.Split(',');
                for (int i = 0; i < tokens.Length; i++)
                    if (tokens[i].Trim().OICEquals(sought)) return true;
            }
            return false;
        }

        // Probe a socket for reusability.
        //            if (!socket?.Connected == true) return false;
        //       bool readReady = socket.Poll(0, SelectMode.SelectRead);
        //       bool noData = socket.Available == 0;

        private static async Task<Socket> ConnectSocketAsync(string host, int port)
        {
            IPAddress[] addresses = Dns.GetHostAddresses(host);
            Exception last = null;
            for (int i = 0; i < addresses.Length; i++)
            {
                Socket socket = null;
                try
                {
                    socket = new Socket(addresses[i].AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    await Task.Factory.FromAsync(
                        (callback, state) => socket.BeginConnect(addresses[i], port, callback, state),
                        socket.EndConnect,
                        null).ConfigureAwait(false);
                    return socket;
                }
                catch (Exception eX)
                {
                    last = eX;
                    try { socket?.Close(); } catch { }
                }
            }

            throw new SocketException((int)SocketError.HostUnreachable);
        }

        private static bool TryGetConnectTarget(Exchange exchange, out string host, out int port)
        {
            host = null;
            port = 443;

            string target = exchange?.RequestHeaders?.RequestPath ?? string.Empty;
            if (!target.HasText())
            {
                target = exchange?.RequestHeaders?["Host"] ?? string.Empty;
            }

            target = target.Trim();
            if (!target.HasText()) return false;

            if (target.StartsWith("[", StringComparison.Ordinal))
            {
                int end = target.IndexOf(']');
                if (end <= 1) return false;

                host = target.Substring(1, end - 1);
                if (end + 1 < target.Length)
                {
                    if (target[end + 1] != ':') return false;
                    if (!int.TryParse(target.Substring(end + 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out port)) return false;
                }
                return true;
            }

            int ixColon = target.LastIndexOf(':');
            if (ixColon < 0)
            {
                host = target;
                return true;
            }

            host = target.Substring(0, ixColon);
            if (!int.TryParse(target.Substring(ixColon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
            {
                return false;
            }

            return host.HasText();
        }

        private static async Task<long> PumpStreamAsync(Stream source, Stream destination, CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            long bytesRead = 0;
            while (!token.IsCancellationRequested)
            {
                int read;
                try
                {
                    read = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return bytesRead;
                }
                catch (IOException)
                {
                    return bytesRead;
                }
                catch (ObjectDisposedException)
                {
                    return bytesRead;
                }

                if (read <= 0) return bytesRead;

                bytesRead += read;

                try
                {
                    await destination.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                    await destination.FlushAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return bytesRead;
                }
                catch (IOException)
                {
                    return bytesRead;
                }
                catch (ObjectDisposedException)
                {
                    return bytesRead;
                }
            }

            return bytesRead;
        }


        /// <summary>
        /// Send a simple HTML page to the client.
        /// 
        /// TODO: The right way for this to work is to overwrite the response of the Exchange and then
        /// let it flow through the rest of the state machine as normal.
        /// 
        /// TODO: Should allow the user to pick a URL path containing the response code (e.g. /reflector/404) 
        /// and then use that code in the response.
        /// 
        /// </summary>
        private async Task CreateReflectorPage(Exchange xc, Stream stream, CancellationToken token)
        {
            xc.SetBitFlag(ExchangeFlags.ResponseGeneratedByClearinet);

            if (xc.urlContains("ClearinetRoot.cer"))
            {
                var cert = _certificateProvider.GetRootCertificate();
                byte[] certBytes = cert.Export(X509ContentType.Cert);

                xc.ResponseHeaders = new HTTPResponseHeaders(200, "OK", new[]
                {
                    "Connection: close",
                    "Content-Type: application/x-x509-ca-cert",
                    $"Content-Length: {certBytes.Length}"
                });
                xc.ResponseBody = certBytes;
                await WriteExchangeResponseToClientAsync(xc, stream, token).ConfigureAwait(false);
                xc.state = ExchangeState.Done;
                return;
            }

            xc.state = ExchangeState.ReadingResponseHeaders;
            StringBuilder sbResponse = new StringBuilder(2048);
            sbResponse.AppendLine("<!doctype html>\r\n<html><head><title>Clearinet Echo Service</title>");
            sbResponse.AppendLine("<style>body {font-family: sans-serif, arial} a { text-decoration: none } a:hover { text-decoration: underline; }</style></head>");
            sbResponse.AppendLine("<body><h1>Clearinet Reflector</h1>");

            sbResponse.Append("<pre>");
            sbResponse.Append(Utilities.HtmlEncode(xc.RequestHeaders.ToString()));
            if (xc.RequestBody != null && xc.RequestBody.Length > 0)
            {
                sbResponse.Append(Utilities.HtmlEncode(Encoding.UTF8.GetString(xc.RequestBody)));
            }
            else
            {
                sbResponse.Append("<i>(No request body)</i>");
            }
            sbResponse.AppendLine("</pre>");

            #region UserSelectedStatusCode
            // Check if the URL had a status code in it, and if so, use that as the response code. Otherwise default to 200.
            int iStatusCode = 200;
            if (xc.RequestHeaders.RequestPath != "/")
            {
                string[] parts = xc.RequestHeaders.RequestPath.Split('/');
                if (parts.Length > 1 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                {
                    if (parsed < 0 || parsed > 999) parsed = 200;
                    iStatusCode = parsed;
                }
            }
            sbResponse.Append($"<hr />This response's <b>Status Code: {iStatusCode}</b> (<a href='/200/'>200</a> <a href='/400/'>400</a> <a href='/500/'>500</a>)<br />");
            #endregion UserSelectedStatusCode

            // TODO: Should we support TCP/IP termination parameters, e.g. /RST to send a TCP RST instead of a normal response?
            // /CLOSE to close the connection after the response?
            // /DELAY=5000 to delay the response by 5 seconds?
            // /ECHO to echo back the request body as the response body with no modifications?
            // Each of these has interesting security implications.

            sbResponse.AppendLine($"Clearinet Version: {CApp.VersionString}<br />Originating Process: {xc.LocalProcess}<br />");

            sbResponse.AppendLine("<hr /><ul><li>To configure Clearinet as a reverse proxy (instead of this service), see " +
                $"<a href='{CONFIG.GetURL("HelpReverseProxy")}'>Reverse Proxy</a>");
            sbResponse.AppendLine("<li>You can also download the <a href='ClearinetRoot.cer'>Clearinet Root Certificate</a>");
            sbResponse.AppendLine("</ul>");

            sbResponse.AppendLine("</body></html>");

            xc.ResponseBody = Encoding.UTF8.GetBytes(sbResponse.ToString());
            string sBody = sbResponse.ToString();

            xc.ResponseHeaders = new HTTPResponseHeaders(iStatusCode, "Generated", new[]
            {
                "Connection: close",
                "Content-Type: text/html; charset=utf-8",
                $"Content-Length: {xc.ResponseBody.Length}"
            });

            xc.Timers.ClientBeginResponse = DateTime.Now;

            // Write headers.
            byte[] bytes = xc.ResponseHeaders.ToByteArray();
            await stream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);

            // Write body.
            await stream.WriteAsync(xc.ResponseBody, 0, xc.ResponseBody.Length, token).ConfigureAwait(false);

            await stream.FlushAsync(token).ConfigureAwait(false);
            xc.Timers.ClientDoneResponse = DateTime.Now;
            xc.state = ExchangeState.Done;
        }

        private static void AbortWithRst(TcpClient client)
        {
            if (client == null) return;
            try
            {
                client.Client.LingerState = new LingerOption(true, 0);
                try
                {
                    client.Client.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                    // Ignore shutdown failures; close still tears down the socket.
                }
                client.Close();
            }
            catch
            {
                // Ignore and fall back to normal close by caller disposal.
            }
        }
    }
}
