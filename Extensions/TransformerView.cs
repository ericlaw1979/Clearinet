using Clearinet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Extensions
{
    public sealed class TransformerView : UserControl
    {
        readonly Transformer owner;
        readonly Dictionary<string, CheckBox> encodingBoxes = new Dictionary<string, CheckBox>();
        readonly Label lblStatus;
        bool assigning;
        Font viewFont;

        public TransformerView(Transformer owner)
        {
            this.owner = owner;
            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(15)
            };
            layout.Controls.Add(new Label
            {
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10),
                Text = "Changes apply immediately to the selected response."
            });
            AddEncoding(layout, "chunked", "Chunked transfer encoding");
            AddEncoding(layout, "gzip", "GZIP compression");
            AddEncoding(layout, "deflate", "DEFLATE compression");
            AddEncoding(layout, "br", "Brotli compression");
            AddEncoding(layout, "zstd", "ZStd compression");
            layout.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "Compression layers are applied in the order added."
            });
            lblStatus = new Label { AutoSize = true, MaximumSize = new Size(600, 0) };
            layout.Controls.Add(lblStatus);
            Controls.Add(layout);
            SetFontSize(CONFIG.flFontSize);
            Assign(null, null, true, "Select a response to transform.");
        }

        private void AddEncoding(FlowLayoutPanel layout, string encoding, string title)
        {
            var box = new CheckBox { AutoSize = true, Text = title };
            box.CheckedChanged += (sender, e) =>
            {
                if (!assigning) owner.ToggleEncoding(encoding, box.Checked);
            };
            encodingBoxes.Add(encoding, box);
            layout.Controls.Add(box);
        }

        internal void Assign(HTTPResponseHeaders headers, byte[] body, bool readOnly, string message)
        {
            var encodings = new HashSet<string>(ResponseTransformer.GetEncodings(headers, "Content-Encoding"), StringComparer.OrdinalIgnoreCase);
            encodings.UnionWith(ResponseTransformer.GetEncodings(headers, "Transfer-Encoding"));
            assigning = true;
            try
            {
                foreach (var item in encodingBoxes)
                {
                    item.Value.Checked = encodings.Contains(item.Key);
                    item.Value.Enabled = !readOnly && headers != null;
                }
            }
            finally
            {
                assigning = false;
            }
            lblStatus.Text = message ?? $"Encoded body: {body?.LongLength ?? 0:N0} bytes"; // TODO: Show original and decoded sizes.
            lblStatus.ForeColor = message == null ? SystemColors.ControlText : SystemColors.Highlight;
        }

        internal void SetFontSize(float size)
        {
            Font oldFont = viewFont;
            viewFont = new Font(Font.FontFamily, size);
            Font = viewFont;
            oldFont?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) viewFont?.Dispose();
        }
    }
}
