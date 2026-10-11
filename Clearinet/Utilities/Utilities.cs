using BrotliSharpLib;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Windows.Forms;

namespace Clearinet
{
    public static class Utilities
    {
        [DllImport("Kernel32.dll", EntryPoint = "GetTickCount64", CharSet = CharSet.Unicode)]
        private static extern UInt64 GetTickCount64();

        // Legacy compat from before the .NET Framework had Array.Empty<T>().
        public static byte[] emptyByteArray = Array.Empty<byte>();
        public static UInt64 GetTickCount()
        {
            try
            {
                return GetTickCount64();
            }
            catch
            {
                return (UInt64)Environment.TickCount;
            }
        }

        #region Converters
        public static int HexToInt(char c)
        {
            if ((c >= '0') && (c <= '9')) return (c - '0');
            if ((c >= 'a') && (c <= 'f')) return ((c - 'a') + 10);
            if ((c >= 'A') && (c <= 'F')) return ((c - 'A') + 10);
            return -1;
        }
        public static string JSEncode(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder();

            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append(@"\\"); break;
                    case '\r': sb.Append(@"\r"); break;
                    case '\n': sb.Append(@"\n"); break;
                    case '"': sb.Append(@"\"""); break;
                    default:
                        if (c > 127)
                        {
                            sb.Append($"\\u{(int)c:X4}");
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }

            return sb.ToString();
        }

        public static string JSDecode(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            if (!s.Contains("\\")) return s;
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char nextChar = s[i + 1];
                    switch (nextChar)
                    {
                        case 'r': sb.Append('\r'); i++; break;
                        case 'n': sb.Append('\n'); i++; break;
                        case '"': sb.Append('"'); i++; break;
                        case '\\': sb.Append('\\'); i++; break;
                        case 'u':
                            if (i + 5 < s.Length)
                            {
                                string hex = s.Substring(i + 2, 4);
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int unicode))
                                {
                                    sb.Append((char)unicode);
                                    i += 5; // Skip the next 5 characters (\uXXXX)
                                }
                            }
                            break;
                        default:
                            sb.Append(nextChar);
                            i++;
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public static string HtmlDecode(string s)
        {
            return WebUtility.HtmlDecode(s);
        }

        public static string HtmlEncode(string s)
        {
            return WebUtility.HtmlEncode(s);
        }

        public static string UrlDecode(string s)
        {
            return UrlDecode(s, Encoding.UTF8);
        }

        public static string UrlDecode(string s, Encoding enc)
        {
            return HttpUtility.UrlDecode(s, enc);
        }

        public static string UrlEncode(string s, Encoding enc)
        {
            // TODO: Uri.EscapeDataString(s) has different behavior which will be
            // more correct in some cases. We probably ought to offer both options.
            return HttpUtility.UrlEncode(s, enc);
        }

        public static byte[] UrlDecodeToBytes(string s, Encoding enc)
        {
            return HttpUtility.UrlDecodeToBytes(s, enc);
        }

        public static byte[] _HashBytes(string sAlg, byte[] arrIn)
        {
            HashAlgorithm oHasher;
            switch (sAlg.ToLower())
            {
                case "md5":
                    oHasher = MD5.Create();
                    break;
                case "sha1":
                    oHasher = SHA1.Create();
                    break;
                case "sha256":
                    oHasher = SHA256.Create();
                    break;
                case "sha384":
                    oHasher = SHA384.Create();
                    break;
                case "sha512":
                    oHasher = SHA512.Create();
                    break;
                default:
                    throw new NotImplementedException("Unknown hash: " + sAlg);
            }
            byte[] result = oHasher.ComputeHash(arrIn);
            oHasher.Clear();
            return result;
        }

        public static string GetBase64Hash(string sAlg, byte[] arrIn)
        {
            return Convert.ToBase64String(_HashBytes(sAlg, arrIn));
        }
        public static string GetHash(string sAlg, byte[] arrIn)
        {
            return BitConverter.ToString(_HashBytes(sAlg, arrIn)); // .Replace("-", "").ToLower();
        }

        #endregion

        #region ByteDisplays
        public static string ByteArrayToString(byte[] arrIn)
        {
            if (null == arrIn) return "null";
            if (0 == arrIn.Length) return "empty";
            return BitConverter.ToString(arrIn).Replace('-', ' ');
        }

        public static string ByteArrayToHexView(byte[] inArr, int iStartAt, int iBytesPerLine, int iMaxByteCount, bool bShowASCII)
        {
            if (inArr is null || inArr.Length == 0) return string.Empty;
            if (iBytesPerLine < 1 || iMaxByteCount < 1) return string.Empty;
            if (iStartAt < 0 || iStartAt >= inArr.Length) return string.Empty;

            int iMaxOffset = Math.Min(iMaxByteCount + iStartAt, inArr.Length);
            int totalBytes = iMaxOffset - iStartAt;

            // Calculate precise buffer capacity to avoid re-allocations
            int lines = (int)Math.Ceiling((double)totalBytes / iBytesPerLine);
            int charsPerLine = (iBytesPerLine * 3) + (bShowASCII ? 1 + iBytesPerLine : 0) + 2; // 2 for \r\n
            var sbOutput = new StringBuilder(lines * charsPerLine);

            int iPtr = iStartAt;
            while (iPtr < iMaxOffset)
            {
                int iLineLen = Math.Min(iBytesPerLine, iMaxOffset - iPtr);

                // Write Hex representation
                for (int i = 0; i < iLineLen; i++)
                {
                    byte b = inArr[iPtr + i];
                    sbOutput.AppendHexByte(b);
                    sbOutput.Append(' ');
                }

                // Pad hex column if last line is short
                if (iLineLen < iBytesPerLine)
                {
                    sbOutput.Append(' ', 3 * (iBytesPerLine - iLineLen));
                }

                // Write ASCII representation
                if (bShowASCII)
                {
                    sbOutput.Append(' ');

                    for (int i = 0; i < iLineLen; i++)
                    {
                        byte b = inArr[iPtr + i];
                        // ASCII printable characters range from 32 (space) to 126 (~)
                        sbOutput.Append(b >= 32 && b <= 126 ? (char)b : '.');
                    }

                    if (iLineLen < iBytesPerLine)
                    {
                        sbOutput.Append(' ', iBytesPerLine - iLineLen);
                    }
                }

                sbOutput.AppendLine();
                iPtr += iBytesPerLine;
            }

            return sbOutput.ToString();
        }

        /// <summary>
        /// Fast hex byte formatting directly into StringBuilder without ToString("X2") allocation.
        /// </summary>
        private static void AppendHexByte(this StringBuilder sb, byte b)
        {
            const string hexMap = "0123456789ABCDEF";
            sb.Append(hexMap[b >> 4]);
            sb.Append(hexMap[b & 0x0F]);
        }

        #endregion

        public static void LaunchHyperlink(string sURL)
        {
            try
            {
                Process.Start(sURL);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to launch the requested hyperlink:\n\n{ex.Message}", "Clearinet", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static void LaunchNative(string sApp, string sParams)
        {
            try
            {
                Trace.WriteLine($"Launching: '{sApp}' with parameters: '{sParams}'");
                Process.Start(sApp, sParams);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to launch the requested application:\n\n{ex.Message}", "Clearinet", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static string UNSTABLE_DescribeClientHello(MemoryStream _)
        {
            return "TODO: don't call this! It's meant to parse a ClientHello to text.";
        }

        public static string UNSTABLE_DescribeServerHello(MemoryStream _)
        {
            return "TODO: don't call this!";
        }

        public static int GetRegistryInt(RegistryKey rk, string sName, int iDefault)
        {
            int retVal = iDefault;
            object o = rk.GetValue(sName);
            if (o is Int32) return (int)o;

            string strVal = (o as string);
            if (!String.IsNullOrEmpty(strVal))
            {
                if (!int.TryParse(strVal, out retVal)) return iDefault;
            }
            return retVal;
        }

        public static string ObtainSaveFilename(string sDialogTitle, string sFilter, string sInitialDirectory = null)
        {
            return ObtainSaveFilenameAndType(sDialogTitle, sFilter, sInitialDirectory).sFilename;
        }

        public static (string sFilename, int iChosenType) ObtainSaveFilenameAndType(string sDialogTitle, string sFilter, string sInitialDirectory = null)
        {
            string sFilename = null;
            int iChosenType = -1;

            using (FileDialog oFileDialog = new SaveFileDialog())
            {
                oFileDialog.Title = sDialogTitle;
                oFileDialog.Filter = sFilter;
                if (!String.IsNullOrEmpty(sInitialDirectory))
                {
                    oFileDialog.InitialDirectory = sInitialDirectory;
                    oFileDialog.RestoreDirectory = true;
                }

                if (DialogResult.OK == oFileDialog.ShowDialog(CApp.UI))
                {
                    sFilename = oFileDialog.FileName;
                    iChosenType = oFileDialog.FilterIndex;
                }
            }

            return (sFilename, iChosenType);
        }

        public static string ObtainOpenFilename(string sDialogTitle, string sFilter, string sInitialDirectory = null)
        {
            string sFilename = null;

            using (FileDialog oFileDialog = new OpenFileDialog())
            {
                oFileDialog.Title = sDialogTitle;
                oFileDialog.Filter = sFilter;
                oFileDialog.CheckFileExists = true;
                if (!String.IsNullOrEmpty(sInitialDirectory))
                {
                    oFileDialog.InitialDirectory = sInitialDirectory;
                    oFileDialog.RestoreDirectory = true;
                }

                if (DialogResult.OK == oFileDialog.ShowDialog(CApp.UI))
                {
                    sFilename = oFileDialog.FileName;
                }
            }

            return sFilename;
        }

        public static string[] Parameterize(string sInput)
        {
            return Parameterize(sInput, false);
        }

        public static byte[] Zstdexpand(byte[] arrData)
        {
            if (!arrData.HasData()) return Array.Empty<byte>();
            using (var msInput = new MemoryStream(arrData))
            using (var msOutput = new MemoryStream())
            {
                using (var zstd = new ZstdSharp.DecompressionStream(msInput))
                {
                    zstd.CopyTo(msOutput);
                }
                return msOutput.ToArray();
            }
        }

        // Note: System.IO.Compression.BrotliStream is only available in
        // .NET Standard 2.1 and later, so we use BrotliSharpLib.
        public static byte[] BrotliExpand(byte[] arrData)
        {
            if (!arrData.HasData()) return Array.Empty<byte>();
            using (var msInput = new MemoryStream(arrData))
            using (var msOutput = new MemoryStream())
            {
                using (var br = new BrotliStream(msInput, CompressionMode.Decompress))
                {
                    br.CopyTo(msOutput);
                }
                return msOutput.ToArray();
            }
        }

        public static byte[] GzipExpand(byte[] arrData)
        {
            if (!arrData.HasData()) return Array.Empty<byte>();
            using (var msInput = new MemoryStream(arrData))
            using (var msOutput = new MemoryStream())
            {
                using (var gzip = new GZipStream(msInput, CompressionMode.Decompress))
                {
                    gzip.CopyTo(msOutput);
                }
                return msOutput.ToArray();
            }
        }

        public static byte[] DeflaterCompress(byte[] arrData)
        {
            if (!arrData.HasData()) return Array.Empty<byte>();
            try
            {
                using (var destinationStream = new MemoryStream())
                {
                    using (var deflateStream = new DeflateStream(destinationStream, CompressionMode.Compress))
                    {
                        deflateStream.Write(arrData, 0, arrData.Length);
                    }

                    return destinationStream.ToArray();
                }
            }
            catch (Exception e)
            {
                CApp.DoNotifyUser($"The content could not be compressed.\n\n{e.Message}", "Deflate failed");
                return arrData;
            }
        }

        public static byte[] DeflaterExpand(byte[] arrDeflated, bool bThrowOnErrors)
        {
            try
            {
                return CompatibleDeflaterExpand(arrDeflated);
            }
            catch (Exception eX)
            {
                if (bThrowOnErrors)
                {
                    throw new InvalidDataException("The deflated data could not be decompressed.", eX);
                }
                CApp.DoNotifyUser($"The deflated data could not be decompressed\n\n{eX.Message}", "Inflate failed");
                return Array.Empty<byte>();
            }
        }
        public static byte[] CompatibleDeflaterExpand(byte[] arrDeflated)
        {
            if (!arrDeflated.HasData()) return Array.Empty<byte>();

            // An RFC1950 ZLIB wrapper around DEFLATE may be present.
            // http://www.faqs.org/rfcs/rfc1950.html
            // Check for the ZLIB header bytes and if present, skip over them.
            var ixStart = (arrDeflated.Length > 2 &&
                           (arrDeflated[0] & 0x0F) == 0x8 &&
                           (arrDeflated[0] & 0x80) == 0 &&
                           ((arrDeflated[0] << 8) + arrDeflated[1]) % 31 == 0) ? 2 : 0;

            using (var sourceStream = new MemoryStream(arrDeflated, ixStart, arrDeflated.Length - ixStart, writable: false))
            using (var destinationStream = new MemoryStream(arrDeflated.Length))
            {
                using (var deflate = new DeflateStream(sourceStream, CompressionMode.Decompress))
                {
                    deflate.CopyTo(destinationStream);
                }

                return destinationStream.ToArray();
            }
        }

        /// <summary>
        /// Tokenizes a string into an array of parameters, respecting quoted strings.
        /// Supports both single and double quotes if specified.
        /// </summary>
        /// <param name="sInput">The string to tokenize.</param>
        /// <param name="supportSingleQuotes">Allow single quotes be supported as wrappers.</param>
        /// <returns></returns>
        public static string[] Parameterize(string sInput, bool supportSingleQuotes)
        {
            if (string.IsNullOrWhiteSpace(sInput)) return Array.Empty<string>();

            var tokens = new List<string>();
            var inDoubleQuotes = false;
            var inSingleQuotes = false;
            var currentToken = new StringBuilder();

            for (var i = 0; i < sInput.Length; ++i)
            {
                var ch = sInput[i];
                switch (ch)
                {
                    case '\'':
                        if (!supportSingleQuotes || inDoubleQuotes)
                        {
                            currentToken.Append(ch);
                            continue;
                        }

                        if (i > 0 && sInput[i - 1] == '\\')
                        {
                            --currentToken.Length;
                            currentToken.Append('\'');
                            continue;
                        }

                        inSingleQuotes = !inSingleQuotes;
                        break;

                    case '"':
                        if (inSingleQuotes)
                        {
                            currentToken.Append(ch);
                            continue;
                        }

                        // Allow double-quoted string ending with a trailing \
                        if (i > 0 && sInput[i - 1] == '\\' && !(inDoubleQuotes && i == sInput.Length - 1))
                        {
                            --currentToken.Length;
                            currentToken.Append('"');
                            continue;
                        }

                        inDoubleQuotes = !inDoubleQuotes;
                        break;

                    case '\t':
                    case ' ':
                        if (!inDoubleQuotes && !inSingleQuotes)
                        {
                            // Found a token
                            // Allow "" to be an empty string
                            if (currentToken.Length > 0 || (i > 0 && '"' == sInput[i - 1]))
                            {
                                tokens.Add(currentToken.ToString());
                                currentToken.Clear();
                            }
                        }
                        else
                        {
                            currentToken.Append(ch);
                        }
                        break;

                    default:
                        currentToken.Append(ch);
                        break;
                }
            }

            if (currentToken.Length > 0)
            {
                tokens.Add(currentToken.ToString());
            }

            return tokens.ToArray();
        }

        internal static Color ParseColor(string sColor)
        {
            if (!sColor.HasText()) return Color.Empty;
            try
            {
                return ColorTranslator.FromHtml(sColor);
            }
            catch (Exception)
            {
                return Color.FromName(sColor);
            }
        }

        public static string GetExecutableOutput(string sApp, string sParams, out int iExitCode)
        {
            iExitCode = -999;
            var sbResult = new StringBuilder();
            sbResult.Append($"Results from {sApp} {sParams}\r\n\r\n");

            try
            {
                using (var oProc = new Process())
                {
                    oProc.StartInfo = new ProcessStartInfo
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = false,
                        CreateNoWindow = true,
                        FileName = sApp,
                        Arguments = sParams
                    };

                    oProc.Start();
                    while (oProc.StandardOutput.ReadLine() is string str)
                    {
                        str = str.TrimEnd();
                        if (str.Length > 0)
                        {
                            sbResult.AppendLine(str);
                        }
                    }

                    iExitCode = oProc.ExitCode;
                }
            }
            catch (Exception eX)
            {
                sbResult.Append($"Threw: {eX}\r\n{eX.StackTrace}");
            }

            sbResult.Append("------------------------------------\r\n");
            return sbResult.ToString();
        }
    }
}
