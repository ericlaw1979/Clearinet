using System;
using System.Text;
using System.Windows.Forms;

namespace Clearinet.UI
{
    public partial class FindPanel : UserControl
    {
        public FindPanel()
        {
            InitializeComponent();
            txtToFind.Text = CApp.Prefs.GetStringPref("app.find.lastfind.text.ephemeral", string.Empty);
            Win32UI.SetCueText(txtToFind, "Plaintext to find, or REGEX:");
            ccbxWhere.CheckItems.AddRange(new object[] {
                "URLs", "ExchangeListbox Columns", "Request Headers", "Request Body", "Response Headers", "Response Body"});
            // cbxFindIn.SelectedIndex = CApp.Prefs.GetInt32Pref("app.find.lastfind.findin", 0);
            // cbxWhichComponent.SelectedIndex = CApp.Prefs.GetInt32Pref("app.find.lastfind.whichcomponent", 0);
            ccbxWhere.CheckAll();
            cbSelectMatches.Checked = CApp.Prefs.GetBoolPref("app.find.lastfind.selectmatches", true);
        }

        private void _MarkFound(Exchange exch, bool bSelectMatches)
        {
            exch.ViewItem.BackColor = CONFIG.colorHighlight;
            if (bSelectMatches) exch.ViewItem.Selected = true;
        }

        private void ExecuteFind()
        {
            try
            {
                string sSearchFor = txtToFind.Text; // trim?
                CApp.Prefs.SetStringPref("app.find.lastfind.text.ephemeral", sSearchFor);
                CApp.Prefs.SetStringPref("app.find.lastfind.findin", string.Join(";", ccbxWhere.GetCheckedItems()));

                bool bSelectMatches = cbSelectMatches.Checked;
                CApp.Prefs.SetBoolPref("app.find.lastfind.selectmatches", bSelectMatches);

                bool bRequestHeaders = ccbxWhere.GetCheckedItems().Contains("Request Headers");
                bool bRequestBody = ccbxWhere.GetCheckedItems().Contains("Request Body");
                bool bResponseHeaders = ccbxWhere.GetCheckedItems().Contains("Response Headers");
                bool bResponseBody = ccbxWhere.GetCheckedItems().Contains("Response Body");
                bool bURLs = ccbxWhere.GetCheckedItems().Contains("URLs");
                bool bExchangeListboxColumns = ccbxWhere.GetCheckedItems().Contains("ExchangeListbox Columns");

                if (!bRequestHeaders && !bRequestBody && !bResponseHeaders && !bResponseBody && !bURLs && !bExchangeListboxColumns)
                {
                    CApp.UI.SetStatusText("Nothing to search; please select at least one search target.");
                    return;
                }

                bool bDecode = cbDecode.Checked;
                if (bSelectMatches) CApp.UI.lvExchanges.SelectedItems.Clear();
                var iMatchCount = 0;
                foreach (Exchange exch in CApp.UI.GetAllExchanges())
                {
                    if (bURLs && exch.fullUrl.OICContains(sSearchFor))
                    {
                        ++iMatchCount;
                        _MarkFound(exch, bSelectMatches);
                        continue;
                    }
                    if (bExchangeListboxColumns)
                    {
                        string sAllCols = _GetAllColumnText(exch.ViewItem);

                        if (sAllCols.OICContains(sSearchFor))
                        {
                            ++iMatchCount;
                            _MarkFound(exch, bSelectMatches);
                            continue;
                        }
                    }
                    if (bRequestHeaders && true == exch.RequestHeaders?.ToString().OICContains(sSearchFor))
                    {
                        ++iMatchCount;
                        _MarkFound(exch, bSelectMatches);
                        continue;
                    }
                    if (bResponseHeaders && true == exch.ResponseHeaders?.ToString().OICContains(sSearchFor))
                    {
                        ++iMatchCount;
                        _MarkFound(exch, bSelectMatches);
                        continue;
                    }
                    if (bRequestBody && exch.RequestBody.HasData())
                    {
                        ++iMatchCount;
                        string sBody = cbDecode.Checked ? exch.GetRequestBodyAsString() : Encoding.UTF8.GetString(exch.RequestBody);
                        if (sBody.OICContains(sSearchFor))
                        {
                            _MarkFound(exch, bSelectMatches);
                            continue;
                        }
                    }
                    if (bResponseBody && exch.ResponseBody.HasData())
                    {
                        ++iMatchCount;
                        string sBody = cbDecode.Checked ? exch.GetResponseBodyAsString() : Encoding.UTF8.GetString(exch.ResponseBody);
                        if (sBody.OICContains(sSearchFor))
                        {
                            _MarkFound(exch, bSelectMatches);
                            continue;
                        }
                    }

                    // Not found; unmark the item.
                    exch.ViewItem.BackColor = CApp.UI.lvExchanges.BackColor;
                }
                if (iMatchCount > 0 && bSelectMatches)
                {
                    CApp.UI.lvExchanges.EnsureVisible(CApp.UI.lvExchanges.SelectedIndices[0]);
                    CApp.UI.lvExchanges.Focus();
                }
                CApp.UI.SetStatusText($"Found {iMatchCount} Exchanges matching search criteria.");
            }
            catch (Exception ex)
            {
                CApp.ReportException(ex, "Search failed...");
            }
        }

        private string _GetAllColumnText(ListViewItem viewItem)
        {
            StringBuilder sbAllText = new StringBuilder();
            sbAllText.Append(viewItem.Text).Append(";");
            foreach (ListViewItem.ListViewSubItem subItem in viewItem.SubItems)
            {
                sbAllText.Append(subItem.Text).Append(";");
            }
            return sbAllText.ToString();
        }

        private void txtToFind_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ExecuteFind();
                e.Handled = e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Escape)
            {
                if (txtToFind.Text.Length > 0)
                {
                    txtToFind.Text = string.Empty;
                }
                else
                {
                    btnFindClose.PerformClick();
                }
                e.Handled = e.SuppressKeyPress = true;
                return;
            }
        }

        private void btnFindClose_Click(object sender, EventArgs e)
        {
            CApp.oFindPanel.Parent.Visible = false;
            CApp.oFindPanel.Parent.Controls.Remove(CApp.oFindPanel);
            CApp.oFindPanel.Dispose();
            CApp.oFindPanel = null;
        }
    }
}
