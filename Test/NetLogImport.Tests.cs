using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Clearinet;
using Clearinet.WebFormats;
using ImportNetlog;

namespace TestExtensions
{
    public static class NetLogImportTests
    {
        private static int assertions;
        private static readonly MethodInfo ReadLines = typeof(NetLogFormatImport).Assembly
            .GetType("ImportNetlog.NetlogImporter").GetMethod("ReadJSONLines", BindingFlags.NonPublic | BindingFlags.Static);
        private const string Constants = "{\"clientInfo\":{\"name\":\"Chromium\"},\"logFormatVersion\":1,\"timeTickOffset\":\"1700000000000\",\"logEventTypes\":{\"URL_REQUEST_START_JOB\":1,\"HTTP_TRANSACTION_SEND_REQUEST_HEADERS\":2,\"HTTP_TRANSACTION_READ_RESPONSE_HEADERS\":3,\"URL_REQUEST_JOB_FILTERED_BYTES_READ\":4},\"logSourceType\":{\"URL_REQUEST\":1},\"netError\":{}}";
        private const string First = "{\"type\":\"constants\",\"constants\":" + Constants + "}";
        private static readonly string[] Events =
        {
            "{\"type\":1,\"phase\":1,\"time\":\"100\",\"source\":{\"id\":42,\"type\":1},\"params\":{\"url\":\"https://example.test/path?q=1\",\"method\":\"GET\"}}",
            "{\"type\":2,\"phase\":0,\"time\":\"101\",\"source\":{\"id\":42,\"type\":1},\"params\":{\"headers\":[\"Host: example.test\",\"X-Test: request\"]}}",
            "{\"type\":3,\"phase\":0,\"time\":\"102\",\"source\":{\"id\":42,\"type\":1},\"params\":{\"headers\":[\"HTTP/1.1 200 OK\",\"Content-Type: text/plain\",\"Content-Length: 5\"]}}",
            "{\"type\":4,\"phase\":0,\"time\":\"103\",\"source\":{\"id\":42,\"type\":1},\"params\":{\"bytes\":\"aGVsbG8=\",\"byte_count\":5}}"
        };

        public static void Run(string directory, string sample)
        {
            string lines = First + "\n" + String.Join("\n", Events.Select(e => "{\"type\":\"event\",\"event\":" + e + "}"))
                + "\n{\"type\":\"polledData\",\"polledData\":{\"extensionInfo\":[]}}\n{\"type\":\"end\"}";
            string json = "{\"constants\":" + Constants + ",\"events\":[" + String.Join(",", Events) + "],\"polledData\":{\"extensionInfo\":[]}}";
            Check(((ArrayList)Normalize(lines)["events"]).Count == 4, "All JSONL events retained");
            Exchange[] expected = Import(json, false);
            Exchange request = expected.Single(e => e.fullUrl == "https://example.test/path?q=1");
            Check(request.RequestHeaders.HTTPMethod == "GET", "Request method");
            Check(request.RequestHeaders["Host"] == "example.test", "Request headers");
            Check(request.responseCode == 200 && Encoding.UTF8.GetString(request.ResponseBody) == "hello", "Response status and decoded body");
            Compare(expected, Import(lines, false));
            Compare(expected, Import("\n\n" + lines.Replace("\n", "\r\n\r\n") + "\r\n", false));
            Compare(expected, Import(lines.Substring(0, lines.LastIndexOf('\n')), false));
            Compare(expected, Import("{\n\"constants\":" + Constants + ",\n\"events\":[\n" + String.Join(",\n", Events) + "\n],\n\"polledData\":{\"extensionInfo\":[]}}", false));
            Hashtable empty = Normalize(First + "\n{\"type\":\"end\"}");
            Check(((ArrayList)empty["events"]).Count == 0 && !empty.ContainsKey("polledData"), "Empty capture and optional polledData");
            Reject("{\"type\":\"constants\"}", "", "constants");
            foreach (string bad in new[] { "{", "[]", "{\"type\":\"event\"}", "{\"type\":\"event\",\"event\":[]}", "{\"type\":\"polledData\"}", First, "{\"type\":\"unknown\"}" })
                Reject(First, bad, "line 2");
            Reject(First, "{\"type\":\"end\"}\n{\"type\":\"event\",\"event\":{}}", "line 3");
            Reject(First, "{\"type\":\"polledData\",\"polledData\":{}}\n{\"type\":\"polledData\",\"polledData\":{}}", "line 3");

            string plain = Path.Combine(directory, "capture.JSONL");
            File.WriteAllText(plain, lines, new UTF8Encoding(true));
            Compare(expected, Import(plain, true));
            string gzip = Path.Combine(directory, "capture.jsonl.gz");
            using (var file = File.Create(gzip))
            using (var compressed = new GZipStream(file, CompressionMode.Compress))
            using (var writer = new StreamWriter(compressed)) writer.Write(lines);
            Compare(expected, Import(gzip, true));
            string zip = Path.Combine(directory, "capture.zip");
            using (var file = File.Create(zip))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(archive.CreateEntry("readme.txt").Open())) writer.Write("not a capture");
                using (var writer = new StreamWriter(archive.CreateEntry("nested/capture.JSONL").Open())) writer.Write(lines);
            }
            Compare(expected, Import(zip, true));
            if (!String.IsNullOrEmpty(sample))
            {
                string content = File.ReadAllText(sample);
                Hashtable normalized = Normalize(content);
                Check(((ArrayList)normalized["events"]).Count > 0, "Sample contains events");
                Check(normalized["polledData"] is Hashtable, "Sample polledData retained");
                Exchange[] sampleExchanges = Import(content, false);
                Check(sampleExchanges.Any(e => e.hostname != "NETLOG"), "Sample produces traffic exchanges");
                Compare(Import(JSON.JsonEncode(normalized), false), sampleExchanges);
                Console.WriteLine("Sample: " + ((ArrayList)normalized["events"]).Count + " events, " + sampleExchanges.Length + " exchanges.");
            }
            Console.WriteLine("NetLog import: " + assertions + " assertions passed.");
        }

        private static Hashtable Decode(string text)
        {
            JSON.JSONParseErrors errors;
            return (Hashtable)JSON.JsonDecode(text, out errors);
        }

        private static Hashtable Normalize(string text)
        {
            using (var reader = new StringReader(text))
                return (Hashtable)ReadLines.Invoke(null, new object[] { reader, Decode(reader.ReadLine()), 1 });
        }

        private static void Reject(string first, string rest, string diagnostic)
        {
            try
            {
                Normalize(first + "\n" + rest);
            }
            catch (TargetInvocationException error)
            {
                Check(error.InnerException is FormatException && error.InnerException.Message.Contains(diagnostic), "Malformed record diagnostic: " + diagnostic);
                return;
            }
            throw new Exception("Malformed JSONL was accepted: " + rest);
        }

        private static Exchange[] Import(string input, bool filename)
        {
            EventHandler<CApp.NotificationEventArgs> onNotification = (sender, args) => { throw new Exception(args.NotifyString); };
            CApp.OnNotification += onNotification;
            MethodInfo setClosing = typeof(CApp).GetProperty("isClosing").GetSetMethod(true);
            bool wasClosing = CApp.isClosing;
            // Suppress asynchronous UI updates in this standalone importer test process.
            setClosing.Invoke(null, new object[] { true });
            try
            {
                Exchange[] result = new NetLogFormatImport().ImportExchanges("NetLog JSON",
                    new Dictionary<string, object> { { filename ? "Filename" : "Content", input } }, null);
                Check(result != null, "Import returned no exchanges");
                return result;
            }
            finally
            {
                setClosing.Invoke(null, new object[] { wasClosing });
                CApp.OnNotification -= onNotification;
            }
        }

        private static void Compare(Exchange[] expected, Exchange[] actual)
        {
            Check(actual != null && expected.Length == actual.Length, "Exchange count matches JSON");
            for (int i = 0; i < expected.Length; ++i)
            {
                Exchange a = expected[i], b = actual[i];
                Check(a.fullUrl == b.fullUrl, "URL at " + i);
                Check(a.RequestHeaders.ToString() == b.RequestHeaders.ToString(), "Request headers at " + i);
                Check(a.ResponseHeaders.ToString() == b.ResponseHeaders.ToString(), "Response headers at " + i);
                if (a.hostname == "NETLOG" && a.ResponseHeaders["Content-Type"].StartsWith("application/json"))
                {
                    JSON.JSONParseErrors errors;
                    object expectedBody = JSON.JsonDecode(Encoding.UTF8.GetString(a.ResponseBody), out errors);
                    object actualBody = JSON.JsonDecode(Encoding.UTF8.GetString(b.ResponseBody), out errors);
                    Check(JsonEqual(expectedBody, actualBody), "Diagnostic JSON at " + a.fullUrl);
                }
                else
                    Check(a.ResponseBody.SequenceEqual(b.ResponseBody), "Response body at " + a.fullUrl);
                Check(a.Timers.ClientBeginRequest == b.Timers.ClientBeginRequest && a.Timers.ServerBeginResponse == b.Timers.ServerBeginResponse
                    && a.Timers.ClientDoneResponse == b.Timers.ClientDoneResponse, "Timestamps at " + i);
            }
        }

        private static bool JsonEqual(object expected, object actual)
        {
            Hashtable table = expected as Hashtable;
            if (table != null)
            {
                Hashtable other = actual as Hashtable;
                return other != null && table.Count == other.Count && table.Keys.Cast<object>()
                    .All(key => other.ContainsKey(key) && JsonEqual(table[key], other[key]));
            }
            ArrayList array = expected as ArrayList;
            if (array != null)
            {
                ArrayList other = actual as ArrayList;
                return other != null && array.Count == other.Count && Enumerable.Range(0, array.Count)
                    .All(i => JsonEqual(array[i], other[i]));
            }
            return Object.Equals(expected, actual);
        }

        private static void Check(bool condition, string message)
        {
            ++assertions;
            if (!condition) throw new Exception(message);
        }
    }
}
