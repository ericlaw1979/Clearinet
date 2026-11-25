using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Clearinet;
using Clearinet.CaptureEngine;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CaptureEngine.Tests
{
    [TestClass]
    [DoNotParallelize]
    public sealed class HostRemappingTests
    {
        [TestMethod]
        public async Task BuiltInRules_RespectEnabledPreference()
        {
            const string enabledPref = "app.host_remapping.enabled";
            const string rulesPref = "app.host_remapping.rules";
            string previousEnabled = CApp.Prefs[enabledPref];
            string previousRules = CApp.Prefs[rulesPref];
            var listener = new TcpCaptureEngineListener(new CaptureEngineOptions(IPAddress.Loopback, 8888), new MapSocketToProcess());
            try
            {
                CApp.Prefs.SetStringPref(rulesPref, "127.0.0.1 example.test");
                CApp.Prefs.SetBoolPref(enabledPref, false);
                var disabled = CreateExchange("example.test", "/");
                await listener.RunBuiltInRulesBeforeRequest(disabled, Stream.Null, CancellationToken.None);
                Assert.IsNull(disabled["x-overridehost"]);

                CApp.Prefs.SetBoolPref(enabledPref, true);
                var enabled = CreateExchange("example.test", "/");
                await listener.RunBuiltInRulesBeforeRequest(enabled, Stream.Null, CancellationToken.None);
                Assert.AreEqual("127.0.0.1", enabled["x-overridehost"]);
                Assert.AreEqual("example.test", enabled.hostname);
            }
            finally
            {
                if (previousEnabled == null) CApp.Prefs.RemovePref(enabledPref);
                else CApp.Prefs.SetStringPref(enabledPref, previousEnabled);
                if (previousRules == null) CApp.Prefs.RemovePref(rulesPref);
                else CApp.Prefs.SetStringPref(rulesPref, previousRules);
                listener.Dispose();
            }
        }

        [TestMethod]
        public void ApplyHostRemapping_ParsesLinesAndUsesFirstExactCaseInsensitiveMatch()
        {
            var exchange = CreateExchange("ExAmPlE.test:1234", "/");
            TcpCaptureEngineListener.ApplyHostRemapping(exchange,
                " # comment\r\n\r\nincomplete\nignored sub.example.test\r\n127.0.0.1\tEXAMPLE.TEST # comment\n127.0.0.2 example.test");

            Assert.AreEqual("127.0.0.1", exchange["x-overridehost"]);
            Assert.AreEqual(1, exchange.oFlags.Count);
            Assert.AreEqual("ExAmPlE.test:1234", exchange.host);
        }

        [TestMethod]
        public void ApplyHostRemapping_NoMatchLeavesExistingOverrideUnchanged()
        {
            var exchange = CreateExchange("sub.example.test", "/");
            exchange["x-overridehost"] = "existing.test";
            TcpCaptureEngineListener.ApplyHostRemapping(exchange, "# ignored sub.example.test\n127.0.0.1 example.test\n127.0.0.2 other.test sub.example.test");

            Assert.AreEqual("existing.test", exchange["x-overridehost"]);
            Assert.AreEqual(1, exchange.oFlags.Count);
        }

        [TestMethod]
        public void ApplyHostRemapping_MatchOnlyChangesOverrideFlag()
        {
            var exchange = CreateExchange("example.test:1234", "/path?q=1");
            exchange["x-overridehost"] = "old.test";
            exchange["other-flag"] = "unchanged";
            string headers = exchange.RequestHeaders.ToString();

            TcpCaptureEngineListener.ApplyHostRemapping(exchange, "target.test:8080 example.test");

            Assert.AreEqual("target.test:8080", exchange["x-overridehost"]);
            Assert.AreEqual("unchanged", exchange["other-flag"]);
            Assert.AreEqual(2, exchange.oFlags.Count);
            Assert.AreEqual(headers, exchange.RequestHeaders.ToString());
        }

        private static Exchange CreateExchange(string authority, string path)
        {
            return new Exchange(new HTTPRequestHeaders(path, new[] { "Host: " + authority }), Array.Empty<byte>());
        }
    }
}
