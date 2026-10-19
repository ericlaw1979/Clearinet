using Clearinet;
using System;
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
            if (XC.RequestBody.HasData()) request.Content = new ByteArrayContent(XC.RequestBody);
            AssignHeaders(request, XC.RequestHeaders);

            try
            {
                // Send request; return immediately upon reading headers (no body buffering)
                using (HttpResponseMessage response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    XC.state = ExchangeState.ReadingResponse; // TODO: Should this be earlier?

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
                        // TODO: re-chunk the body with pseudo chunks.
                    }

                    using (Stream rawResponseBodyStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            await rawResponseBodyStream.CopyToAsync(ms).ConfigureAwait(false);
                            XC.ResponseBody = ms.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                XC.ResponseHeaders = new HTTPResponseHeaders(500, "Send Failed", new[] { $"X-Error: {ex.Message}", "Content-Type: text/plain; charset=utf-8" });
                XC.utilSetResponseBody($"Error: {ex.Message}\n{ex.InnerException?.Message}");
                XC.BitFlags |= ExchangeFlags.ResponseGeneratedByClearinet;
            }
            finally
            {
                XC.state = ExchangeState.AutoTamperResponseBefore;
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