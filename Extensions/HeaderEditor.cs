using Clearinet;
using System.Drawing;
using System.Windows.Forms;

namespace Extensions
{
    public partial class HeaderEditor : Form
    {
        public HeaderEditor()
        {
            InitializeComponent();
            txtValue.Focus();
            // TODO: Set icon to something reasonable
        }

        public bool IsReadonly
        {
            get { return txtName.ReadOnly; }
            set
            {
                txtName.ReadOnly = value;
                txtValue.ReadOnly = value;
                txtName.BackColor = value ? CONFIG.colorDisabledEdit : SystemColors.Window;
                txtValue.BackColor = value ? CONFIG.colorDisabledEdit : SystemColors.Window;
                if (!value)
                {
                    btnTemplates.Enabled = btnSave.Enabled = false;
                }
            }
        }

        private void lnkHeaderLookup_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Utilities.LaunchHyperlink(CApp.Prefs.GetStringPref("inspector.headers.searchurl",
                 "https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/$W$").Replace("$W$", txtName.Text));
        }

        private void txtName_TextChanged(object sender, System.EventArgs e)
        {
            if (txtName.Text.HasText())
            {
                if (!txtName.ReadOnly) btnSave.Enabled = true;
                lnkHeaderLookup.Enabled = true;
            }
            else
            {
                btnSave.Enabled = false;
                lnkHeaderLookup.Enabled = false;
            }
        }

        private void txtValue_TextChanged(object sender, System.EventArgs e)
        {
            if (!txtName.ReadOnly) btnSave.Enabled = true;
        }

        private void btnSave_Click(object sender, System.EventArgs e)
        {
            txtName.Text = txtName.Text.RemoveAllWhitespace();
        }
    }
}
