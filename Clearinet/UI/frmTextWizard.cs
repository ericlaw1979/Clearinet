using System;
using System.Diagnostics;
using System.Drawing;
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

                    default:
                        Debug.Assert(false, "Not reachable");
                        throw new IndexOutOfRangeException("Invalid conversion requested");
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

        private string ParseBytes(string sText)
        {
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

        internal frmTextWizard(string sInput)
        {
            InitializeComponent();

            txtInput.Font = new Font(txtInput.Font.FontFamily, CONFIG.flFontSize);

            txtOutput.Font = new Font(txtOutput.Font.FontFamily, CONFIG.flFontSize);
            txtOutput.BackColor = CONFIG.colorDisabledEdit;

            try
            {
                encodingInput = Encoding.GetEncoding(CApp.Prefs.GetStringPref("textwizard.InputEncoding", "UTF-8"));
                throw new NotImplementedException("preferences are not yet implemented.");
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
                this.cbxTransforms.SelectedIndex = CApp.Prefs.GetInt32Pref("textwizard.LastTransform", 0);
            }
            catch { }
        }

        private void txtInput_TextChanged(object sender, EventArgs e)
        {
            Recalc();
            this.Text = $"TextWizard [ {txtInput.TextLength} => {txtOutput.TextLength} characters]";
        }

        private void frmTextWizard_KeyUp(object sender, KeyEventArgs e)
        {
            if (Keys.Escape == e.KeyCode)
            {
                Close();
            }
        }

        private void lnkSaveAsExchange_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // nyi
            MessageBox.Show("This feature is not yet implemented.", "Clearinet", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            CApp.Prefs.SetInt32Pref("textwizard.LastTransforms", cbxTransforms.SelectedIndex);
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
    }
}
