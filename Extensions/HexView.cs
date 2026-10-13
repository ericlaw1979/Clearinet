using Be.Windows.Forms;
using Clearinet;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Extensions
{
    public sealed class HexView : UserControl
    {
        readonly HexBox hexBox;
        readonly ContextMenuStrip contextMenu;
        readonly ToolStripMenuItem miShowHeaders;
        readonly ToolStripStatusLabel lblOffset;
        byte[] headerBytes = Array.Empty<byte>();
        byte[] bodyBytes = Array.Empty<byte>();
        Font hexFont;

        public HexView()
        {
            contextMenu = new ContextMenuStrip();
            var miCopy = new ToolStripMenuItem("&Copy");
            miCopy.Click += (sender, e) => hexBox.Copy();
            var miSelectAll = new ToolStripMenuItem("Select &All");
            miSelectAll.Click += (sender, e) => hexBox.SelectAll();
            miShowHeaders = new ToolStripMenuItem("Show &Headers") { CheckOnClick = true };
            miShowHeaders.CheckedChanged += (sender, e) => RefreshBytes();
            contextMenu.Items.AddRange(new ToolStripItem[]
            {
                miCopy, miSelectAll, new ToolStripSeparator(), miShowHeaders
            });
            contextMenu.Opening += (sender, e) =>
            {
                miCopy.Enabled = hexBox.CanCopy();
                miSelectAll.Enabled = hexBox.CanSelectAll();
            };

            hexFont = new Font("Lucida Console", CONFIG.flFontSize);
            hexBox = new HexBox
            {
                Dock = DockStyle.Fill,
                Font = hexFont,
                ReadOnly = true,
                BackColor = CONFIG.colorDisabledEdit,
                BytesPerLine = 16,
                UseFixedBytesPerLine = false,
                LineInfoVisible = true,
                ColumnInfoVisible = false,
                StringViewVisible = true,
                ContextMenuStrip = contextMenu,
                GroupSeparatorVisible = true,
                HexCasing = HexCasing.Lower
            };
            lblOffset = new ToolStripStatusLabel()
            {
                Padding = new Padding(10, 0, 10, 0)
            };

            var statusStrip = new StatusStrip { Dock = DockStyle.Bottom, SizingGrip = false };
            statusStrip.Items.Add(lblOffset);
            Controls.Add(hexBox);
            Controls.Add(statusStrip);
            hexBox.SelectionStartChanged += (sender, e) => UpdateOffsetAndSelection();
            hexBox.SelectionLengthChanged += (sender, e) => UpdateOffsetAndSelection();

            UpdateOffsetAndSelection();
            hexBox.GroupSize = 128;
        }

        internal void Assign(byte[] arrHeaders, byte[] arrBody)
        {
            headerBytes = arrHeaders ?? Array.Empty<byte>();
            bodyBytes = arrBody ?? Array.Empty<byte>();
            RefreshBytes();
        }

        private void RefreshBytes()
        {
            byte[] bytes = bodyBytes;
            if (miShowHeaders.Checked && headerBytes.Length > 0)
            {
                bytes = new byte[checked(headerBytes.Length + bodyBytes.Length)];
                Buffer.BlockCopy(headerBytes, 0, bytes, 0, headerBytes.Length);
                Buffer.BlockCopy(bodyBytes, 0, bytes, headerBytes.Length, bodyBytes.Length);
            }
            hexBox.ByteProvider = new DynamicByteProvider(bytes);
            UpdateOffsetAndSelection();
        }

        private void UpdateOffsetAndSelection()
        {
            long offset = hexBox.ByteProvider == null ? 0 : Math.Max(0, hexBox.SelectionStart);
            if (hexBox.SelectionLength <= 1)
            {
                lblOffset.Text = $"{offset:N0} (0x{offset:x})";
            }
            else
            {
                lblOffset.Text = $"Selection: {hexBox.SelectionLength}bytes @ Offset: {offset:N0} (0x{offset:x})";
            }
        }

        public void Clear()
        {
            headerBytes = bodyBytes = Array.Empty<byte>();
            hexBox.ByteProvider = null;
            UpdateOffsetAndSelection();
        }

        internal void SetFontSize(float flSizeInPoints)
        {
            Font oldFont = hexFont;
            hexFont = new Font(oldFont.FontFamily, flSizeInPoints);
            hexBox.Font = hexFont;
            oldFont.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                contextMenu.Dispose();
                hexFont.Dispose();
            }
        }
    }
}
