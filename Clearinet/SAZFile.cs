using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using static System.Collections.Specialized.BitVector32;

namespace Clearinet
{
    /// <summary>
    /// TODO: Consider adding support for a better archive format which performs 
    /// inter-file compression. See discussion in https://textslashplain.com/2016/04/07/compression-context/
    /// 
    /// Clearinet's implementation supports only AES256 and not the 56bit PKZIP encryption.
    /// Clearinet currently uses DotNetZip; it does not currently support extensibility via an ISAZProvider interface.
    /// 
    /// </summary>
    internal class SAZFile: IDisposable
    {
        /// <summary>
        /// If set, this function is called to supply a password, either because one
        /// wasn't set or because it was incorrect. Return null to cancel the operation.
        /// </summary>
        public static Func<string> SupplyPassword { get; set; }

        /// <summary>
        /// Set of Exchanges inside this SAZ File
        /// </summary>
        public List<Exchange> Exchanges { get; private set; } = new List<Exchange>();

        /// <summary>
        /// e.g. "FileToLoad.saz"
        /// </summary>
        public string sFilename
        {
            get => _sFilename ?? string.Empty;
            private set => _sFilename = value;
        } private string _sFilename;

        /// <summary>
        /// The ZIP-file comment stored in the SAZ file.
        /// Often used to write the version info of the generator.
        /// </summary>
        public string sComment
        {
            get => _sComment ?? string.Empty;
            private set => _sComment = value;
        } private string _sComment;

        /// <summary>
        /// Set from either the function or by calling SupplyPassword.
        /// </summary>
        private string _sPassword;

        internal static SAZFile LoadFrom(string filePath, string sPassword=null)
        {
            SAZFile sazFile = new SAZFile();
            sazFile.sFilename = filePath;
            sazFile._sPassword = sPassword;

            using (ZipFile zf = ZipFile.Read(filePath))
            {
                // Comment on the ZIP file, not encrypted.
                sazFile.sComment = zf.Comment;

                // Set the password for the ZIP file. If the password is wrong,
                // DotNetZip will throw an exception when we try to extract a file.
                zf.Password = sazFile._sPassword;

                foreach (ZipEntry e in zf)
                {
                    // We need to handle the case where the file is encrypted.
                    if (e.UsesEncryption && sazFile._sPassword.HasText())
                    {
                        sazFile._sPassword = SupplyPassword?.Invoke();
                        if (sazFile._sPassword == null)
                        {
                            throw new Exception("Password required to open SAZ file.");
                        }
                        zf.Password = sazFile._sPassword;
                    }
                    // TODO: catch (Ionic.Zip.BadPasswordException) and reprompt for password.
                    if (e.FileName.StartsWith("raw/") && e.FileName.EndsWith(".xml"))
                    {
                        using (var ms = new MemoryStream())
                        {
                            e.Extract(ms);
/*                            ms.Position = 0;
                            var doc = new XmlDocument();
                            doc.Load(ms);
                            var exchange = Exchange.FromSAZXml(doc);*/
                            sazFile.Exchanges.Add(new Exchange(null, null));
                        }
                    }
                }
            }
            return sazFile;
        }

        internal static bool SaveTo(List<Exchange> exchanges, string filePath, string password = null, string comment = null)
        {
            ZipFile zf = new ZipFile();
            // Each Exchange writes at least 3 files. SAZ Files with over 21844 exchanges
            // need Zip64 because without it the number of files exceeds the 65535 file
            // limit of non-Zip64 format.
            zf.UseZip64WhenSaving = Zip64Option.AsNecessary;

            if (password.HasText())
            {
                zf.Password = password;
                zf.Encryption = EncryptionAlgorithm.WinZipAes256;
            }

            #region SetupFileStructure
            zf.Comment = comment ?? $"{CApp.VersionString} Session Archive. See https://clearinet.app";
            WriteOPCFile(zf);
            #endregion

            #region WriteExchanges
            // Folder that will hold each exchange.
            zf.AddDirectoryByName("raw");

            // foreach (Exchange exchange in exchanges){
            //    write _c.txt, _s.txt, and _m.xml files for each exchange into the raw folder.
            // }
            #endregion

            // TODO: Write _index.html containing the columns of the Exchanges list.

            zf.Save();
            zf.Dispose();
            return true;
        }

        /// <summary>
        /// By convention, we add an OPC manifest to allow Packaging APIs
        /// to read our SAZ files. This allows System.IO.Packaging to
        /// read the content without using an external ZIP library.
        /// http://en.wikipedia.org/wiki/Open_Packaging_Conventions
        /// </summary>
        private static void WriteOPCFile(ZipFile zf)
        {
            const string contentTypesXml = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
            <Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
              <Default Extension=""htm"" ContentType=""text/html"" />
              <Default Extension=""txt"" ContentType=""text/plain"" />
              <Default Extension=""xml"" ContentType=""application/xml"" />
            </Types>";

            zf.AddEntry("[Content_Types].xml", (_, strm) =>
            {
                byte[] bytes = Encoding.UTF8.GetBytes(contentTypesXml);
                strm.Write(bytes, 0, bytes.Length);
            });
        }

        void IDisposable.Dispose() { /*TODO Free anything?*/ }

    }
}
