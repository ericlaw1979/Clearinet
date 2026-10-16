#Requires -Version 5.1
param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Extensions\bin\Release'))
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
    throw 'Run with powershell.exe -NoProfile -STA -File .\Test\DataInspector.Tests.ps1'
}
$directory = (Resolve-Path -LiteralPath $BuildDirectory).Path
$corePath = Join-Path $directory 'Clearinet.exe'
$extensionPath = Join-Path $directory 'CAE-Default.dll'
$null = [Reflection.Assembly]::LoadFrom($corePath)
$null = [Reflection.Assembly]::LoadFrom($extensionPath)
$tests = @'
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Clearinet;
using Extensions;

public static class DataInspectorTests
{
    static int assertions;
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }
    static T Field<T>(object target, string name) { return (T)target.GetType().GetField(name, Members).GetValue(target); }
    static void Invoke(object target, string name, params object[] args) { target.GetType().GetMethod(name, Members).Invoke(target, args); }
    static HTTPResponseHeaders Headers(string mime) { return new HTTPResponseHeaders(200, "OK", new[] { "Content-Type: " + mime }); }
    static void Assign(DataResponseInspector inspector, string mime, string text) { inspector.Assign(Headers(mime), Encoding.UTF8.GetBytes(text), true); }
    static string[] Children(TreeNode node) { return node.Nodes.Cast<TreeNode>().Select(n => n.Text).ToArray(); }
    static void Equal(string[] actual, params string[] expected) { Check(actual.SequenceEqual(expected), "Expected [" + string.Join(", ", expected) + "]; got [" + string.Join(", ", actual) + "]"); }

    public static void Run()
    {
        foreach (InspectorBase inspector in new InspectorBase[] { new DataRequestInspector(), new DataResponseInspector() })
        {
            Check(inspector.TabTitle == "Data", "Data tab title");
            foreach (string mime in new[] { "application/json", "application/xml", "application/atom+xml", "APPLICATION/SOAP+XML; charset=utf-8", "application/json; charset=\"utf-8\"", "application/xhtml+xml" })
                Check(inspector.ScoreForContentType(mime) == 75, "Exact supported score: " + mime);
            foreach (string mime in new[] { "image/svg+xml", "IMAGE/SVG+XML; charset=utf-8", "application/problem+json", "text/xml", "text/plain", "application/jsonp", "application/xmlstuff", "invalid", "", null })
                Check(inspector.ScoreForContentType(mime) < 0, "Unsupported score: " + mime);
            inspector.Clear();
        }
        var response = new DataResponseInspector();
        using (var form = new Form { Width = 500, Height = 300, ShowInTaskbar = false })
        using (var tab = new TabPage())
        {
            response.AddToTab(tab);
            var view = (DataView)tab.Controls[0];
            tab.Controls.Remove(view);
            form.Controls.Add(view);
            form.Show();
            TreeView tree = Field<TreeView>(view, "tree");
            TextBox search = Field<TextBox>(view, "search");
            Label status = Field<Label>(view, "searchStatus");
            Label message = Field<Label>(view, "message");
            Assign(response, "application/json", "{\"person\":{\"name\":\"Alice\"},\"items\":[42,true,null,\"\",{},[]],\"odd name\":\"line\\nvalue\",\"item\":false,\"\":1,\"a:b\":2}");
            Check(tree.Nodes.Count == 1 && tree.Nodes[0].Text == "$: {}", "JSON root");
            TreeNode root = tree.Nodes[0];
            Equal(Children(root), "person: {}", "items: []", "odd name: \"line\\nvalue\"", "item: false", ": 1", "a:b: 2");
            Equal(Children(root.Nodes[0]), "name: \"Alice\"");
            Equal(Children(root.Nodes[1]), "[0]: 42", "[1]: true", "[2]: null", "[3]: \"\"", "[4]: {}", "[5]: []");
            Check(!tree.LabelEdit, "Read-only tree");
            foreach (string primitive in new[] { "null", "true", "-12.5e2", "\"hello\"", "[]", "{}" })
            {
                Assign(response, "application/json", primitive);
                Check(tree.Nodes.Count == 1 && tree.Nodes[0].Text == "$: " + primitive, "JSON primitive root: " + primitive);
            }
            Assign(response, "application/atom+xml", "<?xml version=\"1.0\"?><r xmlns=\"urn:test\" id=\"7\"><item>Alpha</item><!--note--><![CDATA[<raw>]]></r>");
            Check(tree.Nodes[0].Text.StartsWith("XmlDeclaration:"), "XML declaration");
            root = tree.Nodes[1];
            Check(root.Text == "<r>", "XML root");
            Equal(Children(root), "@xmlns = urn:test", "@id = 7", "<item>", "Comment: note", "CDATA: <raw>");
            Equal(Children(root.Nodes[2]), "Text: Alpha");

            Assign(response, "application/json", "{\"outer\":{\"first\":\"needle\",\"second\":\"NEEDLE\"},\"last\":\"needle\"}");
            var footer = view.Controls.OfType<TableLayoutPanel>().Single();
            Button expandAll = footer.Controls.OfType<Button>().Single(button => button.Text == "Expand All");
            Button collapseAll = footer.Controls.OfType<Button>().Single(button => button.Text == "Collapse All");
            expandAll.PerformClick();
            Check(tree.Nodes[0].IsExpanded && tree.Nodes[0].Nodes[0].IsExpanded, "Expand All expands root and descendants");
            collapseAll.PerformClick();
            Check(!tree.Nodes[0].IsExpanded && !tree.Nodes[0].Nodes[0].IsExpanded, "Collapse All collapses root and descendants");
            Check(footer.Bottom == view.ClientSize.Height, "Tree buttons are in the footer");
            search.Text = "NeEdLe";
            TreeNode first = tree.Nodes[0].Nodes[0].Nodes[0];
            Check(tree.SelectedNode == first && first.IsVisible && status.Text == "1 of 3", "Search opens collapsed ancestors and scrolls");
            var enter = new KeyEventArgs(Keys.Enter);
            Invoke(search, "OnKeyDown", enter);
            Check(tree.SelectedNode == first.NextNode && status.Text == "2 of 3" && enter.SuppressKeyPress, "Enter advances without a beep");
            Button next = footer.Controls.OfType<Button>().Single(button => button.Text == "Next");
            next.PerformClick();
            Check(tree.SelectedNode.Text == "last: \"needle\"" && status.Text == "3 of 3", "Next button");
            Invoke(view, "SelectMatch", 1);
            Check(tree.SelectedNode == first && status.Text == "1 of 3", "Forward wrap");
            Invoke(search, "OnKeyDown", new KeyEventArgs(Keys.Shift | Keys.Enter));
            Check(tree.SelectedNode.Text == "last: \"needle\"" && status.Text == "3 of 3", "Shift+Enter wraps backwards");
            search.Text = "absent";
            Check(status.Text == "No matches", "Unmatched query");
            search.Clear();
            Check(status.Text == "", "Empty query");
            Assign(response, "application/json", "[" + string.Join(",", Enumerable.Range(0, 150).Select(i => "\"value " + i + "\"")) + "]");
            search.Text = "value 149";
            Check(tree.SelectedNode == tree.Nodes[0].Nodes[149] && tree.SelectedNode.IsVisible && tree.TopNode != tree.Nodes[0], "Search scrolls a distant match into the viewport");
            Check(search.Parent.Bottom == view.ClientSize.Height, "Search is docked at the bottom");
            Assign(response, "application/json", "  {\"escaped\":\"braces {} [] and quote \\\"ok\\\"\"}  ");
            Equal(Children(tree.Nodes[0]), "escaped: \"braces {} [] and quote \\\"ok\\\"\"");
            Assign(response, "application/json", "{\"slash\":\"a\\\\b\"}");
            Equal(Children(tree.Nodes[0]), "slash: \"a\\\\b\"");

            foreach (var invalid in new[] {
                Tuple.Create("application/json", "{\"a\":"), Tuple.Create("application/json", "<r/>"),
                Tuple.Create("application/json", "{} {}"), Tuple.Create("application/json", "[] true"),
                Tuple.Create("application/json", "\"x\" false"), Tuple.Create("application/json", "42 false"),
                Tuple.Create("application/xml", "<a><b></a>"),
                Tuple.Create("application/xml", "<!DOCTYPE r [<!ENTITY e SYSTEM 'file:///nonexistent'>]><r>&e;</r>"),
                Tuple.Create("application/xml; charset=unknown-charset", "<r/>") })
            {
                Assign(response, invalid.Item1, invalid.Item2);
                Check(tree.Nodes.Count == 0 && message.Visible && message.Text.StartsWith("Unable to display"), "Explicit parse error and no stale nodes: " + invalid.Item2);
                search.Text = "needle";
                Check(status.Text == "No matches", "No stale search after failure");
            }
            string xml = "<?xml version=\"1.0\" encoding=\"windows-1252\"?><r>caf\u00e9</r>";
            response.Assign(Headers("application/xml"), Encoding.GetEncoding(1252).GetBytes(xml), true);
            Equal(Children(tree.Nodes[1]), "Text: caf\u00e9");
            response.Assign(Headers("application/xml; charset=windows-1252"), Encoding.GetEncoding(1252).GetBytes("<r>caf\u00e9</r>"), true);
            Equal(Children(tree.Nodes[0]), "Text: caf\u00e9");
            byte[] unicode = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("{\"name\":\"caf\u00e9\"}")).ToArray();
            response.Assign(Headers("application/json"), unicode, true);
            Equal(Children(tree.Nodes[0]), "name: \"caf\u00e9\"");
            var compressedHeaders = Headers("application/json");
            compressedHeaders["Content-Encoding"] = "gzip";
            byte[] original = Encoding.UTF8.GetBytes("{\"compressed\":true}");
            byte[] compressed = Utilities.GzipCompress(original);
            response.Assign(compressedHeaders, compressed, false);
            Equal(Children(tree.Nodes[0]), "compressed: true");
            Check(ReferenceEquals(response.body, compressed) && ReferenceEquals(response.headers, compressedHeaders), "Original captured body and headers preserved");
            Check(!response.CommitAnyChanges(new Exchange(new HTTPRequestHeaders(), new byte[0])), "Viewer never commits edits");
            var chunkedHeaders = Headers("application/json");
            chunkedHeaders["Transfer-Encoding"] = "chunked";
            response.Assign(chunkedHeaders, Encoding.ASCII.GetBytes("2\r\n{}\r\n0\r\n\r\n"), true);
            Check(tree.Nodes[0].Text == "$: {}", "Chunked body decoding");
            Assign(response, "text/plain", "<r>detected</r>");
            Equal(Children(tree.Nodes[0]), "Text: detected");
            Assign(response, "text/plain", "{\"detected\":true}");
            Equal(Children(tree.Nodes[0]), "detected: true");
            search.Text = "detected";
            response.Clear();
            Check(tree.Nodes.Count == 0 && search.Text == "" && status.Text == "" && response.body == null && response.headers == null, "Complete clear");
            response.Assign(null, null, true);
            Check(message.Text == "No body to inspect." && tree.Nodes.Count == 0, "Missing body");
            response.SetFontSize(14);
            Check(tree.Font.SizeInPoints == 14, "Inspector font size");
            form.Close();
        }
        var request = new DataRequestInspector();
        using (var tab = new TabPage())
        {
            request.AddToTab(tab);
            var headers = new HTTPRequestHeaders("/", new[] { "Content-Type: application/json" });
            byte[] body = Encoding.UTF8.GetBytes("{\"request\":true}");
            request.Assign(headers, body, false);
            var tree = Field<TreeView>(tab.Controls[0], "tree");
            Equal(Children(tree.Nodes[0]), "request: true");
            Check(ReferenceEquals(request.headers, headers) && ReferenceEquals(request.body, body), "Request assignment state");
            request.Clear();
            Check(request.headers == null && request.body == null && tree.Nodes.Count == 0, "Request clear");
        }
        Console.WriteLine("Data inspector assertions passed: " + assertions);
    }
}
'@
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.ReferencedAssemblies.AddRange([string[]]@('System.dll', 'System.Core.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll', $corePath, $extensionPath))
Add-Type -TypeDefinition $tests -CompilerParameters $compiler
[DataInspectorTests]::Run()
