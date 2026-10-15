using System;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Extensions
{
    internal sealed class WebPreviewDocument
    {
        internal const string ContentSecurityPolicy = "sandbox; default-src 'none'; script-src 'none'; " +
            "style-src 'unsafe-inline'; img-src data:; media-src data:; font-src data:; object-src 'none'; frame-src 'none'; " +
            "base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

        internal string Url { get; } = "https://clearinet-preview.invalid/" + Guid.NewGuid().ToString("N") + "/document";
        internal byte[] Content { get; }
        internal string ContentType { get; }
        internal string ResponseHeaders => "Content-Type: " + ContentType + "\r\n" +
            "Content-Security-Policy: " + ContentSecurityPolicy + "\r\n" +
            "Cache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nX-Content-Type-Options: nosniff\r\n" +
            "X-DNS-Prefetch-Control: off\r\n" +
            "Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=(), usb=(), " +
            "display-capture=(), fullscreen=(), clipboard-read=(), clipboard-write=()\r\n";

        private WebPreviewDocument(byte[] content, string contentType)
        {
            Content = content;
            ContentType = contentType;
        }

        internal static bool IsSupported(string contentType)
        {
            return MediaTypeHeaderValue.TryParse(contentType, out MediaTypeHeaderValue type) &&
                (IsHtml(type.MediaType) || type.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                IsMedia(type.MediaType) || IsWoff2(type.MediaType) || IsText(type.MediaType));
        }

        internal static bool IsHtml(string mediaType) =>
            string.Equals(mediaType, "text/html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mediaType, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase);

        internal static bool IsSvg(string mediaType) => string.Equals(mediaType, "image/svg+xml", StringComparison.OrdinalIgnoreCase);

        private static bool IsMedia(string mediaType) =>
            mediaType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
            mediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

        private static bool IsWoff2(string mediaType) =>
            string.Equals(mediaType, "font/woff2", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mediaType, "application/font-woff2", StringComparison.OrdinalIgnoreCase);

        private static bool IsText(string mediaType) =>
            mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mediaType, "application/xml", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mediaType, "application/x-javascript", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mediaType, "application/javascript", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mediaType, "application/ecmascript", StringComparison.OrdinalIgnoreCase) ||
            mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase) ||
            mediaType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase);

        internal static WebPreviewDocument Create(string contentType, byte[] decodedBody)
        {
            if (!IsSupported(contentType)) throw new NotSupportedException("No passive WebView preview is available for this Content-Type.");
            if (decodedBody == null) throw new ArgumentNullException(nameof(decodedBody));
            MediaTypeHeaderValue type = MediaTypeHeaderValue.Parse(contentType);
            if (IsSvg(type.MediaType))
                return new WebPreviewDocument((byte[])decodedBody.Clone(), type.ToString());
            if (IsHtml(type.MediaType))
            {
                // Retain the bytes so Chromium can honor HTML charset declarations and BOMs.
                var htmlType = new MediaTypeHeaderValue("text/html");
                if (!string.IsNullOrEmpty(type.CharSet))
                {
                    Encoding encoding = Encoding.GetEncoding(type.CharSet.Trim('"'));
                    htmlType.CharSet = encoding.WebName;
                }
                return new WebPreviewDocument((byte[])decodedBody.Clone(), htmlType.ToString());
            }

            string body;
            string previewStyle = "<style>body{margin:12px}img,video{max-width:100%;height:auto}pre{white-space:pre-wrap;overflow-wrap:anywhere}</style>";
            if (type.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                body = "<img alt=\"Response image\" src=\"data:" + WebUtility.HtmlEncode(type.MediaType) +
                    ";base64," + Convert.ToBase64String(decodedBody) + "\">";
            }
            else if (IsMedia(type.MediaType))
            {
                string element = type.MediaType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? "video" : "audio";
                body = "<" + element + " controls preload=\"none\" src=\"data:" + WebUtility.HtmlEncode(type.MediaType) +
                    ";base64," + Convert.ToBase64String(decodedBody) + "\"></" + element + ">";
            }
            else if (IsWoff2(type.MediaType))
            {
                string fontData = "data:font/woff2;base64," + Convert.ToBase64String(decodedBody);
                previewStyle = "<style>@font-face{font-family:ResponsePreview;src:url('" + fontData + "') format('woff2')}" +
                    "body{margin:12px}.sample{font:32px ResponsePreview,sans-serif;overflow-wrap:anywhere}</style>";
                body = "<h2>WOFF2 font preview</h2><div class=\"sample\">Aa Bb Cc 0123 &amp; !?\r\nThe quick brown fox jumps over the lazy dog.</div>";
            }
            else
            {
                Encoding encoding = string.IsNullOrEmpty(type.CharSet) ? Encoding.UTF8 : Encoding.GetEncoding(type.CharSet.Trim('"'));
                using (var stream = new MemoryStream(decodedBody, false))
                using (var reader = new StreamReader(stream, encoding, true))
                    body = "<pre>" + WebUtility.HtmlEncode(reader.ReadToEnd()) + "</pre>";
            }
            string html = "<!doctype html><html><head><meta charset=\"utf-8\"><title>Response preview</title>" +
                previewStyle + "</head><body>" + body + "</body></html>";
            return new WebPreviewDocument(Encoding.UTF8.GetBytes(html), "text/html; charset=utf-8");
        }
    }
}
