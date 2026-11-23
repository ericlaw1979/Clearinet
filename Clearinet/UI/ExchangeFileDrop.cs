using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace Clearinet
{
    /// <summary>
    /// Adds ordinary Windows file-drop data to the Exchange drag payload by staging decoded
    /// HTTP/200 response bodies as temporary files. Internal targets can still use the Exchanges.
    /// </summary>
    internal static class ExchangeFileDrop
    {
        internal static DataObject CreateDataObject(Exchange[] exchanges, string tempRoot)
        {
            var data = new DataObject("Clearinet.Exchange[]", exchanges);
            var files = new List<string>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string directory = null;

            try
            {
                foreach (Exchange exchange in exchanges)
                {
                    if (exchange.responseCode != 200) continue;

                    byte[] body = exchange.GetResponseBodyAsDecodedBytes();
                    if (directory == null)
                    {
                        // Create the folder only when there is a file to offer. Keep it until exit
                        // because a drop target may read the paths after the drag completes.
                        string candidate = Path.Combine(tempRoot, "Clearinet-Drag-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                        if (Directory.Exists(candidate) || File.Exists(candidate))
                            throw new IOException("The temporary drag folder already exists: " + candidate);
                        Directory.CreateDirectory(candidate);
                        directory = candidate;
                        Utilities.CleanupOnExit(directory);
                    }

                    string suggested = exchange.SuggestedFilename;
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

                // FileDrop contains paths to real files; do not advertise it for an empty selection.
                if (files.Count > 0) data.SetData(DataFormats.FileDrop, files.ToArray());
                return data;
            }
            catch (Exception ex)
            {
                if (directory != null && Directory.Exists(directory))
                {
                    try
                    {
                        // Remove only files created by this drag, and remove the folder only if empty.
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
