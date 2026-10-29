using System;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Clearinet.UI
{
    public partial class FindPanel : UserControl
    {
        public FindPanel()
        {
            InitializeComponent();
            txtToFind.Text = CApp.Prefs.GetStringPref("app.find.lastfind.text.ephemeral", string.Empty);
            Win32UI.SetCueText(txtToFind, "Plaintext to find, EXACT: or REGEX:");
            ccbxWhere.CheckItems.AddRange(new object[] {
                "URLs", "ExchangeListbox Columns", "Request Headers", "Request Body", "Response Headers", "Response Body"});
            // TODO: Restore last-find targets from prefs, if any. For now, just check all.
            ccbxWhere.CheckAll();
            cbSelectMatches.Checked = CApp.Prefs.GetBoolPref("app.find.lastfind.selectmatches", true);
            cbDecode.Checked = CApp.Prefs.GetBoolPref("app.find.lastfind.decodebodies", true);
            cbSearchBinaries.Checked = CApp.Prefs.GetBoolPref("app.find.lastfind.searchbinaries", false);
            dudColor.SelectedItem = dudColor.Items[0]; //CApp.Prefs.GetStringPref("app.find.lastfind.highlightcolor", "Yellow");
                                                       //   dudColor.BackColor = Utilities.ParseColor(dudColor.SelectedItem.ToString());
        }

        private void _MarkFound(Exchange exch, bool bSelectMatches)
        {
            exch.ViewItem.BackColor = colorSearchResult;
            exch["ui-backcolor"] = ColorTranslator.ToHtml(colorSearchResult);
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

                Func<string, bool> isMatch;
                if (sSearchFor.OICStartsWith("exact:"))
                {
                    sSearchFor = sSearchFor.Substring(6);
                    isMatch = text => text.IndexOf(sSearchFor, StringComparison.Ordinal) >= 0;
                }
                else if (sSearchFor.OICStartsWith("regex:"))
                {
                    var regex = new Regex(sSearchFor.Substring(6), RegexOptions.None, TimeSpan.FromSeconds(1));
                    isMatch = regex.IsMatch;
                }
                else
                {
                    isMatch = text => text.OICContains(sSearchFor);
                }

                bool bDecode = cbDecode.Checked;
                if (bSelectMatches) CApp.UI.lvExchanges.SelectedItems.Clear();
                var iMatchCount = 0;
                foreach (Exchange xc in CApp.UI.GetAllExchanges())
                {
                    if (bURLs && isMatch(xc.fullUrl))
                    {
                        ++iMatchCount;
                        _MarkFound(xc, bSelectMatches);
                        continue;
                    }
                    if (bExchangeListboxColumns)
                    {
                        string sAllCols = _GetAllColumnText(xc.ViewItem);

                        if (isMatch(sAllCols))
                        {
                            ++iMatchCount;
                            _MarkFound(xc, bSelectMatches);
                            continue;
                        }
                    }
                    if (bRequestHeaders && xc.RequestHeaders != null && isMatch(xc.RequestHeaders.ToString()))
                    {
                        ++iMatchCount;
                        _MarkFound(xc, bSelectMatches);
                        continue;
                    }
                    if (bResponseHeaders && xc.ResponseHeaders != null && isMatch(xc.ResponseHeaders.ToString()))
                    {
                        ++iMatchCount;
                        _MarkFound(xc, bSelectMatches);
                        continue;
                    }
                    if (bRequestBody && xc.RequestBody.HasData())
                    {
                        string sBody = cbDecode.Checked ? xc.GetRequestBodyAsString() : Encoding.UTF8.GetString(xc.RequestBody);
                        if (isMatch(sBody))
                        {
                            ++iMatchCount;
                            _MarkFound(xc, bSelectMatches);
                            continue;
                        }
                    }
                    if (bResponseBody && xc.ResponseBody.HasData())
                    {
                        bool bResponseIsBinary = Utilities.IsMediaTypeBinary(xc.MediaType);
                        if (!bResponseIsBinary || cbSearchBinaries.Checked)
                        {
                            string sBody = cbDecode.Checked ? xc.GetResponseBodyAsString() : Encoding.UTF8.GetString(xc.ResponseBody);
                            if (isMatch(sBody))
                            {
                                ++iMatchCount;
                                _MarkFound(xc, bSelectMatches);
                                continue;
                            }
                        }
                    }

                    // Not found; unmark the item.
                    xc.ViewItem.BackColor = CApp.UI.lvExchanges.BackColor;
                }
                if (iMatchCount > 0 && bSelectMatches)
                {
                    Debug.Assert(CApp.UI.lvExchanges.SelectedCount > 0);
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

        private void dudColor_SelectedItemChanged(object sender, EventArgs e)
        {
            if (dudColor.SelectedItem != null)
            {
                CApp.Prefs.SetStringPref("app.find.lastfind.highlightcolor", dudColor.SelectedItem.ToString());
                dudColor.BackColor = Utilities.ParseColor(dudColor.SelectedItem.ToString());
            }
        }

        public Color colorSearchResult
        {
            get { return dudColor.BackColor; }
        }
    }
}
