using Clearinet;
using System.Windows.Forms;

namespace Extensions
{
    public sealed class RequestCookieInspector : RequestInspectorBase
    {
        CookieView cookieViewer;

        public override string TabTitle => "Cookies";
        public override int GetOrder() => 0;

        public override void AddToTab(TabPage tab)
        {
            cookieViewer = new CookieView { Dock = DockStyle.Fill };
            tab.Controls.Add(cookieViewer);
        }

        public override void SetFontSize(float flSizeInPoints) => cookieViewer?.SetFontSize(flSizeInPoints);

        public override void Assign(HTTPRequestHeaders hrh, byte[] arrBody, bool bReadOnly)
        {
            headers = hrh;
            cookieViewer.Assign(hrh, false);
        }

        public override void Clear()
        {
            headers = null;
            body = null;
            cookieViewer?.Clear();
        }
    }

    public sealed class ResponseCookieInspector : ResponseInspectorBase
    {
        CookieView cookieViewer;

        public override string TabTitle => "Cookies";
        public override int GetOrder() => 0;

        public override void AddToTab(TabPage tab)
        {
            cookieViewer = new CookieView { Dock = DockStyle.Fill };
            tab.Controls.Add(cookieViewer);
        }

        public override void SetFontSize(float flSizeInPoints) => cookieViewer?.SetFontSize(flSizeInPoints);

        public override void Assign(HTTPResponseHeaders hrh, byte[] arrBody, bool bReadOnly)
        {
            headers = hrh;
            cookieViewer.Assign(hrh, true);
        }

        public override void Clear()
        {
            headers = null;
            body = null;
            cookieViewer?.Clear();
        }
    }
}
