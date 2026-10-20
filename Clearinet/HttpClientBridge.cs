using Clearinet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
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
            ServicePoint servicePoint = ServicePointManager.FindServicePoint(new Uri(XC.fullUrl));
            try
            {
                // Send request; return immediately upon reading headers (no body buffering)
                using (HttpResponseMessage response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    AddAddressFlagsViaReflectionHackery(servicePoint, XC);

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

    /// <summary>
    /// The .NET Framework's HttpClient makes it very difficult to understand where the connection is connected.
    /// </summary>
    private static void AddAddressFlagsViaReflectionHackery(ServicePoint sp, Exchange XC)
    {
        try
        {
            FieldInfo fieldInfo = typeof(ServicePoint).GetField("m_ConnectionGroupList", BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfo == null) return;

            Hashtable groupTable = (Hashtable)fieldInfo.GetValue(sp);

            if (groupTable is Hashtable hashtable)
            {
                foreach (DictionaryEntry entry in hashtable)
                {
                    object connGroup = entry.Value;
                    object connList = connGroup.GetType()
                        .GetField("m_ConnectionList", BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.GetValue(connGroup);

                    if (connList is ArrayList list && list.Count > 0)
                    {
                        object connection = list[0]; // Active connection object

                        if (connection != null)
                        {
                            Socket socket = null;

                            // 1. Get the declaring base type context where the stream field actually lives
                            Type pooledStreamType = typeof(ServicePoint).Assembly.GetType("System.Net.PooledStream");

                            if (pooledStreamType != null && pooledStreamType.IsAssignableFrom(connection.GetType()))
                            {
                                // 2. Extract m_NetworkStream from the PooledStream base layer
                                FieldInfo networkStreamField = pooledStreamType.GetField("m_NetworkStream", BindingFlags.NonPublic | BindingFlags.Instance);
                                object networkStream = networkStreamField?.GetValue(connection);

                                if (networkStream != null)
                                {
                                    // 3. NetworkStream exposes a protected "Socket" property we can grab
                                    PropertyInfo socketProperty = networkStream.GetType().GetProperty("Socket", BindingFlags.NonPublic | BindingFlags.Instance);
                                    socket = socketProperty?.GetValue(networkStream) as Socket;
                                }
                            }

                            if (socket != null)
                            {
                                XC["X-CLIENTIP"] = ((IPEndPoint)socket.LocalEndPoint).Address.ToString();
                                XC["X-EGRESSPORT"] = ((IPEndPoint)socket.LocalEndPoint).Port.ToString();
                                XC["X-HOSTIP"] = ((IPEndPoint)socket.RemoteEndPoint).Address.ToString();
                            }
                        }
                    }
                }
            }
        }
        catch { Debug.Assert(false); }
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