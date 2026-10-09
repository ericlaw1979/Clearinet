using Clearinet;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace TestExtensions
{
    public partial class HeaderView : UserControl
    {
        InspectorBase _owner;
        bool _readonly = true;
        internal HTTPHeaders _headers;

        internal bool bReadOnly
        {
            get => _readonly;
            set
            {
                _readonly = value;
                txtFirstLine.ReadOnly = _readonly;
            }
        }
        public HeaderView(InspectorBase owner)
        {
            _owner = owner;
            InitializeComponent();
            tvNVP.BackColor = txtFirstLine.BackColor = CONFIG.colorDisabledEdit;
        }

        /// <summary>
        /// Is a non-category node (representing a header item) selected?
        /// </summary>
        private bool HasHeaderNodeSelected() => tvNVP.SelectedNode?.Parent != null;
        private string CurrentHeaderName() => tvNVP.SelectedNode?.Text?.TrimAfter(":");
        private string CurrentHeaderValue() => tvNVP.SelectedNode?.Text?.TrimBefore(" ");

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
            bool bHasSelNode = HasHeaderNodeSelected();
            miHighlightHeader.Enabled = miCopyHeader.Enabled = miCopyHeaderValue.Enabled = miSendToTextWizard.Enabled
                = miLookupHeader.Enabled = bHasSelNode;
            miCopyAll.Enabled = tvNVP.Nodes.Count > 0;

            miAddHeader.Enabled = !_readonly;
            miEditHeader.Enabled = !_readonly && bHasSelNode;    // TODO: We should have a "View Full Header" for readonly mode to handle the case where the node value is >256 characters
            miRemoveHeader.Enabled = !_readonly && bHasSelNode;
            miPasteHeaders.Enabled = !_readonly && Clipboard.ContainsText(); // TODO: And that text is headers
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
            n.BackColor = (n.BackColor == Color.Yellow) ? n.BackColor = tvNVP.BackColor : Color.Yellow;
            tvNVP.SelectedNode = tvNVP.SelectedNode.Parent ?? tvNVP.Nodes[0];
        }

        private void tvNVP_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Space)
            {
                miHighlightHeader_Click(null, null);
                e.Handled = e.SuppressKeyPress = true;
            }
        }

        private void miCopyAll_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(_headers.ToString());
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
            CApp.UI.actShowTextWizard(sValue);
        }

        private void miCopyHeader_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(tvNVP.SelectedNode?.Text);
        }
        private void miCopyHeaderValue_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(CurrentHeaderValue());
        }




    }
}
