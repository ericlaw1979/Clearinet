using System.Collections.Generic;
using Clearinet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CaptureEngine.Tests
{
    [TestClass]
    public sealed class HostListTests
    {
        [TestMethod]
        public void Contains_ExactRule_MatchesCaseInsensitive()
        {
            var list = new HostList("fiddler2.com");

            Assert.IsTrue(list.Contains("Fiddler2.com"));
            Assert.IsTrue(list.Contains("fiddler2.com."));
            Assert.IsFalse(list.Contains("www.fiddler2.com"));
        }

        [TestMethod]
        public void Contains_WildcardAndSemicolonRules_MatchesAnyRule()
        {
            var list = new HostList("*example.com; fiddler2.com");

            Assert.IsTrue(list.Contains("example.com"));
            Assert.IsTrue(list.Contains("sub.example.com"));
            Assert.IsTrue(list.Contains("fiddler2.com"));
            Assert.IsFalse(list.Contains("contoso.com"));
        }

        [TestMethod]
        public void Contains_LocalToken_MatchesHostsWithoutDots()
        {
            var list = new HostList("<local>");

            Assert.IsTrue(list.Contains("localhost"));
            Assert.IsTrue(list.Contains("printer"));
            Assert.IsFalse(list.Contains("example.com"));
        }

        [TestMethod]
        public void Contains_RuleCollectionConstructor_ParsesAndMatches()
        {
            var list = new HostList(new List<string> { "", "  <local> ", "*.test" });

            Assert.IsTrue(list.Contains("intranet"));
            Assert.IsTrue(list.Contains("a.test"));
            Assert.IsFalse(list.Contains("a.example"));
        }
    }
}
