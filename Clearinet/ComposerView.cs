using System;
using System.Text;
using System.Threading.Tasks;
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
            txtURL.Text = "https://debugtheweb.com/echo.aspx";
            Win32UI.SetCueText(txtHeaders, "Message headers");
            Win32UI.SetCueText(txtBody, "Request body");
        }

        private void btnSend_Click(object sender, System.EventArgs e)
        {
            try
            {
                if (!txtURL.Text.OICStartsWith("http"))
                {
                    txtURL.Text = "https://" + txtURL.Text.Trim();
                }

                // TODO: Fixup header block, looking for empty lines, missing colon, etc
                // TODO: Fixup Content-Length on request header

                var (method, path, version, headerList, origin) =
                    Parser.ParseRequestHeaders(
                        $"{cbxMethod.Text} {txtURL.Text} {cbxHTTPVersion.Text}\r\n" +
                        txtHeaders.Text.Trim());
                HTTPRequestHeaders rqh = new HTTPRequestHeaders(path, headerList);
                rqh.HTTPVersion = version;
                rqh.HTTPMethod = method;
                if ((origin != null) && origin.Contains(":"))
                {
                    rqh.UriScheme = origin.TrimAfter(":");
                    if (!rqh.Exists("Host"))
                    {
                        rqh["Host"] = origin.TrimBefore("://");
                        txtHeaders.Text = rqh.ToString(false, false);
                    }
                }
                Exchange XC = new Exchange(rqh, Encoding.UTF8.GetBytes(txtBody.Text));
                XC.BitFlags = ExchangeFlags.RequestGeneratedByClearinet;
                XC.EnsureID();
                XC.state = ExchangeState.AutoTamperRequestBefore;
                if ((Control.ModifierKeys & Keys.Shift) != 0) XC["x-breakrequest"] = "Shift+Click";
                Task.Run(async () => await new HTTPClientBridge().SendAndReceiveRawAsync(XC));
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
    }
}
