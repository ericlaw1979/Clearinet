using Clearinet.CaptureEngine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
            Assert.IsTrue(first.pipeIsReusable);
            Assert.AreEqual(204, second.responseHeaders.StatusCode);
            Assert.AreEqual(0, second.responseBody.Length);
        }

        [TestMethod]
        public async Task ReadResponseAsync_UpgradePreservesBufferedFramesAndDisablesReuse()
        {
            byte[] headers = Encoding.ASCII.GetBytes(
                "HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: websocket\r\n\r\n");
            byte[] frame = { 0x82, 0x04, 0x00, 0xFF, 0x0D, 0x0A };
            using (var stream = new MemoryStream())
            {
                stream.Write(headers, 0, headers.Length);
                stream.Write(frame, 0, frame.Length);
                stream.Position = 0;
                var reader = new HttpStreamReader(stream);

                var response = await reader.ReadResponseAsync("GET", CancellationToken.None);
                Assert.AreEqual(101, response.responseHeaders.StatusCode);
                Assert.AreEqual(0, response.responseBody.Length);
                Assert.IsFalse(response.pipeIsReusable);
                using (var received = new MemoryStream())
                {
                    await reader.DetachUnreadStream().CopyToAsync(received);
                    CollectionAssert.AreEqual(frame, received.ToArray());
                }
            }
        }

        [TestMethod]
        public async Task ReadRequestAsync_UpgradePreservesBufferedClientFrames()
        {
            byte[] request = Encoding.ASCII.GetBytes(
                "GET /socket HTTP/1.1\r\nHost: example.test\r\nConnection: Upgrade\r\nUpgrade: websocket\r\n\r\n");
            byte[] frame = { 0x82, 0x84, 0x01, 0x02, 0x03, 0x04, 0xFF, 0x00, 0x0D, 0x0A };
            using (var stream = new MemoryStream())
            {
                stream.Write(request, 0, request.Length);
                stream.Write(frame, 0, frame.Length);
                stream.Position = 0;
                var reader = new HttpStreamReader(stream);
                var parsed = await reader.ReadRequestAsync(CancellationToken.None);
                Assert.AreEqual("/socket", parsed.requestHeaders.RequestPath);
                Assert.AreEqual(0, parsed.requestBody.Length);
                using (var received = new MemoryStream())
                {
                    await reader.DetachUnreadStream().CopyToAsync(received);
                    CollectionAssert.AreEqual(frame, received.ToArray());
                }
            }
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
