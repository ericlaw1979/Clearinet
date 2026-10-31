using Be.Windows.Forms;
using Clearinet;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Extensions
{
    public sealed class HexView : UserControl
    {
        readonly InspectorBase _owner;
        readonly HttpHexBox hexBox;
        readonly ContextMenuStrip contextMenu;
        readonly ToolStripMenuItem miShowHeaders;
        readonly ToolStripStatusLabel lblOffset;
        byte[] headerBytes = Array.Empty<byte>();
        byte[] bodyBytes = Array.Empty<byte>();
        Font hexFont;

        private string _sHeaderPref;

        public bool ReadOnly
        {
            get => hexBox.ReadOnly;
            internal set
            {
                hexBox.ReadOnly = value;
                hexBox.BackColor = value ? CONFIG.colorDisabledEdit : SystemColors.Window;
            }
        }

        internal HexView(InspectorBase owner)
        {
            this.SuspendLayout();

            _owner = owner;
            _sHeaderPref = owner is HexRequestInspector ? "inspectors.hex.show_request_headers" :
                                                          "inspectors.hex.show_response_headers";
            contextMenu = new ContextMenuStrip();
            var miCopy = new ToolStripMenuItem("&Copy");
            miCopy.Click += (sender, e) => hexBox.Copy();
            var miSelectAll = new ToolStripMenuItem("Select &All");
            miSelectAll.Click += (sender, e) => hexBox.SelectAll();
            miShowHeaders = new ToolStripMenuItem("Show &Headers")
            {
                CheckOnClick = true,
                Checked = CApp.Prefs.GetBoolPref(_sHeaderPref, true)
            };
            miShowHeaders.CheckedChanged += (sender, e) =>
            {
                CApp.Prefs.SetBoolPref(_sHeaderPref, miShowHeaders.Checked);
                RefreshBytes();
            };
            contextMenu.Items.AddRange(new ToolStripItem[]
            {
                miCopy, miSelectAll, new ToolStripSeparator(), miShowHeaders
            });
            contextMenu.Opening += (sender, e) =>
            {
                miCopy.Enabled = hexBox.CanCopy();
                miSelectAll.Enabled = hexBox.CanSelectAll();
            };

            hexFont = new Font(CApp.Prefs.GetStringPref("inspectors.hex.font_face", "Cascadia Mono"), CONFIG.flFontSize);
            hexBox = new HttpHexBox
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
                HexCasing = HexCasing.Lower,
                ForeColor = CONFIG.GetColor("inspectors.hex.color.body", Color.Blue),
                HeaderColor = CONFIG.GetColor("inspectors.hex.color.headers", Color.Green),
                VScrollBarVisible = true        // Needed to autoscroll
            };
            lblOffset = new ToolStripStatusLabel()
            {
                Padding = new Padding(10, 0, 10, 0)
            };
            hexBox.TextChanged += (sender, e) => { this._owner.MarkDirty(); };

            var statusStrip = new StatusStrip { Dock = DockStyle.Bottom, SizingGrip = false };
            statusStrip.Items.Add(lblOffset);
            Controls.Add(hexBox);
            Controls.Add(statusStrip);
            hexBox.SelectionStartChanged += (sender, e) => UpdateOffsetAndSelection();
            hexBox.SelectionLengthChanged += (sender, e) => UpdateOffsetAndSelection();

            hexBox.GroupSize = 128;
            UpdateOffsetAndSelection();
            this.ResumeLayout(false);
        }

        internal void Assign(byte[] arrHeaders, byte[] arrBody)
        {
            // TODO: If "ReadOnly" then we can use the StaticByteProvider instead of DynamicByteProvider, which is more efficient.
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
                hexBox.HeaderEndOffset = headerBytes.Length - 1;
            }
            else
            {
                hexBox.HeaderEndOffset = -1;
            }

            // TODO: Do I need to explicitly dispose of the old one?
            hexBox.ByteProvider = new DynamicByteProvider(bytes);
            hexBox.ByteProvider.ApplyChanges();  // Ensure dirty flag is clear
            UpdateOffsetAndSelection();
        }

        private void UpdateOffsetAndSelection()
        {
            long offset = hexBox.ByteProvider == null ? 0 : Math.Max(0, hexBox.SelectionStart);
            var sOfWhat = string.Empty;

            if (miShowHeaders.Checked)
            {
                if (offset >= headerBytes.Length)
                {
                    offset -= headerBytes.Length;
                    sOfWhat = " (of body)";
                }
                else
                {
                    sOfWhat = " (of headers)";
                }
            }
            if (hexBox.SelectionLength <= 1)
            {
                lblOffset.Text = $"{offset:N0} (0x{offset:x}){sOfWhat}";
            }
            else
            {
                lblOffset.Text = $"Selection: {hexBox.SelectionLength}bytes @ Offset: {offset:N0} (0x{offset:x}){sOfWhat}";
            }
        }

        /*  public byte[] getHeaderBytes()
          {
              return (hexBox.ByteProvider == null) ? Array.Empty<byte>() : hexBox.ByteProvider.GetBytes(0, hexBox.HeaderEndOffset + 1);
          }*/

        public Byte[] getBody()
        {
            return Array.Empty<byte>(); // TODO: nYi
            //              return (hexBox.ByteProvider == null) ? Array.Empty<byte>() : hexBox.ByteProvider.GetBytes(hexBox.HeaderEndOffset + 1, hexBox.ByteProvider.Length - (hexBox.HeaderEndOffset + 1));
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
