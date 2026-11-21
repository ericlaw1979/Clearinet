using Clearinet;
using Microsoft.Win32;
using Svg;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Extensions
{
    public class ImageInspector : ResponseInspectorBase
    {
        ImageView oView;
        Image imageRendered;
        byte[] imageBody;
        string imageMediaType;

        public override string TabTitle => "Images";
        public override void AddToTab(TabPage o)
        {
            oView = new ImageView(this);
            o.Controls.Add(oView);
            oView.Dock = DockStyle.Fill;
            oView.txtMetadata.BackColor = oView.pbImage.BackColor = CONFIG.colorDisabledEdit;
            oView.txtMetadata.ForeColor = CONFIG.colorDisabledEditForeColor;
            oView.pbImage.MouseUp += PbImage_MouseUp;
        }

        internal void DumpImageToDesktop()
        {
            // TODO: Keep original format
            oView.txtMetadata.BackColor = Color.Cyan;  // Flash background
            oView.txtMetadata.Refresh();

            var oMS = new MemoryStream();
            imageRendered.Save(oMS, ImageFormat.Png);
            string sFilename = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + Path.DirectorySeparatorChar +
                $"{DateTime.Now:H-mm-ss}.png";
            File.WriteAllBytes(sFilename, oMS.ToArray());
            oView.txtMetadata.BackColor = CONFIG.colorDisabledEdit; // Unflash background
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
                imageBody = imageBytes;
                imageMediaType = sContentType.TrimAfter(';').Trim();
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
            imageBody = null;
            imageMediaType = null;
            oView.pbImage.Cursor = Cursors.Default;
        }

        internal void PopulateToolsMenu(ToolStripMenuItem tools)
        {
            while (tools.DropDownItems.Count > 1) tools.DropDownItems[1].Dispose();
            if (imageBody == null) return;

            try
            {
                using (var root = Registry.CurrentUser.OpenSubKey(@"Software\Clearinet\ImagesMenuExt"))
                {
                    if (root == null) return;
                    foreach (string name in root.GetSubKeyNames())
                    {
                        try
                        {
                            using (var key = root.OpenSubKey(name))
                            {
                                if (key == null) continue;
                                string command = key.GetValue("Command") as string;
                                string parameters = key.GetValue("Parameters", string.Empty) as string;
                                string options = key.GetValue("Options", string.Empty) as string;
                                string types = key.GetValue("Types", string.Empty) as string;
                                if (string.IsNullOrWhiteSpace(command) || parameters == null || options == null || types == null)
                                    throw new FormatException("Command must be a nonempty string; Parameters, Options and Types must be strings.");
                                if (!string.IsNullOrWhiteSpace(types) && !Array.Exists(types.Split(';'), type => type.Trim().OICEquals(imageMediaType)))
                                    continue;

                                var item = new ToolStripMenuItem(name);
                                item.Click += async (sender, e) => await RunMenuExtension(name, command, parameters, options);
                                tools.DropDownItems.Add(item);
                            }
                        }
                        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException || ex is FormatException)
                        {
                            CApp.Log.Log($"Image menu extension '{name}' could not be loaded: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            {
                CApp.Log.Log("Image menu extensions could not be loaded: " + ex.Message);
            }
        }

        private async Task RunMenuExtension(string name, string command, string parameters, string options)
        {
            string inputPath = null;
            string outputPath = null;
            string text = string.Empty;
            try
            {
                if (imageBody == null) throw new InvalidOperationException("No image is selected.");
                inputPath = Path.Combine(Path.GetTempPath(), "Clearinet-Image-" + Guid.NewGuid().ToString("N") + Utilities.FileExtensionForMediaType(imageMediaType));
                File.WriteAllBytes(inputPath, imageBody);
                var output = Regex.Match(parameters, @"\{out(?::([^{}]*))?\}");
                if (output.Success)
                {
                    string extension = output.Groups[1].Success ? "." + output.Groups[1].Value : ".tmp";
                    if (!Regex.IsMatch(extension, @"^\.[A-Za-z0-9]+$")) throw new FormatException("Invalid {out:extension} filename extension.");
                    outputPath = Path.Combine(Path.GetTempPath(), "Clearinet-Image-" + Guid.NewGuid().ToString("N") + extension);
                    parameters = parameters.Replace(output.Value, outputPath);
                }
                if (parameters.Contains("{out")) throw new FormatException("Use one output filename token: {out} or {out:extension}.");
                parameters = parameters.Replace("{in}", inputPath);

                bool captureOutput = options.Contains("<stdout>");
                bool captureError = options.Contains("<stderr>");
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo(command, parameters)
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = captureOutput,
                        RedirectStandardError = captureError
                    };
                    if (!process.Start()) throw new InvalidOperationException("The image tool did not start.");
                    // Drain both redirected pipes concurrently so a verbose tool cannot deadlock.
                    Task<string> stdout = captureOutput ? process.StandardOutput.ReadToEndAsync() : Task.FromResult(string.Empty);
                    Task<string> stderr = captureError ? process.StandardError.ReadToEndAsync() : Task.FromResult(string.Empty);
                    await Task.Run(() => process.WaitForExit());
                    string outputText = await stdout;
                    string errorText = await stderr;
                    text = (captureOutput ? "Standard output:\r\n" + outputText : string.Empty)
                        + (captureError ? "\r\nStandard error:\r\n" + errorText : string.Empty);
                    if (process.ExitCode != 0)
                        throw new InvalidOperationException($"The image tool exited with code {process.ExitCode}.");
                    if (outputPath != null)
                    {
                        byte[] result = File.ReadAllBytes(outputPath);
                        CApp.UI.AddFauxExchange("/" + Path.GetFileName(outputPath), Utilities.MediaTypeFromFilename(outputPath), result);
                    }
                    if (captureOutput || captureError) CApp.DoNotifyUser(text, name);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException ||
                ex is ArgumentException || ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception || ex is NotSupportedException || ex is FormatException)
            {
                CApp.DoNotifyUser(ex.Message + (text.Length > 0 ? "\r\n" + text : string.Empty), "Unable to run image tool: " + name, MessageBoxIcon.Error);
            }
            finally
            {
                DeleteToolFile(inputPath);
                DeleteToolFile(outputPath);
            }
        }

        private static void DeleteToolFile(string path)
        {
            if (path == null) return;
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            {
                CApp.Log.Log($"Unable to delete image tool file '{path}': {ex.Message}");
            }
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

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        internal void ConvertToJpegExchange()
        {
            try
            {
                var oMS = new MemoryStream();
                imageRendered.Save(oMS, ImageFormat.Png);
                byte[] arrBody = oMS.ToArray();
                using (MemoryStream inputStream = new MemoryStream(arrBody))
                using (Bitmap bitmap = new Bitmap(inputStream))
                using (MemoryStream outputStream = new MemoryStream())
                {
                    ImageCodecInfo encoder = GetEncoder(ImageFormat.Jpeg) ?? throw new InvalidOperationException("Unable to find a JPEG encoder.");
                    EncoderParameters parameters = new EncoderParameters(1);
                    parameters.Param[0] = new EncoderParameter(Encoder.Quality, 90L); /* TODO: Make configurable. Note you must pass as a LONG. Lame. */
                    bitmap.Save(outputStream, encoder, parameters);

                    CApp.UI.AddFauxExchange($"/CONVERTED_{DateTime.Now:H-mm-ss}.jpg", "image/jpeg", outputStream.ToArray());
                }
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Unable to convert image to JPEG.");
            }

        }

        internal void CopyAsDataUrl()
        {
            var oMS = new MemoryStream();
            imageRendered.Save(oMS, ImageFormat.Png);
            byte[] arrBody = oMS.ToArray();
            PlatformAPI.WriteToClipboard($"data:image/png;base64,{Convert.ToBase64String(arrBody)}");
        }

        internal void CopyAsImage()
        {
            PlatformAPI.WriteToClipboard(imageRendered);
        }
    }
}
