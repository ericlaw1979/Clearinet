using System;
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
                //_listener = new TcpListener(_options.ListenAddress, _options.ListenPort);
                _listener = TcpListener.Create(_options.ListenPort);
                //_listener.Server.DualMode = _options.ListenAddress.Equals(IPAddress.IPv6Any);
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
                        byte[] requestBytes = await ReadRequestHeaderBytesAsync(stream, token).ConfigureAwait(false);
                        if (requestBytes == null || requestBytes.Length == 0)
                        {
                            return;
                        }

                        Exchange exchange = BuildExchange(requestBytes, context);
                        context.Exchange = exchange;
                        PublishExchange(exchange);

                        await SendNotYetImplementedAsync(stream, token).ConfigureAwait(false);
                    }
                    catch (Exception eX)
                    {
                        CApp.Log.Log($"! CaptureEngine client handling error: {eX.Message}");
                    }
                }
            }
        }

        private static async Task<byte[]> ReadRequestHeaderBytesAsync(NetworkStream stream, CancellationToken token)
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
                        return headersOnly;
                    }

                    if (endedWith == Parser.HeadersEndedWith.Oversized)
                    {
                        throw new InvalidDataException("Request headers exceeded maximum allowed size.");
                    }
                }

                return ms.ToArray();
            }
        }

        private static Exchange BuildExchange(byte[] requestHeaderBytes, CaptureConnectionContext context)
        {
            string requestText = CONFIG.encodingOfHeaders.GetString(requestHeaderBytes);
            var (method, path, version, headerLines, origin) = Parser.ParseRequestHeaders(requestText);

            HTTPRequestHeaders headers = new HTTPRequestHeaders(path, headerLines)
            {
                HTTPMethod = method,
                HTTPVersion = version
            };

            if (origin.HasText() && origin.Contains(":"))
            {
                headers.UriScheme = origin.TrimAfter(":");
                if (!headers.Exists("Host"))
                {
                    headers["Host"] = origin.TrimBefore("://");
                }
            }

            Exchange xc = new Exchange(headers, Array.Empty<byte>());
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
    }
}
