using Clearinet;
using System.Windows.Forms;

namespace TestExtensions
{
    public class RequestHeaderInspector : RequestInspectorBase
    {
        HeaderView hvViewer;

        public override void AddToTab(TabPage tab)
        {
            tab.Text = "Headers";
            hvViewer = new HeaderView(this);
            tab.Controls.Add(hvViewer);
            hvViewer.Dock = DockStyle.Fill;
        }

        public override int GetOrder()
        {
            return 0;
        }

        public override void Assign(HTTPRequestHeaders hrh, byte[] arrBody, bool bReadOnly)
        {
            headers = hrh;
            hvViewer.txtFirstLine.ReadOnly = bReadOnly;
            hvViewer.txtFirstLine.Text = $"{hrh.HTTPMethod} {hrh.RequestPath} {hrh.HTTPVersion}";
            hvViewer.tvNVP.Nodes.Clear();
            /*foreach (var kvp in hrh)
            {
                hvViewer.tvNVP.Nodes.Add(kvp.Key, $"{kvp.Key}: {kvp.Value}");
            }*/
        }

        public override void Clear()
        {
            hvViewer.Clear();
        }
    }

    public class ResponseHeaderInspector : ResponseInspectorBase
    {
        HeaderView hvViewer;
        public override void AddToTab(TabPage tab)
        {
            tab.Text = "Headers";
            hvViewer = new HeaderView(this);
            tab.Controls.Add(hvViewer);
            hvViewer.Dock = DockStyle.Fill;
        }

        public override int GetOrder()
        {
            return 0;
        }

        public override void Assign(HTTPResponseHeaders hrh, byte[] arrBody, bool bReadOnly)
        {
            headers = hrh;
            hvViewer.txtFirstLine.ReadOnly = bReadOnly;
            hvViewer.txtFirstLine.Text = $"{hrh.HTTPVersion} {hrh.StatusCode} {hrh.StatusText}";
            hvViewer.tvNVP.Nodes.Clear();
        }

        public override void Clear()
        {
            hvViewer.Clear();
        }
    }
}
