using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
/*
 * Previously documented at https://fiddler.wikidot.com/saz-files
 * 
 * SAZ files are simply specially formatted .ZIP files. If you rename a .SAZ file to .ZIP, you can open it for viewing using standard ZIP viewing tools.
 * Inside a SAZ file, you will find:
 * 
 * _index.htm - an optional file containing a human readable version of the Session List. This file is not processed when loading a .SAZ file and exists solely for manual examination.
 * [Content_Types.xml] — (Added in v2.4.0.9) A metadata file which specifies a few MIME types so the archive can be read by System.IO.Packaging or other clients that 
 * support the Open Packaging Conventions.
 * 
 * a /raw/ folder
 *
 * Inside the Raw folder, there will be three or four files for each web exchange:
 *
 * exchangeid#_c.txt - contains the raw client request.
 * exchangeid#_s.txt - contains the raw server request.
 * exchangeid#_m.xml - contains metadata including flags, socket reuse information, etc.
 * exchangeid#_w.txt - (optional) contains WebSocket messages.
 * 
 * The SAZ/ZIP file's comment field contains information about the app that generated the archive.
 *
 *
 *
 * SAZ files should always be forward/backward compatible, although some features may be missing (e.g. socket reuse information) 
 * when loading older files into the newest versions of readers.
 * 
 * SAZ files use the MIME type: application/vnd.telerik-fiddler.SessionArchive
 * 
 */
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
    internal class SAZFile : IDisposable
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
        }
        private string _sFilename;

        /// <summary>
        /// The ZIP-file comment stored in the SAZ file.
        /// Often used to write the version info of the generator.
        /// </summary>
        public string sComment
        {
            get => _sComment ?? string.Empty;
            private set => _sComment = value;
        }
        private string _sComment;

        /// <summary>
        /// Set from either the function or by calling SupplyPassword.
        /// </summary>
        private string _sPassword;

        private static List<ZipEntry> GetExchangeRequests(ZipFile zf)
        {
            var listRequests = new List<ZipEntry>();
            foreach (ZipEntry entry in zf.Entries)
            {
                // Skip non-Exchange Client Request files.
                if (!entry.FileName.OICStartsWith("raw/") || !entry.FileName.OICEndsWith("_c.txt")) continue;
                listRequests.Add(entry);
            }
            return listRequests;
        }

        internal static SAZFile LoadFrom(string filePath, string sPassword = null)
        {
            SAZFile sazFile = new SAZFile
            {
                sFilename = filePath,
                _sPassword = sPassword
            };

            using (ZipFile zf = ZipFile.Read(filePath))
            {
                // Comment on the ZIP file, not encrypted.
                sazFile.sComment = zf.Comment;

                // Set the password for the ZIP file. If the password is wrong,
                // DotNetZip will throw an exception when we try to extract a file.
                zf.Password = sazFile._sPassword;

                List<ZipEntry> listRequests = GetExchangeRequests(zf);
                if (listRequests.Count < 1) throw new Exception("The selected file does not contain any Exchanges.");

                foreach (ZipEntry eRequest in listRequests)
                {
                    // We need to handle the case where the file is encrypted.
                    if (eRequest.UsesEncryption && !sazFile._sPassword.HasText())
                    {
                        sazFile._sPassword = SupplyPassword?.Invoke();
                        if (sazFile._sPassword == null) throw new Exception("Password required to open SAZ file.");
                        zf.Password = sazFile._sPassword;
                    }

                    Stream strmContent = null;
                RetryPassword:
                    try
                    {
                        strmContent = eRequest.OpenReader();
                    }
                    catch (Ionic.Zip.BadPasswordException)
                    {
                        sazFile._sPassword = SupplyPassword?.Invoke();
                        if (sazFile._sPassword == null) throw new Exception("Password required to open SAZ file.");
                        goto RetryPassword;
                    }
                    catch (Exception eX)
                    {
                        CApp.ReportException(eX, "SAZ Read Failed");
                    }

                    Exchange excNew = CreateExchangeFromStream(strmContent);
                    strmContent.Dispose();

                    ZipEntry eResponse = zf[eRequest.FileName.TrimAfter("_") + "_s.txt"];
                    if (null != eResponse)
                    {
                        strmContent = eResponse.OpenReader();
                        AddResponseFromStream(excNew, strmContent);
                        strmContent.Dispose();
                    }

                    ZipEntry eMetadata = zf[eRequest.FileName.TrimAfter("_") + "_m.xml"];
                    if (null != eMetadata)
                    {
                        strmContent = eMetadata.OpenReader();
                        AddMetadataFromStream(excNew, strmContent);
                        strmContent.Dispose();
                    }

                    // TODO: WebSocket data

                    sazFile.Exchanges.Add(excNew);
                }
            }
            return sazFile;
        }

        private static void AddMetadataFromStream(Exchange exchNew, Stream strmContent)
        {
            ExchangeFlags sfInferredFlags = ExchangeFlags.None;
            string sOriginalID = null;

            try
            {
                using (XmlReader oXML = XmlReader.Create(strmContent, new XmlReaderSettings { IgnoreWhitespace = true }))
                {
                    while (oXML.Read())
                    {
                        if (oXML.NodeType != XmlNodeType.Element) continue;

                        switch (oXML.Name)
                        {
                            case "Session":
                                // Simplified null check using pattern matching
                                if (oXML.GetAttribute("Aborted") is string)
                                {
                                    exchNew.state = ExchangeState.Aborted;
                                }

                                if (uint.TryParse(oXML.GetAttribute("BitFlags"), NumberStyles.HexNumber, null, out var bitFlags))
                                {
                                    exchNew.BitFlags = (ExchangeFlags)bitFlags;
                                }

                                if (oXML.GetAttribute("SID") is string sid)
                                {
                                    sOriginalID = sid;
                                }
                                break;

                            case "SessionFlag":
                                exchNew.oFlags.TryAdd(oXML.GetAttribute("N"), oXML.GetAttribute("V"));
                                break;

                            case "SessionTimers":
                                exchNew.Timers.ClientConnected = XmlConvert.ToDateTime(oXML.GetAttribute("ClientConnected"), XmlDateTimeSerializationMode.RoundtripKind);

                                // Local function to simplify repetitive attribute parsing
                                void ParseAndSetDateTime(string attributeName, Action<DateTime> setter)
                                {
                                    var value = oXML.GetAttribute(attributeName);
                                    if (value != null)
                                    {
                                        setter(XmlConvert.ToDateTime(value, XmlDateTimeSerializationMode.RoundtripKind));
                                    }
                                }

                                void ParseAndSetInt(string attributeName, Action<int> setter)
                                {
                                    var value = oXML.GetAttribute(attributeName);
                                    if (value != null)
                                    {
                                        setter(XmlConvert.ToInt32(value));
                                    }
                                }

                                ParseAndSetDateTime("ClientBeginRequest", dt => exchNew.Timers.ClientBeginRequest = dt);
                                ParseAndSetDateTime("GotRequestHeaders", dt => exchNew.Timers.ClearinetGotRequestHeaders = dt);
                                exchNew.Timers.ClientDoneRequest = XmlConvert.ToDateTime(oXML.GetAttribute("ClientDoneRequest"), XmlDateTimeSerializationMode.RoundtripKind);

                                ParseAndSetInt("GatewayTime", val => exchNew.Timers.GatewayDeterminationTime = val);
                                ParseAndSetInt("DNSTime", val => exchNew.Timers.DNSTime = val);
                                ParseAndSetInt("TCPConnectTime", val => exchNew.Timers.TCPConnectTime = val);
                                ParseAndSetInt("HTTPSHandshakeTime", val => exchNew.Timers.HTTPSHandshakeTime = val);

                                ParseAndSetDateTime("ServerConnected", dt => exchNew.Timers.ServerConnected = dt);
                                ParseAndSetDateTime("FiddlerBeginRequest", dt => exchNew.Timers.ClearinetBeginRequest = dt);
                                exchNew.Timers.ClearinetBeginRequest = XmlConvert.ToDateTime(oXML.GetAttribute("ServerGotRequest"), XmlDateTimeSerializationMode.RoundtripKind);
                                ParseAndSetDateTime("ServerBeginResponse", dt => exchNew.Timers.ServerBeginResponse = dt);
                                ParseAndSetDateTime("GotResponseHeaders", dt => exchNew.Timers.ClearinetGotResponseHeaders = dt);

                                exchNew.Timers.ServerDoneResponse = XmlConvert.ToDateTime(oXML.GetAttribute("ServerDoneResponse"), XmlDateTimeSerializationMode.RoundtripKind);
                                exchNew.Timers.ClientBeginResponse = XmlConvert.ToDateTime(oXML.GetAttribute("ClientBeginResponse"), XmlDateTimeSerializationMode.RoundtripKind);
                                exchNew.Timers.ClientDoneResponse = XmlConvert.ToDateTime(oXML.GetAttribute("ClientDoneResponse"), XmlDateTimeSerializationMode.RoundtripKind);
                                break;

                            case "TunnelInfo":
                                // Out variables directly declared inside long.TryParse
                                if (long.TryParse(oXML.GetAttribute("BytesEgress"), out var lngBytesEgress) &&
                                    long.TryParse(oXML.GetAttribute("BytesIngress"), out var lngBytesIngress))
                                {
                                    // __oTunnel = new MockTunnel(lngBytesEgress, lngBytesIngress);
                                }
                                break;

                            case "PipeInfo":
                                if ("true" != oXML.GetAttribute("Streamed"))
                                {
                                    sfInferredFlags |= ExchangeFlags.ResponseStreamed;
                                }

                                if ("true" == oXML.GetAttribute("CltReuse"))
                                {
                                    sfInferredFlags |= ExchangeFlags.ClientPipeReused;
                                }

                                if ("true" == oXML.GetAttribute("Reused"))
                                {
                                    sfInferredFlags |= ExchangeFlags.ServerPipeReused;
                                }

                                //TODO:Gatewayinfo
                                break;
                        }
                    }

                    if (exchNew.BitFlags == ExchangeFlags.None) exchNew.BitFlags = sfInferredFlags;
                }
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Metadata load failed");
                return;
            }
        }

        private static void AddResponseFromStream(Exchange e, Stream strmContent)
        {
            var (sHeaderBlock, body) = Parser.CrackHttpMessage(strmContent);
            var (version, statusCode, statusText, headerList) = Parser.ParseResponseHeaders(sHeaderBlock);
            HTTPResponseHeaders resph = new HTTPResponseHeaders(statusCode, statusText, headerList);
            resph.HTTPVersion = version;
            e.ResponseHeaders = resph;
            e.ResponseBody = body;
        }

        private static Exchange CreateExchangeFromStream(Stream strmContent)
        {
            var (sHeaderBlock, body) = Parser.CrackHttpMessage(strmContent);
            var (method, path, version, headerList) = Parser.ParseRequestHeaders(sHeaderBlock);
            HTTPRequestHeaders rqh = new HTTPRequestHeaders(path, headerList);
            rqh.HTTPVersion = version;
            rqh.HTTPMethod = method;
            Exchange e = new Exchange(rqh, body);
            e.EnsureID();
            e.SetBitFlag(ExchangeFlags.LoadedFromSAZ);
            return e;
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
