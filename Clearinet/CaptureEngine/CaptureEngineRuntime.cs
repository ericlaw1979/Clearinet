using System;
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
        private const int MaxHeaderBytes = 64 * 1024;

        private readonly CaptureEngineOptions _options;
        private readonly MapSocketToProcess _mapper;
        private readonly ICertificateProvider _certificateProvider;
        private readonly ISocketPool _socketPool;
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
                    _acceptLoop?.Wait(TimeSpan.FromSeconds(2));
                    if (_socketPoolJanitorGig != null)
                    {
                        CApp.gwHoneyDew.cancelGig(_socketPoolJanitorGig);
                        _socketPoolJanitorGig = null;
                    }
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
                CaptureConnectionContext context = new CaptureConnectionContext(client.Client);
                IPEndPoint remote = client.Client.RemoteEndPoint as IPEndPoint;
                if (remote != null)
                {
                    context.LocalProcessDescriptor = _mapper.MapSocketToOriginatingProcess(remote);
                }

                using (NetworkStream stream = client.GetStream())
                {
                    Exchange exchange = null;
                    try
                    {
                        var requestData = await ReadHttpRequestAsync(stream, token).ConfigureAwait(false);
                        if (requestData.requestHeaders == null)
                        {
                            AbortWithRst(client);
                            return;
                        }

                        exchange = BuildExchange(requestData.requestHeaders, requestData.requestBody, context);
                        context.Exchange = exchange;
                        PublishExchange(exchange);

                        if (ShouldReturnYetImplemented(exchange))
                        {
                            await SendNotYetImplementedAsync(stream, token).ConfigureAwait(false);
                            return;
                        }

                        if (exchange.HTTPMethodIs("CONNECT"))
                        {
                            bool shouldDecrypt = ShouldDecryptConnectTunnel(exchange);
                            if (shouldDecrypt)
                            {
                                exchange["x-connect-decrypt-eligible"] = "true";
                                await HandleMitmConnectTunnelAsync(exchange, client, stream, token).ConfigureAwait(false);
                                return;
                            }

                            await HandleBlindConnectTunnelAsync(exchange, client, stream, token).ConfigureAwait(false);
                            return;
                        }

                        // ********************************************************
                        await ForwardToServerViaHttpClientBridgeAsync(exchange, stream, token).ConfigureAwait(false);
                    }
                    catch (Exception eX)
                    {
                        await HandleCaptureFailureAsync(exchange, stream, eX, token).ConfigureAwait(false);
                    }
                }
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
            PublishExchange(exchange);
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

            if (method.OICEquals("CONNECT"))
            {
                xc.SetBitFlag(ExchangeFlags.IsBlindTunnel);
            }

            xc.state = ExchangeState.ReadingRequestBody;
            return xc;
        }

        private static void PublishExchange(Exchange exchange)
        {
            CApp.UIInvokeAsync(() =>
            {
                if (CApp.UI == null || exchange == null) return;
                CApp.UI.UpdateViewItem(exchange);
            });
        }

        private static bool ShouldReturnYetImplemented(Exchange exchange)
        {
            if (exchange == null) return false;
            return exchange.hostname.OICEquals("localhost.fiddler");
        }

        private static bool ShouldDecryptConnectTunnel(Exchange exchange)
        {
            return false;
            /* NYI
            if (exchange == null || !exchange.HTTPMethodIs("CONNECT")) return false;
            if (!CaptureEnginePrefs.ShouldDecryptHttps()) return false;
            if (exchange["x-no-decrypt"] != null) return false;
            return true;*/
        }

        private static async Task ForwardToServerViaHttpClientBridgeAsync(Exchange exchange, NetworkStream clientStream, CancellationToken token)
        {
            if (exchange == null) throw new ArgumentNullException(nameof(exchange));
            if (clientStream == null) throw new ArgumentNullException(nameof(clientStream));

            await new HTTPClientBridge().SendAndReceiveRawAsync(exchange).ConfigureAwait(false);

            if (exchange.ResponseHeaders == null)
            {
                exchange.ResponseHeaders = new HTTPResponseHeaders(502, "No Response", new[]
                {
                    "Content-Type: text/plain; charset=utf-8"
                });
                exchange.utilSetResponseBody("No response was generated by upstream transport.");
            }

            exchange.Timers.ClientBeginResponse = DateTime.Now;

            byte[] headerBytes = exchange.ResponseHeaders.ToByteArray();
            await clientStream.WriteAsync(headerBytes, 0, headerBytes.Length, token).ConfigureAwait(false);

            byte[] body = exchange.ResponseBody ?? Array.Empty<byte>();
            if (body.Length > 0)
            {
                await clientStream.WriteAsync(body, 0, body.Length, token).ConfigureAwait(false);
            }

            await clientStream.FlushAsync(token).ConfigureAwait(false);
            exchange.Timers.ClientDoneResponse = DateTime.Now;
            exchange.state = ExchangeState.Done;
            PublishExchange(exchange);
        }

        private async Task HandleMitmConnectTunnelAsync(Exchange exchange, TcpClient client, NetworkStream clientStream, CancellationToken token)
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
                PublishExchange(exchange);
                return;
            }

            _certificateProvider.EnsureRootTrust();
            X509Certificate2 leaf = _certificateProvider.GetCertificateForHost(host);
            exchange["x-tunnel-mode"] = "mitm";

            ServerConnectionKey upstreamKey = new ServerConnectionKey(host, port, true);
            Socket upstreamSocket = null;
            bool reuseUpstream = false;
            bool keepAliveAllowed = true;

            if (_socketPool.TryRent(upstreamKey, out Socket pooledSocket))
            {
                upstreamSocket = pooledSocket;
                exchange.BitFlags |= ExchangeFlags.ServerPipeReused;
                exchange["x-serverpool-reused"] = "true";
            }
            else
            {
                upstreamSocket = await ConnectSocketAsync(host, port).ConfigureAwait(false);
            }

            try
            {
                upstreamSocket.NoDelay = true;
                exchange.Timers.ServerConnected = DateTime.Now;

                exchange.ResponseHeaders = new HTTPResponseHeaders(200, "Connection Established", new[]
                {
                    "Connection: keep-alive"
                });
                exchange.Timers.ClientBeginResponse = DateTime.Now;
                byte[] established = exchange.ResponseHeaders.ToByteArray();
                await clientStream.WriteAsync(established, 0, established.Length, token).ConfigureAwait(false);
                await clientStream.FlushAsync(token).ConfigureAwait(false);

                using (NetworkStream upstreamTcp = new NetworkStream(upstreamSocket, false))
                using (SslStream clientTls = new SslStream(clientStream, false))
                using (SslStream serverTls = new SslStream(upstreamTcp, true,
                    (sender, cert, chain, errors) => callbackValidateServerCertificate(host, sender, cert, chain, errors)) /* TODO: Can attach client cert here */)
                {
                    clientTls.AuthenticateAsServer(leaf, false,
                        /*SslProtocols.Tls | SslProtocols.Tls11 |*/ SslProtocols.Tls12 | SslProtocols.Tls13, false); // TODO: Needs a Pref
                    serverTls.AuthenticateAsClient(host /* TODO: Allow override */, null, SslProtocols.Tls12 | SslProtocols.Tls13, false);

                    while (!token.IsCancellationRequested)
                    {
                        var tunneledRequest = await ReadHttpRequestAsync(clientTls, token).ConfigureAwait(false);
                        if (tunneledRequest.requestHeaders == null) break;

                        tunneledRequest.requestHeaders.UriScheme = "https";
                        if (!tunneledRequest.requestHeaders.Exists("Host"))
                        {
                            tunneledRequest.requestHeaders["Host"] = port == 443 ? host : host + ":" + port;
                        }

                        Exchange inner = BuildExchange(tunneledRequest.requestHeaders, tunneledRequest.requestBody,
                            new CaptureConnectionContext(client.Client) { LocalProcessDescriptor = exchange["x-processinfo"] });
                        inner.SetBitFlag(ExchangeFlags.IsDecryptingTunnel);
                        inner.SetBitFlag(ExchangeFlags.IsHTTPS);

                        inner.state = ExchangeState.AutoTamperRequestBefore;

                        await WriteHttpRequestAsync(serverTls, inner.RequestHeaders, inner.RequestBody ?? Array.Empty<byte>(), token).ConfigureAwait(false);

                        var upstreamResponse = await ReadHttpResponseAsync(serverTls, token).ConfigureAwait(false);
                        if (upstreamResponse.responseHeaders == null)
                        {
                            break;
                        }

                        inner.ResponseHeaders = upstreamResponse.responseHeaders;
                        inner.ResponseBody = upstreamResponse.responseBody;
                        inner.state = ExchangeState.AutoTamperResponseBefore;

                        await WriteHttpResponseAsync(clientTls, inner.ResponseHeaders, inner.ResponseBody ?? Array.Empty<byte>(), token).ConfigureAwait(false);

                        inner.Timers.ClientDoneResponse = DateTime.Now;
                        inner.state = ExchangeState.Done;
                        PublishExchange(inner);

                        keepAliveAllowed = ShouldKeepAlive(inner.RequestHeaders, inner.ResponseHeaders);
                        if (!keepAliveAllowed)
                        {
                            break;
                        }
                    }
                }

                reuseUpstream = keepAliveAllowed && IsSocketReusable(upstreamSocket);
            }
            finally
            {
                if (reuseUpstream)
                {
                    _socketPool.Return(upstreamKey, upstreamSocket, DateTime.UtcNow);
                    exchange["x-serverpool-returned"] = "true";
                }
                else
                {
                    try
                    { /*TODO: Should this call .Shutdown()? */
                        upstreamSocket?.Close();
                    }
                    catch { }
                }

                exchange["x-tunnel-mode"] = "blind"; // Huh? Why?
            }

            exchange.Timers.ClientDoneResponse = DateTime.Now;
            exchange.state = ExchangeState.Done;
            PublishExchange(exchange);
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

        private static async Task HandleBlindConnectTunnelAsync(Exchange exchange, TcpClient client, NetworkStream clientStream, CancellationToken token)
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
                PublishExchange(exchange);
                return;
            }

            using (TcpClient upstream = new TcpClient())
            {
                upstream.NoDelay = true;

                await upstream.ConnectAsync(host, port).ConfigureAwait(false);
                exchange.Timers.ServerConnected = DateTime.Now;

                exchange.ResponseHeaders = new HTTPResponseHeaders(200, "Connection Established", new[]
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
            PublishExchange(exchange);
        }

        private static async Task WriteExchangeResponseToClientAsync(Exchange exchange, NetworkStream clientStream, CancellationToken token)
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
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length, token).ConfigureAwait(false);
            if (body != null && body.Length > 0)
            {
                await stream.WriteAsync(body, 0, body.Length, token).ConfigureAwait(false);
            }
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        private static async Task WriteHttpResponseAsync(Stream stream, HTTPResponseHeaders headers, byte[] body, CancellationToken token)
        {
            byte[] headerBytes = headers.ToByteArray();
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length, token).ConfigureAwait(false);
            if (body != null && body.Length > 0)
            {
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

        private static bool ShouldKeepAlive(HTTPRequestHeaders requestHeaders, HTTPResponseHeaders responseHeaders)
        {
            if (requestHeaders == null || responseHeaders == null) return false;

            if (requestHeaders.ExistsAndContains("Connection", "close")) return false;
            if (responseHeaders.ExistsAndContains("Connection", "close")) return false;

            string reqVersion = requestHeaders.HTTPVersion ?? string.Empty;
            string respVersion = responseHeaders.HTTPVersion ?? string.Empty;
            bool reqHttp10 = reqVersion.OICContains("HTTP/1.0");
            bool respHttp10 = respVersion.OICContains("HTTP/1.0");

            if (reqHttp10 || respHttp10)
            {
                return requestHeaders.ExistsAndContains("Connection", "keep-alive") ||
                       responseHeaders.ExistsAndContains("Connection", "keep-alive");
            }

            return true;
        }

        private static bool IsSocketReusable(Socket socket)
        {
            if (socket == null || !socket.Connected) return false;
            try
            {
                bool readReady = socket.Poll(0, SelectMode.SelectRead);
                bool noData = socket.Available == 0;
                return !(readReady && noData);
            }
            catch
            {
                return false;
            }
        }

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

        private static async Task<long> PumpStreamAsync(NetworkStream source, NetworkStream destination, CancellationToken token)
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

        private static async Task SendNotYetImplementedAsync(NetworkStream stream, CancellationToken token)
        {
            const string message = "Capture engine transport is not fully implemented yet.";
            string responseText =
                "HTTP/1.1 501 Not Implemented\r\n" +
                "Connection: close\r\n" +
                "Content-Type: text/plain; charset=utf-8\r\n" +
                "Content-Length: " + Encoding.UTF8.GetByteCount(message) + "\r\n\r\n" +
                message;
            byte[] bytes = Encoding.ASCII.GetBytes(responseText);
            await stream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
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
