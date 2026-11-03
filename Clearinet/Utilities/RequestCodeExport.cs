using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Clearinet
{
    /// <summary>
    /// Formats captured requests as single-line PowerShell Invoke-WebRequest commands or JavaScript fetch() calls.
    /// Like Copy as CURL, these only produce text; nothing is executed and no files are created.
    /// </summary>
    internal static class RequestCodeExport
    {
        // Connection-level and framing headers that the client regenerates (or refuses to accept).
        // HTTP/2 pseudo-headers (":authority" etc.) are skipped separately.
        private static readonly string[] s_HopByHopHeaders =
        {
            "Host", "Content-Length", "Transfer-Encoding", "Connection", "Keep-Alive",
            "Proxy-Connection", "TE", "Trailer", "Upgrade", "Expect"
        };

        // The -Method values accepted by Windows PowerShell 5.1 (WebRequestMethod enum). Others need -CustomMethod.
        private static readonly string[] s_PowerShellMethods =
        {
            "GET", "HEAD", "POST", "PUT", "DELETE", "TRACE", "OPTIONS", "MERGE", "PATCH"
        };

        /// <summary>
        /// Format the request as a one-line Invoke-WebRequest command that runs in Windows PowerShell 5.1 and PowerShell 7+
        /// (methods outside the 5.1 set use -CustomMethod, which requires PowerShell 6+).
        /// </summary>
        public static string FormatPowerShellCommand(Exchange exch)
        {
            HTTPRequestHeaders headers = GetRequestHeaders(exch);
            StringBuilder command = new StringBuilder("Invoke-WebRequest -UseBasicParsing -Uri ")
                .Append(QuotePowerShellString(exch.fullUrl));

            string method = headers.HTTPMethod;
            if (Array.IndexOf(s_PowerShellMethods, method) >= 0)
            {
                command.Append(" -Method ").Append(method);
            }
            else
            {
                command.Append(" -CustomMethod ").Append(QuotePowerShellString(method));
            }

            // Invoke-WebRequest takes headers as a hashtable, whose keys must be unique (case-insensitively),
            // so repeated headers are folded into a single value. User-Agent and Content-Type have dedicated parameters.
            List<string> names = new List<string>();
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string userAgent = null;
            string contentType = null;
            foreach (HTTPHeaderItem header in headers)
            {
                if (ShouldSkipHeader(header.Name)) continue;
                if (header.Name.OICEquals("User-Agent")) { userAgent = FoldHeader(userAgent, header, ", "); continue; }
                if (header.Name.OICEquals("Content-Type")) { contentType = FoldHeader(contentType, header, ", "); continue; }
                if (values.TryGetValue(header.Name, out string existing))
                {
                    values[header.Name] = existing + (header.Name.OICEquals("Cookie") ? "; " : ", ") + header.Value;
                }
                else
                {
                    names.Add(header.Name);
                    values[header.Name] = header.Value;
                }
            }
            if (userAgent != null) command.Append(" -UserAgent ").Append(QuotePowerShellString(userAgent));
            if (contentType != null) command.Append(" -ContentType ").Append(QuotePowerShellString(contentType));
            if (names.Count > 0)
            {
                command.Append(" -Headers @{");
                for (int i = 0; i < names.Count; ++i)
                {
                    if (i > 0) command.Append("; ");
                    command.Append(QuotePowerShellString(names[i])).Append(" = ").Append(QuotePowerShellString(values[names[i]]));
                }
                command.Append('}');
            }

            if (TryGetBody(exch, headers, out byte[] body))
            {
                command.Append(" -Body ");
                if (IsPrintableAscii(body))
                {
                    command.Append(QuotePowerShellString(Encoding.ASCII.GetString(body)));
                }
                else
                {
                    command.Append("([Convert]::FromBase64String('").Append(Convert.ToBase64String(body)).Append("'))");
                }
            }
            return command.ToString();
        }

        /// <summary>
        /// Format the request as a one-line fetch() call usable in browser DevTools or Node.js 18+.
        /// </summary>
        public static string FormatFetchCall(Exchange exch)
        {
            HTTPRequestHeaders headers = GetRequestHeaders(exch);
            StringBuilder call = new StringBuilder("fetch(")
                .Append(QuoteJavaScriptString(exch.fullUrl))
                .Append(", {method: ").Append(QuoteJavaScriptString(headers.HTTPMethod));

            // An array of name/value pairs (rather than an object) preserves order and repeated headers.
            bool firstHeader = true;
            foreach (HTTPHeaderItem header in headers)
            {
                if (ShouldSkipHeader(header.Name)) continue;
                call.Append(firstHeader ? ", headers: [" : ", ");
                firstHeader = false;
                call.Append('[').Append(QuoteJavaScriptString(ToByteString(header.Name))).Append(", ")
                    .Append(QuoteJavaScriptString(ToByteString(header.Value))).Append(']');
            }
            if (!firstHeader) call.Append(']');

            if (TryGetBody(exch, headers, out byte[] body))
            {
                if (headers.HTTPMethod.OICEquals("GET") || headers.HTTPMethod.OICEquals("HEAD"))
                {
                    // fetch() throws a TypeError if a GET or HEAD request has a body.
                    call.Append(" /* ").Append(body.Length)
                        .Append("-byte request body omitted: fetch() does not allow a body on GET or HEAD */");
                }
                else if (IsPrintableAscii(body))
                {
                    call.Append(", body: ").Append(QuoteJavaScriptString(Encoding.ASCII.GetString(body)));
                }
                else
                {
                    call.Append(", body: Uint8Array.from(atob(\"").Append(Convert.ToBase64String(body))
                        .Append("\"), c => c.charCodeAt(0))");
                }
            }
            return call.Append("});").ToString();
        }

        private static HTTPRequestHeaders GetRequestHeaders(Exchange exch)
        {
            HTTPRequestHeaders headers = exch.RequestHeaders;
            if (headers == null || !headers.HTTPMethod.HasText())
            {
                throw new InvalidDataException($"Exchange #{exch.id} has no request headers or method.");
            }
            return headers;
        }

        private static bool ShouldSkipHeader(string name)
        {
            if (name.StartsWith(":", StringComparison.Ordinal)) return true;
            foreach (string skip in s_HopByHopHeaders)
            {
                if (name.OICEquals(skip)) return true;
            }
            return false;
        }

        private static string FoldHeader(string existing, HTTPHeaderItem header, string separator)
        {
            return existing == null ? header.Value : existing + separator + header.Value;
        }

        /// <summary>
        /// Gets the body to send, without transfer framing (content encoding is preserved, matching the headers).
        /// Returns false if the request has no body; an explicitly empty body (e.g. Content-Length: 0) is returned as empty.
        /// </summary>
        private static bool TryGetBody(Exchange exch, HTTPRequestHeaders headers, out byte[] body)
        {
            body = exch.RequestBody ?? Array.Empty<byte>();
            if (headers.Exists("Transfer-Encoding"))
            {
                HTTPRequestHeaders transferHeaders = headers.Clone();
                transferHeaders.Remove("Content-Encoding");
                body = Exchange.DecodeBodyFromHeaders(transferHeaders, body);
            }
            bool isHead = headers.HTTPMethod.OICEquals("HEAD");
            return body.Length > 0 || (!isHead && headers.ExistsAny("Content-Length", "Transfer-Encoding"));
        }

        private static bool IsPrintableAscii(byte[] body)
        {
            foreach (byte b in body)
            {
                if (b < 0x20 || b > 0x7e) return false;
            }
            return true;
        }

        /// <summary>
        /// Single-quoted PowerShell literal: no variable or subexpression expansion. PowerShell treats the
        /// typographic single quotes as quote characters too, so each of them is escaped by doubling.
        /// </summary>
        internal static string QuotePowerShellString(string value)
        {
            if (value.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0)
            {
                throw new InvalidDataException("PowerShell command arguments cannot contain NUL characters or line breaks.");
            }
            StringBuilder quoted = new StringBuilder(value.Length + 2).Append('\'');
            foreach (char c in value)
            {
                if (c == '\'' || c == '\u2018' || c == '\u2019' || c == '\u201A' || c == '\u201B') quoted.Append(c);
                quoted.Append(c);
            }
            return quoted.Append('\'').ToString();
        }

        /// <summary>
        /// fetch() header values are ByteStrings: each character is sent as one byte, and characters above U+00FF throw.
        /// Headers are stored decoded (see CONFIG.encodingOfHeaders), so re-encode them and map each byte to one character
        /// so that fetch() sends the original header bytes.
        /// </summary>
        private static string ToByteString(string value)
        {
            byte[] bytes = CONFIG.encodingOfHeaders.GetBytes(value);
            StringBuilder result = new StringBuilder(bytes.Length);
            foreach (byte b in bytes) result.Append((char)b);
            return result.ToString();
        }

        /// <summary>
        /// Double-quoted JavaScript literal using only ASCII; everything outside printable ASCII is \u-escaped.
        /// </summary>
        internal static string QuoteJavaScriptString(string value)
        {
            StringBuilder quoted = new StringBuilder(value.Length + 2).Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': quoted.Append("\\\""); break;
                    case '\\': quoted.Append("\\\\"); break;
                    case '\r': quoted.Append("\\r"); break;
                    case '\n': quoted.Append("\\n"); break;
                    case '\t': quoted.Append("\\t"); break;
                    default:
                        if (c < 0x20 || c > 0x7e) quoted.Append("\\u").Append(((int)c).ToString("x4"));
                        else quoted.Append(c);
                        break;
                }
            }
            return quoted.Append('"').ToString();
        }
    }
}
