using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Clearinet;
using Clearinet.WebFormats;

namespace TestExtensions
{
    public static class HarImportTests
    {
        private static int assertions;
        private static readonly MethodInfo ParseString = typeof(JSON).GetMethod("ParseString", BindingFlags.NonPublic | BindingFlags.Static);
        private const string Entry = "{\"startedDateTime\":\"2026-10-22T12:00:00Z\",\"time\":3,\"request\":{\"method\":\"GET\",\"url\":\"https://example.test/path?q=1\",\"httpVersion\":\"HTTP/1.1\",\"headers\":[],\"bodySize\":0},\"response\":{\"status\":200,\"statusText\":\"OK\",\"httpVersion\":\"HTTP/1.1\",\"headers\":[],\"content\":{\"size\":11,\"mimeType\":\"text/plain; charset=utf-8\",\"text\":\"hello\\nworld\"},\"bodySize\":11},\"timings\":{\"send\":1,\"wait\":1,\"receive\":1}}";

        public static void Run()
        {
            CheckString("\"\"", "", 2);
            CheckString(" \t\"plain\" trailing", "plain", 9);
            CheckString("\"a\\\"b\\\\c\\/d\\b\\f\\n\\r\\tend\"", "a\"b\\c/d\b\f\n\r\tend", null);
            CheckString("\"before\\u0041middle\\uD83D\\uDE00after\"", "beforeAmiddle\uD83D\uDE00after", null);
            CheckString("\"\\uZZZZ\\qtail\"", "\uFFFDtail", null);
            CheckString("\"\u00e9\u4e2d\uD83D\uDE00\"", "\u00e9\u4e2d\uD83D\uDE00", null);
            CheckString("\"" + new string('x', 1000000) + "\"", new string('x', 1000000), null);
            CheckString("\"" + new string('x', 100000) + "\\n" + new string('y', 100000) + "\"", new string('x', 100000) + "\n" + new string('y', 100000), null);
            foreach (string truncated in new[] { "\"", "\"abc", "\"abc\\", "\"abc\\u", "\"abc\\u123", "\"abc\\nrest" })
                CheckString(truncated, null, truncated.Contains("\\u") ? 6 : truncated.Length);
            string encoded = JSON.JsonEncode(new System.Collections.ArrayList { "plain", "escaped\n\t\"\\", "\uD83D\uDE00", 42.5, true, null });
            JSON.JSONParseErrors errors;
            Check(JSON.JsonEncode(JSON.JsonDecode(encoded, out errors)) == encoded && errors.iErrorIndex == -1, "Nested JSON round trip");

            EventHandler<CApp.NotificationEventArgs> onNotification = (sender, args) => { throw new Exception(args.NotifyString); };
            CApp.OnNotification += onNotification;
            MethodInfo setClosing = typeof(CApp).GetProperty("isClosing").GetSetMethod(true);
            bool wasClosing = CApp.isClosing;
            setClosing.Invoke(null, new object[] { true });
            try
            {
                TestImport();
            }
            finally
            {
                setClosing.Invoke(null, new object[] { wasClosing });
                CApp.OnNotification -= onNotification;
            }
            Console.WriteLine("HAR/JSON regression checks: " + assertions + " assertions passed.");
        }

        private static void CheckString(string input, string expected, int? expectedIndex)
        {
            object[] arguments = { input, 0 };
            string actual = (string)ParseString.Invoke(null, arguments);
            Check(actual == expected, "Decoded string differs for input of length " + input.Length);
            Check((int)arguments[1] == (expectedIndex ?? input.Length), "String parser position differs");
        }

        private static void TestImport()
        {
            string small = Capture(1);
            Exchange[] exchanges = Import(small, null);
            Check(exchanges.Length == 1, "Single entry imported without handler");
            Exchange exchange = exchanges[0];
            Check(exchange.fullUrl == "https://example.test/path?q=1" && exchange.RequestHeaders.HTTPMethod == "GET", "Request preserved");
            Check(exchange.responseCode == 200 && Encoding.UTF8.GetString(exchange.ResponseBody) == "hello\nworld", "Decoded response preserved");
            Check(exchange.ResponseHeaders["Content-Length"] == "11", "Body length normalized");
            Check(Import(Capture(0), null).Length == 0, "Empty HAR imported");
            Check(Import(small, (sender, progress) => { progress.CancellationRequested = true; }) == null, "Phase cancellation honored");
            Check(Import(small, (sender, progress) => { if (progress.CompletionPercentage == 100) progress.CancellationRequested = true; }) == null, "Completion cancellation honored");

            const int count = 30000;
            string large = Capture(count);
            int previousPercent = 0;
            int entryNotifications = 0;
            bool completed = false;
            long previousEntryTime = 0;
            var timer = Stopwatch.StartNew();
            exchanges = Import(large, (sender, progress) =>
            {
                Check(progress.CompletionPercentage >= previousPercent && progress.CompletionPercentage <= 100, "Progress monotonic and bounded");
                previousPercent = progress.CompletionPercentage;
                if (progress.CurrentStatus.StartsWith("Imported HAR entry ", StringComparison.Ordinal))
                {
                    long now = timer.ElapsedTicks;
                    if (entryNotifications > 0)
                        Check((now - previousEntryTime) * 1000.0 / Stopwatch.Frequency >= 99, "Entry callbacks spaced at least 100 ms apart (1 ms tolerance)");
                    previousEntryTime = now;
                    entryNotifications++;
                    Check(progress.CurrentStatus.EndsWith(" of " + count + ".", StringComparison.Ordinal), "Entry progress total correct");
                }
                if (progress.CompletionPercentage == 100 && progress.CurrentStatus == "Imported " + count + " HAR entries.") completed = true;
            });
            Check(exchanges.Length == count && completed, "All entries imported and completion always reported");
            Check(entryNotifications <= timer.ElapsedMilliseconds / 100, "Entry callback count bounded by elapsed time");
            Check(entryNotifications < count / 100, "No per-entry notification flood");
            exchanges = null;
            int cancellationNotifications = 0;
            Check(Import(large, (sender, progress) =>
            {
                if (progress.CurrentStatus.StartsWith("Imported HAR entry ", StringComparison.Ordinal) || progress.CompletionPercentage == 100)
                {
                    cancellationNotifications++;
                    progress.CancellationRequested = true;
                }
            }) == null && cancellationNotifications == 1, "Cancellation stops conversion at first entry/completion notification");
            Console.WriteLine("Large HAR: " + count + " entries; " + entryNotifications + " intermediate callbacks.");
        }

        private static string Capture(int count)
        {
            var builder = new StringBuilder();
            builder.Append("{\"log\":{\"entries\":[");
            for (int i = 0; i < count; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append(Entry);
            }
            return builder.Append("]}}").ToString();
        }

        private static Exchange[] Import(string content, EventHandler<ProgressEventArgs> handler)
        {
            return new HARFormatImport().ImportExchanges("HTTP Archive", new Dictionary<string, object> { { "Content", content } }, handler);
        }

        private static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new Exception(message);
        }
    }
}
