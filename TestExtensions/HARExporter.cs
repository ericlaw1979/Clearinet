using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using Clearinet;
using ImportNetlog.WebFormats;

namespace TestExtensions
{
    [OfferFormat("HTTP Archive",
        "HTTP Archive (HAR) traffic capture files. See http://www.softwareishard.com/blog/har-12-spec/.",
        ".har")]
    public class HARFormatExport : IExchangeExporter
    {
        public bool ExportExchanges(string sFormat, Exchange[] arrExchanges,
            Dictionary<string, object> dictOptions, EventHandler<ProgressEventArgs> evtProgress)
        {
            if (sFormat != "HTTP Archive")
                throw new ArgumentException("Unsupported export format.", nameof(sFormat));
            if (arrExchanges == null) throw new ArgumentNullException(nameof(arrExchanges));

            string filename = null;
            if (dictOptions != null && dictOptions.TryGetValue("Filename", out object option))
                filename = option as string ?? throw new ArgumentException("Filename must be a string.");
            if (String.IsNullOrEmpty(filename))
            {
                filename = Utilities.ObtainSaveFilename("Export HTTP Archive", "HTTP Archive (*.har)|*.har");
                if (String.IsNullOrEmpty(filename)) return false;
            }

            try
            {
                ArrayList entries = new ArrayList(arrExchanges.Length);
                ArrayList pages = new ArrayList();
                HashSet<string> pageRefs = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < arrExchanges.Length; i++)
                {
                    if (!NotifyProgress(evtProgress, (float)i / arrExchanges.Length,
                        $"Exporting HAR entry {i + 1} of {arrExchanges.Length}.")) return false;
                    Exchange exchange = arrExchanges[i] ??
                        throw new InvalidDataException($"Exchange {i + 1} is null.");
                    Hashtable entry = WriteEntry(exchange);
                    entries.Add(entry);
                    if (entry["pageref"] is string pageRef && pageRefs.Add(pageRef))
                    {
                        pages.Add(new Hashtable
                        {
                            ["id"] = pageRef,
                            ["title"] = pageRef,
                            ["startedDateTime"] = entry["startedDateTime"],
                            ["pageTimings"] = new Hashtable { ["onContentLoad"] = -1, ["onLoad"] = -1 }
                        });
                    }
                }

                Hashtable archive = new Hashtable
                {
                    ["log"] = new Hashtable
                    {
                        ["version"] = "1.2",
                        ["creator"] = new Hashtable
                        {
                            ["name"] = "Clearinet",
                            ["version"] = typeof(Exchange).Assembly.GetName().Version.ToString()
                        },
                        ["pages"] = pages,
                        ["entries"] = entries
                    }
                };
                string json = JSON.JsonEncode(archive) ??
                    throw new InvalidDataException("Unable to serialize HTTP Archive.");
                if (!NotifyProgress(evtProgress, 1, "Writing HTTP Archive.")) return false;
                File.WriteAllText(filename, json, new UTF8Encoding(false));
                return true;
            }
            catch (IOException eX)
            {
                CApp.ReportException(eX, "Failed to export HTTP Archive");
                return false;
            }
            catch (UnauthorizedAccessException eX)
            {
                CApp.ReportException(eX, "Failed to write HTTP Archive");
                return false;
            }
            catch (ArgumentException eX)
            {
                CApp.ReportException(eX, "Invalid HTTP Archive export data");
                return false;
            }
            catch (FormatException eX)
            {
                CApp.ReportException(eX, "Invalid HTTP Archive export data");
                return false;
            }
        }

        private static Hashtable WriteEntry(Exchange exchange)
        {
            HTTPRequestHeaders request = exchange.RequestHeaders ??
                throw new InvalidDataException("Cannot export an exchange without request headers.");
            string fullUrl = exchange.fullUrl;
            if (String.IsNullOrEmpty(fullUrl))
                fullUrl = request.UriScheme + "://" + request["Host"] + request.RequestPath;
            if (!Uri.TryCreate(fullUrl, UriKind.Absolute, out Uri uri))
                throw new InvalidDataException("Cannot export an exchange without an absolute URL.");

            HTTPResponseHeaders response = exchange.ResponseHeaders;
            byte[] requestBytes = exchange.RequestBody ?? Array.Empty<byte>();
            byte[] responseBytes = exchange.ResponseBody ?? Array.Empty<byte>();
            bool requestMissing = exchange.isFlagSet(ExchangeFlags.RequestBodyDropped);
            bool responseMissing = response == null || exchange.isFlagSet(ExchangeFlags.ResponseBodyDropped);
            Hashtable harRequest = new Hashtable
            {
                ["method"] = request.HTTPMethod,
                ["url"] = fullUrl,
                ["httpVersion"] = request.HTTPVersion,
                ["headers"] = WriteHeaders(request),
                ["cookies"] = new ArrayList(),
                ["queryString"] = WriteQuery(uri),
                ["headersSize"] = -1,
                ["bodySize"] = requestMissing ? -1 : requestBytes.Length
            };
            if (!requestMissing && requestBytes.Length > 0)
            {
                byte[] decoded = DecodeBody(request, requestBytes);
                Hashtable postData = new Hashtable { ["mimeType"] = GetMimeType(request) };
                WriteRequestText(postData, decoded);
                harRequest["postData"] = postData;
            }
            if (requestMissing)
            {
                harRequest["_bodyMissing"] = true;
                harRequest["comment"] = "Request body was not captured.";
            }

            Hashtable content = new Hashtable
            {
                ["size"] = -1,
                ["mimeType"] = GetMimeType(response)
            };
            if (!responseMissing)
            {
                byte[] decoded = DecodeBody(response, responseBytes);
                content["size"] = decoded.Length;
                content["text"] = Convert.ToBase64String(decoded);
                content["encoding"] = "base64";
                if (response.Exists("Content-Encoding"))
                    content["compression"] = decoded.Length - responseBytes.Length;
            }
            else
            {
                content["_bodyMissing"] = true;
                content["comment"] = "Response body was not captured.";
            }

            Hashtable harResponse = new Hashtable
            {
                ["status"] = response?.StatusCode ?? 0,
                ["statusText"] = response?.StatusText ?? String.Empty,
                ["httpVersion"] = response?.HTTPVersion ?? String.Empty,
                ["headers"] = WriteHeaders(response),
                ["cookies"] = new ArrayList(),
                ["content"] = content,
                ["redirectURL"] = response?["Location"] ?? String.Empty,
                ["headersSize"] = -1,
                ["bodySize"] = responseMissing ? -1 : responseBytes.Length
            };

            ExchangeTimers timers = exchange.Timers ?? new ExchangeTimers();
            DateTime start = timers.ClientBeginRequest;
            if (start == DateTime.MinValue) start = timers.ProxyGotRequestHeaders;
            if (start == DateTime.MinValue) start = timers.ProxyBeginRequest;
            if (start == DateTime.MinValue) start = timers.ServerBeginResponse;
            if (start == DateTime.MinValue) start = timers.ClientDoneResponse;
            bool timeMissing = start == DateTime.MinValue;
            // HAR requires a timestamp even when the exchange has none; explicitly mark the placeholder.
            if (timeMissing) start = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            double dns = timers.DNSTime > 0 ? timers.DNSTime : -1;
            double ssl = timers.HTTPSHandshakeTime > 0 ? timers.HTTPSHandshakeTime : -1;
            double connect = timers.TCPConnectTime > 0 || ssl >= 0 || timers.ServerConnected != DateTime.MinValue
                ? Math.Max(0, timers.TCPConnectTime) + Math.Max(0, ssl) : -1;
            double blocked = Math.Max(0, Duration(start, timers.ProxyBeginRequest) -
                Math.Max(0, dns) - Math.Max(0, connect));
            double send = Duration(timers.ProxyBeginRequest, timers.ProxyDoneRequest);
            double wait = Duration(timers.ProxyDoneRequest, timers.ServerBeginResponse);
            double receive = Duration(timers.ServerBeginResponse, timers.ServerDoneResponse);
            double total = blocked + Math.Max(0, dns) + Math.Max(0, connect) + send + wait + receive;
            double elapsed = Duration(start, timers.ClientDoneResponse);
            blocked += Math.Max(0, elapsed - total);
            total = Math.Max(total, elapsed);
            Hashtable entry = new Hashtable
            {
                ["startedDateTime"] = new DateTimeOffset(start).ToString("o", CultureInfo.InvariantCulture),
                ["time"] = total,
                ["request"] = harRequest,
                ["response"] = harResponse,
                ["cache"] = new Hashtable(),
                ["timings"] = new Hashtable
                {
                    ["blocked"] = blocked, ["dns"] = dns, ["connect"] = connect,
                    ["ssl"] = ssl, ["send"] = send, ["wait"] = wait, ["receive"] = receive
                }
            };
            if (timeMissing) entry["comment"] = "Capture timestamp unavailable; startedDateTime is a placeholder.";
            CopyFlag(exchange, "x-hostIP", entry, "serverIPAddress");
            CopyFlag(exchange, "x-har-connection", entry, "connection");
            CopyFlag(exchange, "x-har-pageref", entry, "pageref");
            string comment = exchange["ui-comments"];
            if (!String.IsNullOrEmpty(comment))
                entry["comment"] = timeMissing ? entry["comment"] + " " + comment : comment;
            return entry;
        }

        private static ArrayList WriteHeaders(HTTPHeaders headers)
        {
            ArrayList result = new ArrayList();
            if (headers == null) return result;
            IEnumerable<HTTPHeaderItem> items = headers is HTTPRequestHeaders request
                ? (IEnumerable<HTTPHeaderItem>)request : (HTTPResponseHeaders)headers;
            foreach (HTTPHeaderItem header in items)
                result.Add(new Hashtable { ["name"] = header.Name, ["value"] = header.Value });
            return result;
        }

        private static ArrayList WriteQuery(Uri uri)
        {
            ArrayList result = new ArrayList();
            if (uri.Query.Length < 2) return result;
            foreach (string pair in uri.Query.Substring(1).Split('&'))
            {
                int separator = pair.IndexOf('=');
                string name = separator < 0 ? pair : pair.Substring(0, separator);
                string value = separator < 0 ? String.Empty : pair.Substring(separator + 1);
                result.Add(new Hashtable { ["name"] = Utilities.UrlDecode(name), ["value"] = Utilities.UrlDecode(value) });
            }
            return result;
        }

        private static string GetMimeType(HTTPHeaders headers)
        {
            string mimeType = headers?["Content-Type"];
            return String.IsNullOrEmpty(mimeType) ? "application/octet-stream" : mimeType;
        }

        private static void WriteRequestText(Hashtable postData, byte[] bytes)
        {
            string charset = new ContentType((string)postData["mimeType"]).CharSet;
            Encoding encoding = String.IsNullOrEmpty(charset) ? new UTF8Encoding(false, true) :
                Encoding.GetEncoding(charset, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            try
            {
                string text = encoding.GetString(bytes);
                if (bytes.SequenceEqual(encoding.GetBytes(text)))
                {
                    postData["text"] = text;
                    return;
                }
            }
            catch (DecoderFallbackException)
            {
                WriteBinaryRequest(postData, bytes);
                return;
            }
            WriteBinaryRequest(postData, bytes);
        }

        private static void WriteBinaryRequest(Hashtable postData, byte[] bytes)
        {
            // HAR has no standard binary postData representation. Match the importer's base64 extension.
            postData["text"] = Convert.ToBase64String(bytes);
            postData["encoding"] = "base64";
            postData["comment"] = "Binary request body uses the Clearinet base64 postData extension.";
        }

        private static byte[] DecodeBody(HTTPHeaders headers, byte[] bytes)
        {
            if (bytes.Length == 0) return bytes;
            if (headers.Exists("Transfer-Encoding"))
            {
                string transfer = headers["Transfer-Encoding"].Trim();
                if (!transfer.OICEquals("chunked"))
                    throw new InvalidDataException($"Unsupported transfer encoding: {transfer}.");
                bytes = DecodeChunks(bytes);
            }
            string encodings = headers["Content-Encoding"];
            if (String.IsNullOrWhiteSpace(encodings)) return bytes;
            string[] tokens = encodings.Split(',');
            for (int i = tokens.Length - 1; i >= 0; i--)
            {
                switch (tokens[i].Trim().ToLowerInvariant())
                {
                    case "identity": break;
                    case "gzip": bytes = Utilities.GzipExpand(bytes); break;
                    case "deflate": bytes = Utilities.CompatibleDeflaterExpand(bytes); break;
                    default: throw new InvalidDataException($"Unsupported content encoding: {tokens[i]}.");
                }
            }
            return bytes;
        }

        private static byte[] DecodeChunks(byte[] bytes)
        {
            int offset = 0;
            using (MemoryStream decoded = new MemoryStream())
            {
                while (true)
                {
                    string line = ReadChunkLine(bytes, ref offset);
                    int extension = line.IndexOf(';');
                    string sizeText = extension < 0 ? line : line.Substring(0, extension);
                    if (!Int32.TryParse(sizeText.Trim(), NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture, out int size) || size < 0)
                        throw new InvalidDataException("Invalid HTTP chunk size.");
                    if (size == 0)
                    {
                        while (ReadChunkLine(bytes, ref offset).Length > 0) { }
                        if (offset != bytes.Length) throw new InvalidDataException("Unexpected data after HTTP chunks.");
                        return decoded.ToArray();
                    }
                    if (size > bytes.Length - offset - 2)
                        throw new InvalidDataException("Truncated HTTP chunk.");
                    decoded.Write(bytes, offset, size);
                    offset += size;
                    if (bytes[offset++] != 13 || bytes[offset++] != 10)
                        throw new InvalidDataException("Invalid HTTP chunk terminator.");
                }
            }
        }

        private static string ReadChunkLine(byte[] bytes, ref int offset)
        {
            int start = offset;
            while (offset < bytes.Length - 1)
            {
                if (bytes[offset] == 13 && bytes[offset + 1] == 10)
                {
                    string line = Encoding.ASCII.GetString(bytes, start, offset - start);
                    offset += 2;
                    return line;
                }
                offset++;
            }
            throw new InvalidDataException("Truncated HTTP chunk header or trailer.");
        }

        private static double Duration(DateTime start, DateTime end)
        {
            return start == DateTime.MinValue || end == DateTime.MinValue ? 0 : Math.Max(0, (end - start).TotalMilliseconds);
        }

        private static void CopyFlag(Exchange exchange, string flag, Hashtable entry, string name)
        {
            string value = exchange[flag];
            if (!String.IsNullOrEmpty(value)) entry[name] = value;
        }

        private bool NotifyProgress(EventHandler<ProgressEventArgs> handler, float completion, string status)
        {
            ProgressEventArgs progress = new ProgressEventArgs(completion, status);
            handler?.Invoke(this, progress);
            return !progress.CancellationRequested;
        }

        public void Dispose() { }
    }
}
