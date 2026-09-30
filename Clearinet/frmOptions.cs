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
        }

        private void lnkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Utilities.LaunchHyperlink("https://clearinet.app/r/?ClearinetHelpOptions");
        }

        private void tcOptions_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if (e.TabPage == pageExtensions) 
            {
                txtExtensionList.Text = CApp.oExtensions.ToString(true);
            }
        }
    }
}
