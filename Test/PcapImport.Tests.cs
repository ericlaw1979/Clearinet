using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Clearinet;

namespace TestExtensions
{
    public static class PcapImportTests
    {
        private static int assertions;
        private static string summary;
        private static readonly byte[] Request = Text("GET / HTTP/1.1\r\nHost: example.test:8080\r\n\r\n");
        private static readonly byte[] Response = Text("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nabc");
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static void Run(string filename)
        {
            Containers();
            TcpReassembly();
            HttpFraming();
            UnsupportedAndMalformed();
            Cancellation();
            Integration(filename);
            Console.WriteLine("PCAP import: " + assertions + " assertions plus truncation/mutation coverage passed.");
        }

        private static void Containers()
        {
            foreach (bool little in new[] { false, true })
            foreach (bool nano in new[] { false, true })
            {
                IList<PcapExchange> items = Decode(Classic(little, nano, 101, Pair(Request, Response)));
                Check(items.Count == 1 && !items[0].ResponseIncomplete, "Classic endian/resolution HTTP");
                Check(items[0].RequestStart == Epoch.AddSeconds(10).AddTicks(nano ? 1234567 : 1234560), "Classic timestamp precision");
            }
            foreach (uint link in new uint[] { 1, 12, 101, 105, 228, 229 })
            {
                bool ipv6 = link == 229;
                byte[][] pair = Pair(Request, Response, ipv6);
                if (link == 1) pair = new[] { Ethernet(pair[0], true), Ethernet(pair[1], true) };
                if (link == 105) pair = new[] { Wifi(pair[0], false), Wifi(pair[1], true) };
                IList<PcapExchange> items = Decode(Classic(true, false, link, pair));
                Check(items.Count == 1 && items[0].RequestBody.Length == 0, "Raw IP / VLAN Ethernet / 802.11");
                if (ipv6) Check(items[0].ServerIP == "2001:db8::2", "IPv6 endpoint");
            }
            byte[] extension = Packet(true, 100, 0x18, Request, true);
            byte[] extended = new byte[extension.Length + 8];
            Buffer.BlockCopy(extension, 0, extended, 0, 40);
            Buffer.BlockCopy(extension, 40, extended, 48, extension.Length - 40);
            extended[6] = 60;
            extended[40] = 6;
            Put16(extended, 4, (uint)(extended.Length - 40), false);
            Check(Decode(Classic(true, false, 229, extended, Packet(false, 200, 0x18, Response, true))).Count == 1, "IPv6 destination options");

            foreach (bool little in new[] { false, true })
            {
                byte[] capture = Ng(little, false, false, Pair(Request, Response));
                IList<PcapExchange> items = Decode(capture);
                Check(items.Count == 1, "PCAPNG endian");
                Check(items[0].RequestStart == Epoch.AddSeconds(12).AddTicks(1234567), "PCAPNG nanosecond resolution and signed offset");
                capture = Ng(little, true, false, Pair(Request, Response));
                items = Decode(capture);
                Check(items[0].RequestStart == Epoch.AddSeconds(12.5), "PCAPNG binary timestamp resolution");
                Check(Decode(Ng(little, false, true, Pair(Request, Response))).Count == 1, "Obsolete packet blocks");
                Check(Decode(Join(Ng(little, false, false, Pair(Request, Response)), Ng(!little, false, false, Pair(Request, Response)))).Count == 2, "Multiple sections switch byte order and isolate connections");
                byte[] negativeOffset = Join(Short(14, little), Short(8, little), Number(little ? 0xfffffffeu : 0xffffffffu, little), Number(little ? 0xffffffffu : 0xfffffffeu, little));
                items = Decode(Join(Section(little), Interface(little, 101, negativeOffset), Enhanced(little, Packet(true, 100, 24, Request), 0, 10123456), Enhanced(little, Packet(false, 200, 24, Response), 0, 11123456)));
                Check(items[0].RequestStart == Epoch.AddSeconds(8).AddTicks(1234560), "Negative PCAPNG offset and default microseconds");
            }
            byte[] simple = Join(Section(true), Interface(true, 101, null), Block(3, Join(Number((uint)Packet(true, 100, 24, Request).Length, true), Pad(Packet(true, 100, 24, Request))), true));
            IList<PcapExchange> simpleItems = Decode(simple);
            Check(simpleItems.Count == 1 && simpleItems[0].RequestStart == DateTime.MinValue, "Simple packet has explicitly unknown timing");
            Check(summary.Contains("no timestamp"), "Simple packet diagnostic");
            byte[] twoIfaces = Join(Section(true), Interface(true, 1, null), Interface(true, 101, null), Enhanced(true, Packet(true, 100, 24, Request), 1, 0), Enhanced(true, Packet(false, 200, 24, Response), 1, 1));
            Check(Decode(twoIfaces).Count == 1, "PCAPNG interface ID selection");
            byte[] truncated = Classic(true, false, 101, Pair(Request, Response));
            Put32(truncated, 36, (uint)(Packet(true, 100, 24, Request).Length + 10), true);
            Check(Decode(truncated).Count == 1 && summary.Contains("Snap-length"), "Original/captured length truncation diagnosed");
        }

        private static void TcpReassembly()
        {
            int split = Request.Length / 2;
            byte[][] ordered = new[]
            {
                Packet(true, 99, 2, new byte[0]), Packet(false, 199, 0x12, new byte[0]),
                Packet(true, (uint)(100 + split), 24, Sub(Request, split, Request.Length - split)),
                Packet(true, 100, 24, Sub(Request, 0, split)), Packet(true, 100, 24, Request),
                Packet(false, 200, 24, Response), Packet(false, 200, 24, Response)
            };
            IList<PcapExchange> items = Decode(Classic(true, false, 101, ordered));
            Check(items.Count == 1 && Text(items[0].ResponseBody) == "abc", "Out-of-order and identical overlapping retransmission");
            byte[] conflict = (byte[])Request.Clone(); conflict[5] ^= 1;
            Check(Decode(Classic(true, false, 101, Packet(true, 100, 24, Request), Packet(true, 100, 24, conflict), Packet(false, 200, 24, Response))).Count == 0 && summary.Contains("Conflicting TCP"), "Conflicting overlap skips connection");
            byte[] header = Text("HTTP/1.1 200 OK\r\nContent-Length: 6\r\n\r\n");
            items = Decode(Classic(true, false, 101, Packet(true, 100, 24, Request), Packet(false, 200, 24, Join(header, Text("ab"))), Packet(false, (uint)(200 + header.Length + 4), 25, Text("ef"))));
            Check(items.Count == 1 && Text(items[0].ResponseBody) == "ab" && items[0].ResponseIncomplete, "Gap never concatenates subsequent bytes");
            Check(summary.Contains("TCP gap"), "Gap diagnostic");
            items = Decode(Classic(true, false, 101, Packet(true, 99, 2, new byte[0]), Packet(true, 101, 24, Request), Packet(false, 200, 24, Response)));
            Check(items.Count == 0 && summary.Contains("TCP gap"), "Missing first payload after SYN is not resynchronized");
            uint basis = UInt32.MaxValue - 8;
            items = Decode(Classic(true, false, 101, Packet(true, basis - 1, 2, new byte[0]), Packet(true, unchecked(basis + (uint)split), 24, Sub(Request, split, Request.Length - split)), Packet(true, basis, 24, Sub(Request, 0, split)), Packet(false, 200, 24, Response)));
            Check(items.Count == 1, "TCP sequence wraparound");
            byte[] capture = Classic(true, false, 101, Packet(true, 99, 2, new byte[0]), Packet(true, 100, 24, Request), Packet(false, 200, 24, Response), Packet(true, 900, 2, new byte[0]), Packet(true, 901, 24, Request), Packet(false, 800, 24, Response));
            Check(Decode(capture).Count == 2, "SYN tuple reuse starts a new generation");
            byte[] closeResponse = Text("HTTP/1.0 200 OK\r\n\r\nbody");
            foreach (byte flags in new byte[] { 24, 25, 28 })
            {
                items = Decode(Classic(true, false, 101, Packet(true, 100, 24, Request), Packet(false, 200, flags, closeResponse)));
                Check(items.Count == 1 && items[0].ResponseIncomplete == (flags != 25), "Close-delimited requires contiguous FIN, not capture EOF or RST");
            }
            items = Decode(Classic(true, false, 101, Packet(true, 100, 24, Request), Packet(false, 200, 24, closeResponse), Packet(false, (uint)(202 + closeResponse.Length), 17, new byte[0])));
            Check(items[0].ResponseIncomplete, "FIN beyond a gap does not complete close-delimited body");
            byte[] truncatedFin = Classic(true, false, 101, Packet(true, 100, 24, Request), Packet(false, 200, 25, closeResponse));
            Put32(truncatedFin, 24 + 16 + Packet(true, 100, 24, Request).Length + 12, (uint)(Packet(false, 200, 25, closeResponse).Length + 1), true);
            Check(Decode(truncatedFin)[0].ResponseIncomplete, "Snap-truncated FIN cannot prove a complete close-delimited response");
            items = Decode(Classic(true, false, 101, Packet(true, 100, 24, Request), Packet(false, (uint)(200 + closeResponse.Length), 17, new byte[0]), Packet(false, 200, 24, closeResponse)));
            Check(!items[0].ResponseIncomplete, "Out-of-order FIN can close a subsequently reassembled stream");
        }

        private static void HttpFraming()
        {
            byte[] binary = new byte[] { 0, 128, 255, 13, 10 };
            byte[] post = Join(Text("POST /upload HTTP/1.1\r\nHost: h\r\nContent-Length: 5\r\n\r\n"), binary);
            byte[] head = Text("HEAD /head HTTP/1.1\r\nHost: h\r\n\r\n");
            byte[] responses = Join(Text("HTTP/1.1 100 Continue\r\n\r\n"), Response, Text("HTTP/1.1 200 OK\r\nContent-Length: 500\r\n\r\nHTTP/1.1 204 No Content\r\n\r\n"));
            IList<PcapExchange> items = Decode(Classic(true, false, 101, Pair(Join(post, head, Request), responses)));
            Check(items.Count == 3 && Equal(items[0].RequestBody, binary) && items[1].ResponseBody.Length == 0 && items[2].ResponseBody.Length == 0, "Persistent pipelined binary POST, HEAD, 204");
            Check(items[0].Informationals.Contains("100 Continue"), "Interim headers preserved separately");
            byte[] chunks = Join(Text("5;foo=bar\r\n"), binary, Text("\r\n0\r\nX-Trailer: yes\r\n\r\n"));
            responses = Join(Text("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n"), chunks, Response);
            items = Decode(Classic(true, false, 101, Pair(Join(Request, Request), responses)));
            Check(items.Count == 2 && Equal(items[0].ResponseBody, chunks) && Text(items[1].ResponseBody) == "abc", "Chunked wire bytes, extensions, trailers and subsequent response");
            Check(items[0].ResponseHeaders.Contains("Transfer-Encoding: chunked"), "Framing header remains consistent with wire body");
            byte[] chunkRequest = Join(Text("POST / HTTP/1.1\r\nHost: h\r\nTransfer-Encoding: chunked\r\n\r\n"), chunks);
            items = Decode(Classic(true, false, 101, Pair(chunkRequest, Response)));
            Check(Equal(items[0].RequestBody, chunks), "Chunked request raw framing");
            items = Decode(Classic(true, false, 101, Pair(Request, Text("HTTP/1.1 304 Not Modified\r\nContent-Length: 800\r\n\r\n"))));
            Check(items[0].ResponseBody.Length == 0 && !items[0].ResponseIncomplete, "304 no body despite representation length");
            foreach (string switchRequest in new[] { "CONNECT [2001:db8::2]:443 HTTP/1.1\r\nHost: [2001:db8::2]:443\r\n\r\n", Text(Request) })
            {
                string response = switchRequest.StartsWith("CONNECT", StringComparison.Ordinal) ? "HTTP/1.1 200 Connected\r\n\r\n" : "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\n\r\n";
                items = Decode(Classic(true, false, 101, Pair(Join(Text(switchRequest), Text("opaque tunnel data")), Join(Text(response), Text("opaque server data")))));
                Check(items.Count == 1 && items[0].ResponseBody.Length == 0 && summary.Contains("tunnel"), "CONNECT/101 terminal upgrade");
            }
            items = Decode(Classic(true, false, 101, Packet(true, 100, 24, Request)));
            Check(items.Count == 1 && items[0].ResponseHeaders == null && items[0].ResponseIncomplete, "Missing response not fabricated");
            items = Decode(Classic(true, false, 101, Pair(post, Sub(Response, 0, Response.Length - 2))));
            Check(items[0].ResponseIncomplete && items[0].ResponseBody.Length == 1, "Incomplete Content-Length body");
            items = Decode(Classic(true, false, 101, Pair(Sub(post, 0, post.Length - 3), Response)));
            Check(items[0].RequestIncomplete && items[0].RequestBody.Length == 2, "Incomplete request body");
            items = Decode(Classic(true, false, 101, Pair(Request, Join(Text("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n"), Text("5\r\na")))));
            Check(items[0].ResponseIncomplete, "Incomplete chunked body");
            foreach (string invalid in new[]
            {
                "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nContent-Length: 3\r\n\r\nabc",
                "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nTransfer-Encoding: chunked\r\n\r\n0\r\n\r\n",
                "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked, gzip\r\n\r\n",
                "HTTP/1.1 200 OK\r\nContent-Length: 99999999999\r\n\r\n",
                "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nZ\r\n",
                "HTTP/1.1 200 OK\r\nContent-Length: 0\v\r\n\r\n",
                "HTTP/1.1 200 OK\r\n Folded: not supported\r\n\r\n"
            })
            {
                Check(Decode(Classic(true, false, 101, Pair(Request, Text(invalid)))).Count == 0, "Reject ambiguous/invalid framing");
                Check(summary.Contains("HTTP"), "Invalid framing diagnostic");
            }
        }

        private static void UnsupportedAndMalformed()
        {
            Check(Decode(Classic(true, false, 101, Pair(Text("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"), new byte[0]))).Count == 0 && summary.Contains("HTTP/2"), "h2 preface is not an HTTP/1 request");
            Check(Decode(Classic(true, false, 101, Pair(new byte[] { 22, 3, 3, 0, 0 }, new byte[0]))).Count == 0 && summary.Contains("TLS"), "TLS explicitly skipped");
            Check(Decode(Classic(true, false, 999, Pair(Request, Response))).Count == 0 && summary.Contains("link type"), "Unsupported link diagnostic");
            Check(Decode(Classic(true, false, 105, new byte[23])).Count == 0 && summary.Contains("Truncated 802.11"), "Truncated 802.11 diagnostic");
            byte[] encryptedWifi = Wifi(Packet(true, 100, 24, Request), false); encryptedWifi[1] |= 0x40;
            Check(Decode(Classic(true, false, 105, encryptedWifi)).Count == 0 && summary.Contains("Encrypted 802.11"), "Encrypted 802.11 diagnostic");
            Check(Decode(Classic(true, false, 101, Packet(false, 200, 24, Response))).Count == 0, "Response-only capture does not invent a request");
            Check(Decode(Classic(true, false, 101, Pair(Text("GET unsupported-target HTTP/1.1\r\nHost: h\r\n\r\n"), Response))).Count == 0 && summary.Contains("request-target"), "Unsupported target form diagnosed");
            byte[] fragment = Packet(true, 100, 24, Request); fragment[6] = 32;
            Check(Decode(Classic(true, false, 101, fragment)).Count == 0 && summary.Contains("Fragmented IPv4"), "IPv4 fragmentation diagnostic");
            fragment = Packet(true, 100, 24, Request, true); fragment[6] = 44;
            Check(Decode(Classic(true, false, 229, fragment)).Count == 0 && summary.Contains("Fragmented IPv6"), "IPv6 fragmentation diagnostic");
            Invalid(new byte[0]);
            Invalid(new byte[24]);
            byte[] bad = Classic(true, false, 101, Pair(Request, Response)); Put32(bad, 32, 0xffffffff, true); Invalid(bad);
            bad = Ng(true, false, false, Pair(Request, Response)); bad[bad.Length - 1] ^= 1; Invalid(bad);
            Invalid(Join(Section(true), Interface(true, 101, null), Enhanced(true, Packet(true, 100, 24, Request), 1, 0)));
            Invalid(Join(Section(true), Block(1, Join(new byte[] { 101, 0, 0, 0 }, Number(65535, true), new byte[] { 9, 0, 10, 0 }), true)));
            byte[] valid = Ng(false, false, false, Pair(Request, Response));
            for (int length = 0; length < valid.Length; length++) SafeDecode(Sub(valid, 0, length));
            valid = Classic(false, true, 1, Ethernet(Packet(true, 100, 24, Request), true), Ethernet(Packet(false, 200, 24, Response), true));
            for (int length = 0; length < valid.Length; length++) SafeDecode(Sub(valid, 0, length));
            Random random = new Random(20261015);
            for (int i = 0; i < 1000; i++)
            {
                bad = (byte[])valid.Clone(); bad[random.Next(bad.Length)] = (byte)random.Next(256); SafeDecode(bad);
                byte[] randomData = new byte[random.Next(256)]; random.NextBytes(randomData); SafeDecode(randomData);
            }
        }

        private static void Cancellation()
        {
            byte[] capture = Classic(true, false, 101, Pair(Request, Response));
            foreach (string stage in new[] { "Reading", "Reassembling", "Decoding" })
            {
                bool canceled = false;
                try { new PcapDecoder(delegate(int percent, string status) { return !status.StartsWith(stage, StringComparison.Ordinal); }).Read(new MemoryStream(capture)); }
                catch (OperationCanceledException) { canceled = true; }
                Check(canceled, "Cancellation during " + stage);
            }
            bool processingCanceled = false;
            // Scanning a large header exercises cancellation in byte-level parsing, rather than only at packet boundaries.
            byte[] hugeHeader = Join(Text("HTTP/1.1 200 OK\r\nX-Long: "), new byte[20000]);
            try { new PcapDecoder(delegate(int percent, string status) { return !status.StartsWith("Processing", StringComparison.Ordinal); }).Read(new MemoryStream(Classic(true, false, 101, Pair(Request, hugeHeader)))); }
            catch (OperationCanceledException) { processingCanceled = true; }
            Check(processingCanceled, "Cancellation during HTTP processing");
        }

        private static void Integration(string filename)
        {
            byte[] capture = Classic(true, false, 101, Pair(Request, Response));
            PcapFormatImport importer = new PcapFormatImport();
            List<int> progress = new List<int>();
            Exchange[] items = importer.ImportExchanges("Packet Capture", new Dictionary<string, object> { { "Content", capture } }, delegate(object sender, ProgressEventArgs args) { progress.Add(args.CompletionPercentage); });
            Check(items.Length == 1 && items[0].fullUrl == "http://example.test:8080/", "Actual importer preserves Host port");
            Check(items[0].clientIP == "10.0.0.1" && items[0].port == 8080 && items[0]["x-hostIP"] == "10.0.0.2", "Imported endpoint metadata");
            Check(items[0].Timers.ClientBeginRequest == Epoch.AddSeconds(10).AddTicks(1234560), "Imported request timing");
            Check(items[0].isFlagSet(ExchangeFlags.ImportedFromOtherTool) && items[0].responseCode == 200, "Imported flags/status");
            Check(progress[0] == 0 && progress[progress.Count - 1] == 100, "Fractional progress API reaches 100 percent");
            File.WriteAllBytes(filename, capture);
            Check(importer.ImportExchanges("Packet Capture", new Dictionary<string, object> { { "Filename", filename } }, null).Length == 1, "Filename import");
            items = importer.ImportExchanges("Packet Capture", new Dictionary<string, object> { { "Content", Classic(true, false, 101, Packet(true, 100, 24, Request)) } }, null);
            Check(items[0].responseCode == 0 && items[0].isFlagSet(ExchangeFlags.ResponseBodyDropped), "Actual importer missing response is not 200");
            byte[] abs = Text("GET http://[2001:db8::2]:8080/path?q=1 HTTP/1.1\r\nHost: proxy.invalid\r\n\r\n");
            items = importer.ImportExchanges("Packet Capture", new Dictionary<string, object> { { "Content", Classic(true, false, 229, Pair(abs, Response, true)) } }, null);
            Check(new Uri(items[0].fullUrl).Equals(new Uri("http://[2001:db8::2]:8080/path?q=1")), "Absolute-form IPv6 URL retains port: " + items[0].fullUrl);
            byte[] raw = Text("GET /v6 HTTP/1.0\r\n\r\n");
            items = importer.ImportExchanges("Packet Capture", new Dictionary<string, object> { { "Content", Classic(true, false, 229, Pair(raw, Response, true)) } }, null);
            Check(items[0].fullUrl == "http://[2001:db8::2]:8080/v6", "IPv6 fallback authority");
            byte[] wireChunks = Join(Text("3\r\n"), new byte[] { 0, 128, 255 }, Text("\r\n0\r\nX-Trailer: yes\r\n\r\n"));
            items = importer.ImportExchanges("Packet Capture", new Dictionary<string, object> { { "Content", Classic(true, false, 101, Pair(Request, Join(Text("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n"), wireChunks))) } }, null);
            Check(Equal(items[0].ResponseBody, wireChunks) && items[0].ResponseHeaders["Transfer-Encoding"] == "chunked", "Actual importer preserves binary chunks and trailer framing");
            items = importer.ImportExchanges("Packet Capture", new Dictionary<string, object> { { "Content", Classic(true, false, 101, Pair(Request, Sub(Response, 0, Response.Length - 1))) } }, null);
            Check(items[0].isFlagSet(ExchangeFlags.ResponseBodyDropped) && items[0]["ui-comments"].Contains("Incomplete"), "Actual importer marks partial body and explains incompleteness");
            Check(importer.ImportExchanges("Packet Capture", new Dictionary<string, object> { { "Content", capture } }, delegate(object sender, ProgressEventArgs args) { args.CancellationRequested = true; }) == null, "Actual importer cancellation returns null");
            Check(importer.ImportExchanges("Packet Capture", new Dictionary<string, object> { { "Content", capture } }, delegate(object sender, ProgressEventArgs args) { if (args.CurrentStatus.StartsWith("Creating", StringComparison.Ordinal)) args.CancellationRequested = true; }) == null, "Actual importer conversion cancellation returns null");
            bool rejected = false;
            try { importer.ImportExchanges("Packet Capture", new Dictionary<string, object> { { "Content", "not bytes" } }, null); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "Binary Content option type checked");
            importer.Dispose();
        }

        private static IList<PcapExchange> Decode(byte[] data)
        {
            PcapDecoder decoder = new PcapDecoder(null);
            IList<PcapExchange> result = decoder.Read(new MemoryStream(data)); summary = decoder.Summary; return result;
        }
        private static void SafeDecode(byte[] data) { try { Decode(data); } catch (InvalidDataException) { } }
        private static void Invalid(byte[] data)
        {
            bool invalid = false;
            try { Decode(data); } catch (InvalidDataException) { invalid = true; }
            Check(invalid, "Malformed capture must fail validation");
        }
        private static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message + "\n" + summary); }
        private static byte[] Text(string value) { return Encoding.GetEncoding(28591).GetBytes(value); }
        private static string Text(byte[] value) { return Encoding.GetEncoding(28591).GetString(value); }
        private static bool Equal(byte[] a, byte[] b) { return Convert.ToBase64String(a) == Convert.ToBase64String(b); }
        private static byte[][] Pair(byte[] request, byte[] response) { return Pair(request, response, false); }
        private static byte[][] Pair(byte[] request, byte[] response, bool ipv6) { return new[] { Packet(true, 100, 24, request, ipv6), Packet(false, 200, 24, response, ipv6) }; }
        private static byte[] Packet(bool client, uint seq, byte flags, byte[] payload) { return Packet(client, seq, flags, payload, false); }
        private static byte[] Packet(bool client, uint seq, byte flags, byte[] payload, bool ipv6)
        {
            int header = ipv6 ? 40 : 20;
            byte[] packet = new byte[header + 20 + payload.Length];
            packet[0] = (byte)(ipv6 ? 0x60 : 0x45);
            if (ipv6)
            {
                Put16(packet, 4, (uint)(packet.Length - 40), false); packet[6] = 6;
                packet[8] = packet[24] = 0x20; packet[9] = packet[25] = 1; packet[10] = packet[26] = 0x0d; packet[11] = packet[27] = 0xb8;
                packet[23] = (byte)(client ? 1 : 2); packet[39] = (byte)(client ? 2 : 1);
            }
            else
            {
                Put16(packet, 2, (uint)packet.Length, false); packet[9] = 6;
                packet[12] = packet[16] = 10; packet[15] = (byte)(client ? 1 : 2); packet[19] = (byte)(client ? 2 : 1);
            }
            Put16(packet, header, client ? 12345u : 8080u, false); Put16(packet, header + 2, client ? 8080u : 12345u, false);
            Put32(packet, header + 4, seq, false); packet[header + 12] = 0x50; packet[header + 13] = flags;
            Buffer.BlockCopy(payload, 0, packet, header + 20, payload.Length);
            return packet;
        }
        private static byte[] Ethernet(byte[] ip, bool vlan)
        {
            byte[] frame = new byte[(vlan ? 22 : 14) + ip.Length];
            Put16(frame, 12, vlan ? 0x88a8u : 0x0800u, false);
            if (vlan) { Put16(frame, 16, 0x8100, false); Put16(frame, 20, 0x0800, false); }
            Buffer.BlockCopy(ip, 0, frame, frame.Length - ip.Length, ip.Length); return frame;
        }
        private static byte[] Wifi(byte[] ip, bool qosWds)
        {
            int header = qosWds ? 36 : 24;
            byte[] frame = new byte[header + 8 + ip.Length];
            Put16(frame, 0, qosWds ? 0x8388u : 0x0008u, true);
            frame[header] = frame[header + 1] = 0xaa; frame[header + 2] = 3;
            Put16(frame, header + 6, ip[0] >> 4 == 6 ? 0x86ddu : 0x0800u, false);
            Buffer.BlockCopy(ip, 0, frame, header + 8, ip.Length); return frame;
        }
        private static byte[] Classic(bool little, bool nano, uint link, params byte[][] packets)
        {
            byte[] header = new byte[24]; Put32(header, 0, nano ? 0xa1b23c4du : 0xa1b2c3d4u, little); Put16(header, 4, 2, little); Put16(header, 6, 4, little);
            Put32(header, 16, 65535, little); Put32(header, 20, link, little);
            List<byte[]> records = new List<byte[]>(); records.Add(header);
            for (int i = 0; i < packets.Length; i++)
            {
                byte[] record = new byte[16]; Put32(record, 0, (uint)(10 + i), little); Put32(record, 4, nano ? 123456789u : 123456u, little);
                Put32(record, 8, (uint)packets[i].Length, little); Put32(record, 12, (uint)packets[i].Length, little);
                records.Add(record); records.Add(packets[i]);
            }
            return Join(records.ToArray());
        }
        private static byte[] Ng(bool little, bool binary, bool obsolete, byte[][] packets)
        {
            byte[] resolution = new byte[] { 9, 0, 1, 0, (byte)(binary ? 0x8a : 9), 0, 0, 0 };
            if (!little) resolution = new byte[] { 0, 9, 0, 1, (byte)(binary ? 0x8a : 9), 0, 0, 0 };
            byte[] offset = Join(Short(14, little), Short(8, little), Number(little ? 2u : 0u, little), Number(little ? 0u : 2u, little));
            List<byte[]> blocks = new List<byte[]> { Section(little), Interface(little, 101, Join(resolution, offset)), Block(0xbad, new byte[4], little) };
            for (int i = 0; i < packets.Length; i++)
            {
                ulong time = binary ? 10752ul + (ulong)i * 1024 : 10123456789ul + (ulong)i * 1000000000;
                if (obsolete)
                {
                    byte[] body = Join(Short(0, little), Short(0, little), Number((uint)(time >> 32), little), Number((uint)time, little), Number((uint)packets[i].Length, little), Number((uint)packets[i].Length, little), Pad(packets[i]));
                    blocks.Add(Block(2, body, little));
                }
                else blocks.Add(Enhanced(little, packets[i], 0, time));
            }
            return Join(blocks.ToArray());
        }
        private static byte[] Section(bool little) { return Block(0x0a0d0d0a, Join(Number(0x1a2b3c4d, little), Short(1, little), Short(0, little), new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }), little); }
        private static byte[] Interface(bool little, uint link, byte[] options) { return Block(1, Join(Short(link, little), Short(0, little), Number(65535, little), options ?? new byte[0]), little); }
        private static byte[] Enhanced(bool little, byte[] packet, uint id, ulong time) { return Block(6, Join(Number(id, little), Number((uint)(time >> 32), little), Number((uint)time, little), Number((uint)packet.Length, little), Number((uint)packet.Length, little), Pad(packet)), little); }
        private static byte[] Block(uint type, byte[] body, bool little) { return Join(Number(type, little), Number((uint)(12 + body.Length), little), body, Number((uint)(12 + body.Length), little)); }
        private static byte[] Number(uint value, bool little) { byte[] result = new byte[4]; Put32(result, 0, value, little); return result; }
        private static byte[] Short(uint value, bool little) { byte[] result = new byte[2]; Put16(result, 0, value, little); return result; }
        private static void Put16(byte[] data, int offset, uint value, bool little) { data[offset] = (byte)(little ? value : value >> 8); data[offset + 1] = (byte)(little ? value >> 8 : value); }
        private static void Put32(byte[] data, int offset, uint value, bool little) { for (int i = 0; i < 4; i++) data[offset + i] = (byte)(value >> ((little ? i : 3 - i) * 8)); }
        private static byte[] Pad(byte[] value) { byte[] result = new byte[(value.Length + 3) & ~3]; Buffer.BlockCopy(value, 0, result, 0, value.Length); return result; }
        private static byte[] Sub(byte[] value, int offset, int count) { byte[] result = new byte[count]; Buffer.BlockCopy(value, offset, result, 0, count); return result; }
        private static byte[] Join(params byte[][] values) { using (MemoryStream result = new MemoryStream()) { foreach (byte[] value in values) result.Write(value, 0, value.Length); return result.ToArray(); } }
    }
}
