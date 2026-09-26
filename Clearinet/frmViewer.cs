using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Clearinet
{
    public partial class frmViewer : Form
    {
        public frmViewer()
        {
            InitializeComponent();
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {

        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frmAbout f = new frmAbout();
            f.ShowDialog(this);
        }

        #region ACTIONS
        private void actMinimizeToTray()
        {
            throw new NotImplementedException();
        }
        public void actShowTextWizard(string sText)
        {
            frmTextWizard oFrm = new frmTextWizard(sText);
            oFrm.Left = this.Left + 100;
            oFrm.Top = this.Top + 100;
            oFrm.Show(this);
        }
        public void actSpawnViewer()
        {
            // Respawn the currently-executing application in Viewer mode.
            Process.Start(Application.ExecutablePath, "-viewer");
        }
        public void actShowOptions()
        {
            frmOptions oFrm = new frmOptions();
            oFrm.Left = this.Left + 100;
            oFrm.Top = this.Top + 100;
            oFrm.ShowDialog(this);
        }
        #endregion ACTIONS

        private void miTextWizard_Click(object sender, EventArgs e)
        {
            actShowTextWizard(null);
        }

        private void miViewStayOnTop_CheckedChanged(object sender, EventArgs e)
        {
            this.TopMost = miViewStayOnTop.Checked;
        }

        private void miViewMinimize_Click(object sender, EventArgs e)
        {
            actMinimizeToTray();
        }

        private void miFileNewViewer_Click(object sender, EventArgs e)
        {
            actSpawnViewer();
        }

        private void miToolsOptions_Click(object sender, EventArgs e)
        {
            actShowOptions();
        }
    }
}
