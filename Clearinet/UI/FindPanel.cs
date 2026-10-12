using System;
using System.Drawing;
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
            exch.ViewItem.BackColor = Color.Yellow;
            if (bSelectMatches) exch.ViewItem.Selected = true;
        }

        private void btnFind_Click(object sender, EventArgs e)
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

            foreach (Exchange exch in CApp.UI.GetAllExchanges())
            {
                if (bURLs && exch.fullUrl.OICContains(sSearchFor))
                {
                    _MarkFound(exch, bSelectMatches);
                    continue;
                }
                if (bExchangeListboxColumns)
                {
                    string sAllCols = _GetAllColumnText(exch.ViewItem);

                    if (sAllCols.OICContains(sSearchFor))
                    {
                        _MarkFound(exch, bSelectMatches);
                        continue;
                    }
                }
                if (bRequestHeaders && exch.RequestHeaders?.ToString().OICContains(sSearchFor) == true)
                {
                    _MarkFound(exch, bSelectMatches);
                    continue;
                }
                if (bResponseHeaders && exch.ResponseHeaders?.ToString().OICContains(sSearchFor) == true)
                {
                    _MarkFound(exch, bSelectMatches);
                    continue;
                }
                if (bRequestBody)
                {
                    string sBody = exch.GetRequestBodyAsString();  // TODO: Decode or not
                    {
                        _MarkFound(exch, bSelectMatches);
                        continue;
                    }
                }
                if (bResponseBody)
                {
                    string sBody = exch.GetResponseBodyAsString();  // TODO: Decode or not
                    {
                        _MarkFound(exch, bSelectMatches);
                        continue;
                    }
                }

                // Not found; unmark the item.
                exch.ViewItem.BackColor = CApp.UI.lvExchanges.BackColor;
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
                btnFind_Click(sender, e);
                e.Handled = e.SuppressKeyPress = true;
            }
            if (e.KeyCode == Keys.Escape)
            {
                if (txtToFind.Text.Length > 0)
                {
                    txtToFind.Text = string.Empty;
                }
                else
                {
                    //CApp.UI.HideFindPanel();
                }
                e.Handled = e.SuppressKeyPress = true;
            }
        }
    }
}
