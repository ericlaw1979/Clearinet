using Clearinet;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Extensions
{
    public partial class HeaderView : UserControl
    {
        InspectorBase _owner;
        bool _readonly = true;
        internal bool HasChanges { get; set; }

        internal HTTPHeaders _headers;

        internal bool bReadOnly
        {
            get => _readonly;
            set
            {
                _readonly = value;
                txtFirstLine.ReadOnly = _readonly;
                txtFirstLine.BackColor = _readonly ? CONFIG.colorDisabledEdit : SystemColors.Window;
                tvNVP.BackColor = _readonly ? CONFIG.colorDisabledEdit : SystemColors.Window;
            }
        }
        public HeaderView(InspectorBase owner)
        {
            _owner = owner;
            InitializeComponent();
            tvNVP.BackColor = txtFirstLine.BackColor = CONFIG.colorDisabledEdit;
            txtFirstLine.TextChanged += (sender, e) => HasChanges = true;
        }

        /// <summary>
        /// Is a non-category node (representing a header item) selected?
        /// </summary>
        private bool HasHeaderNodeSelected() => tvNVP.SelectedNode?.Parent != null;

        private HTTPHeaderItem CurrentHeader() => tvNVP.SelectedNode?.Tag as HTTPHeaderItem;
        private string CurrentHeaderName() => CurrentHeader()?.Name ?? string.Empty;
        private string CurrentHeaderValue() => CurrentHeader()?.Value ?? string.Empty;

        public void Clear()
        {
            txtFirstLine.Clear();
            tvNVP.Nodes.Clear();
        }
        private void tvNVP_MouseDown(object sender, MouseEventArgs e)
        {
            // If you don't do this, right-clicks don't activate nodes.
            if (MouseButtons.Right == e.Button)
            {
                tvNVP.SelectedNode = tvNVP.GetNodeAt(e.X, e.Y);
            }
        }

        private void mnuNodes_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            miCopyAll.Enabled = tvNVP.Nodes.Count > 0;

            bool bHasSelNode = HasHeaderNodeSelected();
            miHighlightHeader.Enabled = miCopyHeader.Enabled = miCopyHeaderValue.Enabled = miSendToTextWizard.Enabled
                = miLookupHeader.Enabled = miEditHeader.Enabled = bHasSelNode;
            if (bHasSelNode)
            {
                if (_readonly)
                {
                    miEditHeader.Text = "&View Header...";
                }
                else
                {
                    miEditHeader.Text = "&Edit Header...";
                }
            }
            miRemoveHeader.Enabled = !_readonly && bHasSelNode;
            miPasteHeaders.Enabled = !_readonly && Clipboard.ContainsText(); // TODO: And that text also has at least one header
            miAddHeader.Enabled = !_readonly;
        }

        private void AddNewHeader(object sender, EventArgs e)
        {
            if (_readonly) return;
            HasChanges = true;

            // Add container node if it doesn't exist yet.
            if ((tvNVP.Nodes.Count < 1) || (tvNVP.Nodes[0].Text != "Clearinet-Added"))
            {
                TreeNode tnContainer = new TreeNode("Clearinet-Added");
                tnContainer.NodeFont = new Font(tvNVP.Font, FontStyle.Italic);
                tvNVP.Nodes.Insert(0, tnContainer);
            }
            var newHeader = _headers.Add("New-Header", "Value");
            var newNode = tvNVP.Nodes[0].Nodes.Add(newHeader.ToString());

            newNode.Tag = newHeader;
            tvNVP.SelectedNode = newNode;
            ViewOrEditHeader(sender, e);
        }

        private void RemoveHeader(object sender, EventArgs e)
        {
            if (!HasHeaderNodeSelected() || _readonly) return;
            HasChanges = true;
            _headers.RemoveHeader(CurrentHeader());
            tvNVP.SelectedNode.Remove();
        }

        private void ViewOrEditHeader(object sender, EventArgs e)
        {
            if (!HasHeaderNodeSelected()) return;
            using (var editor = new HeaderEditor
            {
                IsReadonly = _readonly,
                Text = _readonly ? "View Header" : "Edit Header",
                txtName = { Text = CurrentHeaderName() },
                txtValue = { Text = CurrentHeaderValue() }
            })
            {
                DialogResult oDR = editor.ShowDialog();

                if (!_readonly && oDR == DialogResult.OK)
                {
                    CurrentHeader().Name = editor.txtName.Text;
                    CurrentHeader().Value = editor.txtValue.Text;
                    tvNVP.SelectedNode.Text = $"{CurrentHeaderName()}: {CurrentHeaderValue()}";
                }
            }
        }

        internal void SetFontSize(float flSizeInPoints)
        {
            txtFirstLine.Font = new Font(txtFirstLine.Font.FontFamily, flSizeInPoints);
            tvNVP.Font = new Font(tvNVP.Font.FontFamily, flSizeInPoints, tvNVP.Font.Style);
        }

        private void miHighlightHeader_Click(object sender, EventArgs e)
        {
            if (!HasHeaderNodeSelected()) return;
            var n = tvNVP.SelectedNode;
            n.BackColor = (n.BackColor == CONFIG.colorHighlight) ? n.BackColor = tvNVP.BackColor : CONFIG.colorHighlight;
            tvNVP.SelectedNode = tvNVP.SelectedNode.Parent ?? tvNVP.Nodes[0];
        }

        private void tvNVP_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Space)
            {
                miHighlightHeader_Click(null, null);
                e.Handled = e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.C && e.Modifiers == Keys.Control)
            {
                PlatformAPI.WriteToClipboard(tvNVP.SelectedNode?.Text);
                e.Handled = e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Delete)
            {
                RemoveHeader(null, null);
                e.Handled = e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Insert)
            {
                AddNewHeader(null, null);
                e.Handled = e.SuppressKeyPress = true;
                return;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Enter) && tvNVP.Focused)
            {
                ViewOrEditHeader(null, null);
                return true; // indicate key was handled.
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void miCopyAll_Click(object sender, EventArgs e)
        {
            PlatformAPI.WriteToClipboard(_headers.ToString());
        }

        private void miLookupHeader_Click(object sender, EventArgs e)
        {
            string sName = CurrentHeaderName();
            Utilities.LaunchHyperlink(CApp.Prefs.GetStringPref("inspector.headers.searchurl",
                   "https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/$W$").Replace("$W$", sName));
        }

        private void miSendToTextWizard_Click(object sender, EventArgs e)
        {
            string sValue = CurrentHeaderValue();
            CApp.UI.actShowTextransform(sValue);
        }

        private void miCopyHeader_Click(object sender, EventArgs e)
        {
            PlatformAPI.WriteToClipboard(tvNVP.SelectedNode?.Text);
        }
        private void miCopyHeaderValue_Click(object sender, EventArgs e)
        {
            PlatformAPI.WriteToClipboard(CurrentHeaderValue());
        }


    }
}
