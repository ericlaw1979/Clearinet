using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Clearinet
{
    public partial class frmOptions : Form
    {
        public frmOptions()
        {
            InitializeComponent();
            cbAttachOnStartup.Checked = CApp.Prefs.GetBoolPref("app.attach_on_startup", true);
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
