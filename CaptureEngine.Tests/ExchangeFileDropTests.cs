using Clearinet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;

namespace CaptureEngine.Tests
{
    [TestClass]
    [DoNotParallelize]
    public sealed class ExchangeFileDropTests
    {
        [TestMethod]
        public void RunExchangeDrag_TracksOnlyTheSourceAndRestoresState()
        {
            using (var source = new ExchangeListView())
            using (var other = new ExchangeListView())
            {
                Assert.IsFalse(source.IsDraggingExchanges);
                Assert.IsFalse(other.IsDraggingExchanges);
                source.RunExchangeDrag(() =>
                {
                    Assert.IsTrue(source.IsDraggingExchanges);
                    Assert.IsFalse(other.IsDraggingExchanges);
                    source.RunExchangeDrag(() => Assert.IsTrue(source.IsDraggingExchanges));
                    Assert.IsTrue(source.IsDraggingExchanges);
                });
                Assert.IsFalse(source.IsDraggingExchanges);

                Assert.ThrowsException<InvalidOperationException>(() => source.RunExchangeDrag(() =>
                {
                    Assert.IsTrue(source.IsDraggingExchanges);
                    throw new InvalidOperationException("Drag failed");
                }));
                Assert.IsFalse(source.IsDraggingExchanges);
                Assert.IsFalse(other.IsDraggingExchanges);
            }
        }

        [TestMethod]
        public void CreateDataObject_StagesOnlySuccessfulDecodedResponsesAndPreservesExchangeFormat()
        {
            string root = Path.Combine(Path.GetTempPath(), "Clearinet-FileDropTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                byte[] expected = Encoding.UTF8.GetBytes("decoded response");
                byte[] compressed;
                using (var output = new MemoryStream())
                {
                    using (var gzip = new GZipStream(output, CompressionMode.Compress, true))
                    {
                        gzip.Write(expected, 0, expected.Length);
                    }
                    compressed = output.ToArray();
                }

                var exchanges = new[]
                {
                    CreateExchange(200, "/file.txt", compressed, "Content-Encoding: gzip"),
                    CreateExchange(404, "/missing.txt", Encoding.UTF8.GetBytes("not found")),
                    CreateExchange(200, "/FILE.txt", Encoding.ASCII.GetBytes("3\r\nabc\r\n0\r\n\r\n"), "Transfer-Encoding: chunked"),
                    CreateExchange(200, "/empty.bin", Array.Empty<byte>()),
                    CreateExchange(206, "/partial.bin", new byte[] { 1 })
                };
                var data = ExchangeFileDrop.CreateDataObject(exchanges, root);
                Assert.AreSame(exchanges, data.GetData("Clearinet.Exchange[]"));
                Assert.IsTrue(data.GetDataPresent(DataFormats.FileDrop));
                Assert.IsTrue(data.GetDataPresent(DataFormats.FileDrop, false));
                CollectionAssert.Contains(data.GetFormats(), DataFormats.FileDrop);
                CollectionAssert.Contains(data.GetFormats(false), DataFormats.FileDrop);
                Assert.AreEqual(0, Directory.GetFileSystemEntries(root).Length);
                var paths = (string[])data.GetData(DataFormats.FileDrop);
                Assert.AreSame(paths, data.GetData(DataFormats.FileDrop, false));
                Assert.AreEqual(1, Directory.GetDirectories(root).Length);
                Assert.AreEqual(3, paths.Length);
                Assert.AreEqual("file.txt", Path.GetFileName(paths[0]));
                Assert.AreEqual("FILE (2).txt", Path.GetFileName(paths[1]));
                Assert.AreEqual("empty.bin", Path.GetFileName(paths[2]));
                CollectionAssert.AreEqual(expected, File.ReadAllBytes(paths[0]));
                CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("abc"), File.ReadAllBytes(paths[1]));
                Assert.AreEqual(0L, new FileInfo(paths[2]).Length);
                CollectionAssert.AreEqual(compressed, exchanges[0].ResponseBody);

                var format = new FORMATETC
                {
                    cfFormat = (short)DataFormats.GetFormat(DataFormats.FileDrop).Id,
                    dwAspect = DVASPECT.DVASPECT_CONTENT,
                    lindex = -1,
                    tymed = TYMED.TYMED_HGLOBAL
                };
                ((System.Runtime.InteropServices.ComTypes.IDataObject)data).GetData(ref format, out STGMEDIUM medium);
                try
                {
                    Assert.AreEqual(TYMED.TYMED_HGLOBAL, medium.tymed);
                    Assert.AreEqual((uint)paths.Length, DragQueryFile(medium.unionmember, uint.MaxValue, null, 0));
                    for (uint i = 0; i < paths.Length; i++)
                    {
                        var actual = new StringBuilder(1024);
                        Assert.IsTrue(DragQueryFile(medium.unionmember, i, actual, (uint)actual.Capacity) > 0);
                        Assert.AreEqual(paths[i], actual.ToString());
                    }
                }
                finally
                {
                    ReleaseStgMedium(ref medium);
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void CreateDataObject_RegistersStagingDirectoryForExitCleanup()
        {
            string root = Path.Combine(Path.GetTempPath(), "Clearinet-FileDropTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var data = ExchangeFileDrop.CreateDataObject(new[]
                {
                    CreateExchange(200, "/response.txt", new byte[] { 1, 2, 3 })
                }, root);
                var paths = (string[])data.GetData(DataFormats.FileDrop);
                string stagingDirectory = Path.GetDirectoryName(paths[0]);
                Assert.IsTrue(File.Exists(paths[0]));

                Utilities.CleanupTempDirectories();

                Assert.IsFalse(Directory.Exists(stagingDirectory));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void CreateDataObject_NoSuccessfulResponses_DoesNotAdvertiseFilesOrCreateDirectory()
        {
            string root = Path.Combine(Path.GetTempPath(), "Clearinet-FileDropTest-" + Guid.NewGuid().ToString("N"));
            var exchanges = new[] { CreateExchange(404, "/missing.txt", Array.Empty<byte>()) };
            var data = ExchangeFileDrop.CreateDataObject(exchanges, root);
            Assert.AreSame(exchanges, data.GetData("Clearinet.Exchange[]"));
            Assert.IsFalse(data.GetDataPresent(DataFormats.FileDrop));
            Assert.IsFalse(Directory.Exists(root));
        }

        [TestMethod]
        public void CreateDataObject_DecodeFailure_RemovesPartiallyStagedFiles()
        {
            string root = Path.Combine(Path.GetTempPath(), "Clearinet-FileDropTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var exchanges = new[]
                {
                    CreateExchange(200, "/ok.txt", new byte[] { 1 }),
                    CreateExchange(200, "/bad.txt", new byte[] { 2 }, "Content-Encoding: unsupported")
                };
                int errors = 0;
                var data = ExchangeFileDrop.CreateDataObject(exchanges, root, ex =>
                {
                    Assert.IsInstanceOfType<InvalidDataException>(ex);
                    errors++;
                });
                Assert.AreEqual(0, Directory.GetFileSystemEntries(root).Length);
                Assert.ThrowsException<InvalidDataException>(() => data.GetData(DataFormats.FileDrop));
                Assert.AreEqual(0, Directory.GetFileSystemEntries(root).Length);
                Assert.ThrowsException<InvalidDataException>(() => data.GetData(DataFormats.FileDrop));
                Assert.AreEqual(0, Directory.GetFileSystemEntries(root).Length);
                Assert.AreEqual(1, errors);
                Assert.AreSame(exchanges, data.GetData("Clearinet.Exchange[]"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void CreateDataObject_OleQueriesDoNotStageUntilGetData()
        {
            string root = Path.Combine(Path.GetTempPath(), "Clearinet-FileDropTest-" + Guid.NewGuid().ToString("N"));
            try
            {
                var data = ExchangeFileDrop.CreateDataObject(new[]
                {
                    CreateExchange(200, "/response.txt", new byte[] { 1, 2, 3 })
                }, root);
                var oleData = (System.Runtime.InteropServices.ComTypes.IDataObject)data;
                var format = new FORMATETC
                {
                    cfFormat = (short)DataFormats.GetFormat(DataFormats.FileDrop).Id,
                    dwAspect = DVASPECT.DVASPECT_CONTENT,
                    lindex = -1,
                    tymed = TYMED.TYMED_HGLOBAL
                };
                Assert.AreEqual(0, oleData.QueryGetData(ref format));
                var formats = new FORMATETC[1];
                var enumerator = oleData.EnumFormatEtc(DATADIR.DATADIR_GET);
                bool found = false;
                while (enumerator.Next(1, formats, null) == 0)
                {
                    if (formats[0].cfFormat == format.cfFormat) found = true;
                }
                Assert.IsTrue(found);
                Assert.IsFalse(Directory.Exists(root));

                oleData.GetData(ref format, out STGMEDIUM medium);
                try
                {
                    Assert.AreEqual(TYMED.TYMED_HGLOBAL, medium.tymed);
                    Assert.AreEqual(1u, DragQueryFile(medium.unionmember, uint.MaxValue, null, 0));
                    var path = new StringBuilder(1024);
                    Assert.IsTrue(DragQueryFile(medium.unionmember, 0, path, (uint)path.Capacity) > 0);
                    CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path.ToString()));
                    var paths = (string[])data.GetData(DataFormats.FileDrop);
                    Assert.AreEqual(path.ToString(), paths[0]);
                    Assert.AreEqual(1, Directory.GetDirectories(root).Length);
                }
                finally
                {
                    ReleaseStgMedium(ref medium);
                }
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "DragQueryFileW")]
        private static extern uint DragQueryFile(IntPtr drop, uint index, StringBuilder filename, uint length);

        [DllImport("ole32.dll")]
        private static extern void ReleaseStgMedium(ref STGMEDIUM medium);

        private static Exchange CreateExchange(int status, string path, byte[] body, params string[] headers)
        {
            var request = new HTTPRequestHeaders(path, new[] { "Host: example.test" });
            var response = new HTTPResponseHeaders(status, "Test", headers);
            return Exchange.BuildFromData(true, request, null, response, body, ExchangeFlags.None);
        }
    }
}
