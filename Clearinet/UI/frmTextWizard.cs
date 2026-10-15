using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Clearinet
{
    internal partial class frmTextWizard : Form
    {
        private Encoding encodingInput;
        private Encoding encodingOutput;

        /// <summary>
        /// Some conversions result in non-textual output, so we keep the backing byte array if the
        /// output of the conversion will be passed to a context that requires the original bytes (e.g. saving to a file).
        /// </summary>
        byte[] arrLatestOutput = null;
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }

        private void Recalc()
        {
            string sText = txtInput.Text;
            string sOutput;
            arrLatestOutput = null;
            bool bThrew = false;
            try
            {
                switch (cbxTransforms.SelectedIndex)
                {
                    case -1:
                        sOutput = String.Empty;
                        break;
                    case 0:
                        sOutput = Convert.ToBase64String(encodingInput.GetBytes(sText));
                        break;
                    case 1:
                        sOutput = Convert.ToBase64String(encodingInput.GetBytes(sText));
                        sOutput = sOutput.TrimEnd('=').Replace('+', '-').Replace('/', '_');
                        break;
                    case 2:
                        sText = Regex.Replace(sText, @"\s+", String.Empty);
                        arrLatestOutput = Convert.FromBase64String(sText);
                        sOutput = encodingInput.GetString(arrLatestOutput);
                        break;
                    case 3:
                        sOutput = Utilities.UrlEncode(sText, encodingOutput);
                        break;
                    case 4: // Decoding URLs can result in unprintable characters, so we need to keep track of the byte array as well as the string.
                        arrLatestOutput = Utilities.UrlDecodeToBytes(sText, encodingInput);
                        sOutput = Utilities.UrlDecode(sText, encodingInput);
                        break;
                    case 5:
                        {
                            StringBuilder sbOutput = new StringBuilder();
                            byte[] arrOut = encodingOutput.GetBytes(sText);
                            foreach (byte b in arrOut)
                            {
                                sbOutput.AppendFormat("%{0:x2}", b);
                            }
                            sOutput = sbOutput.ToString();
                        }
                        break;
                    case 6:
                        {
                            sOutput = ParseBytes(sText);
                        }
                        break;
                    case 7: // C# array
                        {
                            StringBuilder sbOutput = new StringBuilder();
                            sbOutput.Append("byte[] arrBytes = {");
                            byte[] arrOut = encodingOutput.GetBytes(sText);
                            foreach (byte b in arrOut)
                            {
                                sbOutput.AppendFormat(" 0x{0:X2},", b);
                            }
                            // Trim the final trailing comma
                            if (arrOut.Length > 1) sbOutput.Length -= 1;
                            sbOutput.Append(" };");
                            sOutput = sbOutput.ToString();
                        }
                        break;
                    case 8:
                        sOutput = Utilities.JSEncode(sText);
                        break;
                    case 9:
                        sOutput = Utilities.JSDecode(sText);
                        break;
                    case 10:
                        sOutput = Utilities.HtmlEncode(sText);
                        break;
                    case 11:
                        sOutput = Utilities.HtmlDecode(sText);
                        break;
                    case 12:
                        sOutput = Encoding.ASCII.GetString(Encoding.UTF7.GetBytes('\uFEFF' + sText));
                        break;
                    case 13:
                        sOutput = Encoding.UTF7.GetString(encodingInput.GetBytes(sText));
                        break;
                    case 14: // DeflatedSAML
                        {
                            byte[] arrIn = Utilities.DeflaterCompress(Encoding.UTF8.GetBytes(sText));
                            string sB64 = Convert.ToBase64String(arrIn);
                            sOutput = Utilities.UrlEncode(sB64, Encoding.UTF8);
                        }
                        break;
                    case 15: // FromDeflatedSAML
                        {
                            // Strip spaces.
                            sText = sText.RemoveAllWhitespace();
                            string sB64 = Utilities.UrlDecode(sText, Encoding.UTF8);

                            // SAML uses + characters, so undo any decode into spaces.
                            sB64 = sB64.Replace(' ', '+');
                            byte[] arrDeflated = Convert.FromBase64String(sB64);
                            // Not all SAML is deflated,
                            if ((arrDeflated.Length > 0) && (arrDeflated[0] == 0x3c))
                            {
                                sOutput = Encoding.UTF8.GetString(arrDeflated);
                            }
                            else
                            {
                                arrLatestOutput = Utilities.DeflaterExpand(arrDeflated, false);
                                sOutput = Encoding.UTF8.GetString(arrLatestOutput);
                            }
                        }
                        break;
                    case 16:
                        {
                            byte[] arrIn = encodingInput.GetBytes(sText);
                            sOutput = Utilities.GetBase64Hash("md5", arrIn) + "\r\n\r\n" + Utilities.GetHash("md5", arrIn);
                        }
                        break;
                    case 17:
                        {
                            byte[] arrIn = encodingInput.GetBytes(sText);
                            sOutput = Utilities.GetBase64Hash("sha1", arrIn) + "\r\n\r\n" + Utilities.GetHash("sha1", arrIn);
                        }
                        break;
                    case 18:
                        {
                            byte[] arrIn = encodingInput.GetBytes(sText);
                            sOutput = Utilities.GetBase64Hash("sha256", arrIn) + "\r\n\r\n" + Utilities.GetHash("sha256", arrIn);
                        }
                        break;
                    case 19:
                        {

                            byte[] arrIn = encodingInput.GetBytes(sText);
                            sOutput = Utilities.GetBase64Hash("sha384", arrIn) + "\r\n\r\n" + Utilities.GetHash("sha384", arrIn);
                        }
                        break;
                    case 20:
                        {
                            byte[] arrIn = encodingInput.GetBytes(sText);
                            sOutput = Utilities.GetBase64Hash("sha512", arrIn) + "\r\n\r\n" + Utilities.GetHash("sha512", arrIn);
                        }
                        break;
                    case 21: // CrackURL
                        {
                            if (!sText.HasText())
                            {
                                sOutput = String.Empty;
                            }
                            else
                            {
                                try
                                {
                                    Uri oUri = new Uri(sText);
                                    StringBuilder sb = new StringBuilder();

                                    sb.Append($"Scheme:\t{oUri.Scheme}\r\nHost:\t{oUri.Host}\r\nPort:\t{oUri.Port}\r\nPath:\t{oUri.AbsolutePath}\r\n");
                                    if (oUri.Fragment.HasText()) sb.Append($"Fragment: {oUri.Fragment}\r\n");
                                    string sQuery = oUri.Query;
                                    if (sQuery.StartsWith("?")) sQuery = sQuery.Substring(1);
                                    if (sQuery.HasText())
                                    {
                                        sb.Append("\r\nQueryString:");
                                        foreach (string sKey in sQuery.Split('&'))
                                        {
                                            if (!sKey.HasText()) continue;
                                            string[] arrParts = sKey.Split('=');
                                            if (arrParts.Length < 2)
                                            {
                                                sb.Append($"\r\n\t{arrParts[0]}");
                                            }
                                            else
                                            {
                                                sb.Append($"\r\n\t{Utilities.UrlDecode(arrParts[0])} = {Utilities.UrlDecode(arrParts[1])}");
                                            }
                                        }
                                    }
                                    sOutput = sb.ToString();
                                }
                                catch (Exception eX)
                                {
                                    arrLatestOutput = Utilities.UrlDecodeToBytes(sText, encodingInput);
                                    sOutput = Utilities.UrlDecode(sText, encodingInput);
                                }
                            }
                        }
                        break;

                    case 22: // epoch
                        if (TimestampConverter.TryParseToUtc(sText, out DateTimeOffset dto))
                        {
                            var sb = new StringBuilder();
                            TimestampResult result = TimestampConverter.Convert(dto);

                            sb.AppendLine($"ISO UTC:\t\t{result.Iso8601Utc}");
                            sb.AppendLine($"Local Time:\t{result.Iso8601Local}");
                            sb.AppendLine($"Seconds:\t\t{result.UnixSeconds}");
                            sb.AppendLine($"Millisec:\t\t{result.UnixMilliseconds}");
                            sb.AppendLine($".NET ticks:\t{result.NetTicks}");
                            sOutput = sb.ToString();
                        }
                        else
                        {
                            sOutput = $"Error: Unable to parse '{sText}' as a valid timestamp.";
                        }
                        break;

                    case 23: // JWT
                        try
                        {
                            JwtDecodeResult result = JwtDecoder.Decode(sText);
                            var sb = new StringBuilder();
                            sb.AppendLine("=== HEADER ===");
                            sb.AppendLine(result.HeaderJson);

                            sb.AppendLine("\n=== PAYLOAD ===");
                            sb.AppendLine(result.PayloadJson);

                            sb.AppendLine("\n=== SIGNATURE (HEX) ===");
                            sb.AppendLine(result.SignatureHex);
                            sOutput = sb.ToString();
                        }
                        catch (Exception ex)
                        {
                            sOutput = $"Failed to decode JWT: {ex.Message}";
                        }

                        break;

                    case 24: // CBOR
                        try
                        {
                            sText = sText.Trim();
                            if (sText.OICStartsWith("0x"))
                            {
                                sText = HexToBase64(sText.Substring(2));
                            }
                            else if (IsHexString(sText))
                            {
                                sText = HexToBase64(sText);
                            }
                            // if sText contains '[' or '{', it's likely a JSON representation
                            // of CBOR, so we can try to parse it as JSON first:
                            if (sText.Contains("{") || sText.Contains("["))
                            {  // JSON to CBOR
                                var cborObject = PeterO.Cbor.CBORObject.FromJSONString(sText);
                                arrLatestOutput = cborObject.EncodeToBytes();
                                sOutput = Convert.ToBase64String(arrLatestOutput);
                            }
                            else  // CBOR to JSON
                            {
                                byte[] cborBytes = Convert.FromBase64String(sText);
                                var cborObject = PeterO.Cbor.CBORObject.DecodeFromBytes(cborBytes);
                                sOutput = cborObject.ToJSONString();
                            }
                        }
                        catch (Exception ex)
                        {
                            sOutput = $"Failed to convert CBOR: {ex.Message}";
                        }
                        break;

                    default:
                        if (!CApp.scriptRules.RunTextCodec(cbxTransforms.Text, sText, out sOutput, out arrLatestOutput))
                        {
                            sOutput = $"Error: No text codec found for '{cbxTransforms.Text}'";
                        }
                        break;
                }

                Text = String.Format("TextWizard [{0} => {1} chars]", txtInput.TextLength, sOutput.Length);
            }
            catch (Exception eX)
            {
                sOutput = "Error: " + eX.Message;
                bThrew = true;
            }

            if (!cbViewBytes.Checked)
            {
                // TODO: Optimize. 
                // .NET Textbox doesn't like embedded nulls or unpaired \r\n sequences
                sOutput = sOutput.Replace("\r\n", "\n").Replace("\n", "\r\n").Replace('\0', '\uFFFD');
            }
            else
            {
                if (null == arrLatestOutput)
                {
                    arrLatestOutput = encodingOutput.GetBytes(sOutput);
                }
                sOutput = Utilities.ByteArrayToHexView(arrLatestOutput, 0, 16, 1024, true);
                Text = $"TextWizard [{txtInput.TextLength} => {arrLatestOutput.Length} bytes]";
            }

            txtOutput.WordWrap = !cbViewBytes.Checked;
            txtOutput.Text = sOutput;

            btnSetInputFromOutput.Enabled = (!bThrew && !cbViewBytes.Checked && (txtOutput.TextLength > 0));
            if (bThrew)
            {
                Text = "TextWizard [Invalid]";
                _EnableExport(false);
            }
        }

        // Parse a Hex string into bytes, no matter how we delimit the text
        private string ParseBytes(string sText)
        {
            sText = sText.Trim();
            if (sText.OICStartsWith("0x")) sText = sText.Substring(2);
            string sOutput;
            MemoryStream oMS = new MemoryStream();
            int iLast = -1;
            foreach (char c in sText)
            {
                switch (c)
                {
                    // Skip prefixes/delimiters
                    case '$':
                    case '%':
                    case ' ':
                    case ',':
                    case ';':
                    case ':':
                    case '-':
                    case '.':
                    case '\t':
                    case '\r':
                    case '\n':
                        continue;

                    default:
                        int iThis = Utilities.HexToInt(c);
                        if (iThis < 0)
                        {
                            // TODO: Warning on error?
                            continue;
                        }

                        if (iLast < 0)
                        {
                            iLast = iThis;
                            continue;
                        }

                        oMS.WriteByte((byte)(iLast * 16 + iThis));
                        iLast = -1;
                        break;
                }
            }

            arrLatestOutput = oMS.ToArray();
            sOutput = encodingInput.GetString(arrLatestOutput);
            return sOutput;
        }

        //TODO: Offer an overload that specifies the Transormation to select by its name
        internal frmTextWizard(string sInput)
        {
            InitializeComponent();

            txtInput.Font = new Font(txtInput.Font.FontFamily, CONFIG.flFontSize);

            txtOutput.Font = new Font(txtOutput.Font.FontFamily, CONFIG.flFontSize);
            txtOutput.BackColor = CONFIG.colorDisabledEdit;

            // TODO:Fix
            cbxTransforms.Items.Add("Epoch");
            cbxTransforms.Items.Add("JWT Token Decoder");
            cbxTransforms.Items.Add("CBOR");

            cbxTransforms.Items.AddRange(CApp.scriptRules.AvailableTextCodecs());

            try
            {
                encodingInput = Encoding.GetEncoding(CApp.Prefs.GetStringPref("textwizard.InputEncoding", "UTF-8"));
            }
            catch
            {
                encodingInput = Encoding.UTF8;
            }

            try
            {
                encodingOutput = Encoding.GetEncoding(CApp.Prefs.GetStringPref("textwizard.OutputEncoding", "UTF-8"));
            }
            catch
            {
                encodingOutput = Encoding.UTF8;
            }

            // Populate the Input box with the provided text, or if none, attempt to pull from the clipboard.
            if (!String.IsNullOrEmpty(sInput))
            {
                txtInput.Text = sInput.Replace("\n", "\r\n");
                this.ActiveControl = txtOutput;
            }
            else
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        string sText = Clipboard.GetText();
                        if (sText.Length < Int16.MaxValue)
                        {
                            txtInput.Text = Clipboard.GetText();
                        }
                    }
                }
                catch
                {
                    // Don't fail if clipboard access fails.
                }
            }

            try
            {
                // Select the last used transform if it exists in the list, otherwise default to the first item.
                string sTarget = CApp.Prefs.GetStringPref("textwizard.LastTransform", "URLDecode");
                int iIndex = cbxTransforms.Items.IndexOf(sTarget);
                if (iIndex < 0) iIndex = 0;
                this.cbxTransforms.SelectedIndex = iIndex;
            }
            catch { }
        }

        private void txtInput_TextChanged(object sender, EventArgs e)
        {
            Recalc();
            var sByteInfo = (arrLatestOutput != null) ? $"({arrLatestOutput.Length} bytes) " : string.Empty;
            this.Text = $"TextWizard [ {txtInput.TextLength} => {txtOutput.TextLength} characters {sByteInfo}]";
        }

        private void frmTextWizard_KeyUp(object sender, KeyEventArgs e)
        {
            if ((Keys.Escape == e.KeyCode) &&
                !(this.ActiveControl is ComboBox))
            {
                e.SuppressKeyPress = e.Handled = true;
                Close();
            }
        }

        private void lnkSaveAsExchange_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                if (null == arrLatestOutput)
                {
                    arrLatestOutput = encodingOutput.GetBytes(txtOutput.Text);
                }
                CApp.UI.AddFauxExchange($"/TextWizard/{DateTime.Now.ToString("H-mm-ss")}", "application/octet-stream", arrLatestOutput);
            }
            catch (Exception eX) { CApp.ReportException(eX, "Failed"); }
        }

        private void lnkSaveAsFile_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                if (null == arrLatestOutput)
                {
                    arrLatestOutput = encodingOutput.GetBytes(txtOutput.Text);
                }
                string sFilename = Utilities.ObtainSaveFilename("Save output...", "All files|*.*");
                if (!sFilename.HasText()) return;
                File.WriteAllBytes(sFilename, arrLatestOutput);
            }
            catch (Exception eX) { CApp.ReportException(eX, "Export failed"); }
        }

        private void cbxTransforms_SelectedIndexChanged(object sender, EventArgs e)
        {
            Recalc();
            CApp.Prefs.SetStringPref("textwizard.LastTransform", cbxTransforms.Text);
        }

        private void btnSetInputFromOutput_Click(object sender, EventArgs e)
        {
            txtInput.Text = txtOutput.Text;
        }

        private void txtOutput_TextChanged(object sender, EventArgs e)
        {
            _EnableExport(txtOutput.TextLength > 0);
        }

        private void _EnableExport(bool b)
        {
            lnkSaveAsExchange.Enabled = lnkSaveAsFile.Enabled = b;
        }

        private void cbViewBytes_CheckedChanged(object sender, EventArgs e)
        {
            // Ensure we use a monospaced font for the output box when viewing bytes, and a Unicode font otherwise.
            txtOutput.Font = new Font((cbViewBytes.Checked ? "Consolas" : "Arial Unicode MS"), CONFIG.flFontSize);
            Recalc();
        }

        private void lnkToClipboard_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Clipboard.SetText(txtOutput.Text);
        }

        // TODO: Find a better home for this?
        public static bool IsHexString(string input)
        {
            if (string.IsNullOrEmpty(input) || (input.Length & 1) != 0)
                return false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                // Checks '0'-'9', 'A'-'F', and 'a'-'f' using range bounds
                bool isValidHex = (c >= '0' && c <= '9') ||
                                  (c >= 'A' && c <= 'F') ||
                                  (c >= 'a' && c <= 'f');

                if (!isValidHex)
                    return false;
            }

            return true;
        }
        public static string HexToBase64(string hex)
        {
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have an even length.", nameof(hex));

            byte[] bytes = new byte[hex.Length / 2];

            for (int i = 0; i < bytes.Length; i++)
            {
                int hi = GetHexNibble(hex[i * 2]);
                int lo = GetHexNibble(hex[i * 2 + 1]);
                bytes[i] = (byte)((hi << 4) | lo);
            }

            return Convert.ToBase64String(bytes);
        }

        private static int GetHexNibble(char c)
        {
            int val = c;
            // Handles '0'-'9', 'A'-'F', 'a'-'f'
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }
    }

    public static class TimestampConverter
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// Parses an epoch numeric value (seconds, milliseconds, or microseconds) 
        /// or an ISO 8601 string into a DateTimeOffset (UTC).
        /// </summary>
        public static bool TryParseToUtc(string input, out DateTimeOffset result)
        {
            result = default(DateTimeOffset);

            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim();

            // Handle numeric epoch values (seconds, ms, us, ticks)
            if (long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out long numericValue))
            {
                // 1. Seconds (e.g., 1735689600 -> 10 digits)
                if (Math.Abs(numericValue) < 32503680000L)
                {
                    result = DateTimeOffset.FromUnixTimeSeconds(numericValue);
                    return true;
                }

                // 2. Milliseconds (e.g., 1735689600000 -> 13 digits)
                if (Math.Abs(numericValue) < 32503680000000L)
                {
                    result = DateTimeOffset.FromUnixTimeMilliseconds(numericValue);
                    return true;
                }

                // 3. Microseconds (e.g., 1735689600000000 -> 16 digits)
                if (Math.Abs(numericValue) < 32503680000000000L)
                {
                    long seconds = numericValue / 1000000L;
                    long micro = numericValue % 1000000L;
                    result = DateTimeOffset.FromUnixTimeSeconds(seconds).AddTicks(micro * 10);
                    return true;
                }

                // 4. .NET Ticks (100-nanosecond intervals since 0001-01-01)
                if (numericValue >= 0 && numericValue <= DateTimeOffset.MaxValue.Ticks)
                {
                    result = new DateTimeOffset(numericValue, TimeSpan.Zero);
                    return true;
                }

                return false;
            }

            // Handle string representation (ISO 8601, RFC 1123, etc.)
            if (DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsedDate))
            {
                result = parsedDate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts a DateTimeOffset to unix seconds, milliseconds, and formatted string representations.
        /// </summary>
        public static TimestampResult Convert(DateTimeOffset dto)
        {
            DateTimeOffset utc = dto.ToUniversalTime();

            return new TimestampResult(
                unixSeconds: utc.ToUnixTimeSeconds(),
                unixMilliseconds: utc.ToUnixTimeMilliseconds(),
                unixMicroseconds: (utc.Ticks - Epoch.Ticks) / 10,
                netTicks: utc.Ticks,
                iso8601Utc: utc.ToString("o", CultureInfo.InvariantCulture),
                iso8601Local: dto.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture)
            );
        }
    }
    public struct TimestampResult
    {
        public long UnixSeconds { get; }
        public long UnixMilliseconds { get; }
        public long UnixMicroseconds { get; }
        public long NetTicks { get; }
        public string Iso8601Utc { get; }
        public string Iso8601Local { get; }

        public TimestampResult(
            long unixSeconds,
            long unixMilliseconds,
            long unixMicroseconds,
            long netTicks,
            string iso8601Utc,
            string iso8601Local)
        {
            UnixSeconds = unixSeconds;
            UnixMilliseconds = unixMilliseconds;
            UnixMicroseconds = unixMicroseconds;
            NetTicks = netTicks;
            Iso8601Utc = iso8601Utc;
            Iso8601Local = iso8601Local;
        }
    }

    public static class JwtDecoder
    {
        public static JwtDecodeResult Decode(string jwt)
        {
            if (string.IsNullOrWhiteSpace(jwt))
                return new JwtDecodeResult(string.Empty, string.Empty, string.Empty);

            string[] parts = jwt.Trim().Split('.');
            if (parts.Length < 2 || parts.Length > 3)
                throw new FormatException("Invalid JWT format. Expected 3 dot-separated segments.");

            string headerJson = Base64UrlDecode(parts[0]);
            string payloadJson = Base64UrlDecode(parts[1]);
            string signatureHex = parts.Length == 3 ? PartsToHex(parts[2]) : string.Empty;

            return new JwtDecodeResult(headerJson, payloadJson, signatureHex);
        }

        private static string Base64UrlDecode(string input)
        {
            // Convert Base64URL to standard Base64
            string base64 = input.Replace('-', '+').Replace('_', '/');

            // Restore required Base64 padding
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            byte[] bytes = System.Convert.FromBase64String(base64);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        private static string PartsToHex(string input)
        {
            string base64 = input.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            try
            {
                byte[] bytes = System.Convert.FromBase64String(base64);
                return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
            catch
            {
                return input; // Fallback to raw string if signature is non-standard
            }
        }
    }

    public struct JwtDecodeResult
    {
        public string HeaderJson { get; }
        public string PayloadJson { get; }
        public string SignatureHex { get; }

        public JwtDecodeResult(string headerJson, string payloadJson, string signatureHex)
        {
            HeaderJson = headerJson;
            PayloadJson = payloadJson;
            SignatureHex = signatureHex;
        }
    }
}
