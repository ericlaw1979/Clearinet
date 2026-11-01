using Clearinet;
using System.Diagnostics;
using System.Text;
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
            get
            {

                if (bDirty)
                {
                    hvViewer.SplitContentToByteArrays();
                    return hvViewer.bodyBytes;
                }
                return null;
            }
            set
            {
                // TODO: Who is using this Setter and why?
                Debug.Assert(false);
                base.body = value; // Should we be setting hvViewer.bodyBytes here too?
            }
        }

        public override HTTPRequestHeaders headers
        {
            get
            {
                if (bDirty)
                {
                    hvViewer.SplitContentToByteArrays();
                    HTTPRequestHeaders hrh = HTTPParser.ParseRequest(Encoding.UTF8.GetString(hvViewer.headerBytes));
                    // TODO: Update Content-Length header if present and if bodyBytes.Length != hrh.ContentLength
                    return hrh;
                }
                return null;
            }
            protected set
            {
                // TODO: Who is using this Setter and why?
                Debug.Assert(false);
                base.headers = value; // Should we be setting hvViewer.headerBytes here too?
            }
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

        public override byte[] body
        {
            get
            {

                if (bDirty)
                {
                    hvViewer.SplitContentToByteArrays();
                    return hvViewer.bodyBytes;
                }
                return null;
            }
            set
            {
                // TODO: Who is using this Setter and why?
                Debug.Assert(false);
                base.body = value; // Should we be setting hvViewer.bodyBytes here too?
            }
        }

        public override HTTPResponseHeaders headers
        {
            get
            {
                if (bDirty)
                {
                    hvViewer.SplitContentToByteArrays();
                    HTTPResponseHeaders hrh = HTTPParser.ParseResponse(Encoding.UTF8.GetString(hvViewer.headerBytes));
                    // TODO: Update Content-Length header if present and if bodyBytes.Length != hrh.ContentLength
                    return hrh;
                }
                return null;
            }
            protected set
            {
                // TODO: Who is using this Setter and why?
                Debug.Assert(false);
                base.headers = value; // Should we be setting hvViewer.headerBytes here too?
            }
        }
    }
}
