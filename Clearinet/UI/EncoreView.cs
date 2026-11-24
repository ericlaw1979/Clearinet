using System;
using System.Windows.Forms;

namespace Clearinet.UI
{
    public partial class EncoreView : UserControl
    {
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
                    var item = lvRules.Items.Add(xc.fullUrl);
                    item.SubItems.Add($"Exchange #{xc.id}");
                    item.Tag = xc;
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
                    var item = lvRules.Items.Add(xc.fullUrl);
                    item.SubItems.Add($"Exchange #{xc.id}");
                    item.Tag = xc;
                }
            }
            catch (Exception ex)
            {
                CApp.ReportException(ex, $"Failed to load SAZ file {sFilename}");
            }
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
        }

        private void cbEnableLatency_CheckedChanged(object sender, EventArgs e)
        {
            var bLatencyEnabled = cbEnableLatency.Checked;
            CApp.Prefs.SetBoolPref("app.encore.latency_enabled", bLatencyEnabled);
            lvRules.Columns[2].Width = bLatencyEnabled ? 100 : 0;
        }
    }
}
