using System.IO;
using System.Windows.Forms;

namespace Clearinet.UI
{
    public partial class frmHostRemap : Form
    {
        static frmHostRemap frmInstance = null;
        internal static void ConfigureHOSTS()
        {
            if (frmInstance != null)
            {
                frmInstance.Activate();
                return;
            }
            frmInstance = new frmHostRemap();
            using (frmInstance)
            {
                frmInstance.cbEnableRemap.Checked =
                       CApp.Prefs.GetBoolPref("app.host_remapping.enabled", false);
                frmInstance.txtRules.Text =
                    CApp.Prefs.GetStringPref("app.host_remapping.rules", string.Empty);
                frmInstance.btnSave.Enabled = false;
                frmInstance.lnkImportHOSTS.Enabled = frmInstance.txtRules.Enabled = frmInstance.cbEnableRemap.Checked;

                frmInstance.ShowDialog(CApp.UI);
                if (frmInstance.DialogResult == DialogResult.OK)
                {
                    bool bEnabled = frmInstance.cbEnableRemap.Checked;
                    CApp.Prefs.AssignOrRemoveIfDefault("app.host_remapping.enabled", bEnabled, false);
                    CApp.Prefs.AssignOrRemoveIfDefault("app.host_remapping.rules", frmInstance.txtRules.Text.Trim(), string.Empty);
                }
            }
            frmInstance = null;
        }

        private frmHostRemap()
        {
            InitializeComponent();
            txtRules.BackColor = CONFIG.colorDisabledEdit;
        }

        private void lnkImportHOSTS_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                // Load %SYSTEMROOT%\system32\drivers\etc\hosts into the textbox
                string strHostsFile = Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.System),
                    @"drivers\etc\hosts");
                txtRules.Text = File.ReadAllText(strHostsFile);
            }
            catch (System.Exception ex)
            {
                CApp.ReportException(ex, "Error importing HOSTS file");
            }
        }

        private void txtRules_EnabledChanged(object sender, System.EventArgs e)
        {
            txtRules.BackColor = txtRules.Enabled ? CONFIG.colorEdit : CONFIG.colorDisabledEdit;
        }

        private void cbEnableRemap_CheckedChanged(object sender, System.EventArgs e)
        {
            btnSave.Enabled = true;
            bool bEnabled = cbEnableRemap.Checked;
            lnkImportHOSTS.Enabled = txtRules.Enabled = bEnabled;
        }

        private void txtRules_ModifiedChanged(object sender, System.EventArgs e)
        {
            btnSave.Enabled = txtRules.Modified;
        }

        private void lnkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Utilities.LaunchHyperlink(CONFIG.GetURL("HelpHostRemap"));
        }
    }
}
