using Clearinet;
using Svg;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Extensions
{
    public class ImageInspector : ResponseInspectorBase
    {
        ImageView oView;
        Image imageRendered;

        public override string TabTitle => "Images";
        public override void AddToTab(TabPage o)
        {
            oView = new ImageView();
            o.Controls.Add(oView);
            oView.Dock = DockStyle.Fill;
            oView.txtMetadata.BackColor = oView.pbImage.BackColor = CONFIG.colorDisabledEdit;
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
                $"{DateTime.Now:H-mm-ss}.png";
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
            if (null == headers || !arrBody.HasData()) { Clear(); return; }
            var sContentType = headers["Content-Type"];
            bool bHasEncoding = headers.ExistsAny("Content-Encoding", "Transfer-Encoding");
            Clear();
            oView.txtMetadata.Text = sContentType;
            byte[] imageBytes = null;
            try
            {
                // If the response claims to be an image, then proactively remove any encoding before
                // trying to render it.
                if (bHasEncoding && sContentType.OICStartsWith("image/"))
                {
                    arrBody = Exchange.DecodeBodyFromHeaders(headers, arrBody);
                }
                imageBytes = arrBody;

                if (sContentType.OICStartsWith("image/webp") || arrBody.Length > 12 &&
                    arrBody[0] == 'R' && arrBody[1] == 'I' && arrBody[2] == 'F' && arrBody[3] == 'F' &&
                    arrBody[8] == 'W' && arrBody[9] == 'E' && arrBody[10] == 'B' && arrBody[11] == 'P')
                {
                    arrBody = Utilities.ConvertWebPToPNG(arrBody);
                }
                else
                    if (sContentType.OICStartsWith("image/jxl") || arrBody.Length > 12 &&
                        arrBody[0] == 0x00 && arrBody[1] == 0x00 && arrBody[2] == 0x00 && arrBody[3] == 0x0C &&
                        arrBody[4] == 0x4A && arrBody[5] == 0x58 && arrBody[6] == 0x4C && arrBody[7] == 0x20 &&
                        arrBody[8] == 0x0D && arrBody[9] == 0x0A && arrBody[10] == 0x87 && arrBody[11] == 0x0A)
                    {
                        arrBody = Utilities.ConvertJXLToJPEG(arrBody);
                    }
                    else if (sContentType.OICStartsWith("image/avif") || arrBody.Length > 12 &&
                        arrBody[4] == 'f' && arrBody[5] == 't' && arrBody[6] == 'y' && arrBody[7] == 'p' &&
                        (arrBody[8] == 'a' && arrBody[9] == 'v' && arrBody[10] == 'i' && arrBody[11] == 'f' ||
                         arrBody[8] == 'a' && arrBody[9] == 'v' && arrBody[10] == 'i' && arrBody[11] == 's'))
                    {
                        arrBody = Utilities.ConvertAVIFToJPEG(arrBody);
                    }

                if (sContentType.OICStartsWith("image/svg+xml"))
                {
                    using (var oSvgStream = new MemoryStream(arrBody))
                    {
                        AssignImageFromSVG(oSvgStream);
                    }
                }
                else
                {
                    imageRendered = new Bitmap(new MemoryStream(arrBody));
                }
                oView.pbImage.Image = imageRendered;
                oView.txtMetadata.Text = sContentType + "\r\n\r\n" + ImageMetadata.Analyze(imageBytes, imageRendered);
            }
            catch (Exception e) when (e is ArgumentException || e is ExternalException || e is IOException ||
                e is InvalidOperationException || e is System.Xml.XmlException || e is OutOfMemoryException ||
                e is System.ComponentModel.Win32Exception || e is FormatException || e is NotSupportedException)
            {
                Clear();
                var sEncodingWarning = bHasEncoding ? "HTTP ENCODING PRESENT.\r\n" : string.Empty;
                oView.txtMetadata.Text = $"Unable to render image: {e.Message}\r\n\r\n{sEncodingWarning}Content-Type: {sContentType}";
                if (imageBytes != null) oView.txtMetadata.AppendText("\r\n\r\n" + ImageMetadata.Analyze(imageBytes));
            }
        }

        // SVG is a bit of a pig. Prevent JIT from inlining this method into Assign() so we don't load SVG.dll unless we need it.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AssignImageFromSVG(MemoryStream oSvgStream)
        {
            var oSvgDocument = SvgDocument.Open<SvgDocument>(oSvgStream);
            imageRendered = oSvgDocument.Draw();
        }

        public override void Clear()
        {
            oView.txtMetadata.Text = string.Empty;
            oView.pbImage.Image = null;
            imageRendered?.Dispose();
            imageRendered = null;
            oView.pbImage.Cursor = Cursors.Default;
        }

        public override int ScoreForContentType(string sMIME)
        {
            int iScore = sMIME.OICStartsWith("image/") ? 90 : -1;

            // If the image is in a format that requires an external tool to
            // decode, and that tool is not available, set score to 0.
            if (sMIME.OICStartsWith("image/webp")
                && !CONFIG.isToolAvailable("dwebp.exe")) iScore = 0;
            else if (sMIME.OICStartsWith("image/avif")
             && !CONFIG.isToolAvailable("avifdec.exe")) iScore = 0;
            else if (sMIME.OICStartsWith("image/jxl")
                 && !CONFIG.isToolAvailable("djxl.exe")) iScore = 0;

            return iScore;
        }

        public override int GetOrder() => 200;
    }
}
