using Clearinet;
using System.Windows.Forms;

namespace Extensions
{
    public sealed class HexRequestInspector : RequestInspectorBase
    {
        HexView hvViewer;

        public override string TabTitle => "Hex";

        public override void AddToTab(TabPage tab)
        {
            hvViewer = new HexView(this);
            tab.Controls.Add(hvViewer);
            hvViewer.Dock = DockStyle.Fill;
        }

        public override void SetFontSize(float flSizeInPoints) => hvViewer?.SetFontSize(flSizeInPoints);

        public override int GetOrder() => 100;

        public override void Assign(HTTPRequestHeaders hrh, byte[] arrBody, bool bReadOnly)
        {
            headers = hrh;
            body = arrBody;
            hvViewer.Assign(hrh?.ToByteArray(), arrBody);
            hvViewer.ReadOnly = bReadOnly;
        }

        public override void Clear()
        {
            headers = null;
            body = null;
            hvViewer?.Clear();
        }

        public override byte[] body
        {
            get => hvViewer?.getBody();
            set => base.body = value;
        }

        public override bool bDirty
        {
            get
            {
                return hvViewer?.HasChanges() ?? false;
            }
        }
    }

    public sealed class HexResponseInspector : ResponseInspectorBase
    {
        HexView hvViewer;

        public override string TabTitle => "Hex";

        public override void AddToTab(TabPage tab)
        {
            hvViewer = new HexView(this);
            tab.Controls.Add(hvViewer);
            hvViewer.Dock = DockStyle.Fill;
        }

        public override void SetFontSize(float flSizeInPoints) => hvViewer?.SetFontSize(flSizeInPoints);

        public override int GetOrder() => 100;

        public override void Assign(HTTPResponseHeaders hrh, byte[] arrBody, bool bReadOnly)
        {
            headers = hrh;
            body = arrBody;
            hvViewer.Assign(hrh?.ToByteArray(), arrBody);
            hvViewer.ReadOnly = bReadOnly;
        }

        public override bool bDirty
        {
            get
            {
                return hvViewer?.HasChanges() ?? false;
            }
        }
        public override void Clear()
        {
            headers = null;
            body = null;
            hvViewer?.Clear();
        }
    }
}
