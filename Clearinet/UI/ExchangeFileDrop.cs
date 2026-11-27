using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace Clearinet
{
    /// <summary>
    /// Adds delayed Windows file-drop data to the Exchange drag payload by staging decoded
    /// HTTP/200 response bodies on demand. Internal targets can still use the Exchanges.
    /// </summary>
    internal static class ExchangeFileDrop
    {
        internal static DataObject CreateDataObject(Exchange[] exchanges, string tempRoot, Action<Exception> reportError = null)
        {
            return new DeferredFileDataObject(exchanges, tempRoot, reportError);
        }

        private sealed class DeferredFileDataObject : DataObject
        {
            private readonly Lazy<string[]> files;

            internal DeferredFileDataObject(Exchange[] exchanges, string tempRoot, Action<Exception> reportError)
                : base("Clearinet.Exchange[]", exchanges)
            {
                foreach (Exchange exchange in exchanges)
                {
                    if (exchange.responseCode != 200) continue;
                    SetData(DataFormats.FileDrop, Array.Empty<string>());
                    files = new Lazy<string[]>(() =>
                    {
                        try
                        {
                            return StageFiles(exchanges, tempRoot);
                        }
                        catch (Exception ex)
                        {
                            reportError?.Invoke(ex);
                            throw;
                        }
                    });
                    break;
                }
            }

            public override object GetData(string format, bool autoConvert)
            {
                // OLE uses this overload too; format queries must not materialize the files.
                if (format == DataFormats.FileDrop && files != null) return files.Value;
                return base.GetData(format, autoConvert);
            }
        }

        private static string[] StageFiles(Exchange[] exchanges, string tempRoot)
        {
            var files = new List<string>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string directory = null;

            try
            {
                foreach (Exchange xc in exchanges)
                {
                    if (xc.responseCode != 200) continue;

                    byte[] body = xc.GetResponseBodyAsDecodedBytes();
                    if (directory == null)
                    {
                        // Create the folder only when there is a file to offer. Keep it until exit
                        // because a drop target may read the paths after the drag completes.
                        string candidate = Path.Combine(tempRoot, "Clearinet-Drag-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                        if (Directory.Exists(candidate) || File.Exists(candidate))
                            throw new IOException("The temporary drag folder already exists: " + candidate);
                        Directory.CreateDirectory(candidate);
                        directory = candidate;
                        Utilities.AddToCleanupOnExit(directory);
                    }

                    string suggested = xc.SuggestedFilename;
                    string name = suggested;
                    string extension = Path.GetExtension(suggested);
                    string stem = suggested.Substring(0, suggested.Length - extension.Length);
                    // Resolve Windows case-insensitive collisions while preserving the 96-character
                    // filename limit and, where possible, the extension and complete Unicode characters.
                    for (int suffix = 2; !names.Add(name); suffix++)
                    {
                        string number = " (" + suffix + ")";
                        int maxStemLength = 96 - extension.Length - number.Length;
                        if (maxStemLength < 1)
                        {
                            extension = string.Empty;
                            stem = suggested;
                            maxStemLength = 96 - number.Length;
                        }
                        int length = Math.Min(stem.Length, maxStemLength);
                        if (length > 0 && char.IsHighSurrogate(stem[length - 1])) length--;
                        name = stem.Substring(0, length) + number + extension;
                    }

                    string path = Path.Combine(directory, name);
                    using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                    {
                        // Track the file before writing so a partial write can be cleaned up.
                        files.Add(path);
                        stream.Write(body, 0, body.Length);
                    }
                }

                return files.ToArray();
            }
            catch (Exception ex)
            {
                if (directory != null && Directory.Exists(directory))
                {
                    try
                    {
                        // Remove only files created by this drag, and remove the folder only if empty.
                        // TODO: We should probably wipe the whole folder without deleting individual files.
                        foreach (string file in files) File.Delete(file);
                        Directory.Delete(directory);
                    }
                    catch (Exception cleanupException)
                    {
                        throw new AggregateException("Failed to prepare response files and remove the temporary folder.", ex, cleanupException);
                    }
                }
                throw;
            }
        }
    }
}
