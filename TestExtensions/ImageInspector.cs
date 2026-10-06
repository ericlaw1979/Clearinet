using Clearinet;
using System.Drawing;
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
        }

        public override void Assign(HTTPResponseHeaders headersResponse, byte[] arrBody, bool bReadOnly)
        {
            if (headersResponse != null) oView.txtMetadata.Text = headersResponse["Content-Type"];
            try
            {
                imageRendered = new Bitmap(new MemoryStream(arrBody));
                oView.pbImage.Image = imageRendered;
            }
            catch
            {
                oView.pbImage.Image = null;
                oView.txtMetadata.Text = $"Not an image?\r\n\r\n{oView.txtMetadata.Text}";
            }
        }

        public override void Clear()
        {
            oView.txtMetadata.Text = string.Empty;
            imageRendered = null;
            oView.pbImage.Image = null;
            oView.pbImage.Cursor = Cursors.Default;
        }

        // todo: exclude image/svg+xml until we can handle it.
        public override int ScoreForContentType(string sMIME) => (sMIME.OICStartsWith("image/") ? 90 : -1);

        public override int GetOrder() => -200;
    }
}
