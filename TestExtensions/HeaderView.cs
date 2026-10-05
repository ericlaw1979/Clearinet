using Clearinet;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace TestExtensions
{
    public partial class HeaderView : UserControl
    {
        InspectorBase _owner;
        bool _readonly;
        internal HTTPHeaders _headers;

        internal bool bReadOnly
        {
            get => _readonly;
            set {
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

        public void Clear()
        {
            txtFirstLine.Clear();
            tvNVP.Nodes.Clear();
        }

        private void mnuNodes_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool bHasSelNode = HasHeaderNodeSelected();
            miHighlightHeader.Enabled = miCopyHeader.Enabled = miCopyHeaderValue.Enabled = miSendToTextWizard.Enabled 
                = miLookupHeader.Enabled = bHasSelNode;
            miCopyAll.Enabled = tvNVP.Nodes.Count > 0;

            miAddHeader.Enabled = _readonly;
            miEditHeader.Enabled = !_readonly && bHasSelNode;           // TODO: We should have a "View Full Header" for readonly mode to handle the case where the node value is >256 characters
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
            tvNVP.SelectedNode = tvNVP.Nodes[0];
        }
    }
}
