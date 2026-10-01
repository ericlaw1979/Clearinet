using System;
using System.Windows.Forms;

namespace Clearinet
{
    public partial class frmFind : Form
    {
        public static void BeginFinding(string sFindText = "") {
            using (frmFind f = new frmFind())
            {
                if (sFindText.HasText()) f.txtToFind.Text = sFindText;
                //CApp.alert(f.ShowDialog(CApp.UI).ToString());
                f.ShowDialog(CApp.UI);
            }
        }
        public frmFind()
        {
            InitializeComponent();
            txtToFind.Text = CApp.Prefs.GetStringPref("app.find.lastfind.text.ephemeral", string.Empty);
            cbxFindIn.SelectedIndex = CApp.Prefs.GetInt32Pref("app.find.lastfind.findin", 0);
            cbxWhichComponent.SelectedIndex = CApp.Prefs.GetInt32Pref("app.find.lastfind.whichcomponent", 0);
        }

        private void btnFind_Click(object sender, EventArgs e)
        {
            CApp.Prefs.SetStringPref("app.find.lastfind.text.ephemeral", txtToFind.Text);
            CApp.Prefs.SetInt32Pref("app.find.lastfind.findin", cbxFindIn.SelectedIndex);
            CApp.Prefs.SetInt32Pref("app.find.lastfind.whichcomponent", cbxWhichComponent.SelectedIndex);
            CApp.alert("TODO: Alas, not yet implemented.");
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
