using Clearinet;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Extensions
{
    internal static class ResponseTransformer
    {
        internal static List<string> GetEncodings(HTTPResponseHeaders headers, string name)
        {
            var result = new List<string>();
            if (headers == null) return result;
            foreach (HTTPHeaderItem header in headers)
            {
                if (!header.Name.OICEquals(name)) continue;
                foreach (string value in header.Value.Split(','))
                {
                    string token = value.Trim().TrimAfter(';').Trim().ToLowerInvariant();
                    if (token.Length > 0) result.Add(token);
                }
            }
            return result;
        }

        internal static HTTPResponseHeaders Transform(HTTPResponseHeaders headers, byte[] body, string encoding, bool enabled, out byte[] result)
        {
            var content = GetEncodings(headers, "Content-Encoding");
            var transfer = GetEncodings(headers, "Transfer-Encoding");
            ValidateEncodings(content, false);
            ValidateEncodings(transfer, true);
            if (encoding != "chunked" && encoding != "gzip" && encoding != "deflate" && encoding != "br" && encoding != "zstd")
            {
                throw new NotSupportedException($"Unsupported encoding '{encoding}'.");
            }
            result = Exchange.DecodeBodyFromHeaders(headers, body);
            if (!enabled)
            {
                content.RemoveAll(token => token == encoding);
                transfer.RemoveAll(token => token == encoding);
            }
            else if (encoding == "chunked")
            {
                if (!headers.HTTPVersion.OICEquals("HTTP/1.1"))
                {
                    // TODO: Show a protocol violation instead.
                    CApp.Log.Log("![ProtocolViolation] Chunked Transfer-Encoding requires HTTP/1.1, but the response is " + headers.HTTPVersion);
                    //throw new NotSupportedException("Chunked transfer encoding requires HTTP/1.1.");
                }
                if (!transfer.Contains(encoding)) transfer.Add(encoding);
            }
            else if (!content.Contains(encoding) && !transfer.Contains(encoding))
            {
                content.Add(encoding);
            }

            foreach (string token in content) result = Encode(result, token);
            foreach (string token in transfer) result = Encode(result, token);

            HTTPResponseHeaders updated = headers.Clone();
            updated.Remove("Content-Encoding");
            updated.Remove("Transfer-Encoding");
            updated.Remove("Content-Length");
            updated.Remove("Trailer");
            if (content.Count > 0) updated.Add("Content-Encoding", string.Join(", ", content));
            if (transfer.Count > 0) updated.Add("Transfer-Encoding", string.Join(", ", transfer));
            else updated.Add("Content-Length", result.LongLength.ToString(CultureInfo.InvariantCulture));
            return updated;
        }

        private static void ValidateEncodings(List<string> encodings, bool transfer)
        {
            for (int i = 0; i < encodings.Count; i++)
            {
                string token = encodings[i];
                if (token == "chunked" && transfer && i == encodings.Count - 1 && encodings.IndexOf(token) == i) continue;
                if (token == "identity" || token == "gzip" || token == "deflate" || token == "br" || token == "zstd") continue;
                throw new NotSupportedException($"Unsupported or incorrectly ordered encoding '{token}'.");
            }
        }

        private static byte[] Encode(byte[] input, string encoding)
        {
            switch (encoding)
            {
                case "identity": return input;
                case "chunked": return Utilities.ApplyChunking(input);
                case "gzip": return Utilities.GzipCompress(input);
                case "deflate": return Utilities.DeflaterCompress(input);
                case "br": return Utilities.BrotliCompress(input);
                case "zstd": return Utilities.ZstdCompress(input);
                default: throw new NotSupportedException($"Unsupported encoding '{encoding}'.");
            }
        }
    }
}
