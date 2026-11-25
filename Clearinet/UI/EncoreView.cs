using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace Clearinet.UI
{
    public partial class EncoreView : UserControl
    {
        // The list of Rules.
        private ReaderWriterLockSlim _rwlRuleLock = new ReaderWriterLockSlim();
        private List<EncoreRule> _listRules = new List<EncoreRule>();

        public EncoreView()
        {
            InitializeComponent();
            bool bEnabled = CApp.Prefs.GetBoolPref("app.encore.enabled");
            var bLatencyEnabled = CApp.Prefs.GetBoolPref("app.encore.latency_enabled");

            cbEnableEncore.Checked = bEnabled;
            cbEnableLatency.Checked = bLatencyEnabled;
        }

        private void lvRules_DragDrop(object sender, DragEventArgs e)
        {
            // if there are Clearinet.Exchange[] elements on the clipboard, generate a rule for each.
            if (e.Data.GetDataPresent(typeof(Clearinet.Exchange[])))
            {
                var XCs = (Clearinet.Exchange[])e.Data.GetData(typeof(Clearinet.Exchange[]));
                foreach (var xc in XCs)
                {
                    CreateRuleForExchange(xc);
                }
            }
        }

        private void lvRules_DragEnter(object sender, DragEventArgs e)
        {
            // TODO: Add plain file drops too.
            if (e.Data.GetDataPresent(typeof(Clearinet.Exchange[])))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void cbEnableEncore_CheckedChanged(object sender, System.EventArgs e)
        {
            var bEnabled = cbEnableEncore.Checked;
            CApp.Prefs.SetBoolPref("app.encore.enabled", bEnabled);
            gbRuleEditor.Enabled = bEnabled;
        }

        private void btnImportForPlayback_Click(object sender, System.EventArgs e)
        {
            var sFilenames = Utilities.ObtainOpenFilenames("Open SAZ", "SAZ Files (*.saz)|*.saz");
            if ((sFilenames?.Length ?? 0) == 0) return;
            foreach (var sFilename in sFilenames)
            {
                actGenerateRulesForSAZ(sFilename);
            }
        }

        private void btnAddRule_Click(object sender, EventArgs e)
        {
            var item = lvRules.Items.Add("https://example.com/");
            item.SubItems.Add($"your action here");
        }

        private void actGenerateRulesForSAZ(string sFilename)
        {
            // Load the SAZ file and generate rules for each exchange.
            try
            {
                var saz = SAZFile.LoadFrom(sFilename);
                foreach (var xc in saz.Exchanges)
                {
                    CreateRuleForExchange(xc);
                }
            }
            catch (Exception ex)
            {
                CApp.ReportException(ex, $"Failed to load SAZ file {sFilename}");
            }
        }

        private void CreateRuleForExchange(Exchange xc)
        {
            var ER = new EncoreRule();
            ER._sMatch = xc.fullUrl;
            // Display-text only. No impact on actual response.
            ER._sAction = $"Return HTTP/{xc.ResponseHeaders.StatusCode} ({xc?.ResponseBody?.Length ?? 0} bytes)";
            ER._headersResponse = xc.ResponseHeaders.Clone();
            ER._arrResponseBody = xc.ResponseBody.FastClone();
            UpdateRuleViewItem(ER);
        }

        private void btnGroup_Click(object sender, EventArgs e)
        {

        }

        private void UpdateRuleEditor()
        {
            if (lvRules.SelectedItems.Count == 0)
            {
                cbxMatchCondition.Text = cbxResponseAction.Text = string.Empty;
                gbRuleEditor.Enabled = false;
                return;
            }
            ListViewItem oLVI = lvRules.SelectedItems[0];
            cbxMatchCondition.Text = oLVI.Text;
            cbxResponseAction.Text = oLVI.SubItems[1].Text;
            gbRuleEditor.Enabled = cbEnableEncore.Checked;
        }

        private void lvRules_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateRuleEditor();
        }

        private void lvRules_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                foreach (ListViewItem item in lvRules.SelectedItems)
                {
                    lvRules.Items.Remove(item);
                }
                e.Handled = e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyData == (Keys.X | Keys.Control))
            {
                lvRules.Items.Clear();
                UpdateRuleEditor();
                e.Handled = e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyData == Keys.OemMinus)
            {
                // Promote rule
                e.Handled = e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyData == Keys.Oemplus)
            {
                // Demote rule
                e.Handled = e.SuppressKeyPress = true;
                return;
            }
        }

        private void cbEnableLatency_CheckedChanged(object sender, EventArgs e)
        {
            var bLatencyEnabled = cbEnableLatency.Checked;
            CApp.Prefs.SetBoolPref("app.encore.latency_enabled", bLatencyEnabled);
            lvRules.Columns[2].Width = bLatencyEnabled ? 100 : 0;
        }

        internal void UpdateRuleViewItem(EncoreRule ER)
        {
            if (null == ER.ViewItem)
            {
                var item = lvRules.Items.Add(ER._sMatch);
                item.Checked = ER.IsEnabled;
                item.SubItems.Add(ER._sAction);
                item.SubItems.Add(ER._msLatency.ToString());
                item.SubItems.Add(ER.sRuleComment);
                item.Tag = ER;
            }
        }
    }



    internal class EncoreRule
    {
        // Is the rule enabled?
        internal bool IsEnabled { get; set; } = true;

        // Is the rule a match?
        internal string _sMatch;

        // What should happen?
        internal string _sAction;

        // Should action/response be delayed?
        internal int _msLatency;

        // Should the rule be disabled after a match?
        internal bool _bDisableAfterMatch;

        // The response body to return for this rule.
        internal byte[] _arrResponseBody;

        // The headers to return for this rule.
        internal HTTPResponseHeaders _headersResponse;

        // The ListViewItem associated with this rule in the EncoreView.
        internal ListViewItem ViewItem;

        // The comment for this rule, if any. 
        internal string sRuleComment
        {
            get;
            set;
        }

        // Does the ACTION refer to a response that we should just return? If so, then the sAction text
        // is just for the user to read, but the actual Action is "Return _headersResponse and _arrResponseBody".
        internal bool hasResponse
        {
            get
            {
                return (_headersResponse != null);
            }
        }
    }
}
