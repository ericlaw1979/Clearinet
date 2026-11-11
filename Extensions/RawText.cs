using Clearinet;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Extensions
{
    internal sealed class RawRichTextBox : RichTextBoxV5
    {
        [ComImport, Guid("8CC497C0-A1DF-11CE-8098-00AA0047BE5D"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
        private interface ITextDocument
        {
            string Name { get; }
            object Selection { [return: MarshalAs(UnmanagedType.Interface)] get; }
            int StoryCount { get; }
            object StoryRanges { [return: MarshalAs(UnmanagedType.Interface)] get; }
            int Saved { get; set; }
            float DefaultTabStop { get; set; }
            void New();
            void Open([MarshalAs(UnmanagedType.Struct)] object value, int flags, int codePage);
            void Save([MarshalAs(UnmanagedType.Struct)] object value, int flags, int codePage);
            int Freeze();
            int Unfreeze();
            void BeginEditCollection();
            void EndEditCollection();
            int Undo(int count);
        }

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wparam, IntPtr lparam);

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr GetOleInterface(IntPtr handle, int message, IntPtr wparam, ref IntPtr ole);

        private int presentationDepth;

        private ITextDocument GetTextDocument()
        {
            IntPtr ole = IntPtr.Zero;
            IntPtr document = IntPtr.Zero;
            try
            {
                if (GetOleInterface(Handle, 0x43c /* EM_GETOLEINTERFACE */, IntPtr.Zero, ref ole) == IntPtr.Zero)
                    throw new InvalidOperationException("Raw RichEdit did not expose its text document.");
                Guid id = typeof(ITextDocument).GUID;
                Marshal.ThrowExceptionForHR(Marshal.QueryInterface(ole, ref id, out document));
                return (ITextDocument)Marshal.GetTypedObjectForIUnknown(document, typeof(ITextDocument));
            }
            finally
            {
                if (document != IntPtr.Zero) Marshal.Release(document);
                if (ole != IntPtr.Zero) Marshal.Release(ole);
            }
        }

        private void ApplyPresentationChange(Action change)
        {
            if (!IsHandleCreated || presentationDepth != 0)
            {
                change();
                return;
            }
            ITextDocument document = GetTextDocument();
            IntPtr mask = SendMessage(Handle, 0x43b /* EM_GETEVENTMASK */, IntPtr.Zero, IntPtr.Zero);
            bool suspended = false;
            try
            {
                // Font/SCF_ALL writes are presentation, but RichEdit otherwise records formatting undo and EN_CHANGE.
                // TOM suspension retains existing text undo/redo; this policy is deliberately local to uniform Raw text.
                document.Undo(-9999995 /* tomSuspend */);
                suspended = true;
                SendMessage(Handle, 0x445 /* EM_SETEVENTMASK */, IntPtr.Zero, new IntPtr(mask.ToInt64() & ~1L /* ENM_CHANGE */));
                presentationDepth++;
                try { change(); }
                finally { presentationDepth--; }
            }
            finally
            {
                try
                {
                    SendMessage(Handle, 0x445 /* EM_SETEVENTMASK */, IntPtr.Zero, mask);
                    if (suspended) document.Undo(-9999994 /* tomResume */);
                }
                finally { Marshal.ReleaseComObject(document); }
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            if (presentationDepth == 0) base.OnTextChanged(e);
        }

        internal void SetLogicalFontSize(float points)
        {
            ApplyPresentationChange(() => Win32UI.SetRichTextFontSize(this, points));
        }

        // Framework writes Font before the before-parent DPI event, so capturing Modified in that event is too late.
        // Raw treats font changes as presentation only; the native helper separately guards its own formatting write.
        public override Font Font
        {
            get => base.Font;
            set
            {
                bool wasModified = Modified;
                try
                {
                    ApplyPresentationChange(() => base.Font = value);
                }
                finally
                {
                    if (Modified != wasModified) Modified = wasModified;
                }
            }
        }
    }

    public partial class RawText : UserControl
    {
        InspectorBase _ibOwner;
        private float logicalFontSize = CONFIG.flFontSize;
        private Font editorFont;
        private Font searchFont;

        public RawText(InspectorBase ibOwner)
        {
            _ibOwner = ibOwner;
            InitializeComponent();
            // Native character sizes can be reset by handle creation or hidden-view activation.
            rtbRaw.HandleCreated += (sender, e) => RefreshNativeFontSize();
            rtbRaw.VisibleChanged += (sender, e) => RefreshNativeFontSize();
            editorFont = new Font("Lucida Console", CONFIG.flFontSize);
            rtbRaw.Font = editorFont;

            rtbRaw.BackColor = CONFIG.colorDisabledEdit;
            rtbRaw.ForeColor = CONFIG.colorDisabledEditForeColor;

            Win32UI.SetCueText(txtFind, " Find... (Ctrl+Enter to highlight all)");
        }

        public bool HasChanges
        {
            get
            {
                return rtbRaw.Modified;
            }
            set
            {
                rtbRaw.Modified = value;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F) && rtbRaw.ContainsFocus)
            {
                txtFind.Focus();
                txtFind.SelectAll();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public void Clear()
        {
            rtbRaw.Clear();
            rtbRaw.ReadOnly = true;
        }
        internal void SetFontSize(float flSizeInPoints)
        {
            // Inspector notifications use managed points; retain logical points for native RichEdit and later DPI moves.
            logicalFontSize = DpiFontUnits.ManagedFontPointsToLogicalFontPoints(flSizeInPoints, DeviceDpi);
            ApplyFontSize(flSizeInPoints);
        }

        private void ApplyFontSize(float flSizeInPoints)
        {
            ApplyOwnedFontSize(rtbRaw, ref editorFont, flSizeInPoints);
            ApplyOwnedFontSize(txtFind, ref searchFont, flSizeInPoints);
            RefreshNativeFontSize();
        }

        private static void ApplyOwnedFontSize(Control control, ref Font ownedFont, float points)
        {
            Font activeFont = control.Font;
            // Framework may replace the active font during scaling. Compare that font, not the owned reference.
            if (activeFont.Unit == GraphicsUnit.Point && activeFont.SizeInPoints == points) return;
            Font candidate = new Font(activeFont.FontFamily, points, activeFont.Style);
            if (activeFont.Equals(candidate))
            {
                candidate.Dispose();
                return;
            }
            try
            {
                control.Font = candidate;
            }
            catch
            {
                candidate.Dispose();
                throw;
            }
            Font superseded = ownedFont;
            ownedFont = candidate;
            superseded?.Dispose();
        }

        private void DisposeOwnedFonts()
        {
            editorFont?.Dispose();
            searchFont?.Dispose();
            editorFont = searchFont = null;
        }

        internal void RefreshNativeFontSize()
        {
            ((RawRichTextBox)rtbRaw).SetLogicalFontSize(logicalFontSize);
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            // Framework scales the inherited font without updating Font-mode metadata; prevent a second font scaling pass.
            AutoScaleDimensions = CurrentAutoScaleDimensions;
            RefreshNativeFontSize();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && !Disposing && !IsDisposed && rtbRaw != null)
            {
                // Reapply explicit fonts only after normal Framework visibility/DPI scaling; doing so earlier compounds scaling.
                AutoScaleDimensions = CurrentAutoScaleDimensions;
                ApplyFontSize(DpiFontUnits.LogicalFontPointsToManagedFontPoints(logicalFontSize, DeviceDpi));
            }
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
                txtFind.SelectAll();
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
