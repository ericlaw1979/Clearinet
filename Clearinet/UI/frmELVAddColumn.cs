using System.Collections.Generic;
using System.Windows.Forms;

namespace Clearinet
{
    public partial class frmELVAddColumn : Form
    {
        AutoCompleteStringCollection acscRequestHeaderNames = new AutoCompleteStringCollection();
        AutoCompleteStringCollection acscResponseHeaderNames = new AutoCompleteStringCollection();
        AutoCompleteStringCollection acscExchangeFlags = new AutoCompleteStringCollection();

        // Source list positions. The first three come from the Designer; Timers and Calculated are added below.
        private const int SourceRequestHeaders = 0;
        private const int SourceResponseHeaders = 1;
        private const int SourceExchangeFlags = 2;
        private const int SourceTimers = 3;
        private const int SourceCalculated = 4;

        // For Timers and Calculated, a list of the available fields replaces the free-text Name box.
        private readonly ComboBox cbxField;
        private readonly ToolTip ttField = new ToolTip();
        private string sAutoTitle;

        public frmELVAddColumn()
        {
            InitializeComponent();
            Win32UI.SetCueText(txtName, "header or flag name");
            Win32UI.SetCueText(txtTitle, "title of the column");

            var sPopularRequestHeaders = CApp.Prefs.GetStringPref("lists.common_request_headers", DEFAULTS.sPopularRequestHeaders);
            var sPopularResponseHeaders = CApp.Prefs.GetStringPref("lists.common_response_headers", DEFAULTS.sPopularResponseHeaders);
            var sPopularExchangeFlags = CApp.Prefs.GetStringPref("lists.common_exchange_flags", DEFAULTS.sPopularExchangeFlags);
            acscRequestHeaderNames.AddRange(sPopularRequestHeaders.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
            acscResponseHeaderNames.AddRange(sPopularResponseHeaders.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
            acscExchangeFlags.AddRange(sPopularExchangeFlags.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));

            cbxSource.Items.Add("Timers");
            cbxSource.Items.Add("Calculated");
            cbxField = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = txtName.Location,
                Size = txtName.Size,
                Font = txtName.Font,
                TabIndex = txtName.TabIndex,
                Name = "cbxField",
                Visible = false
            };
            cbxField.SelectedIndexChanged += cbxField_SelectedIndexChanged;
            Controls.Add(cbxField);
            FormClosed += (sender, e) => ttField.Dispose();

            // After all controls exist, so the field list is themed too.
            if (CONFIG.isDarkMode) ThemeManager.ApplyDarkMode(this);
        }

        private bool IsFieldListSource => cbxSource.SelectedIndex == SourceTimers || cbxSource.SelectedIndex == SourceCalculated;

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
            if (IsFieldListSource)
            {
                if (!(cbxField.SelectedItem is ExchangeColumnSources.Source source)) return;
                sSource = (cbxSource.SelectedIndex == SourceTimers ? ExchangeColumnSources.TimersPrefix : ExchangeColumnSources.CalcPrefix)
                    + source.Name;
            }
            else
            {
                if (cbxSource.SelectedIndex == SourceRequestHeaders)
                    sSource = "@request.";
                else if (cbxSource.SelectedIndex == SourceResponseHeaders)
                    sSource = "@response.";
                else
                    sSource = string.Empty;

                sSource += txtName.Text.Trim();
            }

            CApp.UI.lvExchanges.AddUserColumn(txtTitle.Text, (int)udWidth.Value, sSource);
            txtName.Text = txtTitle.Text = string.Empty;
            sAutoTitle = null;
            if (IsFieldListSource)
            {
                cbxField_SelectedIndexChanged(cbxField, System.EventArgs.Empty);
                cbxField.Focus();
            }
            else
            {
                txtName.Focus();
            }
            udWidth.Value = 60;
        }

        private void UpdateAddEnabled()
        {
            bool bHaveField = IsFieldListSource
                ? cbxField.SelectedItem != null
                : !string.IsNullOrWhiteSpace(txtName.Text.RemoveAllWhitespace());
            btnAdd.Enabled = bHaveField && !string.IsNullOrWhiteSpace(txtTitle.Text.Trim());
        }

        private void txtName_TextChanged(object sender, System.EventArgs e)
        {
            UpdateAddEnabled();
        }

        private void txtTitle_TextChanged(object sender, System.EventArgs e)
        {
            UpdateAddEnabled();
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
            bool bList = IsFieldListSource;
            cbxField.Visible = bList;
            txtName.Visible = !bList;
            if (bList)
            {
                IReadOnlyList<ExchangeColumnSources.Source> sources = cbxSource.SelectedIndex == SourceTimers
                    ? ExchangeColumnSources.Timers : ExchangeColumnSources.Calculated;
                cbxField.BeginUpdate();
                cbxField.Items.Clear();
                foreach (ExchangeColumnSources.Source source in sources) cbxField.Items.Add(source);
                cbxField.EndUpdate();
                cbxField.SelectedIndex = 0;
            }
            else if (txtTitle.Text == sAutoTitle)
            {
                txtTitle.Text = string.Empty;
            }
            UpdateAddEnabled();

            if (cbxSource.SelectedIndex == SourceRequestHeaders)
            {
                txtName.AutoCompleteCustomSource = acscRequestHeaderNames;
                txtName.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                txtName.AutoCompleteSource = AutoCompleteSource.CustomSource;
            }
            else if (cbxSource.SelectedIndex == SourceResponseHeaders)
            {
                txtName.AutoCompleteCustomSource = acscResponseHeaderNames;
                txtName.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                txtName.AutoCompleteSource = AutoCompleteSource.CustomSource;
            }
            else if (cbxSource.SelectedIndex == SourceExchangeFlags)
            {
                txtName.AutoCompleteCustomSource = acscExchangeFlags;
                txtName.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                txtName.AutoCompleteSource = AutoCompleteSource.CustomSource;
            }
        }

        private void cbxField_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            if (!(cbxField.SelectedItem is ExchangeColumnSources.Source source)) return;
            ttField.SetToolTip(cbxField, source.Description);
            // Suggest the field's title, unless the user has typed their own.
            if (!txtTitle.Text.HasText() || txtTitle.Text == sAutoTitle)
            {
                txtTitle.Text = source.DefaultTitle;
            }
            sAutoTitle = source.DefaultTitle;
            UpdateAddEnabled();
        }
    }
}
