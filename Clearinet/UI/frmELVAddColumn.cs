using System.Windows.Forms;

namespace Clearinet
{
    public partial class frmELVAddColumn : Form
    {
        AutoCompleteStringCollection acscRequestHeaderNames = new AutoCompleteStringCollection();
        AutoCompleteStringCollection acscResponseHeaderNames = new AutoCompleteStringCollection();
        AutoCompleteStringCollection acscExchangeFlags = new AutoCompleteStringCollection();

        public frmELVAddColumn()
        {
            InitializeComponent();
            Win32UI.SetCueText(txtName, "title of the column");
            Win32UI.SetCueText(txtTitle, "title of the column");

            var sPopularRequestHeaders = CApp.Prefs.GetStringPref("lists.common_request_headers", DEFAULTS.sPopularRequestHeaders);
            var sPopularResponseHeaders = CApp.Prefs.GetStringPref("lists.common_response_headers", DEFAULTS.sPopularResponseHeaders);
            var sPopularExchangeFlags = CApp.Prefs.GetStringPref("lists.common_exchange_flags", DEFAULTS.sPopularExchangeFlags);
            acscRequestHeaderNames.AddRange(sPopularRequestHeaders.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
            acscResponseHeaderNames.AddRange(sPopularResponseHeaders.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
            acscExchangeFlags.AddRange(sPopularExchangeFlags.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
        }

        private void frmELVAddColumn_Shown(object sender, System.EventArgs e)
        {
            cbxSource.SelectedIndex = 0;
            cbxSource.Focus();
        }

        private void lnkHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Utilities.LaunchHyperlink(CONFIG.GetURL("HelpColumns"));
        }

        private void btnAdd_Click(object sender, System.EventArgs e)
        {
            string sSource;
            if (cbxSource.SelectedIndex == 0)
                sSource = "@request.";
            else if (cbxSource.SelectedIndex == 1)
                sSource = "@response.";
            else
                sSource = string.Empty;

            sSource += txtName.Text.Trim();

            CApp.UI.lvExchanges.AddUserColumn(txtTitle.Text, (int)udWidth.Value, sSource);
            txtName.Text = txtTitle.Text = string.Empty;
            txtName.Focus();
            udWidth.Value = 60;
        }

        private void txtName_TextChanged(object sender, System.EventArgs e)
        {
            btnAdd.Enabled = !string.IsNullOrWhiteSpace(txtName.Text.RemoveAllWhitespace()) && !string.IsNullOrWhiteSpace(txtTitle.Text.Trim());
        }

        private void txtTitle_TextChanged(object sender, System.EventArgs e)
        {
            btnAdd.Enabled = !string.IsNullOrWhiteSpace(txtName.Text.RemoveAllWhitespace()) && !string.IsNullOrWhiteSpace(txtTitle.Text.Trim());
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.Close();
                return true;
            }

            // Allow the user to Enter,Enter,Enter through this screen.
            if (keyData == Keys.Enter)
            {
                if (this.ActiveControl.GetType() == typeof(ComboBox) ||
                    this.ActiveControl.GetType() == typeof(NumericUpDown) ||
                    this.ActiveControl.GetType() == typeof(TextBox))
                {
                    do
                    {
                        ActiveControl = GetNextControl(this.ActiveControl, true);
                    }
                    while (!ActiveControl.Visible || typeof(Label) == ActiveControl.GetType());
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void txtName_Validating(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!txtTitle.Text.HasText()) txtTitle.Text = txtName.Text.Trim();
            txtName.Text = txtName.Text.RemoveAllWhitespace();
        }

        private void cbxSource_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            if (cbxSource.SelectedIndex == 0)
            {
                txtName.AutoCompleteCustomSource = acscRequestHeaderNames;
                txtName.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                txtName.AutoCompleteSource = AutoCompleteSource.CustomSource;
            }
            else if (cbxSource.SelectedIndex == 1)
            {
                txtName.AutoCompleteCustomSource = acscResponseHeaderNames;
                txtName.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                txtName.AutoCompleteSource = AutoCompleteSource.CustomSource;
            }
            else if (cbxSource.SelectedIndex == 2)
            {
                txtName.AutoCompleteCustomSource = acscExchangeFlags;
                txtName.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                txtName.AutoCompleteSource = AutoCompleteSource.CustomSource;
            }
        }
    }
}
