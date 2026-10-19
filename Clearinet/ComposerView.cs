using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace Clearinet
{
    public partial class ComposerView : UserControl
    {
        public ComposerView()
        {
            InitializeComponent();
            cbxHTTPVersion.SelectedIndex = 0;
            cbxMethod.SelectedIndex = 0;

            Win32UI.SetCueText(txtURL, "Request URL");
            txtURL.Text = "https://debugtheweb.com/echo2.aspx";
        }

        private void btnSend_Click(object sender, System.EventArgs e)
        {
            try
            {
                if (!txtURL.Text.OICStartsWith("http"))
                {
                    txtURL.Text = "https://" + txtURL.Text.Trim();
                }
                cbxMethod.Text = cbxMethod.Text.Trim().ToUpperInvariant();

                var bHeadersFixed = false;

                // Fixup header block, looking for empty lines, missing colon, etc
                List<String> lines = new List<string>();
                foreach (string line in txtHeaders.Lines)
                {
                    if (line.Trim().Length == 0)
                    {
                        bHeadersFixed = true;
                        continue;
                    }
                    if (!line.Contains(":"))
                    {
                        bHeadersFixed = true;
                        continue;
                    }
                    if (line.OICStartsWithAny(" ", "\t"))
                    {
                        lines.Add(line.Trim());
                        bHeadersFixed = true;
                        continue;
                    }
                    lines.Add(line);
                }

                var (method, path, version, headerList, origin) =
                    Parser.ParseRequestHeaders(
                        $"{cbxMethod.Text} {txtURL.Text} {cbxHTTPVersion.Text}\r\n" +
                        string.Join("\r\n", lines));
                HTTPRequestHeaders rqh = new HTTPRequestHeaders(path, headerList);
                rqh.HTTPVersion = version;
                rqh.HTTPMethod = method;
                if ((origin != null) && origin.Contains(":"))
                {
                    rqh.UriScheme = origin.TrimAfter(":");
                    if (!rqh.Exists("Host"))
                    {
                        rqh["Host"] = origin.TrimBefore("://");
                        bHeadersFixed = true;
                    }
                }

                byte[] arrBody = Encoding.UTF8.GetBytes(txtBody.Text);
                if (arrBody.Length > 0)
                {
                    rqh["Content-Length"] = arrBody.Length.ToString();
                    bHeadersFixed = true;
                }

                if (bHeadersFixed)
                {
                    txtHeaders.Text = rqh.ToString(false, false);
                }
                Exchange XC = new Exchange(rqh, arrBody);
                XC.BitFlags = ExchangeFlags.RequestGeneratedByClearinet;
                XC.EnsureID();
                XC.state = ExchangeState.AutoTamperRequestBefore;
                if ((Control.ModifierKeys & Keys.Shift) != 0) XC["x-breakrequest"] = "Shift+Click";
                XC.state = ExchangeState.HandTamperRequest;
            }
            catch (Exception ex)
            {
                CApp.Log.Log($"Composer failed: {ex.Message}");
            }
        }

        private void ComposerView_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("Clearinet.Exchange[]"))
            {
                e.Effect = DragDropEffects.Copy; return;
            }
        }

        private void ComposerView_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("Clearinet.Exchange[]"))
            {
                Exchange[] arrXC = null;
                try
                {
                    arrXC = (Exchange[])e.Data.GetData("Clearinet.Exchange[]");
                }
                catch (InvalidCastException)
                {
                    CApp.alert("Cross process drops are not yet supported.");
                }
                catch
                {
                    return;
                }

                if (arrXC?.Length < 1) return;
                txtHeaders.Text = arrXC[0].RequestHeaders?.ToString(false, false);
                txtBody.Text = arrXC[0].GetRequestBodyAsString();
                txtURL.Text = arrXC[0].fullUrl;
                cbxMethod.Text = arrXC[0].RequestHeaders?.HTTPMethod ?? "GET";
                cbxHTTPVersion.Text = arrXC[0].RequestHeaders?.HTTPVersion ?? "HTTP/1.1";
            }
        }

        private void txtURL_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnSend_Click(sender, e);
                e.Handled = e.SuppressKeyPress = true;
            }
        }
    }
}
