using Clearinet;
using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace TestExtensions
{
    public partial class ImageView : UserControl
    {
        public ImageView()
        {
            InitializeComponent();
            try
            {
                cbxSizing.SelectedIndex = CApp.Prefs.GetInt32Pref("inspectors.ImageView.SizeMode", 0);
            }
            catch (Exception eX) { Debug.Assert(false, "Invalid scale"); }
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
