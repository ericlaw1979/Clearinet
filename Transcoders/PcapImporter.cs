using Clearinet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace TestExtensions
{
    [OfferFormat("Packet Capture", "PCAP and PCAPNG captures of plaintext HTTP/1.x over TCP. TLS, HTTP/2, and HTTP/3 are not decrypted or imported.", ".pcap;.pcapng;.cap")]
    public sealed class PcapFormatImport : IExchangeImporter
    {
        public Exchange[] ImportExchanges(string sFormat, Dictionary<string, object> dictOptions,
            EventHandler<ProgressEventArgs> evtProgress)
        {
            if (sFormat != "Packet Capture") throw new ArgumentException("Unsupported import format.", nameof(sFormat));
            string filename = null;
            byte[] content = null;
            if (dictOptions != null)
            {
                object option;
                if (dictOptions.TryGetValue("Filename", out option))
                    filename = option as string ?? throw new ArgumentException("Filename must be a string.");
                else if (dictOptions.TryGetValue("Content", out option))
                    content = option as byte[] ?? throw new ArgumentException("Capture Content must be a byte array.");
            }
            if (content == null && String.IsNullOrEmpty(filename))
            {
                filename = Utilities.ObtainOpenFilename("Import Packet Capture", "Packet captures (*.pcap, *.pcapng, *.cap)|*.pcap;*.pcapng;*.cap");
                if (String.IsNullOrEmpty(filename)) return null;
            }

            // Throttle status updates to once per percent.
            int lastPct = -1;

            try
            {
                Func<int, string, bool> progress = delegate (int percent, string status)
                {
                    if (percent == lastPct) return true;
                    lastPct = percent;
                    ProgressEventArgs args = new ProgressEventArgs(percent / 100f, status);
                    if (evtProgress != null) evtProgress(this, args);
                    return !args.CancellationRequested;
                };
                if (!progress(0, "Opening packet capture.")) return null;
                PcapDecoder decoder = new PcapDecoder(progress);
                IList<PcapExchange> captured;
                using (Stream stream = content != null ? (Stream)new MemoryStream(content, false) :
                    new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length > PcapReader.MaxCapture) throw new InvalidDataException("Capture exceeds the 256 MiB import limit.");
                    captured = decoder.Read(stream);
                }
                List<Exchange> exchanges = new List<Exchange>(captured.Count);
                for (int i = 0; i < captured.Count; i++)
                {
                    if (!progress(95 + i * 5 / Math.Max(1, captured.Count), "Creating imported HTTP exchange " + (i + 1) + ".")) return null;
                    exchanges.Add(Build(captured[i]));
                }
                if (!progress(100, decoder.Summary)) return null;
                CApp.Log.Log(decoder.Summary);
                return exchanges.ToArray();
            }
            catch (OperationCanceledException) { return null; }
            catch (IOException ex) { CApp.ReportException(ex, "Failed to import packet capture"); return null; }
            catch (UnauthorizedAccessException ex) { CApp.ReportException(ex, "Failed to read packet capture"); return null; }
            catch (FormatException ex) { CApp.ReportException(ex, "Invalid captured HTTP data"); return null; }
            catch (ArgumentException ex) { CApp.ReportException(ex, "Invalid captured HTTP data"); return null; }
            catch (OverflowException ex) { CApp.ReportException(ex, "Invalid packet capture size or timing"); return null; }
        }

        private static Exchange Build(PcapExchange item)
        {
            HTTPRequestHeaders request = HTTPParser.ParseRequest(item.RequestHeaders);
            request.UriScheme = "http";
            Uri absolute;
            if (item.Method != "CONNECT" && Uri.TryCreate(item.Target, UriKind.Absolute, out absolute) &&
                (absolute.Scheme == "http" || absolute.Scheme == "https"))
            {
                request.UriScheme = absolute.Scheme;
                request.RequestPath = absolute.PathAndQuery;
                request["Host"] = absolute.Authority;
            }
            else
            {
                request.RequestPath = item.Target;
                if (!request.Exists("Host")) request.Add("Host", item.Method == "CONNECT" ? item.Target : Authority(item.ServerIP, item.ServerPort));
            }
            // BuildFromData synthesizes a 200 if passed null: missing captures must instead remain visibly unsuccessful.
            HTTPResponseHeaders response = item.ResponseHeaders == null ? new HTTPResponseHeaders
            {
                HTTPVersion = request.HTTPVersion,
                StatusCode = 0,
                StatusText = "Response not captured"
            } : HTTPParser.ParseResponse(item.ResponseHeaders);
            ExchangeFlags flags = ExchangeFlags.ImportedFromOtherTool;
            if (request.UriScheme == "https") flags |= ExchangeFlags.IsHTTPS;
            if (item.RequestIncomplete) flags |= ExchangeFlags.RequestBodyDropped;
            if (item.ResponseIncomplete) flags |= ExchangeFlags.ResponseBodyDropped;
            Exchange exchange = Exchange.BuildFromData(false, request, item.RequestBody, response, item.ResponseBody, flags);
            exchange.clientIP = item.ClientIP;
            exchange.port = item.ServerPort;
            exchange["x-imported-by"] = "Packet Capture";
            exchange["x-hostIP"] = item.ServerIP;
            exchange["x-clientport"] = item.ClientPort.ToString(CultureInfo.InvariantCulture);
            exchange["x-pcap-serverport"] = item.ServerPort.ToString(CultureInfo.InvariantCulture);
            if (item.Informationals.Length > 0) exchange["x-pcap-informational"] = item.Informationals;
            if (item.RequestIncomplete || item.ResponseIncomplete)
                exchange["ui-comments"] = "Incomplete packet capture: " + (item.RequestIncomplete ? "request body missing bytes; " : "") +
                    (item.ResponseHeaders == null ? "response not captured." : item.ResponseIncomplete ? "response body missing bytes or reliable TCP closure." : "");
            ExchangeTimers timers = exchange.Timers;
            timers.ClientBeginRequest = timers.ProxyBeginRequest = item.RequestStart;
            timers.ProxyGotRequestHeaders = item.RequestHeadersTime;
            timers.ClientDoneRequest = timers.ProxyDoneRequest = item.RequestEnd;
            timers.ServerBeginResponse = timers.ClientBeginResponse = item.ResponseStart;
            timers.ProxyGotResponseHeaders = item.ResponseHeadersTime;
            timers.ServerDoneResponse = timers.ClientDoneResponse = item.ResponseEnd;
            return exchange;
        }

        private static string Authority(string address, int port)
        {
            return (address.IndexOf(':') >= 0 ? "[" + address + "]" : address) + (port == 80 ? "" : ":" + port.ToString(CultureInfo.InvariantCulture));
        }

        public void Dispose() { }
    }
}
