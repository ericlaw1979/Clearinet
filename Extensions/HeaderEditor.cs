using Clearinet;
using System.Drawing;
using System.Windows.Forms;
using static Extensions.HeaderView;

namespace Extensions
{
    internal partial class HeaderEditor : Form
    {
        internal HeaderEditor(HeaderViewMode mode)
        {
            InitializeComponent();
            txtValue.Focus();
            txtName.AutoCompleteMode = AutoCompleteMode.Append;
            txtName.AutoCompleteSource = AutoCompleteSource.CustomSource;
            // Issue: We don't actually need to do this unless we're in Edit mode.
            if (mode == HeaderViewMode.Request)
            {
                txtName.AutoCompleteCustomSource = new AutoCompleteStringCollection();
                var sPopularRequestHeaders = CApp.Prefs.GetStringPref("lists.common_request_headers", DEFAULTS.sPopularRequestHeaders);
                txtName.AutoCompleteCustomSource.AddRange(sPopularRequestHeaders.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
            }
            else
            {
                txtName.AutoCompleteCustomSource = new AutoCompleteStringCollection();
                var sPopularResponseHeaders = CApp.Prefs.GetStringPref("lists.common_response_headers", DEFAULTS.sPopularResponseHeaders);
                txtName.AutoCompleteCustomSource.AddRange(sPopularResponseHeaders.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
            }
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
                btnTemplates.Enabled = !value;

                // We adjust this dynamically on edit.
                btnSave.Enabled = false;
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

        private void btnTemplates_Click(object sender, System.EventArgs e)
        {
            Point pvOrigin = PointToScreen(btnTemplates.Location);
            pvOrigin.Y += btnTemplates.Height;
            mnuTemplates.Show(pvOrigin, ToolStripDropDownDirection.BelowRight);
        }

        // TODO: We should have examples for all of the common things, and select based on whether this is the request or the response
        private void tsmiTemplateSecurityCSP_Click(object sender, System.EventArgs e)
        {
            txtName.Text = "Content-Security-Policy";
            txtValue.Text = "default-src 'none'; script-src 'self'; connect-src 'self'; img-src 'self'; style-src 'self';base-uri 'self';form-action 'self'" +
                                ";frame-ancestors 'self';manifest-src 'self';media-src 'self';object-src 'none';worker-src 'self'";
        }
    }
}
