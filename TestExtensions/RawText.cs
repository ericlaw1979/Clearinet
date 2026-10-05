using Clearinet;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace TestExtensions
{
    public partial class RawText : UserControl
    {
        InspectorBase _ibOwner;

        public RawText(InspectorBase ibOwner)
        {
            _ibOwner = ibOwner;
            InitializeComponent();
            rtbRaw.Font = new Font("Lucida Console", CONFIG.flFontSize);

            rtbRaw.BackColor = CONFIG.colorDisabledEdit;

            Win32UI.SetCueText(txtFind, " Find... (Ctrl+Enter to highlight all)");
        }

        public void Clear()
        {
            rtbRaw.Clear();
            rtbRaw.ReadOnly = true;
        }

        private void SetWordWrapping(bool bWrap)
        {
            // Ensure that toggling Wordwrap doesn't dirty the control.
            bool bWasModified = rtbRaw.Modified;
            rtbRaw.WordWrap = bWrap;
            if (rtbRaw.Modified != bWasModified)
            {
                rtbRaw.Modified = bWasModified;
            }

            // TODO:Store in pref
        }

        private void rtbRaw_ReadOnlyChanged(object sender, EventArgs e)
        {
            rtbRaw.BackColor = rtbRaw.ReadOnly ? CONFIG.colorDisabledEdit : Color.FromKnownColor(KnownColor.Window);
        }

        private void txtFind_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.F3:    // Find first or next
                case Keys.Enter:
                    e.Handled = e.SuppressKeyPress = true;
                    rtbRaw.Search(txtFind.Text, Control.ModifierKeys.HasFlag(Keys.Control));
                    break;

                case Keys.Escape:
                    e.Handled = e.SuppressKeyPress = true;
                    txtFind.Clear();
                    break;

                case Keys.Down:
                    e.Handled = e.SuppressKeyPress = true;
                    rtbRaw.ScrollDown();
                    break;

                case Keys.Up:
                    e.Handled = e.SuppressKeyPress = true;
                    rtbRaw.ScrollUp();
                    break;

            }
        }
        private void txtFind_TextChanged(object sender, EventArgs e)
        {
            if (txtFind.TextLength < 1)
            {  
                txtFind.BackColor = Color.FromKnownColor(KnownColor.Window);
                rtbRaw.Select(0, 0);  // Ensure next earch stops from the top.
                return;
            }

            // Start at 0 if using TAB key for GoNext
            txtFind.BackColor = (rtbRaw.Find(txtFind.Text, 0, RichTextBoxFinds.None) > -1) ?
                Color.LightGreen :
                Color.OrangeRed;
        }

        private void mnuContext_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            miSendToTextWizard.Enabled = miCopy.Enabled = miSendToTextWizard.Enabled = (rtbRaw.SelectionLength > 0);
            miCut.Enabled = !rtbRaw.ReadOnly && (rtbRaw.SelectionLength > 0);
            miPaste.Enabled = !rtbRaw.ReadOnly && Clipboard.ContainsText();
        }

        private void miCopy_Click(object sender, EventArgs e)
        {
            rtbRaw.Copy();
        }

        private void miPaste_Click(object sender, EventArgs e)
        {
            if (rtbRaw.ReadOnly) return;
            
            // Ensure plaintext paste without formatting.
            rtbRaw.Paste(DataFormats.GetFormat(DataFormats.Text));
        }

        private void miCut_Click(object sender, EventArgs e)
        {
            rtbRaw.Cut();
        }

        private void rtbRaw_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F3)
            {
                txtFind.Focus();
                e.Handled = e.SuppressKeyPress = true;
            }
        }

        private void rtbRaw_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            Utilities.LaunchHyperlink(e.LinkText);
        }

        private void miWordwrap_Click(object sender, EventArgs e)
        {
            this.SetWordWrapping(miWordwrap.Checked);
        }

        private void miSendToTextWizard_Click(object sender, EventArgs e)
        {
            CApp.UI.actShowTextWizard(rtbRaw.SelectedText);
        }
    }
}
