using Clearinet;
using Svg;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace TestExtensions
{
    public class ImageInspector : ResponseInspectorBase
    {
        ImageView oView;
        Image imageRendered;

        public override void AddToTab(TabPage o)
        {
            oView = new ImageView();
            o.Controls.Add(oView);
            oView.Dock = DockStyle.Fill;
            oView.txtMetadata.BackColor = CONFIG.colorDisabledEdit;
            oView.pbImage.MouseUp += PbImage_MouseUp;
        }

        private void DumpImageToDesktop()
        {
            // TODO: Keep original format
            oView.txtMetadata.BackColor = Color.Cyan;
            oView.txtMetadata.Refresh();

            var oMS = new MemoryStream();
            imageRendered.Save(oMS, ImageFormat.Png);
            string sFilename = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + Path.DirectorySeparatorChar +
                $"{DateTime.Now.ToString("H-mm-ss")}.png";
            File.WriteAllBytes(sFilename, oMS.ToArray());
            oView.txtMetadata.BackColor = CONFIG.colorDisabledEdit;
            oView.txtMetadata.Refresh();
            CApp.UI.SetStatusText($"Dumped image to {sFilename}");
        }

        private void PbImage_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle) { DumpImageToDesktop(); return; }
        }

        public override void Assign(HTTPResponseHeaders headers, byte[] arrBody, bool bReadOnly)
        {
            if (null == headers || !arrBody.HasData()) { Debug.Assert(false); Clear(); return; }
            var sContentType = headers["Content-Type"];
            bool bHasEncoding = headers.ExistsAny("Content-Encoding", "Transfer-Encoding");
            oView.txtMetadata.Text = sContentType;
            try
            {
                // If the response claims to be an image, then proactively remove encoding before
                // trying to render it.
                if (bHasEncoding && sContentType.StartsWith("image/"))
                {
                    arrBody = Exchange.DecodeBodyFromHeaders(headers, arrBody);
                }

                if (sContentType.OICStartsWith("image/svg+xml"))
                {
                    using (var oSvgStream = new MemoryStream(arrBody))
                    {
                        var oSvgDocument = SvgDocument.Open<SvgDocument>(oSvgStream);
                        imageRendered = oSvgDocument.Draw();
                    }
                }
                else
                {
                    imageRendered = new Bitmap(new MemoryStream(arrBody));
                }
                oView.pbImage.Image = imageRendered;
            }
            catch
            {
                oView.pbImage.Image = null;
                var sEncodingWarning = bHasEncoding ? "HTTP ENCODING PRESENT.\r\n" : string.Empty;
                oView.txtMetadata.Text = $"Not an image?\r\n\r\n{sEncodingWarning}Content-Type: {sContentType}";
            }
        }

        public override void Clear()
        {
            oView.txtMetadata.Text = string.Empty;
            imageRendered = null;
            oView.pbImage.Image = null;
            oView.pbImage.Cursor = Cursors.Default;
        }

        public override int ScoreForContentType(string sMIME) => (sMIME.OICStartsWith("image/") ? 90 : -1);

        public override int GetOrder() => -200;
    }
}
