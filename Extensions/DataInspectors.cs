using Clearinet;
using System.Windows.Forms;

namespace Extensions
{
    public sealed class DataRequestInspector : RequestInspectorBase
    {
        DataView dataViewer;

        public override string TabTitle => "Data";
        public override int GetOrder() => 0;
        public override int ScoreForContentType(string contentType) => DataView.ScoreForContentType(contentType, true);

        public override void AddToTab(TabPage tab)
        {
            dataViewer = new DataView { Dock = DockStyle.Fill };
            tab.Controls.Add(dataViewer);
        }

        public override void SetFontSize(float size) => dataViewer?.SetFontSize(size);

        public override void Assign(HTTPRequestHeaders requestHeaders, byte[] requestBody, bool readOnly)
        {
            headers = requestHeaders;
            body = requestBody;
            dataViewer.Assign(requestHeaders, requestBody);
        }

        public override void Clear()
        {
            headers = null;
            body = null;
            dataViewer?.Clear();
        }
    }

    public sealed class DataResponseInspector : ResponseInspectorBase
    {
        DataView dataViewer;

        public override string TabTitle => "Data";
        public override int GetOrder() => 0;
        public override int ScoreForContentType(string contentType) => DataView.ScoreForContentType(contentType);

        public override void AddToTab(TabPage tab)
        {
            dataViewer = new DataView { Dock = DockStyle.Fill };
            tab.Controls.Add(dataViewer);
        }

        public override void SetFontSize(float size) => dataViewer?.SetFontSize(size);

        public override void Assign(HTTPResponseHeaders responseHeaders, byte[] responseBody, bool readOnly)
        {
            headers = responseHeaders;
            body = responseBody;
            dataViewer.Assign(responseHeaders, responseBody);
        }

        public override void Clear()
        {
            headers = null;
            body = null;
            dataViewer?.Clear();
        }
    }
}
