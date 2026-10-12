using Clearinet.UI;
using System;
using System.Windows.Forms;

namespace Clearinet
{
    public partial class frmFind : Form
    {
        public static void BeginFinding(string sFindText = "")
        {
            using (frmFind f = new frmFind())
            {
                //     if (sFindText.HasText()) f.txtToFind.Text = sFindText;
                FindPanel oFP = new FindPanel();
                f.Controls.Add(oFP);
                oFP.Dock = DockStyle.Fill;
                oFP.txtToFind.Text = sFindText;
                f.ShowDialog(CApp.UI);
            }
        }
        public frmFind()
        {
            InitializeComponent();

        }



        private void btnFind_Click(object sender, EventArgs e)
        {

        }

        // Use ProcessDialogKey to handle Escape without the keystroke "leaking" and causing beeps.
        protected override bool ProcessDialogKey(Keys keyData)
        {
            // Indicates the key was consumed and prevents further processing
            if (ModifierKeys == Keys.None && keyData == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }
    }
}
