using Clearinet;
using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace Extensions
{
    public partial class ImageView : UserControl
    {
        public ImageView()
        {
            InitializeComponent();
            pbImage.MouseDoubleClick += pbImage_MouseDoubleClick;
            try
            {
                cbxSizing.SelectedIndex = CApp.Prefs.GetInt32Pref("inspectors.ImageView.SizeMode", 0);
            }
            catch (Exception) { Debug.Assert(false, "Invalid scale"); }
        }

        private void pbImage_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || pbImage.Image == null) return;

            FullscreenImageForm viewer = null;
            try
            {
                viewer = new FullscreenImageForm(pbImage.Image, Screen.FromControl(pbImage).Bounds);
                viewer.Show(FindForm());
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
    }
}
