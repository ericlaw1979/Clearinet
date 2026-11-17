using Clearinet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace CaptureEngine.Tests
{
    [TestClass]
    [DoNotParallelize]
    public sealed class SAZFileTests
    {
        private const int OneMiB = 1024 * 1024;
        private const int SizeRadius = 1024;
        private static readonly byte[] Payload = CreatePayload();

        public static IEnumerable<object[]> ResponseEntryCases()
        {
            for (int offset = -SizeRadius; offset <= SizeRadius; offset++)
            {
                yield return new object[] { OneMiB + offset, false };
                yield return new object[] { OneMiB + offset, true };
            }
        }

        [TestMethod]
        [DynamicData(nameof(ResponseEntryCases), DynamicDataSourceType.Method)]
        public void SaveAndLoad_ResponseNearCompressionBufferBoundary_PreservesExchange(int responseEntrySize, bool compressionDisabled)
        {
            var requestHeaders = new HTTPRequestHeaders("/compression-boundary", new[] { "Host: example.test" });
            var responseHeaders = new HTTPResponseHeaders(200, "OK", new[]
            {
                "Content-Type: application/octet-stream",
                "Content-Length: 0000000"
            });

            // The ZIP entry includes HTTP headers. Reserve seven digits for Content-Length for these cases.
            var responseBody = new byte[responseEntrySize - responseHeaders.ToByteArray().Length];
            responseHeaders["Content-Length"] = responseBody.Length.ToString(CultureInfo.InvariantCulture);
            Array.Copy(Payload, responseBody, responseBody.Length);

            var exchange = Exchange.BuildFromData(true, requestHeaders, null, responseHeaders, responseBody, ExchangeFlags.None);
            var path = Path.Combine(Path.GetTempPath(), $"Clearinet-{Guid.NewGuid():N}.saz");
            const string DisableCompressionPref = "saz.compression.disabled";
            var previousCompressionPref = CApp.Prefs[DisableCompressionPref];
            try
            {
                CApp.Prefs.SetBoolPref(DisableCompressionPref, compressionDisabled);
                Assert.IsTrue(SAZFile.SaveTo(path, new[] { exchange }));

                using (var archive = new ZipArchive(File.OpenRead(path), ZipArchiveMode.Read))
                {
                    var entry = archive.GetEntry($"raw/{exchange.id}_s.txt");
                    Assert.IsNotNull(entry);
                    Assert.AreEqual((long)responseEntrySize, entry.Length, "The complete response entry must hit the target size.");
                    if (compressionDisabled)
                    {
                        Assert.AreEqual(entry.Length, entry.CompressedLength, "The response entry must be stored without compression.");
                    }
                    else
                    {
                        Assert.IsTrue(entry.CompressedLength < entry.Length, "The response entry must be compressed, not stored.");
                    }
                }

                using (var loaded = SAZFile.LoadFrom(path))
                {
                    Assert.AreEqual(1, loaded.Exchanges.Count);
                    var reloaded = loaded.Exchanges[0];
                    Assert.AreEqual(requestHeaders.ToString(), reloaded.RequestHeaders.ToString());
                    Assert.IsNotNull(reloaded.ResponseHeaders);
                    Assert.AreEqual(responseHeaders.ToString(), reloaded.ResponseHeaders.ToString());
                    CollectionAssert.AreEqual(responseBody, reloaded.ResponseBody, $"Response body changed for a {responseEntrySize}-byte ZIP entry.");
                }
            }
            finally
            {
                CApp.Prefs[DisableCompressionPref] = previousCompressionPref;
                File.Delete(path);
            }
        }

        private static byte[] CreatePayload()
        {
            // Keep the data varied but compressible so the ZIP writer doesn't fall back to storing it.
            var payload = new byte[OneMiB + SizeRadius];
            new Random(42).NextBytes(payload);
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] &= 0x0f;
            }

            return payload;
        }
    }
}
