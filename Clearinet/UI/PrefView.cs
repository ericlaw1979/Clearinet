using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;

namespace Clearinet.UI
{
    public partial class PrefView : UserControl
    {
        static TabPage _myTabPage = null;
        static TabControl _myParent = null;

        public static void CreatePrefsTab(TabControl tabControl)
        {
            if (_myTabPage == null)
            {
                _myParent = tabControl;
                PrefView prefView = new PrefView();
                _myTabPage = new TabPage("about:config");
                _myTabPage.Controls.Add(prefView);
                prefView.Dock = DockStyle.Fill;
                tabControl.TabPages.Add(_myTabPage);
                prefView.dgvPrefs.AlternatingRowsDefaultCellStyle.BackColor = CONFIG.colorDisabledEdit;
                if (CONFIG.isViewerMode)
                {
                    prefView.lblInstructions.ForeColor = System.Drawing.Color.Red;
                    prefView.lblInstructions.Text = "Note: In Viewers, changes are discarded at exit.";
                }
                CApp.Prefs.AddWatcher(null, (s, ea) =>
                {
                    // Note: If CApp.isClosing, the callback will not run.
                    CApp.UIInvokeAsync(() =>
                    {
                        // Find the row for the pref and update it (or remove it)
                        foreach (DataGridViewRow dgvr in prefView.dgvPrefs.Rows)
                        {
                            string sRowName = dgvr.Cells[0].Value?.ToString() ?? String.Empty;
                            if (sRowName == ea.PrefName)
                            {
                                if (null == ea.ValueString)
                                {
                                    prefView.dgvPrefs.Rows.Remove(dgvr);
                                    return;
                                }

                                dgvr.Cells[1].Value = ea.ValueString;
                                return;
                            }
                        }

                        // if we got here, the pref wasn't found in the grid, so add it if it's not null
                        if (null != ea.ValueString)
                        {
                            prefView.dgvPrefs.Rows.Add(ea.PrefName, ea.ValueString);
                            prefView.dgvPrefs.Sort(prefView.dgvPrefs.Columns[0], ListSortDirection.Ascending);
                        }
                    });
                });
                prefView.RefillWholeGrid();
            }
            _myParent.SelectedTab = _myTabPage;
        }

        public PrefView()
        {
            InitializeComponent();
        }

        public void RefillWholeGrid()
        {
            dgvPrefs.SuspendLayout();
            dgvPrefs.Rows.Clear();
            foreach (var sPrefName in (CApp.Prefs as PreferenceBag).GetPrefArray())
            {
                dgvPrefs.Rows.Add(sPrefName, CApp.Prefs[sPrefName]);
            }
            dgvPrefs.Sort(dgvPrefs.Columns[0], ListSortDirection.Ascending);
            dgvPrefs.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
            dgvPrefs.ResumeLayout();
        }

        private void dgvPrefs_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            string sPref = dgvPrefs.Rows[e.RowIndex].Cells[0].Value?.ToString()?.Trim() ?? string.Empty;
            if (!sPref.HasText()) return;

            string sValue = dgvPrefs.Rows[e.RowIndex].Cells[1].Value?.ToString()?.Trim() ?? string.Empty;

            if (!CApp.Prefs.isValidName(sPref))
            {
                MessageBox.Show($"Preference name `{sPref}` is not valid. It cannot contain spaces or any of the following characters: * $ % @ ? !", "Invalid Preference Name", MessageBoxButtons.OK, MessageBoxIcon.Error);
                dgvPrefs.Rows.RemoveAt(e.RowIndex);

                return;
            }

            CApp.Prefs.SetStringPref(sPref, sValue);

            // If the user edited the last row, sort the grid so that the new pref is in the right place.
            // BUG: We could end up with a duplicate of the row if user creates a new row with the same
            // pref name as an existing row.

            // BUG: Trying to do this here can throw with a reentrancy exception. We'll need to clean
            // this up at some point, but we also don't want the whole row jumping around right when
            // the user tries to edit the value. So leave it alone for now.
            /*if (e.RowIndex >= dgvPrefs.Rows.Count - 2)
            {
                dgvPrefs.Sort(dgvPrefs.Columns[0], ListSortDirection.Ascending);
            }*/
            CApp.UI.SetStatusText($"Preference `{sPref}` set to `{sValue}`");
        }

        private void dgvPrefs_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            // Allowing rename of preferences is tricky because we'd have to keep track of the
            // old name and remove it from the preference bag if the new name is valid.
            if ((e.ColumnIndex == 0) &&
                (null != dgvPrefs.Rows[e.RowIndex].Cells[0].Value))
            {
                e.Cancel = true;
            }
        }

        private void dgvPrefs_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            var sPrefName = e.Row?.Cells?[0].Value?.ToString();
            if (null == sPrefName) { Debug.Assert(false, "How'd you do that??"); return; }

            // TODO: Should we check if the pref has no value? Should we check if the pref is "internal"?

            if (DialogResult.No == MessageBox.Show($"Are you sure you want to delete the preference `{sPrefName}`?",
                "Delete Preference", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2))
            {
                e.Cancel = true;
                return;
            }
            CApp.Prefs.RemovePref(sPrefName);
            CApp.UI.SetStatusText($"Preference `{sPrefName}` removed.");
        }

        private void lnkPrefHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Utilities.LaunchHyperlink(CONFIG.GetURL("HelpPrefs"));
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Handle F5 here to prevent broader app's hotkey from eating it
            if (keyData == Keys.F5)
            {
                RefillWholeGrid();
                CApp.UI.SetStatusText("Preferences refreshed.");
                // Return true to mark the key press as handled, 
                // preventing ToolStripMenuItem shortcut processing
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private static readonly char[] ForbiddenChars = new[] { '*', ' ', '$', '%', '@', '?', '!' };
        private void dgvPrefs_CellParsing(object sender, DataGridViewCellParsingEventArgs e)
        {
            if (e.ColumnIndex == 0 && e.Value is string rawInput)
            {
                // Safely strip the characters and commit the parsed result to the cell
                string cleaned = string.Join("", rawInput.Trim().Split(ForbiddenChars, StringSplitOptions.RemoveEmptyEntries));
                e.Value = cleaned;
                e.ParsingApplied = true; // Prevents default conversion
            }
        }

        private void lnkTearoff_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Open a new window with the same PrefView control
            Form frmPrefs = new Form
            {
                Text = "Preferences",
                Size = new System.Drawing.Size(600, 400),
                ShowInTaskbar = false
            };
            lnkTearoff.Visible = false;
            frmPrefs.Controls.Add(this);
            this.Dock = DockStyle.Fill;
            _myParent.TabPages.Remove(_myTabPage);
            frmPrefs.FormClosing += (s, args) =>
            {
                // When the new form is closed, re-add this control back to the original tab page
                _myTabPage.Controls.Add(this);
                lnkTearoff.Visible = true;
                this.Dock = DockStyle.Fill;
                _myParent.TabPages.Add(_myTabPage);
                _myParent.SelectedTab = _myTabPage;
            };
            frmPrefs.Show();
        }
    }
}
