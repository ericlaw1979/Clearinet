using System;
using System.Collections.Generic;
using System.IO;

namespace Clearinet
{
    internal static class Parser
    {
        internal static bool ParseHeaderLines(HTTPHeaders headers, string[] arrHeaderLines, int iStartFrom, ref string sErrors)
        {
            bool bResult = true;
            int ixToken, ixHeader;
            ixHeader = iStartFrom;
            HTTPHeaderItem oNewHeader = null;

            while (ixHeader < arrHeaderLines.Length)
            {
                ixToken = arrHeaderLines[ixHeader].IndexOf(':');

                // Per RFC2616, name must be at least one character, but value can be empty.
                if (ixToken > 0)
                {
                    oNewHeader = headers.Add(
                        arrHeaderLines[ixHeader].Substring(0, ixToken),
                        arrHeaderLines[ixHeader].Substring(ixToken + 1).TrimStart(new[] { ' ', '\t' }));
                }
                else
                {
                    if (0 == ixToken)
                    {
                        bResult = false;
                        oNewHeader = null;
                        sErrors += String.Format("Header missing name #{0}, {1}\n", 1 + ixHeader - iStartFrom, arrHeaderLines[ixHeader]);
                    }
                    else
                    {
                        bResult = false;
                        oNewHeader = headers.Add(arrHeaderLines[ixHeader], string.Empty);
                        sErrors += String.Format("Header missing colon #{0}, {1}\n", 1 + ixHeader - iStartFrom, arrHeaderLines[ixHeader]);
                    }
                }

                ixHeader++;

                // Lines starting with whitespace are a continuation of the value of the previous header.
                bool bIsContinuation = ((null != oNewHeader) && (ixHeader < arrHeaderLines.Length) &&
                                        arrHeaderLines[ixHeader].HasLeadingWhitespace());
                while (bIsContinuation)
                {
                    CApp.Log.Log("[Warning] HTTPHeader value was folded. Not all clients handle folded headers.");
                    oNewHeader.Value = (oNewHeader.Value + " " + arrHeaderLines[ixHeader].TrimStart(' ', '\t'));
                    ixHeader++;
                    bIsContinuation = ((ixHeader < arrHeaderLines.Length) && arrHeaderLines[ixHeader].HasLeadingWhitespace());
                }

            }
            return bResult;
        }

        public static (string headers, byte[] body) CrackHttpMessage(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using (var ms = new MemoryStream())
            {
                int b;
                int state = 0;

                // Read character by character until we match the \r\n\r\n (or \n\r\n) sequence
                while ((b = stream.ReadByte()) != -1)
                {
                    ms.WriteByte((byte)b);

                    // State machine to detect header end: \r \n \r \n (or \n \r \n)
                    if (state == 0 && b == '\r') state = 1;
                    else if ((state == 0 || state == 1) && b == '\n') state = 2;
                    else if (state == 2 && b == '\r') state = 3;
                    else if (state == 3 && b == '\n') state = 4; // Header terminator matched
                    else state = (b == '\r') ? 1 : 0;

                    if (state == 4) break;
                }

                byte[] headerBytes = ms.ToArray();
                string headers = CONFIG.encodingOfHeaders.GetString(headerBytes);

                // Read the remainder of the stream into the body array
                byte[] body;
                if (stream is MemoryStream remainingMs)
                {
                    // Optimization if underlying stream supports buffer reading directly
                    int remainingLength = (int)(remainingMs.Length - remainingMs.Position);
                    body = new byte[remainingLength];
                    remainingMs.Read(body, 0, remainingLength);
                }
                else
                {
                    using (var bodyMs = new MemoryStream())
                    {
                        stream.CopyTo(bodyMs);
                        body = bodyMs.ToArray();
                    }
                }

                return (headers, body);
            }
        }

        public static (string method, string path, string version, string[] headers, string sorigin) ParseRequestHeaders(string headerString)
        {
            using (var reader = new StringReader(headerString))
            {
                string requestLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(requestLine))
                {
                    throw new FormatException("Invalid HTTP request: missing request line.");
                }

                // Parse Request Line (e.g., "GET /index.html HTTP/1.1")
                string[] requestParts = requestLine.Split(' ');
                if (requestParts.Length < 3)
                {
                    throw new FormatException($"Malformed HTTP request line: '{requestLine}'");
                }

                string method = requestParts[0];
                string path = requestParts[1];
                string version = requestParts[2];
                string sorigin = null;

                // Make the path relative.
                // For non-CONNECT requests, check whether the origin was present
                // in the request line. It typically *will* be if the client knows
                // that it's talking to a proxy. But there are both capture and load
                // scenarios where the request path starts with /whatever.html
                if (!method.OICEquals("CONNECT"))
                {
                    if (!path.StartsWith("/") && path.Contains(":"))
                    {
                        if (path.OICStartsWith("data:"))
                        {
                            sorigin = "data:";
                            path = path.Substring(5); // Strip off the "data:" prefix
                        }
                        else
                        {
                            // https://server.com/path?query 
                            int ixDelimiter = path.IndexOf(':');
                            if (ixDelimiter < (path.Length - 1) && path[ixDelimiter + 1] == '/') ixDelimiter++;
                            if (ixDelimiter < (path.Length - 1) && path[ixDelimiter + 1] == '/') ixDelimiter++;
                            ixDelimiter = path.IndexOf('/', ixDelimiter + 1);
                            if (ixDelimiter > 0)
                            {
                                sorigin = path.Substring(0, ixDelimiter);
                                path = path.Substring(ixDelimiter);
                            }
                        }
                    }
                }

                // Parse remaining header lines
                var headerList = new List<string>();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    headerList.Add(line);
                }
                // Strip the trailing one
                if (headerList.Count > 0) headerList.RemoveAt(headerList.Count - 1);

                return (method, path, version, headerList.ToArray(), sorigin);
            }
        }

        public static (string version, int statusCode, string statusText, string[] headers) ParseResponseHeaders(string headerString)
        {
            using (var reader = new StringReader(headerString))
            {
                string statusLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(statusLine))
                {
                    throw new FormatException("Invalid HTTP response: missing status line.\n" + headerString);
                }

                // Parse Status Line (e.g., "HTTP/1.1 200 OK" or "HTTP/1.1 404 Not Found")
                // Split into maximum 3 parts: Version, StatusCode, ReasonPhrase
                string[] statusParts = statusLine.Split(new[] { ' ' }, 3, StringSplitOptions.None);
                if (statusParts.Length < 2)
                {
                    throw new FormatException($"Malformed HTTP status line: '{statusLine}'");
                }

                string version = statusParts[0];

                if (!int.TryParse(statusParts[1], out int statusCode))
                {
                    throw new FormatException($"Invalid HTTP status code: '{statusParts[1]}'");
                }

                // Reason phrase can be empty in HTTP/2 / HTTP/3 or custom responses
                string statusText = statusParts.Length > 2 ? statusParts[2] : string.Empty;

                // Parse remaining header lines
                var headerList = new List<string>();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    headerList.Add(line);
                }
                // Strip the trailing one.
                if (headerList.Count > 0) headerList.RemoveAt(headerList.Count - 1);

                return (version, statusCode, statusText, headerList.ToArray());
            }
        }
    }

    public class HTTPParser
    {
        public static HTTPRequestHeaders ParseRequest(string sRequest)
        {
            var (method, path, version, headerList, origin) = Parser.ParseRequestHeaders(sRequest);
            HTTPRequestHeaders rqh = new HTTPRequestHeaders(path, headerList);
            rqh.HTTPVersion = version;
            rqh.HTTPMethod = method;
            if ((origin != null) && origin.Contains(":"))
            {
                rqh.UriScheme = origin.TrimAfter(":");
            }
            return rqh;
        }
        public static HTTPResponseHeaders ParseResponse(string sResponse)
        {
            var (version, statusCode, statusText, headerList) = Parser.ParseResponseHeaders(sResponse);
            HTTPResponseHeaders resph = new HTTPResponseHeaders(statusCode, statusText, headerList);
            resph.HTTPVersion = version;
            return resph;
        }
    }
}
