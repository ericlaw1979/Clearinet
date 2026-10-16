using Clearinet;
using System;
using System.IO;
using System.Windows.Forms;

namespace Extensions
{
    public sealed class Transformer : ResponseInspectorBase
    {
        TransformerView view;
        Exchange exchange;
        bool readOnly;
        bool dirty;

        public override string TabTitle => "Transformer";
        public override int GetOrder() => 200;
        public override InspectorFlags GetFlags() => InspectorFlags.AlwaysCommitUpdates;
        public override void AddToTab(TabPage tab)
        {
            view = new TransformerView(this) { Dock = DockStyle.Fill };
            tab.Controls.Add(view);
        }

        public override int ScoreForExchange(Exchange oX)
        {
            if (!oX.ResponseBody.HasData()) return 0;
            // If there's a body and it's encoded, we're more relevant than the Header inspector,
            // but many Inspectors can now deal with decoding responses, so let's not get too aggressive. 
            if (oX.ResponseHeaders.ExistsAny("Content-Encoding", "Transfer-Encoding")) return 21;
            return 0;
        }

        public override void AssignExchange(Exchange oX)
        {
            Assign(oX.ResponseHeaders, oX.ResponseBody, false);
            exchange = oX;
            if (oX.RequestHeaders?.HTTPMethod.OICEquals("HEAD") == true)
            {
                readOnly = true;
                RefreshView("HEAD responses cannot have a body.");
            }
        }

        public override void Assign(HTTPResponseHeaders hrh, byte[] arrBody, bool bReadOnly)
        {
            exchange = null;
            headers = hrh;
            body = arrBody;
            dirty = false;
            readOnly = bReadOnly || hrh == null || hrh.StatusCode < 200 || hrh.StatusCode == 204 || hrh.StatusCode == 304;
            RefreshView(readOnly ? "This response cannot be transformed." : null);
        }

        internal void ToggleEncoding(string encoding, bool enabled)
        {
            if (readOnly || headers == null)
            {
                RefreshView("This response cannot be transformed.");
                return;
            }

            try
            {
                byte[] transformedBody;
                HTTPResponseHeaders transformedHeaders = ResponseTransformer.Transform(headers, body, encoding, enabled, out transformedBody);
                headers = transformedHeaders;
                body = transformedBody;
                dirty = true;
                if (exchange != null)
                {
                    CommitAnyChanges(exchange);
                    CApp.UI?.actRefreshUI(false);
                }
                RefreshView();
            }
            catch (Exception e) when (e is IOException || e is NotSupportedException || e is InvalidOperationException)
            {
                CApp.Log?.Log($"Response transformation failed: {e.Message}");
                RefreshView($"Transformation failed: {e.Message}");
            }
        }

        public override bool CommitAnyChanges(Exchange oX)
        {
            if (!dirty) return false;
            if (readOnly || (exchange != null && exchange != oX))
            {
                throw new InvalidOperationException("The response cannot be committed to this exchange.");
            }
            oX.ResponseHeaders = headers;
            oX.ResponseBody = body;
            dirty = false;
            return true;
        }

        private void RefreshView(string message = null)
        {
            view?.Assign(headers, body, readOnly, message);
        }

        public override void SetFontSize(float flSizeInPoints) => view?.SetFontSize(flSizeInPoints);
        public override void Clear()
        {
            exchange = null;
            headers = null;
            body = null;
            readOnly = true;
            dirty = false;
            RefreshView("Select a response to transform.");
        }
    }
}
