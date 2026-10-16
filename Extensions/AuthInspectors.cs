using Clearinet;
using System.Windows.Forms;

namespace Extensions
{
    public sealed class RequestAuthInspector : RequestInspectorBase
    {
        AuthView authViewer;

        public override string TabTitle => "Auth";
        public override int GetOrder() => 0;

        public override void AddToTab(TabPage tab)
        {
            authViewer = new AuthView { Dock = DockStyle.Fill };
            tab.Controls.Add(authViewer);
        }

        public override void SetFontSize(float flSizeInPoints) => authViewer?.SetFontSize(flSizeInPoints);

        public override void Assign(HTTPRequestHeaders hrh, byte[] arrBody, bool bReadOnly)
        {
            headers = hrh;
            // We should never need a body. body = arrBody;
            authViewer.Assign(hrh, "Authorization", "Proxy-Authorization");
        }

        public override void Clear()
        {
            headers = null;
            body = null;
            authViewer?.Clear();
        }
    }

    public sealed class ResponseAuthInspector : ResponseInspectorBase
    {
        AuthView authViewer;

        public override string TabTitle => "Auth";
        public override int GetOrder() => 0;
        public override int ScoreForExchange(Exchange oX) => oX.responseCode == 401 ? 75 : 0;

        public override void AddToTab(TabPage tab)
        {
            authViewer = new AuthView { Dock = DockStyle.Fill };
            tab.Controls.Add(authViewer);
        }

        public override void SetFontSize(float flSizeInPoints) => authViewer?.SetFontSize(flSizeInPoints);

        public override void Assign(HTTPResponseHeaders hrh, byte[] arrBody, bool bReadOnly)
        {
            headers = hrh;
            // We should never need the body. body = arrBody;
            authViewer.Assign(hrh, "WWW-Authenticate", "Proxy-Authenticate");
        }

        public override void Clear()
        {
            headers = null;
            body = null;
            authViewer?.Clear();
        }
    }
}
