using Clearinet;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

public class HTTPClientBridge
{
    // Reuse HttpClient instances to avoid socket exhaustion in .NET Framework
    private static readonly HttpClient client = new HttpClient(new HttpClientHandler
    {
        // Disable automatic decompression to avoid decoding response bytes
        AutomaticDecompression = System.Net.DecompressionMethods.None,
        // Disable automatic redirect following if you want server-returned 3xx headers untouched
        AllowAutoRedirect = false
    });

    public async Task SendAndReceiveRawAsync(Exchange XC /*hstring requestUri, byte[] payload, string rawHeaders*/)
    {
        using (var request = new HttpRequestMessage(new HttpMethod(XC.RequestMethod), XC.fullUrl))
        {
            // 1. Attach the byte array directly without encoding
            if (XC.RequestBody.HasData()) request.Content = new ByteArrayContent(XC.RequestBody);

            // 2. Parse and assign raw HTTP request headers
            AssignHeaders(request, XC.RequestHeaders);

            // 3. Send request; return immediately upon reading headers (no body buffering)
            using (HttpResponseMessage response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                // 4. Access untouched server response headers
                HttpResponseHeaders responseHeaders = response.Headers;
                HttpContentHeaders contentHeaders = response.Content.Headers;

                List<string> allHeaders = new List<string>();
                foreach (var header in responseHeaders)
                {
                    allHeaders.Add($"{header.Key}: {string.Join(", ", header.Value)}");
                }

                foreach (var header in contentHeaders)
                {
                    allHeaders.Add($"{header.Key}: {string.Join(", ", header.Value)}");
                }

                XC.ResponseHeaders = new HTTPResponseHeaders((int)response.StatusCode, response.ReasonPhrase, allHeaders.ToArray());

                if (XC.ResponseHeaders.ExistsAndContains("Transfer-Encoding", "chunked"))
                {
                    XC["HACK-HttpClientAlwaysUnchunks"] = "true";
                    XC.ResponseHeaders.Remove("Transfer-Encoding");
                }

                // 5. Stream the raw un-decoded body bytes directly
                using (Stream rawResponseBodyStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    // Example: Write directly to file or memory stream without string decoding
                    using (MemoryStream ms = new MemoryStream())
                    {
                        await rawResponseBodyStream.CopyToAsync(ms).ConfigureAwait(false);
                        XC.ResponseBody = ms.ToArray();
                    }
                }

                CApp.UIInvokeAsync(() =>
                {
                    CApp.UI.ComposerAddHACKTODO(XC);
                });
            }
        }
    }

    private static void AssignHeaders(HttpRequestMessage request, HTTPRequestHeaders XH)
    {
        foreach (var header in XH)
        {
            if (!request.Headers.TryAddWithoutValidation(header.Name, header.Value))
            {
                request.Content?.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }
        }
    }
}