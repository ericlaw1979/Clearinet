using Clearinet;
using System;
using System.Collections.Generic;
using System.Drawing;
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
            headers = hrh;
            hvViewer.txtFirstLine.ReadOnly = bReadOnly;
            hvViewer.txtFirstLine.Text = $"{hrh.HTTPMethod} {hrh.RequestPath} {hrh.HTTPVersion}";
            hvViewer.tvNVP.Nodes.Clear();

            hvViewer._headers = hrh;
            TreeNode tnAll = new TreeNode("All", 0, 0);

            TreeNode nodeAdding;

            foreach (HTTPHeaderItem oItem in hrh)
            {
                nodeAdding = tnAll.Nodes.Add(oItem.ToString());
                nodeAdding.Tag = oItem;
                nodeAdding.NodeFont = hvViewer.tvNVP.Font;
            }

            hvViewer.tvNVP.Nodes.Add(tnAll);
            tnAll.ExpandAll();
            hvViewer.tvNVP.TopNode = hvViewer.tvNVP.Nodes[0];

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

        public override void Assign(HTTPResponseHeaders hrh, byte[] arrBody, bool bReadOnly)
        {
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
            TreeNode tnAll = new TreeNode("All", 0, 0);

            TreeNode nodeAdding;

            foreach (HTTPHeaderItem oItem in hrh)
            {
                nodeAdding = tnAll.Nodes.Add(oItem.ToString());
                nodeAdding.Tag = oItem;
                nodeAdding.NodeFont = hvViewer.tvNVP.Font;
            }

            hvViewer.tvNVP.Nodes.Add(tnAll);
            tnAll.ExpandAll();
            hvViewer.tvNVP.TopNode = hvViewer.tvNVP.Nodes[0];
        }

        public override void Clear()
        {
            hvViewer.Clear();
        }
    }
}
