using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Clearinet.CaptureEngine;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CaptureEngine.Tests
{
    [TestClass]
    public sealed class HttpStreamReaderTests
    {
        [TestMethod]
        public async Task ReadRequestAsync_PreservesPipelinedRequestAndFramesBody()
        {
            const string requests =
                "POST /first HTTP/1.1\r\nHost: example.test\r\nContent-Length: 3\r\n\r\none" +
                "GET /second HTTP/1.1\r\nHost: example.test\r\n\r\n";
            var reader = new HttpStreamReader(new MemoryStream(Encoding.ASCII.GetBytes(requests)));

            var first = await reader.ReadRequestAsync(CancellationToken.None);
            var second = await reader.ReadRequestAsync(CancellationToken.None);

            Assert.AreEqual("/first", first.requestHeaders.RequestPath);
            Assert.AreEqual("one", Encoding.ASCII.GetString(first.requestBody));
            Assert.AreEqual("/second", second.requestHeaders.RequestPath);
            Assert.AreEqual(0, second.requestBody.Length);
        }

        [TestMethod]
        public async Task ReadResponseAsync_UsesContentLengthAndLeavesNextResponseBuffered()
        {
            const string responses =
                "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nOK" +
                "HTTP/1.1 204 No Content\r\n\r\n";
            var reader = new HttpStreamReader(new MemoryStream(Encoding.ASCII.GetBytes(responses)));

            var first = await reader.ReadResponseAsync("GET", CancellationToken.None);
            var second = await reader.ReadResponseAsync("GET", CancellationToken.None);

            Assert.AreEqual(200, first.responseHeaders.StatusCode);
            Assert.AreEqual("OK", Encoding.ASCII.GetString(first.responseBody));
            Assert.IsTrue(first.reusable);
            Assert.AreEqual(204, second.responseHeaders.StatusCode);
            Assert.AreEqual(0, second.responseBody.Length);
        }

        [TestMethod]
        public async Task ReadRequestAsync_RejectsConflictingFramingHeaders()
        {
            const string request =
                "POST / HTTP/1.1\r\nHost: example.test\r\nTransfer-Encoding: chunked\r\nContent-Length: 0\r\n\r\n";
            var reader = new HttpStreamReader(new MemoryStream(Encoding.ASCII.GetBytes(request)));

            await Assert.ThrowsExceptionAsync<InvalidDataException>(
                () => reader.ReadRequestAsync(CancellationToken.None));
        }

        [TestMethod]
        public async Task ReadRequestAsync_PreservesChunkedBodyAndTrailers()
        {
            const string requests =
                "POST /chunked HTTP/1.1\r\nHost: example.test\r\nTransfer-Encoding: chunked\r\n\r\n" +
                "4\r\nWiki\r\n0\r\nX-Trace: yes\r\n\r\n" +
                "GET /after HTTP/1.1\r\nHost: example.test\r\n\r\n";
            var reader = new HttpStreamReader(new MemoryStream(Encoding.ASCII.GetBytes(requests)));

            var first = await reader.ReadRequestAsync(CancellationToken.None);
            var second = await reader.ReadRequestAsync(CancellationToken.None);

            Assert.AreEqual("4\r\nWiki\r\n0\r\nX-Trace: yes\r\n\r\n", Encoding.ASCII.GetString(first.requestBody));
            Assert.AreEqual("/after", second.requestHeaders.RequestPath);
        }
    }
}
