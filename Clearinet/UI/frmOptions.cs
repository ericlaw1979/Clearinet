using System;
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
        }

        private void lnkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Utilities.LaunchHyperlink("https://clearinet.app/r/?ClearinetHelpOptions");
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            // Don't clutter the prefs with default values.
            if (!cbAttachOnStartup.Checked)
                CApp.Prefs.SetBoolPref("app.attach_on_startup", false);
            else
                CApp.Prefs.RemovePref("app.attach_on_startup");

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
        }

        private void frmOptions_Load(object sender, EventArgs e)
        {
            txtExtensionsList.BackColor = CONFIG.colorDisabledEdit;
        }
    }
}
