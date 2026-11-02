using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
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
        private readonly object _syncRoot = new object();
        private CancellationTokenSource _cts;
        private TcpListener _listener;
        private Task _acceptLoop;

        internal TcpCaptureEngineListener(CaptureEngineOptions options, MapSocketToProcess mapper)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
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
                    try
                    {
                        var requestData = await ReadHttpRequestAsync(stream, token).ConfigureAwait(false);
                        if (requestData.requestHeaders == null)
                        {
                            AbortWithRst(client);
                            return;
                        }

                        Exchange exchange = BuildExchange(requestData.requestHeaders, requestData.requestBody, context);
                        context.Exchange = exchange;
                        PublishExchange(exchange);

                        if (ShouldUseNotYetImplemented(exchange))
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
                            }

                            await HandleBlindConnectTunnelAsync(exchange, client, stream, token).ConfigureAwait(false);
                            return;
                        }

                        await ForwardToServerViaHttpClientBridgeAsync(exchange, stream, token).ConfigureAwait(false);
                    }
                    catch (Exception eX)
                    {
                        CApp.Log.Log($"! CaptureEngine client handling error: {eX.Message}");
                    }
                }
            }
        }

        private static async Task<(HTTPRequestHeaders requestHeaders, byte[] requestBody)> ReadHttpRequestAsync(NetworkStream stream, CancellationToken token)
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

        private static async Task<byte[]> ReadRemainingBodyAsync(NetworkStream stream, byte[] bodySoFar, int expectedLength, CancellationToken token)
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

        private static async Task<byte[]> ReadChunkedBodyToEndAsync(NetworkStream stream, byte[] bodySoFar, HTTPRequestHeaders headers, CancellationToken token)
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

        private static async Task ReadAndApplyTrailerHeadersAsync(NetworkStream stream, MemoryStream buffered, OffsetCursor cursor,
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

        private static async Task ConsumeChunkTerminatorAsync(NetworkStream stream, MemoryStream buffered, OffsetCursor cursor, CancellationToken token)
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

        private static async Task ReadExactlyAsync(NetworkStream stream, MemoryStream buffered, OffsetCursor cursor,
            int byteCount, CancellationToken token)
        {
            for (int i = 0; i < byteCount; i++)
            {
                int b = await ReadByteAsync(stream, buffered, cursor, token).ConfigureAwait(false);
                if (b < 0) throw new InvalidDataException("Unexpected EOF inside chunk payload.");
            }
        }

        private static async Task<string> ReadAsciiLineAsync(NetworkStream stream, MemoryStream buffered, OffsetCursor cursor, CancellationToken token)
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

        private static async Task<int> ReadByteAsync(NetworkStream stream, MemoryStream buffered, OffsetCursor cursor, CancellationToken token)
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
            xc.state = ExchangeState.ReadingRequest;
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

        private static bool ShouldUseNotYetImplemented(Exchange exchange)
        {
            if (exchange == null) return false;
            return exchange.hostname.OICEquals("localhost.fiddler");
        }

        private static bool ShouldDecryptConnectTunnel(Exchange exchange)
        {
            if (exchange == null || !exchange.HTTPMethodIs("CONNECT")) return false;
            if (!CaptureEnginePrefs.ShouldDecryptHttps()) return false;
            if (exchange["x-no-decrypt"] != null) return false;
            return true;
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
                    Task relayClientToServer = PumpStreamAsync(clientStream, upstreamStream, token);
                    Task relayServerToClient = PumpStreamAsync(upstreamStream, clientStream, token);

                    await Task.WhenAny(relayClientToServer, relayServerToClient).ConfigureAwait(false);
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

        private static async Task PumpStreamAsync(NetworkStream source, NetworkStream destination, CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            while (!token.IsCancellationRequested)
            {
                int read = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                if (read <= 0) return;
                await destination.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                await destination.FlushAsync(token).ConfigureAwait(false);
            }
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
