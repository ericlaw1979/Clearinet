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
            if (CONFIG.isViewerMode)
            {
                Text = Text += " (Readonly - Viewer Mode)";
                lblUsageNote.Text = "Options cannot be changed in Viewer mode."; // TODO: this is a problem if the user is *forced* into viewer mode.
                btnOk.Enabled = false;
                btnOk.Text = "View Only";
            }

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
                    break;
            }

            cbCheckForUpdates.Checked = CApp.Prefs.GetBoolPref("app.updater.check_on_startup", true);
            cbAttachOnStartup.Checked = CApp.Prefs.GetBoolPref("app.attach_on_startup", false); // TODO: Change after we have a proxy
            cbEnableIPv6.Checked = CApp.Prefs.GetBoolPref("proxy.enable_ipv6", true);
            cbAutoStream.Checked = CApp.Prefs.GetBoolPref("proxy.auto_stream_media", true);
            cbAllowRemoteConnections.Checked = CApp.Prefs.GetBoolPref("proxy.allow_remote_connections", false);
            cbCaptureCONNECT.Checked = CApp.Prefs.GetBoolPref("proxy.capture_connect", true);
            cbDecryptHTTPS.Checked = CApp.Prefs.GetBoolPref("proxy.decrypt_https", false);
            cbIgnoreUpstreamCertErrors.Checked = CApp.Prefs.GetBoolPref("proxy.unsafe.ignore_cert_errors", false);
            cbChainToGateway.Checked = CApp.Prefs.GetBoolPref("proxy.chain_to_upstream_gateway", true);
            cbDarkMode.Checked = CApp.Prefs.GetBoolPref("app.ui.dark_mode", false);

            if (CApp.Prefs.GetBoolPref("app.ui.dark_mode", false)) ThemeManager.ApplyDarkMode(this);
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
            CApp.Prefs.AssignOrRemoveIfDefault("app.attach_on_startup", cbAttachOnStartup.Checked, true);
            CApp.Prefs.AssignOrRemoveIfDefault("proxy.enable_ipv6", cbEnableIPv6.Checked, true);
            CApp.Prefs.AssignOrRemoveIfDefault("proxy.auto_stream_media", cbAutoStream.Checked, true);
            CApp.Prefs.AssignOrRemoveIfDefault("proxy.allow_remote_connections", cbAllowRemoteConnections.Checked, false);
            CApp.Prefs.AssignOrRemoveIfDefault("proxy.capture_connect", cbCaptureCONNECT.Checked, true);
            CApp.Prefs.AssignOrRemoveIfDefault("proxy.decrypt_https", cbDecryptHTTPS.Checked, false);
            CApp.Prefs.AssignOrRemoveIfDefault("proxy.unsafe.ignore_cert_errors", cbIgnoreUpstreamCertErrors.Checked, false);
            CApp.Prefs.AssignOrRemoveIfDefault("proxy.chain_to_upstream_gateway", cbChainToGateway.Checked, true);
            CApp.Prefs.AssignOrRemoveIfDefault("app.ui.dark_mode", cbDarkMode.Checked, false);

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
                cbCheckForUpdates.Checked = false;
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

                    // BUG: This shouldn't take effect until the user pressed SAVE,
                    // but they can't click SAVE in Viewer mode... hrmph.
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

        private void cbIgnoreUpstreamCertErrors_CheckedChanged(object sender, EventArgs e)
        {
            cbIgnoreUpstreamCertErrors.ForeColor = cbIgnoreUpstreamCertErrors.Checked ? Color.Red : SystemColors.ControlText;
        }
    }
}
