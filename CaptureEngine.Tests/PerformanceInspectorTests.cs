using Clearinet;
using Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;

namespace CaptureEngine.Tests
{
    [TestClass]
    public sealed class PerformanceInspectorTests
    {
        private static readonly DateTime CaptureTime = new DateTime(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

        private static HTTPRequestHeaders Request(string scheme = "https")
        {
            return new HTTPRequestHeaders { HTTPMethod = "GET", UriScheme = scheme };
        }

        private static HTTPResponseHeaders Response(string cacheControl = null)
        {
            var headers = new HTTPResponseHeaders { StatusCode = 200 };
            if (cacheControl != null) headers.Add("Cache-Control", cacheControl);
            return headers;
        }

        private static string Describe(HTTPResponseHeaders headers, HTTPRequestHeaders request = null,
            ExchangeTimers timers = null, byte[] body = null)
        {
            return PerformanceAnalysis.Describe(headers, body, request ?? Request(), timers);
        }

        [TestMethod]
        public void Freshness_SeparatesBrowserAndSharedCachesAndCombinesHeaderLines()
        {
            var headers = Response("public, MAX-AGE = \"60\"");
            headers.Add("Cache-Control", "s-maxage=3600");
            string report = Describe(headers);
            StringAssert.Contains(report, "Browser freshness interval: 60 seconds (1.0 minutes) (explicit max-age).");
            StringAssert.Contains(report, "Shared-cache freshness interval: 3,600 seconds (1.0 hours) (explicit s-maxage).");
            StringAssert.Contains(report, "Browser and shared storage permitted");
        }

        [DataTestMethod]
        [DataRow("max-age=-1")]
        [DataRow("max-age=abc")]
        [DataRow("max-age=60, max-age=120")]
        [DataRow("max-age=9223372036854775808")]
        [DataRow("max-age=\"60")]
        public void Freshness_InvalidOrDuplicateMaxAgeDoesNotFallBackToExpires(string directive)
        {
            var headers = Response(directive);
            headers.Add("Date", "Mon, 23 Nov 2026 12:00:00 GMT");
            headers.Add("Expires", "Mon, 23 Nov 2026 13:00:00 GMT");
            StringAssert.Contains(Describe(headers), "0 seconds (invalid or duplicate max-age; treat as stale).");
        }

        [TestMethod]
        public void Freshness_LargeValidMaxAgeDoesNotOverflowTimeSpan()
        {
            StringAssert.Contains(Describe(Response("max-age=9223372036854775807")), "9,223,372,036,854,775,807 seconds");
        }

        [TestMethod]
        public void Freshness_ExpiresUsesCapturedTimeNotCurrentTimeWhenDateIsMissing()
        {
            var headers = Response();
            headers.Add("Expires", "Mon, 23 Nov 2026 12:02:00 GMT");
            var timers = new ExchangeTimers { ProxyGotResponseHeaders = CaptureTime };
            StringAssert.Contains(Describe(headers, timers: timers), "120 seconds (2.0 minutes) (explicit Expires).");
            StringAssert.Contains(Describe(headers), "Unavailable (Expires needs Date or a captured response time).");
        }

        [DataTestMethod]
        [DataRow("0")]
        [DataRow("not a date")]
        [DataRow("2026-11-23")]
        public void Freshness_InvalidExpiresIsExpired(string expires)
        {
            var headers = Response();
            headers.Add("Expires", expires);
            StringAssert.Contains(Describe(headers), "0 seconds (invalid Expires; already expired).");
        }

        [TestMethod]
        public void Freshness_HeuristicIsAnEstimateAndRestrictedToEligibleStatuses()
        {
            var headers = Response();
            headers.Add("Date", "Mon, 23 Nov 2026 12:00:00 GMT");
            headers.Add("Last-Modified", "Mon, 23 Nov 2026 11:00:00 GMT");
            StringAssert.Contains(Describe(headers), "360 seconds (6.0 minutes) (heuristic estimate:");
            headers.StatusCode = 500;
            string report = Describe(headers);
            StringAssert.Contains(report, "status requires explicit caching permission");
            StringAssert.Contains(report, "Unspecified; no usable explicit lifetime");
        }

        [TestMethod]
        public void Cacheability_NoCacheRetainsFreshnessButRequiresValidation()
        {
            string report = Describe(Response("no-cache=\"Set-Cookie, X-Secret\", max-age=60"));
            StringAssert.Contains(report, "60 seconds (1.0 minutes) (explicit max-age).");
            StringAssert.Contains(report, "no-cache requires validation before reuse");
            Assert.IsFalse(report.Contains("Storage prohibited"));
        }

        [TestMethod]
        public void Cacheability_NoStorePrivateAuthorizationAndVaryAreReported()
        {
            StringAssert.Contains(Describe(Response("no-store, max-age=60")), "Storage prohibited by no-store.");
            string privateReport = Describe(Response("private, max-age=60, s-maxage=120"));
            StringAssert.Contains(privateReport, "shared storage restricted by private");
            StringAssert.Contains(privateReport, "Shared-cache freshness interval: Not applicable (private).");
            var request = Request();
            request.Add("Authorization", "Bearer test");
            StringAssert.Contains(Describe(Response("max-age=60"), request), "shared storage prohibited for an authorized request");
            StringAssert.Contains(Describe(Response("public, max-age=60"), request), "Browser and shared storage permitted");
            request.Add("Cache-Control", "no-store");
            StringAssert.Contains(Describe(Response("public"), request), "Storage prohibited by no-store.");
            var vary = Response();
            vary.Add("Vary", "Accept-Encoding");
            vary.Add("Vary", "*");
            StringAssert.Contains(Describe(vary), "Vary: * prevents reuse");
        }

        [DataTestMethod]
        [DataRow("\"abc\"", "strong validator")]
        [DataRow("W/\"abc\"", "weak validator")]
        [DataRow("abc", "invalid entity tag")]
        [DataRow("\"a b\"", "invalid entity tag")]
        public void Validators_IdentifiesStrongWeakAndInvalidETags(string tag, string expected)
        {
            var headers = Response();
            headers.Add("ETag", tag);
            StringAssert.Contains(Describe(headers), "ETag: " + tag + " (" + expected);
        }

        [TestMethod]
        public void Validators_LastModifiedAndMissingValidatorsAreReported()
        {
            var headers = Response();
            StringAssert.Contains(Describe(headers), "Validators: none;");
            headers.Add("Last-Modified", "Mon, 23 Nov 2026 11:00:00 GMT");
            StringAssert.Contains(Describe(headers), "usable with If-Modified-Since");
            headers["Last-Modified"] = "invalid";
            StringAssert.Contains(Describe(headers), "Last-Modified: invalid (invalid HTTP date).");
        }

        [DataTestMethod]
        [DataRow("gzip")]
        [DataRow("br")]
        [DataRow("deflate")]
        [DataRow("zstd")]
        public void Compression_ReportsEncodingWithoutClaimingSavings(string encoding)
        {
            var headers = Response();
            headers.Add("Content-Encoding", encoding);
            StringAssert.Contains(Describe(headers), "Compressed: " + encoding + ". Compression ratio is not inferred from headers.");
        }

        [TestMethod]
        public void Compression_WarnsOnlyForLargeTextLikeBodiesAndNotBodylessResponses()
        {
            var headers = Response();
            headers.Add("Content-Type", "application/problem+json; charset=utf-8");
            Assert.IsFalse(Describe(headers, body: new byte[1023]).Contains("WARNING: uncompressed"));
            StringAssert.Contains(Describe(headers, body: new byte[1024]), "WARNING: uncompressed text-like body >= 1 KiB");
            headers["Content-Type"] = "image/jpeg";
            Assert.IsFalse(Describe(headers, body: new byte[2048]).Contains("WARNING: uncompressed"));
            headers.StatusCode = 304;
            StringAssert.Contains(Describe(headers), "No response body is expected");
        }

        [DataTestMethod]
        [DataRow(999, 4999, false, false)]
        [DataRow(1000, 5000, true, true)]
        [DataRow(2000, 3000, true, false)]
        public void Timings_UseExactAdvisoryThresholds(int firstMilliseconds, int lastMilliseconds,
            bool firstWarning, bool lastWarning)
        {
            var timers = new ExchangeTimers
            {
                ProxyDoneRequest = CaptureTime,
                ServerBeginResponse = CaptureTime.AddMilliseconds(firstMilliseconds),
                ServerDoneResponse = CaptureTime.AddMilliseconds(lastMilliseconds)
            };
            string report = Describe(Response(), timers: timers);
            StringAssert.Contains(report, "Time to First Byte (TTFB): " + firstMilliseconds.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) +
                " ms - " + (firstWarning ? "WARNING: exceeds advisory threshold." : "below advisory threshold."));
            StringAssert.Contains(report, "Time to Last Byte (TTLB): " + lastMilliseconds.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) +
                " ms - " + (lastWarning ? "WARNING: exceeds advisory threshold." : "below advisory threshold."));
        }

        [TestMethod]
        public void Timings_MissingAndNegativeDurationsAreUnavailable()
        {
            StringAssert.Contains(Describe(Response()), "Time to Last Byte (TTLB): Unavailable (missing or incomplete capture timers).");
            var timers = new ExchangeTimers
            {
                ProxyDoneRequest = CaptureTime,
                ServerBeginResponse = CaptureTime.AddSeconds(-1),
                ServerDoneResponse = CaptureTime.AddSeconds(-1)
            };
            string report = Describe(Response(), timers: timers);
            StringAssert.Contains(report, "Time to First Byte (TTFB): Unavailable (inconsistent capture timers).");
            StringAssert.Contains(report, "Time to Last Byte (TTLB): Unavailable (inconsistent capture timers).");
        }

        [TestMethod]
        public void Analysis_SupportsHttpHttpsAndBareHeadersAndDoesNotMutateThem()
        {
            var headers = Response("max-age=60");
            var body = new byte[] { 1, 2, 3 };
            string before = headers.ToString();
            Assert.AreEqual(Describe(headers, Request("http"), body: body), Describe(headers, Request("https"), body: body));
            string bareReport = PerformanceAnalysis.Describe(headers, body, null, null);
            StringAssert.Contains(bareReport, "request unavailable");
            StringAssert.Contains(bareReport, "explicit max-age");
            Assert.AreEqual(before, headers.ToString());
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, body);
            Assert.AreEqual("No response is available.", PerformanceAnalysis.Describe(null, null, null, null));
        }

        [TestMethod]
        public void Inspector_IsReadOnlyAndDoesNotOverrideDefaultContentSelection()
        {
            var inspector = new PerformanceResponseInspector();
            Assert.AreEqual("Performance", inspector.TabTitle);
            Assert.AreEqual(-1, inspector.ScoreForExchange(null));
            Assert.AreEqual(InspectorFlags.HideInAutoResponder, inspector.GetFlags());
            inspector.bDirty = true;
            Assert.IsFalse(inspector.bDirty);
            var headers = Response();
            var body = new byte[] { 1 };
            inspector.Assign(headers, body, false);
            Assert.IsTrue(inspector.bReadOnly);
            Assert.AreSame(headers, inspector.headers);
            Assert.AreSame(body, inspector.body);
            inspector.Clear();
            Assert.IsNull(inspector.headers);
            Assert.IsNull(inspector.body);
        }

        [TestMethod]
        public void Timings_LastByteBeforeFirstByteIsUnavailable()
        {
            var timers = new ExchangeTimers
            {
                ProxyDoneRequest = CaptureTime,
                ServerBeginResponse = CaptureTime.AddSeconds(2),
                ServerDoneResponse = CaptureTime.AddSeconds(1)
            };
            string report = Describe(Response(), timers: timers);
            StringAssert.Contains(report, "Time to First Byte (TTFB): Unavailable (inconsistent capture timers).");
            StringAssert.Contains(report, "Time to Last Byte (TTLB): Unavailable (inconsistent capture timers).");
        }

        [TestMethod]
        public void Cacheability_ValidationAndInformationalResponsesAreNotIndependentlyStored()
        {
            var headers = Response("public, max-age=60");
            headers.StatusCode = 103;
            StringAssert.Contains(Describe(headers), "Informational responses are not independently cacheable.");
            headers.StatusCode = 304;
            StringAssert.Contains(Describe(headers), "304 validates an existing cached representation");
        }

        [TestMethod]
        public void Inspector_TabDisplaysExchangeAndBareHeadersAndClearsPreviousAnalysis()
        {
            ExceptionDispatchInfo failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var tab = new TabPage())
                    {
                        var inspector = new PerformanceResponseInspector();
                        inspector.AddToTab(tab);
                        Assert.AreEqual(1, tab.Controls.Count);
                        var view = (PerformanceView)tab.Controls[0];
                        var text = (RichTextBox)view.Controls[0];
                        Assert.IsTrue(text.ReadOnly);
                        Assert.AreEqual(DockStyle.Fill, view.Dock);
                        var headers = Response("max-age=60");
                        var exchange = new Exchange(Request("http"), Array.Empty<byte>())
                        {
                            ResponseHeaders = headers,
                            ResponseBody = new byte[] { 1 }
                        };
                        exchange.Timers.ProxyDoneRequest = CaptureTime;
                        exchange.Timers.ServerBeginResponse = CaptureTime.AddSeconds(1);
                        exchange.Timers.ServerDoneResponse = CaptureTime.AddSeconds(6);
                        inspector.AssignExchange(exchange);
                        StringAssert.Contains(text.Text, "Time to First Byte (TTFB): 1,000 ms - WARNING");
                        StringAssert.Contains(text.Text, "Time to Last Byte (TTLB): 6,000 ms - WARNING");
                        inspector.SetFontSize(12);
                        Assert.AreEqual(12f, text.Font.Size);
                        inspector.Assign(Response("no-store"), Array.Empty<byte>(), false);
                        StringAssert.Contains(text.Text, "Storage prohibited by no-store.");
                        StringAssert.Contains(text.Text, "Unavailable (missing or incomplete capture timers).");
                        inspector.Clear();
                        Assert.AreEqual(string.Empty, text.Text);
                        inspector.AssignExchange(null);
                        Assert.AreEqual("No response is available.", text.Text);
                    }
                }
                catch (Exception exception)
                {
                    failure = ExceptionDispatchInfo.Capture(exception);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            failure?.Throw();
        }
    }
}
