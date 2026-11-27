using Clearinet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Extensions
{
    public sealed class PerformanceResponseInspector : ResponseInspectorBase
    {
        private PerformanceView performanceViewer;

        public override string TabTitle => "Performance";
        public override int GetOrder() => 50;
        public override int ScoreForExchange(Exchange exchange) => -1;
        public override InspectorFlags GetFlags() => InspectorFlags.HideInAutoResponder;

        public override void AddToTab(TabPage tab)
        {
            performanceViewer = new PerformanceView { Dock = DockStyle.Fill };
            tab.Controls.Add(performanceViewer);
        }

        public override void SetFontSize(float size) => performanceViewer?.SetFontSize(size);

        public override void AssignExchange(Exchange exchange)
        {
            headers = exchange?.ResponseHeaders;
            body = exchange?.ResponseBody;
            bReadOnly = true;
            performanceViewer?.Assign(exchange);
        }

        public override void Assign(HTTPResponseHeaders responseHeaders, byte[] responseBody, bool readOnly)
        {
            headers = responseHeaders;
            body = responseBody;
            bReadOnly = true;
            performanceViewer?.Assign(responseHeaders, responseBody);
        }

        public override void Clear()
        {
            headers = null;
            body = null;
            performanceViewer?.Clear();
        }

        public override bool bDirty
        {
            get => false;
            set { }
        }
    }

    public sealed class PerformanceView : UserControl
    {
        private readonly RichTextBox textView;

        public PerformanceView()
        {
            textView = new RichTextBox
            {
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                ReadOnly = true,
                DetectUrls = false,
                BackColor = SystemColors.Window,
                ForeColor = SystemColors.WindowText,
                WordWrap = true,
                Font = new Font("Consolas", 9.0f)
            };
            Controls.Add(textView);
        }

        public void SetFontSize(float sizeInPoints)
        {
            Font oldFont = textView.Font;
            textView.Font = new Font("Consolas", sizeInPoints);
            oldFont.Dispose();
        }

        public void Assign(Exchange exchange)
        {
            textView.Text = exchange?.isTunnel == true
                ? "Performance analysis is not applicable to a tunnel."
                : PerformanceAnalysis.Describe(exchange?.ResponseHeaders, exchange?.ResponseBody,
                    exchange?.RequestHeaders, exchange?.Timers);
        }

        public void Assign(HTTPResponseHeaders headers, byte[] body)
        {
            textView.Text = PerformanceAnalysis.Describe(headers, body, null, null);
        }

        public void Clear() => textView.Clear();
    }

    internal static class PerformanceAnalysis
    {
        internal static string Describe(HTTPResponseHeaders headers, byte[] body,
            HTTPRequestHeaders request, ExchangeTimers timers)
        {
            if (headers == null) return "No response is available.";

            var output = new StringBuilder();
            output.AppendLine("PERFORMANCE");
            output.AppendLine(new string('-', 32));
            var directives = ParseDirectives(JoinHeaders(headers, "Cache-Control"));
            output.AppendLine("Cacheability: " + DescribeCacheability(headers, request, directives));
            output.AppendLine("Browser freshness interval: " + DescribeFreshness(headers, directives, timers, false));
            output.AppendLine("Shared-cache freshness interval: " + DescribeFreshness(headers, directives, timers, true));
            if (directives.ContainsKey("no-cache"))
                output.AppendLine("Reuse: no-cache requires validation before reuse (field-qualified directives may limit this to named headers).");
            if (directives.ContainsKey("must-revalidate") || directives.ContainsKey("proxy-revalidate"))
                output.AppendLine("Reuse: stale responses require validation in the caches covered by the revalidation directive.");
            if (headers.Exists("Age")) output.AppendLine("Age: " + headers["Age"] + " seconds (freshness intervals above are lifetimes, not remaining freshness).");
            if (headers.Exists("Vary")) output.AppendLine("Vary: " + JoinHeaders(headers, "Vary"));
            output.AppendLine("ETag: " + DescribeETag(headers["ETag"]));
            output.AppendLine("Last-Modified: " + DescribeLastModified(headers["Last-Modified"]));
            if (!headers.Exists("ETag") && !headers.Exists("Last-Modified"))
                output.AppendLine("Validators: none; a stale response cannot use these validators for a conditional request.");
            output.AppendLine();
            output.AppendLine("Compression: " + DescribeCompression(headers, body, request));
            output.AppendLine();
            bool inconsistent = timers != null && timers.ServerBeginResponse != DateTime.MinValue &&
                timers.ServerDoneResponse != DateTime.MinValue && timers.ServerDoneResponse < timers.ServerBeginResponse;
            output.AppendLine("Time to First Byte (TTFB): " + (inconsistent ? "Unavailable (inconsistent capture timers)." :
                DescribeTiming(timers?.ProxyDoneRequest ?? DateTime.MinValue, timers?.ServerBeginResponse ?? DateTime.MinValue, 1000)));
            output.AppendLine("Time to Last Byte (TTLB): " + (inconsistent ? "Unavailable (inconsistent capture timers)." :
                DescribeTiming(timers?.ProxyDoneRequest ?? DateTime.MinValue, timers?.ServerDoneResponse ?? DateTime.MinValue, 5000)));
            output.AppendLine("Timings are measured at the proxy, from the completed upstream request to the first/last server byte; they exclude DNS, connect, TLS and client delivery.");
            output.AppendLine("Warnings are advisory thresholds (TTFB >= 1 second, TTLB >= 5 seconds), not a measured baseline. Large downloads or streaming responses can legitimately have a long TTLB.");
            return output.ToString();
        }

        private static string DescribeCacheability(HTTPResponseHeaders headers, HTTPRequestHeaders request,
            Dictionary<string, List<string>> directives)
        {
            if (headers.StatusCode < 200)
                return "Informational responses are not independently cacheable.";
            if (headers.StatusCode == 304)
                return "304 validates an existing cached representation; it is not independently stored as a response body.";
            var requestDirectives = ParseDirectives(request == null ? string.Empty : JoinHeaders(request, "Cache-Control"));
            if (directives.ContainsKey("no-store") || requestDirectives.ContainsKey("no-store"))
                return "Storage prohibited by no-store.";
            if (JoinHeaders(headers, "Vary").Split(',').Any(value => value.Trim() == "*"))
                return "Vary: * prevents reuse without contacting the origin.";
            if (request != null && !request.HTTPMethod.OICEquals("GET") && !request.HTTPMethod.OICEquals("HEAD"))
                return "Method-specific caching rules apply; this inspector evaluates ordinary GET/HEAD caching.";
            bool defaultCacheable = IsHeuristicallyCacheable(headers.StatusCode);
            bool explicitCaching = directives.ContainsKey("max-age") || directives.ContainsKey("s-maxage") ||
                directives.ContainsKey("public") || directives.ContainsKey("private") || headers.Exists("Expires");
            if (!defaultCacheable && !explicitCaching)
                return "Not ordinarily cacheable: status requires explicit caching permission.";
            if (directives.ContainsKey("private"))
                return "Browser storage permitted; shared storage restricted by private (field-qualified private may permit storage after removing named headers).";
            if (request?.Exists("Authorization") == true && !directives.ContainsKey("public") &&
                !directives.ContainsKey("s-maxage") && !directives.ContainsKey("must-revalidate"))
                return "Browser storage permitted; shared storage prohibited for an authorized request without explicit permission.";
            if (request == null)
                return "Response permits storage, subject to request method/authorization and cache policy (request unavailable).";
            return "Browser and shared storage permitted, subject to freshness, validation and cache policy.";
        }

        private static bool IsHeuristicallyCacheable(int status)
        {
            return status == 200 || status == 203 || status == 204 || status == 206 ||
                status == 300 || status == 301 || status == 308 || status == 404 ||
                status == 405 || status == 410 || status == 414 || status == 501;
        }

        private static string DescribeFreshness(HTTPResponseHeaders headers,
            Dictionary<string, List<string>> directives, ExchangeTimers timers, bool shared)
        {
            if (directives.ContainsKey("no-store")) return "Not applicable (no-store).";
            if (shared && directives.TryGetValue("private", out List<string> privateValues) && privateValues.Any(string.IsNullOrEmpty))
                return "Not applicable (private).";
            string name = shared && directives.ContainsKey("s-maxage") ? "s-maxage" : "max-age";
            if (directives.TryGetValue(name, out List<string> values))
            {
                if (values.Count != 1 || !long.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out long seconds))
                    return "0 seconds (invalid or duplicate " + name + "; treat as stale).";
                return FormatDuration(seconds) + " (explicit " + name + ").";
            }

            bool hasDate = TryParseHttpDate(headers["Date"], out DateTimeOffset date);
            if (!hasDate && timers != null && timers.ProxyGotResponseHeaders != DateTime.MinValue)
            {
                date = new DateTimeOffset(timers.ProxyGotResponseHeaders);
                hasDate = true;
            }
            if (headers.Exists("Expires"))
            {
                if (!TryParseHttpDate(headers["Expires"], out DateTimeOffset expires))
                    return "0 seconds (invalid Expires; already expired).";
                if (!hasDate) return "Unavailable (Expires needs Date or a captured response time).";
                return FormatDuration((long)Math.Max(0, (expires - date).TotalSeconds)) + " (explicit Expires).";
            }
            if (IsHeuristicallyCacheable(headers.StatusCode) && hasDate &&
                TryParseHttpDate(headers["Last-Modified"], out DateTimeOffset modified) && date >= modified)
            {
                return FormatDuration((long)((date - modified).TotalSeconds * 0.10)) +
                    " (heuristic estimate: 10% of Date minus Last-Modified; cache implementations vary).";
            }
            return "Unspecified; no usable explicit lifetime or Last-Modified heuristic.";
        }

        private static string DescribeETag(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "None.";
            value = value.Trim();
            bool weak = value.StartsWith("W/", StringComparison.Ordinal);
            string tag = weak ? value.Substring(2) : value;
            if (tag.Length < 2 || tag[0] != '"' || tag[tag.Length - 1] != '"' ||
                tag.Substring(1, tag.Length - 2).Any(c => c == '"' || c < 0x21 || c == 0x7f))
                return value + " (invalid entity tag).";
            return value + (weak ? " (weak validator; usable with If-None-Match)." : " (strong validator; usable with If-None-Match).");
        }

        private static string DescribeLastModified(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "None.";
            return value + (TryParseHttpDate(value, out _) ? " (usable with If-Modified-Since)." : " (invalid HTTP date).");
        }

        private static string DescribeCompression(HTTPResponseHeaders headers, byte[] body, HTTPRequestHeaders request)
        {
            if (request?.HTTPMethod.OICEquals("HEAD") == true || headers.StatusCode == 204 || headers.StatusCode == 304)
                return "No response body is expected; Content-Encoding, if present, describes the representation.";
            string encoding = JoinHeaders(headers, "Content-Encoding");
            string[] encodings = encoding.Split(',').Select(value => value.Trim()).Where(value => value.Length > 0).ToArray();
            if (encodings.Any(value => !value.OICEquals("identity")))
            {
                bool compressed = encodings.Any(value => value.OICEquals("gzip") || value.OICEquals("br") ||
                    value.OICEquals("deflate") || value.OICEquals("zstd"));

                var sCompressionRatio = string.Empty;
                if (compressed)
                {
                    // Compute the compression effectiveness.
                    int iDeliverySize = body?.Length ?? 0;
                    var bodyOriginal = HTTPParser.DecodeBodyFromHeaders(headers, body);
                    sCompressionRatio = $"Compressed to {100 * iDeliverySize / (bodyOriginal?.Length > 0 ? bodyOriginal.Length : 1f):N2}% of original ({iDeliverySize:N0}/{bodyOriginal.Length:N0})";
                }

                return (compressed ? "using " : "Content-Encoding (unrecognized): ") + encoding + $". {sCompressionRatio}";
            }
            string contentType = headers["Content-Type"].TrimAfter(';').Trim();
            bool compressible = contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
                contentType.OICEquals("application/json") || contentType.OICEquals("application/javascript") ||
                contentType.OICEquals("application/xml") || contentType.OICEquals("image/svg+xml") ||
                contentType.EndsWith("+json", StringComparison.OrdinalIgnoreCase) || contentType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase);
            return "None (no content encoding)." + (compressible && body?.Length >= 1024
                ? " WARNING: uncompressed text-like body >= 1 KiB; consider compression."
                : " Small or already-compressed formats may not benefit from compression.");
        }

        // TODO: This should
        private static string DescribeTiming(DateTime start, DateTime end, double warningMilliseconds)
        {
            if (start == DateTime.MinValue || end == DateTime.MinValue)
                return "Unavailable (missing or incomplete capture timers).";
            double milliseconds = (end - start).TotalMilliseconds;
            if (milliseconds < 0) return "Unavailable (inconsistent capture timers).";
            return milliseconds.ToString("N0", CultureInfo.InvariantCulture) + " ms" +
                (milliseconds >= warningMilliseconds ? " - WARNING: exceeds advisory threshold." : " - below advisory threshold.");
        }

        private static string JoinHeaders(IEnumerable<HTTPHeaderItem> headers, string name)
        {
            return string.Join(", ", headers.Where(header => header.Name.OICEquals(name)).Select(header => header.Value));
        }

        private static Dictionary<string, List<string>> ParseDirectives(string value)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            bool quoted = false;
            int start = 0;
            for (int i = 0; i <= value.Length; i++)
            {
                if (quoted && i + 1 < value.Length && value[i] == '\\')
                {
                    i++;
                    continue;
                }
                if (i < value.Length && value[i] == '"') quoted = !quoted;
                if (i < value.Length && (value[i] != ',' || quoted)) continue;
                string directive = value.Substring(start, i - start).Trim();
                start = i + 1;
                if (directive.Length == 0) continue;
                int equals = directive.IndexOf('=');
                string name = equals < 0 ? directive : directive.Substring(0, equals).Trim();
                string argument = equals < 0 ? string.Empty : directive.Substring(equals + 1).Trim();
                if (argument.Length >= 2 && argument[0] == '"' && argument[argument.Length - 1] == '"')
                    argument = argument.Substring(1, argument.Length - 2);
                if (!result.TryGetValue(name, out List<string> arguments))
                    result.Add(name, arguments = new List<string>());
                arguments.Add(argument);
            }
            return result;
        }

        private static bool TryParseHttpDate(string value, out DateTimeOffset date)
        {
            return DateTimeOffset.TryParseExact(value?.Trim(), new[]
            {
                "ddd, dd MMM yyyy HH':'mm':'ss 'GMT'",
                "dddd, dd-MMM-yy HH':'mm':'ss 'GMT'",
                "ddd MMM d HH':'mm':'ss yyyy",
                "ddd MMM  d HH':'mm':'ss yyyy"
            }, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);
        }

        private static string FormatDuration(long seconds)
        {
            string result = seconds.ToString("N0", CultureInfo.InvariantCulture) + " seconds";
            if (seconds >= 86400) return result + " (" + (seconds / 86400.0).ToString("N1", CultureInfo.InvariantCulture) + " days)";
            if (seconds >= 3600) return result + " (" + (seconds / 3600.0).ToString("N1", CultureInfo.InvariantCulture) + " hours)";
            if (seconds >= 60) return result + " (" + (seconds / 60.0).ToString("N1", CultureInfo.InvariantCulture) + " minutes)";
            return result;
        }
    }
}
