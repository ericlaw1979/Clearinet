using Clearinet;
using System.Windows.Forms;

namespace Extensions
{
    public sealed class WebViewResponseInspector : ResponseInspectorBase
    {
        private WebViewResponseView view;

        public override string TabTitle => "WebView";
        public override int GetOrder() => 220;

        public override int ScoreForContentType(string contentType)
        {
            if (!WebPreviewDocument.IsSupported(contentType)) return -1;
            if (WebPreviewDocument.IsHtml(contentType) || WebPreviewDocument.IsSvg(contentType)) return 95;
            return contentType.StartsWith("image/", System.StringComparison.OrdinalIgnoreCase) ? 80 : 40;
        }

        public override void AddToTab(TabPage tab)
        {
            view = new WebViewResponseView { Dock = DockStyle.Fill };
            tab.Controls.Add(view);
        }

        public override void Assign(HTTPResponseHeaders responseHeaders, byte[] responseBody, bool readOnly)
        {
            headers = responseHeaders;
            body = responseBody;
            view.Assign(responseHeaders, responseBody);
        }

        public override void Clear()
        {
            headers = null;
            body = null;
            view?.Clear();
        }

        public override void OnBeforeUnload()
        {
            view?.Dispose();
            base.OnBeforeUnload();
        }
    }
}
