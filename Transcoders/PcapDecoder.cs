using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;

namespace TestExtensions
{
    internal sealed class PcapExchange
    {
        internal string RequestHeaders, ResponseHeaders, Method, Target, ClientIP, ServerIP, Informationals;
        internal int ClientPort, ServerPort;
        internal byte[] RequestBody, ResponseBody;
        internal bool RequestIncomplete, ResponseIncomplete;
        internal DateTime RequestStart, RequestHeadersTime, RequestEnd, ResponseStart, ResponseHeadersTime, ResponseEnd;
    }

    internal sealed class PcapDecoder
    {
        private const int MaxStream = 32 * 1024 * 1024, MaxHeaders = 65536;
        private const int MaxExchanges = 100000;
        private readonly Func<int, string, bool> notify;
        private readonly Dictionary<string, int> diagnostics = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, Connection> active = new Dictionary<string, Connection>();
        private readonly List<Connection> connections = new List<Connection>();
        private readonly List<PcapExchange> exchanges = new List<PcapExchange>();
        private long payloadBytes;
        private int segments;
        private int work;
        private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

        private sealed class Segment
        {
            internal long Start;
            internal byte[] Data;
            internal DateTime Time;
            internal int Order;
        }

        private sealed class Direction
        {
            internal string Address;
            internal int Port;
            internal uint Basis, SynSequence;
            internal bool Anchored, Syn, Fin, Reset, Damaged;
            internal long FinAt;
            internal readonly List<Segment> Segments = new List<Segment>();
        }

        private sealed class Connection
        {
            internal Direction A, B;
            internal bool Ended;
        }

        private sealed class TcpStream
        {
            internal byte[] Data;
            internal bool Closed;
            internal List<Segment> Spans = new List<Segment>();

            internal DateTime Time(int start, int end, bool first)
            {
                if (Spans.Count == 0) return DateTime.MinValue;
                int low = 0, high = Spans.Count;
                while (low < high)
                {
                    int mid = (low + high) / 2;
                    if (Spans[mid].Start + Spans[mid].Data.Length <= start) low = mid + 1;
                    else high = mid;
                }
                DateTime result = DateTime.MinValue;
                for (int i = low; i < Spans.Count && Spans[i].Start < Math.Max(end, start + 1); i++)
                {
                    DateTime value = Spans[i].Time;
                    if (value != DateTime.MinValue && (result == DateTime.MinValue || (first ? value < result : value > result))) result = value;
                }
                return result;
            }
        }

        private sealed class Message
        {
            internal string Headers, Method, Target;
            internal int Status, Start, BodyStart, End;
            internal byte[] Body;
            internal bool Complete;
        }

        internal PcapDecoder(Func<int, string, bool> notify)
        {
            this.notify = notify;
        }

        internal IList<PcapExchange> Read(Stream input)
        {
            new PcapReader(input, Warn, Progress).Read(Packet);
            for (int i = 0; i < connections.Count; i++)
            {
                Progress(65 + i * 30 / Math.Max(1, connections.Count), "Decoding TCP connection " + (i + 1) + " of " + connections.Count + ".");
                Connection connection = connections[i];
                try { DecodeConnection(connection); }
                catch (InvalidDataException ex) { Warn(ex.Message); }
            }
            Progress(95, Summary);
            return exchanges;
        }

        internal string Summary
        {
            get
            {
                StringBuilder text = new StringBuilder("Imported " + exchanges.Count + " plaintext HTTP/1.x exchanges from " + connections.Count + " TCP connections.");
                foreach (KeyValuePair<string, int> diagnostic in diagnostics)
                    text.Append(" ").Append(diagnostic.Key).Append(": ").Append(diagnostic.Value).Append('.');
                return text.ToString();
            }
        }

        private void Warn(string text)
        {
            int count;
            diagnostics.TryGetValue(text, out count);
            diagnostics[text] = count + 1;
        }

        private void Progress(int percent, string text)
        {
            if (notify != null && !notify(percent, text)) throw new OperationCanceledException();
        }

        private void CheckWork()
        {
            // TODO: This doesn't look right.
            if ((++work & 1023) == 0) Progress(80, "Processing captured TCP/HTTP bytes.");
        }

        private void Packet(byte[] data, uint link, DateTime time, int scope, bool truncated)
        {
            CheckWork();
            int ip = 0, end = data.Length;
            if (link == 1)
            {
                if (end < 14) { Warn("Truncated Ethernet header"); return; }
                ushort protocol = PcapReader.U16(data, 12, false);
                ip = 14;
                int tags = 0;
                while (protocol == 0x8100 || protocol == 0x88a8 || protocol == 0x9100)
                {
                    if (++tags > 4 || end - ip < 4) { Warn("Unsupported or truncated VLAN stack"); return; }
                    protocol = PcapReader.U16(data, ip + 2, false);
                    ip += 4;
                }
                if (protocol != 0x0800 && protocol != 0x86dd) { Warn("Non-IP Ethernet traffic skipped"); return; }
                if (ip >= end || data[ip] >> 4 != (protocol == 0x0800 ? 4 : 6)) { Warn("Invalid Ethernet IP version"); return; }
            }
            else if (link != 101 && link != 12 && link != 228 && link != 229)
            {
                Warn("Unsupported capture link type " + link.ToString(CultureInfo.InvariantCulture));
                return;
            }
            if (end - ip < 1) { Warn("Truncated IP packet"); return; }
            int version = data[ip] >> 4;
            if ((link == 228 && version != 4) || (link == 229 && version != 6)) { Warn("Invalid raw IP version"); return; }
            int tcp, ipEnd;
            byte next;
            string source, destination;
            if (version == 4)
            {
                if (end - ip < 20) { Warn("Truncated IPv4 header"); return; }
                int header = (data[ip] & 15) * 4, length = PcapReader.U16(data, ip + 2, false);
                if (header < 20 || header > end - ip || length < header) { Warn("Invalid IPv4 length"); return; }
                if ((PcapReader.U16(data, ip + 6, false) & 0x3fff) != 0) { Warn("Fragmented IPv4 traffic skipped"); return; }
                source = Address(data, ip + 12, 4);
                destination = Address(data, ip + 16, 4);
                next = data[ip + 9];
                ipEnd = Math.Min(end, ip + length);
                truncated |= ip + length > end;
                tcp = ip + header;
            }
            else if (version == 6)
            {
                if (end - ip < 40) { Warn("Truncated IPv6 header"); return; }
                int length = PcapReader.U16(data, ip + 4, false);
                if (length == 0) { Warn("IPv6 jumbogram or empty payload skipped"); return; }
                source = Address(data, ip + 8, 16);
                destination = Address(data, ip + 24, 16);
                next = data[ip + 6];
                ipEnd = Math.Min(end, ip + 40 + length);
                truncated |= ip + 40 + length > end;
                tcp = ip + 40;
                int extensions = 0;
                while (next == 0 || next == 43 || next == 60 || next == 51)
                {
                    if (++extensions > 8 || ipEnd - tcp < 2) { Warn("Invalid IPv6 extension chain"); return; }
                    int size = next == 51 ? (data[tcp + 1] + 2) * 4 : (data[tcp + 1] + 1) * 8;
                    if (size > ipEnd - tcp) { Warn("Truncated IPv6 extension"); return; }
                    next = data[tcp];
                    tcp += size;
                }
                if (next == 44) { Warn("Fragmented IPv6 traffic skipped"); return; }
                if (next == 50) { Warn("Encrypted IPsec traffic skipped"); return; }
            }
            else { Warn("Unsupported IP version"); return; }
            if (next != 6) { Warn("Non-TCP or unsupported IP protocol skipped"); return; }
            if (ipEnd - tcp < 20) { Warn("Truncated TCP header"); return; }
            int tcpHeader = (data[tcp + 12] >> 4) * 4;
            if (tcpHeader < 20 || tcpHeader > ipEnd - tcp) { Warn("Invalid TCP header length"); return; }
            int sourcePort = PcapReader.U16(data, tcp, false), destinationPort = PcapReader.U16(data, tcp + 2, false);
            uint sequence = PcapReader.U32(data, tcp + 4, false);
            byte flags = data[tcp + 13];
            bool syn = (flags & 2) != 0, ack = (flags & 16) != 0;
            string endpointA = source + ":" + sourcePort, endpointB = destination + ":" + destinationPort;
            string key = scope + "|" + (String.CompareOrdinal(endpointA, endpointB) < 0 ? endpointA + "|" + endpointB : endpointB + "|" + endpointA);
            Connection connection;
            bool found = active.TryGetValue(key, out connection);
            Direction direction = found && connection.A.Address == source && connection.A.Port == sourcePort ? connection.A : found ? connection.B : null;
            // A new initial SYN is a new generation, except an identical in-flight SYN retransmission.
            if (!found || (syn && !ack && (connection.Ended || !direction.Syn || direction.SynSequence != sequence)))
            {
                if (connections.Count >= 10000) throw new InvalidDataException("TCP connection resource limit exceeded.");
                connection = new Connection
                {
                    A = new Direction { Address = source, Port = sourcePort },
                    B = new Direction { Address = destination, Port = destinationPort }
                };
                active[key] = connection;
                connections.Add(connection);
                direction = connection.A;
            }
            if (syn)
            {
                if (direction.Syn && direction.SynSequence != sequence) direction.Damaged = true;
                direction.Syn = true;
                direction.SynSequence = sequence;
            }
            int payloadStart = tcp + tcpHeader, payloadLength = ipEnd - payloadStart;
            uint dataSequence = unchecked(sequence + (syn ? 1u : 0u));
            if (!direction.Anchored && (syn || payloadLength > 0 || (flags & 1) != 0))
            {
                direction.Anchored = true;
                direction.Basis = dataSequence;
            }
            long position = unchecked((int)(dataSequence - direction.Basis));
            direction.Damaged |= truncated;
            if (payloadLength > 0)
            {
                payloadBytes += payloadLength;
                if (payloadBytes > 128L * 1024 * 1024 || ++segments > 250000)
                    throw new InvalidDataException("TCP payload or segment resource limit exceeded.");
                byte[] bytes = new byte[payloadLength];
                Buffer.BlockCopy(data, payloadStart, bytes, 0, bytes.Length);
                direction.Segments.Add(new Segment { Start = position, Data = bytes, Time = time, Order = segments });
            }
            if ((flags & 1) != 0)
            {
                long finAt = position + payloadLength;
                if (direction.Fin && direction.FinAt != finAt) direction.Damaged = true;
                direction.Fin = true;
                direction.FinAt = finAt;
            }
            if ((flags & 4) != 0)
            {
                direction.Reset = true;
                connection.Ended = true;
            }
            if (connection.A.Fin && connection.B.Fin) connection.Ended = true;
        }

        private static string Address(byte[] data, int offset, int length)
        {
            byte[] bytes = new byte[length];
            Buffer.BlockCopy(data, offset, bytes, 0, length);
            return new IPAddress(bytes).ToString();
        }

        private TcpStream Assemble(Direction direction)
        {
            TcpStream stream = new TcpStream();
            direction.Segments.Sort(delegate (Segment a, Segment b)
            {
                int compare = a.Start.CompareTo(b.Start);
                return compare == 0 ? a.Order.CompareTo(b.Order) : compare;
            });
            long origin = direction.Syn ? unchecked((int)(unchecked(direction.SynSequence + 1) - direction.Basis)) :
                direction.Segments.Count > 0 ? direction.Segments[0].Start : 0;
            long cursor = origin;
            bool gap = false;
            using (MemoryStream bytes = new MemoryStream())
            {
                foreach (Segment segment in direction.Segments)
                {
                    CheckWork();
                    if (segment.Start < origin) throw new InvalidDataException("TCP data precedes SYN; connection skipped");
                    if (segment.Start > cursor) { gap = true; break; }
                    int overlap = (int)Math.Min(segment.Data.Length, cursor - segment.Start);
                    byte[] buffer = bytes.GetBuffer();
                    for (int i = 0; i < overlap; i++)
                    {
                        CheckWork();
                        if (buffer[(int)(segment.Start - origin) + i] != segment.Data[i])
                            throw new InvalidDataException("Conflicting TCP retransmission; connection skipped");
                    }
                    int count = segment.Data.Length - overlap;
                    if (count > 0)
                    {
                        if (bytes.Length + count > MaxStream) throw new InvalidDataException("TCP stream resource limit exceeded");
                        // Store only newly accepted ranges; retransmissions cannot change the original timing or bytes.
                        byte[] accepted = new byte[count];
                        Buffer.BlockCopy(segment.Data, overlap, accepted, 0, count);
                        stream.Spans.Add(new Segment { Start = cursor - origin, Data = accepted, Time = segment.Time });
                        bytes.Write(accepted, 0, count);
                        cursor += count;
                    }
                }
                stream.Data = bytes.ToArray();
            }
            if (gap || (direction.Fin && direction.FinAt > cursor)) Warn("TCP gap; only contiguous prefix retained");
            if (direction.Damaged) Warn("Truncated or inconsistent TCP stream");
            if (direction.Reset) Warn("TCP reset is not a reliable response-body delimiter");
            if (direction.Fin && direction.FinAt < cursor) throw new InvalidDataException("TCP data beyond FIN; connection skipped");
            stream.Closed = direction.Fin && direction.FinAt == cursor && !gap && !direction.Damaged && !direction.Reset;
            return stream;
        }

        private void DecodeConnection(Connection connection)
        {
            TcpStream a = Assemble(connection.A), b = Assemble(connection.B);
            bool aRequest = IsRequest(a.Data), bRequest = IsRequest(b.Data);
            if (aRequest == bRequest)
            {
                if (a.Data.Length == 0 && b.Data.Length == 0) return;
                if (IsTls(a.Data) || IsTls(b.Data)) Warn("Encrypted TLS traffic skipped (no decryption)");
                else if (Starts(a.Data, "PRI * HTTP/2.0") || Starts(b.Data, "PRI * HTTP/2.0")) Warn("HTTP/2 traffic skipped");
                else Warn("Unsupported traffic or capture starts mid-HTTP exchange");
                return;
            }
            TcpStream requests = aRequest ? a : b, responses = aRequest ? b : a;
            Direction client = aRequest ? connection.A : connection.B, server = aRequest ? connection.B : connection.A;
            int requestOffset = 0, responseOffset = 0;
            while (requestOffset < requests.Data.Length)
            {
                CheckWork();
                Message request = ParseMessage(requests, requestOffset, true, null);
                if (request == null) { Warn("Incomplete HTTP request headers; remaining stream skipped"); break; }
                Message response = null;
                StringBuilder informational = new StringBuilder();
                int interims = 0;
                while (responseOffset < responses.Data.Length)
                {
                    response = ParseMessage(responses, responseOffset, false, request.Method);
                    if (response == null) break;
                    if (response.Status < 100 || response.Status >= 200 || response.Status == 101) break;
                    if (++interims > 100) throw new InvalidDataException("HTTP interim-response limit exceeded");
                    informational.Append(response.Headers);
                    responseOffset = response.End;
                    response = null;
                }
                if (exchanges.Count >= MaxExchanges) throw new InvalidDataException("HTTP exchange resource limit exceeded");
                PcapExchange exchange = new PcapExchange
                {
                    RequestHeaders = request.Headers,
                    ResponseHeaders = response == null ? null : response.Headers,
                    Method = request.Method,
                    Target = request.Target,
                    RequestBody = request.Body,
                    ResponseBody = response == null ? new byte[0] : response.Body,
                    RequestIncomplete = !request.Complete,
                    ResponseIncomplete = response == null || !response.Complete,
                    ClientIP = client.Address,
                    ClientPort = client.Port,
                    ServerIP = server.Address,
                    ServerPort = server.Port,
                    RequestStart = requests.Time(request.Start, request.BodyStart, true),
                    RequestHeadersTime = requests.Time(request.Start, request.BodyStart, false),
                    RequestEnd = requests.Time(request.Start, request.End, false),
                    ResponseStart = response == null ? DateTime.MinValue : responses.Time(response.Start, response.BodyStart, true),
                    ResponseHeadersTime = response == null ? DateTime.MinValue : responses.Time(response.Start, response.BodyStart, false),
                    ResponseEnd = response == null ? DateTime.MinValue : responses.Time(response.Start, response.End, false),
                    Informationals = informational.ToString()
                };
                exchanges.Add(exchange);
                if (exchange.RequestIncomplete) Warn("Incomplete HTTP request body imported with dropped flag");
                if (exchange.ResponseIncomplete) Warn(response == null ? "HTTP response missing; no successful response invented" : "Incomplete HTTP response body imported with dropped flag");
                requestOffset = request.End;
                if (response != null) responseOffset = response.End;
                if (response != null && (response.Status == 101 || (request.Method == "CONNECT" && response.Status >= 200 && response.Status < 300)))
                {
                    Warn("CONNECT tunnel or protocol upgrade bytes skipped");
                    break;
                }
                if (!request.Complete || (response != null && !response.Complete)) break;
                // Missing response headers cannot be searched for later: doing so would risk pairing with the wrong request.
                if (response == null) responseOffset = responses.Data.Length;
            }
            if (responseOffset < responses.Data.Length) Warn("Unpaired server bytes skipped");
        }

        private Message ParseMessage(TcpStream stream, int offset, bool request, string requestMethod)
        {
            byte[] data = stream.Data;
            int headerEnd = Find(data, offset, data.Length, true, MaxHeaders);
            if (headerEnd < 0) return null;
            int bodyStart = headerEnd + 4;
            string headers = Latin1.GetString(data, offset, bodyStart - offset);
            string[] lines = headers.Substring(0, headers.Length - 4).Split(new[] { "\r\n" }, StringSplitOptions.None);
            ValidateValue(lines[0]);
            Message message = new Message { Headers = headers, Start = offset, BodyStart = bodyStart, Complete = true };
            if (request)
            {
                string[] words = lines[0].Split(' ');
                if (words.Length != 3 || !Token(words[0]) || !Version(words[2]) || words[1].Length == 0)
                    throw new InvalidDataException("Unsupported or invalid HTTP/1.x request line");
                for (int i = 0; i < words[1].Length; i++) if (words[1][i] <= 32 || words[1][i] == 127) throw new InvalidDataException("Invalid HTTP request target");
                message.Method = words[0];
                message.Target = words[1];
                ValidateTarget(message.Method, message.Target);
            }
            else
            {
                string[] words = lines[0].Split(new[] { ' ' }, 3);
                int status;
                if (words.Length < 2 || !Version(words[0]) || words[1].Length != 3 || !Int32.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out status) || status < 100 || status > 599)
                    throw new InvalidDataException("Unsupported or invalid HTTP/1.x response line");
                message.Status = status;
            }
            long contentLength = -1;
            List<string> encodings = new List<string>();
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon <= 0 || !Token(lines[i].Substring(0, colon))) throw new InvalidDataException("Invalid or folded HTTP header");
                string name = lines[i].Substring(0, colon), value = lines[i].Substring(colon + 1).Trim(' ', '\t');
                ValidateValue(value);
                if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string part in value.Split(','))
                    {
                        long length;
                        if (!Int64.TryParse(part.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out length) || length < 0 || length > MaxStream)
                            throw new InvalidDataException("Invalid or oversized HTTP Content-Length");
                        if (contentLength >= 0 && contentLength != length) throw new InvalidDataException("Conflicting HTTP Content-Length headers");
                        contentLength = length;
                    }
                }
                if (name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string part in value.Split(','))
                    {
                        string coding = part.Trim().ToLowerInvariant();
                        if (!Token(coding)) throw new InvalidDataException("Unsupported HTTP transfer coding");
                        encodings.Add(coding);
                    }
                }
            }
            if (encodings.Count > 0 && contentLength >= 0) throw new InvalidDataException("Ambiguous HTTP Transfer-Encoding and Content-Length");
            int chunked = encodings.IndexOf("chunked");
            if (chunked >= 0 && (chunked != encodings.Count - 1 || encodings.LastIndexOf("chunked") != chunked))
                throw new InvalidDataException("Invalid HTTP chunked transfer-coding order");
            bool noBody = !request && (requestMethod == "HEAD" || message.Status < 200 || message.Status == 204 || message.Status == 304 ||
                (requestMethod == "CONNECT" && message.Status >= 200 && message.Status < 300));
            if (noBody) message.End = bodyStart;
            else if (chunked >= 0)
            {
                int end = ChunkEnd(data, bodyStart);
                message.Complete = end >= 0;
                message.End = end < 0 ? data.Length : end;
            }
            else if (encodings.Count > 0)
            {
                if (request) throw new InvalidDataException("Request transfer coding is not chunked");
                message.End = data.Length;
                message.Complete = stream.Closed;
            }
            else if (contentLength >= 0)
            {
                message.Complete = contentLength <= data.Length - bodyStart;
                message.End = bodyStart + (int)Math.Min(contentLength, data.Length - bodyStart);
            }
            else if (request) message.End = bodyStart;
            else
            {
                message.End = data.Length;
                message.Complete = stream.Closed;
            }
            message.Body = new byte[message.End - bodyStart];
            Buffer.BlockCopy(data, bodyStart, message.Body, 0, message.Body.Length);
            return message;
        }

        private int ChunkEnd(byte[] data, int offset)
        {
            while (offset < data.Length)
            {
                CheckWork();
                int line = Find(data, offset, data.Length, false, 8192);
                if (line < 0) return -1;
                string size = Latin1.GetString(data, offset, line - offset);
                int extension = size.IndexOf(';');
                if (extension >= 0) { ValidateValue(size.Substring(extension)); size = size.Substring(0, extension); }
                uint count;
                if (size.Length == 0 || size.Length > 8 || !UInt32.TryParse(size, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out count) || count > MaxStream)
                    throw new InvalidDataException("Invalid or oversized HTTP chunk");
                offset = line + 2;
                if (count == 0)
                {
                    int trailersStart = offset;
                    while (true)
                    {
                        int trailer = Find(data, offset, data.Length, false, MaxHeaders - (offset - trailersStart));
                        if (trailer < 0) return -1;
                        if (trailer == offset) return trailer + 2;
                        string field = Latin1.GetString(data, offset, trailer - offset);
                        int colon = field.IndexOf(':');
                        if (colon <= 0 || !Token(field.Substring(0, colon))) throw new InvalidDataException("Invalid HTTP chunk trailer");
                        ValidateValue(field.Substring(colon + 1));
                        offset = trailer + 2;
                    }
                }
                if ((long)count + 2 > data.Length - offset) return -1;
                offset += (int)count;
                if (data[offset] != 13 || data[offset + 1] != 10) throw new InvalidDataException("Invalid HTTP chunk terminator");
                offset += 2;
            }
            return -1;
        }

        private int Find(byte[] data, int start, int end, bool headers, int maximum)
        {
            int width = headers ? 4 : 2;
            int limit = (int)Math.Min(end, (long)start + Math.Max(0, maximum));
            for (int i = start; i <= limit - width; i++)
            {
                CheckWork();
                if (data[i] == 13 && data[i + 1] == 10 && (!headers || (data[i + 2] == 13 && data[i + 3] == 10))) return i;
            }
            if (end - start >= maximum) throw new InvalidDataException("HTTP header or line resource limit exceeded");
            return -1;
        }

        private static bool IsRequest(byte[] data)
        {
            int end = Array.IndexOf(data, (byte)13, 0, Math.Min(data.Length, 8192));
            if (end < 0) return false;
            string[] words = Latin1.GetString(data, 0, end).Split(' ');
            return words.Length == 3 && Token(words[0]) && Version(words[2]);
        }

        private static bool Version(string value) { return value == "HTTP/1.0" || value == "HTTP/1.1"; }
        private static bool IsTls(byte[] data) { return data.Length >= 3 && data[0] >= 20 && data[0] <= 23 && data[1] == 3; }
        private static bool Starts(byte[] data, string value) { return data.Length >= value.Length && Latin1.GetString(data, 0, value.Length) == value; }

        private static bool Token(string value)
        {
            if (value.Length == 0) return false;
            foreach (char c in value)
                if (c <= 32 || c >= 127 || "()<>@,;:\\\"/[]?={}".IndexOf(c) >= 0) return false;
            return true;
        }

        private static void ValidateTarget(string method, string target)
        {
            Uri uri;
            if (method == "CONNECT")
            {
                int colon = target.LastIndexOf(':');
                int port;
                if (colon < 1 || !Int32.TryParse(target.Substring(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535 ||
                    target.IndexOfAny(new[] { '/', '?', '#' }) >= 0 || !Uri.TryCreate("http://" + target + "/", UriKind.Absolute, out uri) || uri.UserInfo.Length != 0)
                    throw new InvalidDataException("Invalid HTTP CONNECT authority");
                return;
            }
            if (target[0] == '/' || target == "*") return;
            if (!Uri.TryCreate(target, UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https") ||
                uri.Host.Length == 0 || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
                throw new InvalidDataException("Unsupported HTTP request-target form");
        }

        private static void ValidateValue(string value)
        {
            foreach (char c in value) if ((c < 32 && c != '\t') || c == 127) throw new InvalidDataException("Control character in HTTP header");
        }
    }
}
