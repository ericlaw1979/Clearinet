using Clearinet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TestExtensions
{
    public sealed class RequestHeaderInspector : RequestInspectorBase
    {
        HeaderView hvViewer;

        public override void AddToTab(TabPage tab)
        {
            tab.Text = "Headers";
            hvViewer = new HeaderView(this);
            tab.Controls.Add(hvViewer);
            hvViewer.Dock = DockStyle.Fill;
        }
        public override void SetFontSize(float flSizeInPoints) => hvViewer?.SetFontSize(flSizeInPoints);

        public override int GetOrder()
        {
            return -500;
        }
        public override int ScoreForContentType(string sMIMEType)
        {
            // Return a low score above zero, so unknown types end up on us.
            return 5;
        }

        public override void Assign(HTTPRequestHeaders hrh, byte[] arrBody, bool bReadOnly)
        {
            try
            {
                hvViewer.tvNVP.BeginUpdate();
                headers = hrh;
                hvViewer.txtFirstLine.ReadOnly = bReadOnly;
                hvViewer.txtFirstLine.Text = $"{hrh.HTTPMethod} {hrh.RequestPath} {hrh.HTTPVersion}";
                hvViewer.tvNVP.Nodes.Clear();

                hvViewer._headers = hrh;
                HeaderTreeBuilder.Populate(hvViewer.tvNVP, hrh);
            }
            finally
            {
                hvViewer.tvNVP.EndUpdate();
            }
        }

        public override void Clear()
        {
            hvViewer.Clear();
        }
    }

    public sealed class ResponseHeaderInspector : ResponseInspectorBase
    {
        HeaderView hvViewer;
        public override void AddToTab(TabPage tab)
        {
            tab.Text = "Headers";
            hvViewer = new HeaderView(this);
            tab.Controls.Add(hvViewer);
            hvViewer.Dock = DockStyle.Fill;
        }
        public override void SetFontSize(float flSizeInPoints) => hvViewer?.SetFontSize(flSizeInPoints);

        public override int GetOrder()
        {
            return -500;
        }
        public override int ScoreForContentType(string sMIMEType)
        {
            // Return a low score above zero, so unknown types end up on us.
            return 5;
        }

        public override int ScoreForExchange(Exchange oX)
        {
            // If it's a redirect, we're probably the most relevant:
            if (oX.responseCode >= 300 && oX.responseCode < 400) return 80;
            // If there's no response body, we're pretty relevant:
            if (!oX.ResponseBody.HasData()) return 60;

            return ScoreForContentType(oX.ResponseHeaders?["Content-Type"]);
        }

        public override void Assign(HTTPResponseHeaders hrh, byte[] arrBody, bool bReadOnly)
        {
            try
            {
                hvViewer.tvNVP.BeginUpdate();
                headers = hrh;
                if (null == hrh)
                {
                    Clear();
                    return;
                }
                hvViewer.txtFirstLine.ReadOnly = bReadOnly;
                hvViewer.txtFirstLine.Text = $"{hrh.HTTPVersion} {hrh.StatusCode} {hrh.StatusText}";
                hvViewer.tvNVP.Nodes.Clear();

                hvViewer._headers = hrh;
                HeaderTreeBuilder.Populate(hvViewer.tvNVP, hrh);
            }
            finally
            {
                hvViewer.tvNVP.EndUpdate();
            }
        }

        public override void Clear()
        {
            hvViewer.Clear();
        }
    }

    internal static class HeaderTreeBuilder
    {
        private static readonly string[] CategoryNames =
        {
            "Cache", "Client", "Entity", "Miscellaneous", "Security", "Server", "Transport"
        };

        private static HashSet<string> htHighlight = new HashSet<string>(
            CApp.Prefs.GetStringPref("inspectors.headers.highlighted_names",
                             "authorization,content-type,content-length,content-disposition,cookie,set-cookie,"
                            + "host,location,set-cookie").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);

        internal static void Populate(BetterTreeView tree, IEnumerable<HTTPHeaderItem> headers)
        {
            Dictionary<string, List<HTTPHeaderItem>> headersByCategory =
                new Dictionary<string, List<HTTPHeaderItem>>(StringComparer.Ordinal);
            foreach (string categoryName in CategoryNames)
            {
                headersByCategory.Add(categoryName, new List<HTTPHeaderItem>());
            }

            foreach (HTTPHeaderItem item in headers)
            {
                headersByCategory[GetCategory(item.Name)].Add(item);
            }

            foreach (string categoryName in CategoryNames)
            {
                List<HTTPHeaderItem> categoryHeaders = headersByCategory[categoryName];
                if (categoryHeaders.Count == 0) continue;

                TreeNode categoryNode = new TreeNode(categoryName, 0, 0);
                foreach (HTTPHeaderItem item in categoryHeaders.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
                {
                    TreeNode node = categoryNode.Nodes.Add(item.ToString());
                    node.Tag = item;
                    node.NodeFont = tree.Font;
                    if (htHighlight.Contains(item.Name))
                    {
                        node.ForeColor = Color.Green;
                    }
                }

                tree.Nodes.Add(categoryNode);
                categoryNode.NodeFont = new Font(tree.Font, FontStyle.Italic);
            }

            tree.ExpandAll();
            if (tree.Nodes.Count > 0) tree.TopNode = tree.Nodes[0];
        }

        private static string GetCategory(string headerName)
        {
            // TODO: We probably want to use a dictionary of known headers to categories,
            // rather than a switch statement. This would allow us to easily add new
            // headers in a performant and potentially extensible way.
            switch (headerName.ToLowerInvariant())
            {
                case "age":
                case "cache-control":
                case "etag":
                case "expires":
                case "if-match":
                case "if-modified-since":
                case "if-none-match":
                case "if-range":
                case "if-unmodified-since":
                case "last-modified":
                case "pragma":
                case "vary":
                case "warning":
                    return "Cache";

                case "content-md5":
                case "content-type":
                case "digest":
                case "repr-digest":
                case "want-repr-digest":
                    return "Entity";

                case "alt-svc":
                case "connection":
                case "content-encoding":
                case "expect":
                case "host":
                case "keep-alive":
                case "te":
                case "trailer":
                case "transfer-encoding":
                case "upgrade":
                case "via":
                case "forwarded":
                    return "Transport";

                case "authorization":
                case "proxy-authorization":
                case "proxy-authenticate":
                case "www-authenticate":
                case "cookie":
                case "set-cookie":
                case "strict-transport-security":
                case "content-security-policy":
                case "content-security-policy-report-only":
                case "referrer-policy":
                case "permissions-policy":
                case "x-content-type-options":
                case "x-frame-options":
                case "x-xss-protection":
                    return "Security";

                case "accept":
                case "accept-charset":
                case "accept-encoding":
                case "accept-language":
                case "from":
                case "origin":
                case "referer":
                case "user-agent":
                case "dnt":
                case "priority":
                case "x-requested-with":
                    return "Client";

                case "allow":
                case "accept-ranges":
                case "authentication-info":
                case "date":
                case "location":
                case "retry-after":
                case "server":
                case "x-powered-by":
                    return "Server";
            }

            if (headerName.StartsWith("Content-", StringComparison.OrdinalIgnoreCase)) return "Entity";
            if (headerName.StartsWith("Sec-", StringComparison.OrdinalIgnoreCase)) return "Security";
            if (headerName.StartsWith("Access-", StringComparison.OrdinalIgnoreCase)) return "Security";
            if (headerName.StartsWith("Cross-", StringComparison.OrdinalIgnoreCase)) return "Security";
            if (headerName.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase)) return "Transport";

            return "Miscellaneous";
        }
    }
}
