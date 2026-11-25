using Clearinet;
using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace Extensions
{
    public partial class ImageView : UserControl
    {
        ImageInspector _owner;
        public ImageView(ImageInspector owner)
        {
            _owner = owner;
            InitializeComponent();
            pbImage.MouseDoubleClick += pbImage_MouseDoubleClick;
            try
            {
                cbxSizing.SelectedIndex = CApp.Prefs.GetInt32Pref("inspectors.ImageView.SizeMode");
            }
            catch (Exception) { Debug.Assert(false, "Invalid scale"); }
        }

        private void pbImage_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ShowFullScreen();
        }

        private void ShowFullScreen()
        {
            if (pbImage.Image == null) return;
            FullscreenImageForm viewer = null;
            try
            {
                viewer = new FullscreenImageForm(pbImage.Image, Screen.FromControl(pbImage).Bounds);
                viewer.Show(this.FindForm());
            }
            catch (Exception eX)
            {
                viewer?.Dispose();
                CApp.ReportException(eX, "Unable to open fullscreen image.");
            }
        }

        private void cbxSizing_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            switch (cbxSizing.SelectedIndex)
            {
                case 0: pbImage.SizeMode = PictureBoxSizeMode.Zoom; break;              // Scale to fit
                case 1: pbImage.SizeMode = PictureBoxSizeMode.CenterImage; break;       // No scaling
            }
            CApp.Prefs.SetInt32Pref("inspectors.ImageView.SizeMode", cbxSizing.SelectedIndex);
        }

        private void miCopyBitmap_Click(object sender, EventArgs e)
        {
            _owner.CopyAsImage();
        }

        private void copyAsDataURL_Click(object sender, EventArgs e)
        {
            _owner.CopyAsDataUrl();
        }

        private void miConvertToJPEG_Click(object sender, EventArgs e)
        {
            _owner.ConvertToJpegExchange();
        }

        private void mnuImage_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            mnuImage.Enabled = (pbImage.Image != null);
            _owner.PopulateToolsMenu(miTools);
        }

        private void miSaveOnDesktop_Click(object sender, EventArgs e)
        {
            _owner.DumpImageToDesktop();
        }

        private void miShowFullscreen_Click(object sender, EventArgs e)
        {
            ShowFullScreen();
        }
    }
}
