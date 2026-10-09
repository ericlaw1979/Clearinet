using Clearinet;
using ImportNetlog.WebFormats;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Mime;
using System.Text;

namespace TestExtensions
{
    [OfferFormat("HTTP Archive",
        "HTTP Archive (HAR) traffic capture files. See http://www.softwareishard.com/blog/har-12-spec/.",
        ".har")]
    public class HARFormatImport : IExchangeImporter
    {
        public Exchange[] ImportExchanges(string sFormat, Dictionary<string, object> dictOptions,
            EventHandler<ProgressEventArgs> evtProgress)
        {
            if (sFormat != "HTTP Archive")
                throw new ArgumentException("Unsupported import format.", nameof(sFormat));

            string content = null;
            string filename = null;
            if (dictOptions != null)
            {
                if (dictOptions.TryGetValue("Filename", out object fileOption))
                    filename = fileOption as string ?? throw new ArgumentException("Filename must be a string.");
                else if (dictOptions.TryGetValue("Content", out object contentOption))
                    content = contentOption as string ?? throw new ArgumentException("Content must be a string.");
            }

            if (content == null && String.IsNullOrEmpty(filename))
            {
                filename = Utilities.ObtainOpenFilename("Import HTTP Archive", "HTTP Archive (*.har, *.json)|*.har;*.json");
                if (String.IsNullOrEmpty(filename)) return null;
            }

            try
            {
                if (content == null)
                {
                    if (!NotifyProgress(evtProgress, 0, $"Reading HTTP Archive data from '{filename}'.")) return null;
                    content = File.ReadAllText(filename, Encoding.UTF8);
                    if (!NotifyProgress(evtProgress, 1, "HTTP Archive file read completed.")) return null;
                }

                if (!NotifyProgress(evtProgress, 2, "Parsing HAR JSON...")) return null;
                object parsed = JSON.JsonDecode(content.TrimStart('\uFEFF'), out JSON.JSONParseErrors errors);
                if (errors.iErrorIndex >= 0)
                    throw new InvalidDataException($"Invalid HAR JSON at character {errors.iErrorIndex}.");

                if (!NotifyProgress(evtProgress, 5, $"JSON parsing completed.")) return null;
                Hashtable root = RequireObject(parsed, "HAR");
                Hashtable log = RequireObject(root["log"], "log");
                ArrayList entries = RequireArray(log["entries"], "log.entries");
                List<Exchange> exchanges = new List<Exchange>(entries.Count);
                for (int i = 0; i < entries.Count; i++)
                {
                    if (!NotifyProgress(evtProgress, (float)i / entries.Count,
                        $"Importing HAR entry {i + 1} of {entries.Count}.")) return null;
                    exchanges.Add(ReadEntry(RequireObject(entries[i], $"entry {i + 1}")));
                }

                if (!NotifyProgress(evtProgress, 100, $"Imported {exchanges.Count} HAR entries.")) return null;
                return exchanges.ToArray();
            }
            catch (IOException eX)
            {
                CApp.ReportException(eX, "Failed to import HTTP Archive");
                return null;
            }
            catch (UnauthorizedAccessException eX)
            {
                CApp.ReportException(eX, "Failed to read HTTP Archive");
                return null;
            }
            catch (FormatException eX)
            {
                CApp.ReportException(eX, "Invalid HTTP Archive data");
                return null;
            }
            catch (ArgumentException eX)
            {
                CApp.ReportException(eX, "Invalid HTTP Archive data");
                return null;
            }
            catch (OverflowException eX)
            {
                CApp.ReportException(eX, "Invalid HTTP Archive timing or size");
                return null;
            }
        }

        private static Exchange ReadEntry(Hashtable entry)
        {
            Hashtable request = RequireObject(entry["request"], "request");
            Hashtable response = RequireObject(entry["response"], "response");
            string fullUrl = RequireString(request, "url");
            if (!Uri.TryCreate(fullUrl, UriKind.Absolute, out Uri uri))
                throw new InvalidDataException("HAR request URL must be absolute.");

            HTTPRequestHeaders requestHeaders = new HTTPRequestHeaders
            {
                HTTPMethod = RequireString(request, "method"),
                HTTPVersion = RequireString(request, "httpVersion"),
                UriScheme = uri.Scheme,
                RequestPath = uri.PathAndQuery
            };
            ReadHeaders(request["headers"], requestHeaders);
            if (!requestHeaders.Exists("Host")) requestHeaders.Add("Host", uri.Authority);

            HTTPResponseHeaders responseHeaders = new HTTPResponseHeaders
            {
                StatusCode = checked((int)GetNumber(response, "status")),
                StatusText = RequireString(response, "statusText"),
                HTTPVersion = RequireString(response, "httpVersion")
            };
            ReadHeaders(response["headers"], responseHeaders);

            ExchangeFlags flags = ExchangeFlags.ImportedFromOtherTool;
            if (uri.Scheme == "https") flags |= ExchangeFlags.IsHTTPS;
            if (uri.Scheme == "ftp") flags |= ExchangeFlags.IsFTP;

            byte[] requestBody = Array.Empty<byte>();
            bool hasRequestBody = false;
            if (request["postData"] is Hashtable postData)
            {
                AddContentType(requestHeaders, postData);
                if (postData["text"] is string)
                {
                    requestBody = ReadBody(postData, requestHeaders["Content-Type"]);
                    hasRequestBody = true;
                }
                else if (postData["params"] is ArrayList parameters &&
                    ContentTypeIs(requestHeaders["Content-Type"], "application/x-www-form-urlencoded"))
                {
                    List<string> pairs = new List<string>(parameters.Count);
                    foreach (object parameter in parameters)
                    {
                        Hashtable pair = RequireObject(parameter, "postData.params item");
                        pairs.Add(Utilities.UrlEncode(RequireString(pair, "name"), Encoding.UTF8) + "=" +
                            Utilities.UrlEncode(RequireString(pair, "value"), Encoding.UTF8));
                    }
                    requestBody = Encoding.UTF8.GetBytes(String.Join("&", pairs));
                    hasRequestBody = true;
                }
                else
                {
                    // Multipart parameters do not contain the original wire bytes or boundary layout.
                    flags |= ExchangeFlags.RequestBodyDropped;
                }
            }
            if (Object.Equals(request["_bodyMissing"], true) ||
                (!hasRequestBody && GetNumber(request, "bodySize", -1) > 0))
                flags |= ExchangeFlags.RequestBodyDropped;
            if (hasRequestBody) NormalizeBodyHeaders(requestHeaders, requestBody.Length);

            Hashtable responseContent = RequireObject(response["content"], "response.content");
            AddContentType(responseHeaders, responseContent);
            byte[] responseBody = Array.Empty<byte>();
            if (responseContent["text"] is string)
            {
                responseBody = ReadBody(responseContent, responseHeaders["Content-Type"]);
                NormalizeBodyHeaders(responseHeaders, responseBody.Length);
            }
            else if (Object.Equals(responseContent["_bodyMissing"], true) ||
                GetNumber(responseContent, "size", -1) > 0 || GetNumber(response, "bodySize", -1) > 0)
            {
                flags |= ExchangeFlags.ResponseBodyDropped;
            }

            Exchange exchange = Exchange.BuildFromData(false, requestHeaders, requestBody,
                responseHeaders, responseBody, flags);
            exchange.fullUrl = fullUrl;
            exchange.url = uri.PathAndQuery;
            exchange.hostname = uri.Host;
            exchange.port = uri.Port;
            exchange["x-imported-by"] = "HTTP Archive";
            CopyFlag(entry, "serverIPAddress", exchange, "x-hostIP");
            CopyFlag(entry, "connection", exchange, "x-har-connection");
            CopyFlag(entry, "pageref", exchange, "x-har-pageref");
            CopyFlag(entry, "comment", exchange, "ui-comments");
            ReadTimers(entry, exchange.Timers);
            return exchange;
        }

        private static bool ContentTypeIs(string sContentType, string sMediaType)
        {
            if (!sContentType.HasText()) return false;
            return sMediaType.OICEquals(sContentType.TrimAfter(';'));
        }

        private static void ReadHeaders(object value, HTTPHeaders headers)
        {
            foreach (object item in RequireArray(value, "headers"))
            {
                Hashtable header = RequireObject(item, "header");
                headers.Add(RequireString(header, "name"), RequireString(header, "value"));
            }
        }

        private static void AddContentType(HTTPHeaders headers, Hashtable body)
        {
            if (!headers.Exists("Content-Type") && body["mimeType"] is string mimeType && mimeType.Length > 0)
                headers.Add("Content-Type", mimeType);
        }

        private static byte[] ReadBody(Hashtable body, string contentType)
        {
            string text = RequireString(body, "text");
            string encoding = body["encoding"] as string;
            if (String.Equals(encoding, "base64", StringComparison.OrdinalIgnoreCase))
                return Convert.FromBase64String(text);
            if (!String.IsNullOrEmpty(encoding))
                throw new InvalidDataException($"Unsupported HAR body encoding: {encoding}.");

            string charset = String.IsNullOrEmpty(contentType) ? null : GetCharset(contentType);
            return (String.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset)).GetBytes(text);
        }

        private static string GetCharset(string contentType)
        {
            try
            {
                return new ContentType(contentType).CharSet;
            }
            catch (FormatException)
            {
                Trace.Write("illegal charset in HAR Content-Type: {contentType}");
                return null;
            }
        }

        private static void NormalizeBodyHeaders(HTTPHeaders headers, int length)
        {
            // HAR stores decoded content, not the compressed/chunked bytes described by wire headers.
            IEnumerable<HTTPHeaderItem> items = headers is HTTPRequestHeaders request
                ? (IEnumerable<HTTPHeaderItem>)request : (HTTPResponseHeaders)headers;
            foreach (HTTPHeaderItem header in items)
            {
                if (header.Name.OICEquals("Content-Encoding") || header.Name.OICEquals("Transfer-Encoding"))
                    header.Name = "X-HAR-Original-" + header.Name;
                else if (header.Name.OICEquals("Content-Length"))
                    header.Name = "X-HAR-Original-Content-Length";
            }
            headers.Add("Content-Length", length.ToString(CultureInfo.InvariantCulture));
        }

        private static void ReadTimers(Hashtable entry, ExchangeTimers timers)
        {
            DateTime start = DateTimeOffset.Parse(RequireString(entry, "startedDateTime"),
                CultureInfo.InvariantCulture, DateTimeStyles.None).LocalDateTime;
            Hashtable timings = RequireObject(entry["timings"], "timings");
            double blocked = Math.Max(0, GetNumber(timings, "blocked", -1));
            double dns = Math.Max(0, GetNumber(timings, "dns", -1));
            double connect = Math.Max(0, GetNumber(timings, "connect", -1));
            double ssl = Math.Max(0, GetNumber(timings, "ssl", -1));
            double send = Math.Max(0, GetNumber(timings, "send"));
            double wait = Math.Max(0, GetNumber(timings, "wait"));
            double receive = Math.Max(0, GetNumber(timings, "receive"));

            timers.ClientBeginRequest = timers.ProxyGotRequestHeaders = start;
            timers.DNSTime = checked((int)Math.Round(dns));
            // HAR's connect duration includes the TLS handshake.
            timers.TCPConnectTime = checked((int)Math.Round(Math.Max(0, connect - ssl)));
            timers.HTTPSHandshakeTime = checked((int)Math.Round(ssl));
            DateTime sendStart = start.AddMilliseconds(blocked + dns + connect);
            if (GetNumber(timings, "connect", -1) >= 0) timers.ServerConnected = sendStart;
            timers.ProxyBeginRequest = sendStart;
            timers.ClientDoneRequest = timers.ProxyDoneRequest = sendStart.AddMilliseconds(send);
            timers.ServerBeginResponse = timers.ProxyGotResponseHeaders =
                timers.ClientBeginResponse = timers.ProxyDoneRequest.AddMilliseconds(wait);
            timers.ServerDoneResponse = timers.ClientDoneResponse =
                start.AddMilliseconds(GetNumber(entry, "time", blocked + dns + connect + send + wait + receive));
        }

        private static Hashtable RequireObject(object value, string name)
        {
            return value as Hashtable ?? throw new InvalidDataException($"HAR {name} must be an object.");
        }

        private static ArrayList RequireArray(object value, string name)
        {
            return value as ArrayList ?? throw new InvalidDataException($"HAR {name} must be an array.");
        }

        private static string RequireString(Hashtable value, string name)
        {
            return value[name] as string ?? throw new InvalidDataException($"HAR {name} must be a string.");
        }

        private static double GetNumber(Hashtable value, string name, double? defaultValue = null)
        {
            if (value[name] is double number && !Double.IsNaN(number) && !Double.IsInfinity(number))
                return number;
            if (!value.ContainsKey(name) && defaultValue.HasValue) return defaultValue.Value;
            throw new InvalidDataException($"HAR {name} must be a finite number.");
        }

        private static void CopyFlag(Hashtable entry, string name, Exchange exchange, string flag)
        {
            if (entry[name] is string value) exchange[flag] = value;
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
