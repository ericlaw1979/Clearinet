using Clearinet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;
using System.Xml;

// TODO: May need to use the BetterTreeView control to avoid issues with
// hangs on tooltips for modern OS? Or maybe that's been fixed in the intervening years?
// If so, we should remove the BetterTreeView control from the project and references to it.

namespace Extensions
{
    public sealed class DataView : UserControl
    {
        readonly TreeView tree;
        readonly TextBox search;
        readonly Label searchStatus;
        readonly Label message;
        readonly List<TreeNode> documentNodes = new List<TreeNode>();
        readonly List<TreeNode> matches = new List<TreeNode>();
        int matchIndex = -1;
        Font viewFont;

        public DataView()
        {
            tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false, BackColor = CONFIG.colorDisabledEdit };
            message = new Label { Dock = DockStyle.Fill, Padding = new Padding(12) };
            search = new TextBox { Dock = DockStyle.Fill, AccessibleName = "Find nodes", BorderStyle = BorderStyle.FixedSingle };
            searchStatus = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6) };
            var next = new Button { Text = "Next", AutoSize = true, Anchor = AnchorStyles.Left };
            var expandAll = new Button { Text = "Expand All", AutoSize = true, Anchor = AnchorStyles.Left };
            var collapseAll = new Button { Text = "Collapse All", AutoSize = true, Anchor = AnchorStyles.Left };
            var searchPanel = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 6, RowCount = 1 };
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchPanel.Controls.Add(new Label { Text = "Find:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            searchPanel.Controls.Add(search, 1, 0);
            searchPanel.Controls.Add(next, 2, 0);
            searchPanel.Controls.Add(searchStatus, 3, 0);
            searchPanel.Controls.Add(expandAll, 4, 0);
            searchPanel.Controls.Add(collapseAll, 5, 0);
            Controls.Add(tree);
            Controls.Add(message);
            Controls.Add(searchPanel);
            search.TextChanged += (sender, e) => FindMatches();
            next.Click += (sender, e) => SelectMatch(1);
            expandAll.Click += (sender, e) =>
            {
                tree.BeginUpdate();
                tree.ExpandAll();
                tree.SelectedNode?.EnsureVisible();
                tree.EndUpdate();
            };
            collapseAll.Click += (sender, e) => tree.CollapseAll();
            search.KeyDown += (sender, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                SelectMatch(e.Shift ? -1 : 1);
                e.SuppressKeyPress = true;
            };
            tree.KeyDown += (sender, e) =>
            {
                if (e.KeyCode == Keys.F3)
                {
                    SelectMatch(e.Shift ? -1 : 1);
                    e.SuppressKeyPress = e.Handled = true;
                }
                if (e.KeyData == Keys.Space)
                {
                    var n = tree.SelectedNode;
                    n.BackColor = (n.BackColor == CONFIG.colorHighlight) ? n.BackColor = tree.BackColor : CONFIG.colorHighlight;
                    e.SuppressKeyPress = e.Handled = true;
                }
                if (e.KeyCode == Keys.C && e.Modifiers == Keys.Control)
                {
                    var n = tree.SelectedNode;
                    PlatformAPI.WriteToClipboard(n?.Text ?? string.Empty);  // TODO: What we really want to do is copy the entire subtree in the data model...
                    e.SuppressKeyPress = e.Handled = true;
                }
            };
            SetFontSize(CONFIG.flFontSize);
            Clear();
        }

        internal static int ScoreForContentType(string contentType, bool request = false)
        {
            if (!MediaTypeHeaderValue.TryParse(contentType, out MediaTypeHeaderValue type)) return -1;
            string mime = type.MediaType;
            return (request && IsForm(mime)) || IsBson(mime) || string.Equals(mime, "application/json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mime, "application/xml", StringComparison.OrdinalIgnoreCase) ||
                (mime.EndsWith("+xml", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(mime, "image/svg+xml", StringComparison.OrdinalIgnoreCase)) ? 75 : -1;
        }

        internal void Assign(HTTPHeaders headers, byte[] body)
        {
            Clear();
            var request = headers as HTTPRequestHeaders;
            if (request != null)
            {
                string path = request.RequestPath;
                int fragment = path.IndexOf('#');
                if (fragment >= 0) path = path.Substring(0, fragment);
                int query = path.IndexOf('?');
                if (query >= 0) AddNameValuePairs("QueryString", path.Substring(query + 1), Encoding.UTF8);
            }
            if (body == null || body.Length == 0)
            {
                if (request != null && MediaTypeHeaderValue.TryParse(request["Content-Type"], out MediaTypeHeaderValue emptyType) && IsForm(emptyType.MediaType))
                    AddNameValuePairs("Body", string.Empty, Encoding.UTF8);
                if (tree.Nodes.Count > 0) ShowTree();
                return;
            }
            int queryNodeCount = documentNodes.Count;
            tree.BeginUpdate();
            try
            {
                string contentType = headers?["Content-Type"];
                MediaTypeHeaderValue type = null;
                if (!string.IsNullOrWhiteSpace(contentType) && !MediaTypeHeaderValue.TryParse(contentType, out type))
                    throw new FormatException("Invalid Content-Type: " + contentType);
                byte[] decoded = headers == null ? body : Exchange.DecodeBodyFromHeaders(headers, body);
                if (IsBson(type?.MediaType))
                {
                    TreeNode root = BsonDocument.Parse(decoded);
                    tree.Nodes.Add(root);
                    var pending = new Stack<TreeNode>();
                    pending.Push(root);
                    while (pending.Count > 0)
                    {
                        TreeNode node = pending.Pop();
                        documentNodes.Add(node);
                        for (int i = node.Nodes.Count - 1; i >= 0; i--) pending.Push(node.Nodes[i]);
                    }
                    ShowTree();
                    return;
                }
                var document = new XmlDocument { XmlResolver = new CustomLocalResolver(dummyDtd) };
                bool json;
                using (var stream = new MemoryStream(decoded, false))
                {
                    string mime = type?.MediaType ?? string.Empty;
                    bool xml = mime.OICEqualsAny("application/xml", "text/xml") || mime.OICEndsWith("+xml");
                    // Without an HTTP charset, the XML reader must see the original BOM and encoding declaration.
                    if (xml && string.IsNullOrEmpty(type.CharSet))
                    {
                        using (var reader = XmlReader.Create(stream, XmlSettings())) document.Load(reader);
                        json = false;
                    }
                    else
                    {
                        Encoding encoding = string.IsNullOrEmpty(type?.CharSet) ? Encoding.UTF8 : Encoding.GetEncoding(type.CharSet.Trim('"'));
                        string text;
                        using (var reader = new StreamReader(stream, encoding, true)) text = reader.ReadToEnd();
                        if (request != null && IsForm(mime))
                        {
                            AddNameValuePairs("Body", text, encoding);
                            ShowTree();
                            return;
                        }
                        json = mime.OICEquals("application/json") ||
                            mime.OICEndsWith("+json") ||
                            (!xml && !text.TrimStart().StartsWith("<", StringComparison.Ordinal));

                        // Google sends JSON as JavaScript with a preamble to prevent XSSI attacks. See https://docs.angularjs.org/api/ng/service/$http#cross-site-request-forgery-xsrf-protection
                        if ((mime.OICEqualsAny("text/javascript", "application/javascript"))
                            && text.StartsWith(")]}'"))
                        {
                            text = text.TrimStart().Substring(4);
                            json = true;
                        }
                        if (json)
                        {
                            byte[] utf8 = Encoding.UTF8.GetBytes(text);
                            using (var reader = JsonReaderWriterFactory.CreateJsonReader(utf8, XmlDictionaryReaderQuotas.Max))
                                document.Load(reader);
                            RejectTrailingJson(text);
                        }
                        else
                        {
                            using (var reader = XmlReader.Create(new StringReader(text), XmlSettings())) document.Load(reader);
                        }
                    }
                }
                PopulateTree(document, json);
                ShowTree();
            }
            catch (Exception ex) when (ex is XmlException || ex is SerializationException || ex is IOException || ex is InvalidDataException ||
                ex is ArgumentException || ex is FormatException || ex is NotSupportedException)
            {
                while (tree.Nodes.Count > (queryNodeCount > 0 ? 1 : 0)) tree.Nodes.RemoveAt(tree.Nodes.Count - 1);
                documentNodes.RemoveRange(queryNodeCount, documentNodes.Count - queryNodeCount);
                CApp.Log.Log("Data inspector parsing failed: " + ex.Message);
                string error = "Unable to display this body as XML, JSON, or BSON" + (request != null ? ", or form data" : string.Empty) + ".";
                if (queryNodeCount > 0)
                {
                    var node = new TreeNode("Body: " + error + " " + DisplayText(ex.Message));
                    tree.Nodes.Add(node);
                    documentNodes.Add(node);
                    ShowTree();
                }
                else ShowMessage(error + "\r\n" + ex.Message);
            }
            finally
            {
                tree.EndUpdate();
            }
        }

        private void AddNameValuePairs(string title, string text, Encoding encoding)
        {
            var root = new TreeNode(title);
            root.NodeFont = new Font(tree.Font, FontStyle.Underline);
            tree.Nodes.Add(root);
            documentNodes.Add(root);
            foreach (string pair in text.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = pair.IndexOf('=');
                string name = separator < 0 ? pair : pair.Substring(0, separator);
                string value = separator < 0 ? string.Empty : pair.Substring(separator + 1);
                var node = new TreeNode(DisplayText(Utilities.UrlDecode(name, encoding)));
                if (value.Length > 0)
                {
                    var valuenode = new TreeNode(DisplayText(Utilities.UrlDecode(value, encoding)));
                    documentNodes.Add(valuenode);
                    node.Nodes.Add(valuenode);
                }
                root.Nodes.Add(node);
                node.ExpandAll();
                documentNodes.Add(node);
            }
            root.Expand();
        }

        private static bool IsForm(string mime) => string.Equals(mime, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);

        private static bool IsBson(string mime) => string.Equals(mime, "application/bson", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mime, "application/x-bson", StringComparison.OrdinalIgnoreCase);

        private void ShowTree()
        {
            message.Visible = false;
            tree.Visible = true;
            if (tree.Nodes.Count > 0) tree.Nodes[0].Expand();
        }

        private static XmlReaderSettings XmlSettings() => new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = new CustomLocalResolver(dummyDtd) };

        private static void RejectTrailingJson(string text)
        {
            // The framework reader validates a single value but silently ignores anything following it.
            text = text.Trim();
            bool delimited = text[0] == '{' || text[0] == '[' || text[0] == '"';
            bool quoted = false;
            int depth = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') quoted = false;
                }
                else if (c == '"') quoted = true;
                else if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
                if (!quoted && depth == 0 && (delimited || char.IsWhiteSpace(c)))
                {
                    if (!string.IsNullOrWhiteSpace(text.Substring(i + 1)))
                        throw new XmlException("Unexpected content after the JSON root value.");
                    return;
                }
            }
        }

        private void PopulateTree(XmlDocument document, bool json)
        {
            // Use an explicit stack so deeply nested documents do not consume the UI thread's call stack.
            var pending = new Stack<Tuple<XmlNode, TreeNodeCollection, string>>();
            for (int i = document.ChildNodes.Count - 1; i >= 0; i--)
                pending.Push(Tuple.Create(document.ChildNodes[i], tree.Nodes, "$"));
            while (pending.Count > 0)
            {
                var entry = pending.Pop();
                XmlNode source = entry.Item1;
                var element = source as XmlElement;
                string text;
                if (json)
                {
                    string kind = element.GetAttribute("type");
                    string value = kind == "object" ? "{}" : kind == "array" ? "[]" : kind == "null" ? "null" :
                        kind == "string" ? "\"" + DisplayText(element.InnerText).Replace("\"", "\\\"") + "\"" : element.InnerText;
                    text = entry.Item3 + ": " + value;
                }
                else
                {
                    text = element != null ? "<" + element.Name + ">" : source.NodeType + ": " + DisplayText(source.Value ?? source.InnerText);
                }
                var node = new TreeNode(text);
                entry.Item2.Add(node);
                documentNodes.Add(node);
                if (json)
                {
                    if (element.GetAttribute("type") != "object" && element.GetAttribute("type") != "array") continue;
                    bool array = element.GetAttribute("type") == "array";
                    for (int i = element.ChildNodes.Count - 1; i >= 0; i--)
                    {
                        var child = (XmlElement)element.ChildNodes[i];
                        // The framework JSON reader maps names that are not XML names to an "item" attribute.
                        string name = array ? "[" + i + "]" : child.HasAttribute("item") ? child.GetAttribute("item") : child.LocalName;
                        pending.Push(Tuple.Create((XmlNode)child, node.Nodes, DisplayText(name)));
                    }
                }
                else
                {
                    if (element != null)
                    {
                        foreach (XmlAttribute attribute in element.Attributes)
                        {
                            var attributeNode = new TreeNode("@" + attribute.Name + " = " + DisplayText(attribute.Value));
                            node.Nodes.Add(attributeNode);
                            documentNodes.Add(attributeNode);
                        }
                    }
                    for (int i = source.ChildNodes.Count - 1; i >= 0; i--)
                        pending.Push(Tuple.Create(source.ChildNodes[i], node.Nodes, string.Empty));
                }
            }
        }

        internal static string DisplayText(string text) => text.Replace("\\", "\\\\").Replace("\0", "\\0").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

        private void FindMatches()
        {
            matches.Clear();
            matchIndex = -1;
            if (search.Text.Length > 0)
            {
                foreach (TreeNode node in documentNodes)
                    if (node.Text.IndexOf(search.Text, StringComparison.OrdinalIgnoreCase) >= 0) matches.Add(node);
            }
            SelectMatch(1);
        }

        private void SelectMatch(int direction)
        {
            if (matches.Count == 0)
            {
                searchStatus.Text = search.Text.Length == 0 ? string.Empty : "No matches";
                return;
            }
            matchIndex = matchIndex < 0 ? (direction > 0 ? 0 : matches.Count - 1) : (matchIndex + direction + matches.Count) % matches.Count;
            tree.SelectedNode = matches[matchIndex];
            tree.SelectedNode.EnsureVisible();
            searchStatus.Text = (matchIndex + 1) + " of " + matches.Count;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData != (Keys.Control | Keys.F)) return base.ProcessCmdKey(ref msg, keyData);
            search.Focus();
            search.SelectAll();
            return true;
        }

        private void ShowMessage(string text)
        {
            message.Text = text;
            message.Visible = true;
            tree.Visible = false;
        }

        public void Clear()
        {
            tree.Nodes.Clear();
            documentNodes.Clear();
            search.Clear();
            matches.Clear();
            matchIndex = -1;
            searchStatus.Text = string.Empty;
            ShowMessage("No body to inspect.");
        }

        internal void SetFontSize(float size)
        {
            Font oldFont = viewFont;
            viewFont = new Font("Lucida Console", size);
            tree.Font = viewFont;
            oldFont?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) viewFont?.Dispose();
        }


        #region OfflineXMLEntities

        // ISSUE: Fetching DTDs from the network can be a security issue, and
        // it can also cause hangs if the network is slow or unavailable. Maybe
        // we should have a button to allow the user to explicitly approve a fetch?
        const string dummyDtd = @"
            <!ENTITY nbsp '&#160;'>
            <!ENTITY copy '&#169;'>
            <!ENTITY eacute '&#233;'>";

        public class CustomLocalResolver : XmlUrlResolver
        {
            private readonly string _dtdContent;

            public CustomLocalResolver(string dtdContent)
            {
                _dtdContent = dtdContent;
            }

            public override object GetEntity(Uri absoluteUri, string role, Type ofObjectToReturn)
            {
                // Intercept the request and return the hardcoded DTD string as a stream
                byte[] byteArray = Encoding.UTF8.GetBytes(_dtdContent);
                return new MemoryStream(byteArray);
            }
        }
        #endregion
    }
}
