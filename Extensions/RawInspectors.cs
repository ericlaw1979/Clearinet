using Clearinet;
using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Extensions
{
    public sealed class RawRequestInspector : RequestInspectorBase
    {
        RawText rtViewer;
        Encoding encBody;
        public override string TabTitle => "Raw";

        public override bool bDirty
        {
            get
            {
                return rtViewer?.HasChanges ?? false;
            }
            set
            {
                if (null != rtViewer) rtViewer.HasChanges = value;
            }
        }

        public override bool CommitAnyChanges(Exchange xc)
        {
            if (!bDirty || !CommitIsAllowed(xc)) return false;

            var (sHeaderBlock, body) = Parser.CrackHttpMessage(new MemoryStream(Encoding.UTF8.GetBytes(rtViewer.rtbRaw.Text)));
            var (method, path, version, headerList, origin) = Parser.ParseRequestHeaders(sHeaderBlock);
            HTTPRequestHeaders rqh = new HTTPRequestHeaders(path, headerList);
            rqh.HTTPVersion = version;
            rqh.HTTPMethod = method;
            if ((origin != null) && origin.Contains(":"))
            {
                rqh.UriScheme = origin.TrimAfter(":");
            }
            xc.RequestHeaders = rqh;
            xc.RequestBody = body;
            return true;
        }

        public override void AddToTab(TabPage tab)
        {
            rtViewer = new RawText(this);
            tab.Controls.Add(rtViewer);
            rtViewer.Dock = DockStyle.Fill;
        }
        public override void SetFontSize(float flSizeInPoints) => rtViewer?.SetFontSize(flSizeInPoints);

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
            rtViewer.rtbRaw.ReadOnly = bReadOnly;
        }

        public override void Clear()
        {
            rtViewer?.Clear();
        }
    }

    public sealed class RawResponseInspector : ResponseInspectorBase
    {
        RawText rtViewer;
        Encoding encBody;

        public override string TabTitle => "Raw";
        public override int ScoreForContentType(string sMIME) => (sMIME.OICStartsWith("text/") ? 50 : -1);

        public override void AddToTab(TabPage tab)
        {
            rtViewer = new RawText(this);
            tab.Controls.Add(rtViewer);
            rtViewer.Dock = DockStyle.Fill;
        }
        public override void SetFontSize(float flSizeInPoints) => rtViewer?.SetFontSize(flSizeInPoints);

        public override int GetOrder()
        {
            return 0;
        }

        public override void Assign(HTTPResponseHeaders hrh, byte[] arrBody, bool bReadOnly)
        {
            headers = hrh;
            body = arrBody;
            encBody = Encoding.UTF8; // TODO: Get encoding from headers.
            rtViewer.rtbRaw.Text = (hrh?.ToString() ?? string.Empty) + encBody.GetString(arrBody ?? Array.Empty<byte>());
            rtViewer.rtbRaw.ReadOnly = bReadOnly;
        }

        public override bool bDirty
        {
            get
            {
                return rtViewer?.HasChanges ?? false;
            }
            set
            {
                if (null != rtViewer) rtViewer.HasChanges = value;
            }
        }
        public override bool CommitAnyChanges(Exchange xc)
        {
            if (!bDirty || !CommitIsAllowed(xc)) return false;

            var (sHeaderBlock, body) = Parser.CrackHttpMessage(new MemoryStream(Encoding.UTF8.GetBytes(rtViewer.rtbRaw.Text)));
            var (version, statusCode, statusText, headerList) = Parser.ParseResponseHeaders(sHeaderBlock);
            HTTPResponseHeaders resph = new HTTPResponseHeaders(statusCode, statusText, headerList);
            resph.HTTPVersion = version;
            xc.ResponseHeaders = resph;
            xc.ResponseBody = body;
            return true;
        }

        public override void Clear()
        {
            rtViewer?.Clear();
        }
    }
}
