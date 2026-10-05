using Clearinet;
using System.Text;
using System.Windows.Forms;

namespace TestExtensions
{
    public class RawRequestInspector: RequestInspectorBase
    {
        RawText rtViewer;
        Encoding encBody;

        public override void AddToTab(TabPage tab)
        {
            tab.Text = "Raw";
            rtViewer = new RawText(this);
            tab.Controls.Add(rtViewer);
            rtViewer.Dock = DockStyle.Fill;
        }

        public override int GetOrder()
        {
            return 0;
        }

        public override void Assign(HTTPRequestHeaders hrh, byte[] arrBody, bool bReadOnly)
        {
            headers = hrh;
            body = arrBody;
            encBody = Encoding.UTF8; // TODO: Get encoding from headers.
            rtViewer.rtbRaw.Text = hrh.ToString() + encBody.GetString(arrBody);
        }

        public override void Clear()
        {
            rtViewer.Clear();
        }
    }

    public class RawResponseInspector : ResponseInspectorBase
    {
        RawText rtViewer;
        Encoding encBody;
     
        public override void AddToTab(TabPage tab)
        {
            tab.Text = "Raw";
            rtViewer = new RawText(this);
            tab.Controls.Add(rtViewer);
            rtViewer.Dock = DockStyle.Fill;
        }

        public override int GetOrder()
        {
            return 0;
        }

        public override void Assign(HTTPResponseHeaders hrh, byte[] arrBody, bool bReadOnly)
        {
            headers = hrh;
            body = arrBody;
            encBody = Encoding.UTF8; // TODO: Get encoding from headers.
            rtViewer.rtbRaw.Text = hrh.ToString() + encBody.GetString(arrBody);
            rtViewer.rtbRaw.ReadOnly = bReadOnly;
        }

        public override void Clear()
        {
            rtViewer.Clear();
        }
    }
}
