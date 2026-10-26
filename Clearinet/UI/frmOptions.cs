using System;
using System.Drawing;
using System.Windows.Forms;

namespace Clearinet
{
    public partial class frmOptions : Form
    {
        public frmOptions()
        {
            InitializeComponent();
            cbAttachOnStartup.Checked = CApp.Prefs.GetBoolPref("app.attach_on_startup", false); // TODO: Change after we have a proxy
            cbCheckForUpdates.Checked = CApp.Prefs.GetBoolPref("app.updater.check_on_startup", true);

            switch (CApp.Prefs.GetStringPref("app.service_traffic", "allowed"))
            {
                case "allowed":
                    cbxBigSwitch.SelectedIndex = 0;
                    break;
                case "prompt":
                    cbxBigSwitch.SelectedIndex = 1;
                    break;
                default:
                case "forbidden":
                    cbxBigSwitch.SelectedIndex = 2;
                    CApp.Prefs.SetBoolPref("app.updater.check_on_startup", false);
                    cbCheckForUpdates.Checked = false;
                    break;
            }

            if (CONFIG.isViewerMode)
            {
                Text = Text += " (Readonly - Viewer Mode)";
                btnOk.Enabled = false;
                btnOk.Text = "View Only";
            }

            cbEnableIPv6.Checked = CApp.Prefs.GetBoolPref("proxy.enable_ipv6", true);
            cbAutoStream.Checked = CApp.Prefs.GetBoolPref("proxy.auto_stream_media", true);
            cbAllowRemoteConnections.Checked = CApp.Prefs.GetBoolPref("proxy.allow_remote_connections", false);
            cbCaptureCONNECT.Checked = CApp.Prefs.GetBoolPref("proxy.capture_connect", true);
            cbDecryptHTTPS.Checked = CApp.Prefs.GetBoolPref("proxy.decrypt_https", false);
        }

        private void lnkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Utilities.LaunchHyperlink("https://clearinet.app/r/?ClearinetHelpOptions");
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            // Don't clutter the prefs with default values; if the user
            // adjusts a preference to the non-default, set it, otherwise
            // delete the preference entirely.
            if (!cbAttachOnStartup.Checked)
                CApp.Prefs.SetBoolPref("app.attach_on_startup", false);
            else
                CApp.Prefs.RemovePref("app.attach_on_startup");

            if (!cbEnableIPv6.Checked)
                CApp.Prefs.SetBoolPref("proxy.enable_ipv6", false);
            else
                CApp.Prefs.RemovePref("proxy.enable_ipv6");

            if (!cbAutoStream.Checked)
                CApp.Prefs.SetBoolPref("proxy.auto_stream_media", false);
            else
                CApp.Prefs.RemovePref("proxy.auto_stream_media");

            if (cbAllowRemoteConnections.Checked)
                CApp.Prefs.SetBoolPref("proxy.allow_remote_connections", true);
            else
                CApp.Prefs.RemovePref("proxy.allow_remote_connections");

            if (!cbCaptureCONNECT.Checked)
                CApp.Prefs.SetBoolPref("proxy.capture_connect", false);
            else
                CApp.Prefs.RemovePref("proxy.capture_connect");

            if (cbDecryptHTTPS.Checked)
                CApp.Prefs.SetBoolPref("proxy.decrypt_https", true);
            else
                CApp.Prefs.RemovePref("proxy.decrypt_https");

            if (cbxBigSwitch.SelectedIndex == 0)
            { // Allow all Web Service calls
                CApp.Prefs.RemovePref("app.service_traffic");
            }
            else if (cbxBigSwitch.SelectedIndex == 1)
            { // Prompt on every Web Service call
                CApp.Prefs.SetStringPref("app.service_traffic", "prompt");
            }
            else
            { // Blocked
                CApp.Prefs.SetStringPref("app.service_traffic", "forbidden");
            }

            if (cbCheckForUpdates.Checked)
            {
                CApp.Prefs.RemovePref("app.updater.check_on_startup");
            }
            else
            {
                CApp.Prefs.SetBoolPref("app.updater.check_on_startup", false);
            }
        }

        private void tcOptions_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if (e.TabPage == pageExtensions)
            {
                txtExtensionsList.Text = CApp.oExtensions.ToString(true);
            }
            if (e.TabPage == pageAppearance)
            {
                int iIndex = cbxFontSize.Items.IndexOf(CONFIG.flFontSize.ToString());
                if (iIndex < 0) iIndex = 1;  // Default to 10pt
                cbxFontSize.SelectedIndex = iIndex;
                btnSetColorReadonly.BackColor = CONFIG.colorDisabledEdit;
                btnSetColorMark.BackColor = CONFIG.colorHighlight;
            }
        }

        private void frmOptions_Load(object sender, EventArgs e)
        {
            txtExtensionsList.BackColor = CONFIG.colorDisabledEdit;
        }

        private void btnSetColorReadonly_Click(object sender, EventArgs e)
        {
            using (ColorDialog dlg = new ColorDialog())
            {
                dlg.AnyColor = true;
                dlg.FullOpen = true;
                dlg.CustomColors = new int[] { ColorTranslator.ToOle(CONFIG.COLOR_DEFAULT_DISABLEDEDIT) };
                dlg.Color = CONFIG.colorDisabledEdit;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    btnSetColorReadonly.BackColor = dlg.Color;

                    // BUG: This shouldn't take effect until the user pressed OK, but they can't in Viewer mode... hrmph.
                    CONFIG.colorDisabledEdit = dlg.Color;
                }
            }
        }

        private void btnSetColorMark_Click(object sender, EventArgs e)
        {
            using (ColorDialog dlg = new ColorDialog())
            {
                dlg.AnyColor = true; dlg.FullOpen = true;
                dlg.Color = CONFIG.colorHighlight;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    btnSetColorMark.BackColor = dlg.Color;

                    // BUG: This shouldn't take effect until the user pressed OK, but they can't in Viewer mode... hrmph.
                    CONFIG.colorHighlight = dlg.Color;
                }
            }
        }

        private void cbxFontSize_SelectionChangeCommitted(object sender, EventArgs e)
        {
            CApp.UI.actSetFontSize(float.Parse(cbxFontSize.SelectedItem.ToString()));
        }
    }
}
