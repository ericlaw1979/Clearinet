using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;
using Microsoft.Win32;
using Clearinet;

[assembly: TargetFramework(".NETFramework,Version=v4.8.1")]

public static class DpiScalingHarness
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static int assertions;
    private static int failures;
    private static string context;
    private static SizeF startupMinimumAt96;
    private static readonly List<ExtensionCase> extensionCases = new List<ExtensionCase>();

    private sealed class ExtensionCase
    {
        public string Name;
        public InspectorBase Inspector;
        public TabPage Page;
        public Func<bool> Preserved;
        public string FontFamily;
    }

    private sealed class FontProbe : RequestInspectorBase
    {
        public int AddCalls;
        public int FontCalls;
        public override void Clear() { }
        public override int GetOrder() { return 0; }
        public override void Assign(HTTPRequestHeaders headers, byte[] body, bool readOnly) { }
        public override void AddToTab(TabPage target) { AddCalls++; }
        public override void SetFontSize(float size) { FontCalls++; }
    }

    private sealed class TracedViewer : frmViewer
    {
        private int paints;

        protected override void OnPaint(PaintEventArgs e)
        {
            paints++;
            base.OnPaint(e);
        }

        private void LogLayout(string phase)
        {
            Console.WriteLine("  Viewer layout " + phase + ": font=" + Font.SizeInPoints + ", scale="
                + AutoScaleDimensions + ", current=" + CurrentAutoScaleDimensions + ", client=" + ClientRectangle
                + ", left=" + Find(this, "pnlLeft").Bounds + ", main=" + Find(this, "pnlMain").Bounds);
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            LogLayout("before DPI " + e.DeviceDpiOld + " -> " + e.DeviceDpiNew);
            base.OnDpiChanged(e);
            LogLayout("after DPI");
        }

        protected override void OnLoad(EventArgs e)
        {
            context = "Before production Load";
            int dpi = MonitorDpi(Screen.FromControl(this));
            Console.WriteLine("  Before production Load: monitor=" + Screen.FromControl(this).DeviceName
                + ", window DPI=" + GetDpiForWindow(Handle) + ", managed DPI=" + DeviceDpi);
            Check(DeviceDpi == dpi && GetDpiForWindow(Handle) == dpi,
                "Managed and native DPI match the startup monitor before production Load");
            Check(paints == 0, "Startup DPI restoration completes before the form's first paint");
            LogLayout("before Load");
            base.OnLoad(e);
            LogLayout("after Load");
        }

        public override Size MinimumSize
        {
            get { return base.MinimumSize; }
            set
            {
                Size previous = base.MinimumSize;
                base.MinimumSize = value;
                if (previous != base.MinimumSize)
                    Console.WriteLine("  MinimumSize writer: " + previous + " -> " + base.MinimumSize
                        + ", requested=" + value + ", bounds=" + Bounds + ", DPI=" + DeviceDpi
                        + Environment.NewLine + new System.Diagnostics.StackTrace(1, false));
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern IntPtr GetThreadDpiAwarenessContext();
    [DllImport("user32.dll")]
    private static extern bool AreDpiAwarenessContextsEqual(IntPtr first, IntPtr second);
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int kind, out uint x, out uint y);
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetObject(IntPtr handle, int size, byte[] data);

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [DllImport("user32.dll", EntryPoint = "SendMessage")]
    private static extern IntPtr GetMinMaxInfo(IntPtr handle, int message, IntPtr wparam, ref MinMaxInfo info);

    private static Control Find(Control parent, string name)
    {
        Control[] controls = parent.Controls.Find(name, true);
        if (controls.Length != 1) throw new Exception("Expected one control named " + name);
        return controls[0];
    }

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (condition) return;
        failures++;
        Console.Error.WriteLine("FAIL [" + context + "]: " + message);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++)
        {
            Application.DoEvents();
            System.Threading.Thread.Sleep(10);
        }
    }

    private static void CheckFontUnits()
    {
        context = "Pure font units";
        foreach (int dpi in new[] { 96, 120, 144, 168, 192 })
            foreach (float points in new[] { 0f, 9.25f, 10f, 12f, 12.75f })
            {
                float managed = DpiFontUnits.LogicalFontPointsToManagedFontPoints(points, dpi);
                Check(Math.Abs(managed - points * dpi / 96f) < 0.0001f, "Logical-to-managed conversion at " + dpi);
                Check(Math.Abs(DpiFontUnits.ManagedFontPointsToLogicalFontPoints(managed, dpi) - points) < 0.0001f,
                    "Fractional font points round trip at " + dpi);
            }
        foreach (int dpi in new[] { 0, -96 })
        {
            bool forwardRejected = false, inverseRejected = false;
            try { DpiFontUnits.LogicalFontPointsToManagedFontPoints(10f, dpi); }
            catch (ArgumentOutOfRangeException e) { forwardRejected = e.ParamName == "deviceDpi"; }
            try { DpiFontUnits.ManagedFontPointsToLogicalFontPoints(10f, dpi); }
            catch (ArgumentOutOfRangeException e) { inverseRejected = e.ParamName == "deviceDpi"; }
            Check(forwardRejected && inverseRejected, "Both conversions reject nonpositive DPI " + dpi);
        }
    }

    private static void CheckFont(IntPtr handle, float logicalPoints, int dpi, string name)
    {
        byte[] data = new byte[92];
        IntPtr font = SendMessage(handle, 0x31 /* WM_GETFONT */, IntPtr.Zero, IntPtr.Zero);
        bool valid = font != IntPtr.Zero && GetObject(font, data.Length, data) == data.Length;
        Check(valid, name + " has a valid native font");
        if (!valid) return;
        int height = Math.Abs(BitConverter.ToInt32(data, 0));
        int expected = (int)Math.Round(logicalPoints * dpi / 72f);
        Console.WriteLine("  " + name + ": native=" + height + "px, expected=" + expected + "px");
        Check(Math.Abs(height - expected) <= 1,
            name + " native height " + height + "px should be " + expected + "px at " + dpi + " DPI");
    }

    private static void CheckFont(Control control, int dpi)
    {
        if (control is RichTextBox)
        {
            byte[] format = new byte[92];
            Array.Copy(BitConverter.GetBytes(format.Length), format, 4);
            GCHandle pinned = GCHandle.Alloc(format, GCHandleType.Pinned);
            try
            {
                SendMessage(control.Handle, 0x43a /* EM_GETCHARFORMAT */, IntPtr.Zero, pinned.AddrOfPinnedObject());
                Check((BitConverter.ToUInt32(format, 4) & 0x80000000u) != 0,
                    control.Name + " has a valid native character font size");
                int height = BitConverter.ToInt32(format, 12);
                float expected = CONFIG.flFontSize * 20f;
                Console.WriteLine("  " + control.Name + ": native=" + height + "twips, expected=" + expected
                    + "twips, zoom=" + ((RichTextBox)control).ZoomFactor + ", window DPI=" + GetDpiForWindow(control.Handle));
                Check(Math.Abs(height - expected) <= 1,
                    control.Name + " native character font preserves the logical point-size preference");
                Check(GetDpiForWindow(control.Handle) == dpi,
                    control.Name + " native window uses the current monitor DPI");
            }
            finally { pinned.Free(); }
        }
        else CheckFont(control.Handle, CONFIG.flFontSize, dpi, control.Name);
        Console.WriteLine("    managed=" + control.Font.SizeInPoints.ToString("0.##", CultureInfo.InvariantCulture)
            + "pt, DeviceDpi=" + control.DeviceDpi);
    }

    private static int MonitorDpi(Screen screen)
    {
        uint x, y;
        Marshal.ThrowExceptionForHR(GetDpiForMonitor(MonitorFromPoint(
            new Point(screen.Bounds.Left + screen.Bounds.Width / 2, screen.Bounds.Top + screen.Bounds.Height / 2), 0),
            0, out x, out y));
        return (int)y;
    }

    private static void CheckHeadings(frmViewer viewer, string phase)
    {
        context = phase;
        int dpi = MonitorDpi(Screen.FromControl(viewer));
        Console.WriteLine(phase + ": monitor=" + Screen.FromControl(viewer).DeviceName + ", DPI=" + dpi
            + ", bounds=" + viewer.Bounds + ", minimum=" + viewer.MinimumSize);
        Console.WriteLine("  Form font=" + viewer.Font.SizeInPoints + "pt, scale=" + viewer.AutoScaleDimensions
            + ", current scale=" + viewer.CurrentAutoScaleDimensions);
        Check(viewer.DeviceDpi == dpi, "Managed DPI matches the actual window DPI");
        Check(GetDpiForWindow(viewer.Handle) == dpi, "Window DPI matches the independently queried monitor DPI");
        Check(viewer.AutoScaleDimensions == viewer.CurrentAutoScaleDimensions,
            "Font autoscaling baseline matches the current DPI-scaled font metrics");
        CheckFont(Find(viewer, "tabsViews"), dpi);
        Control list = Find(viewer, "lvExchanges");
        CheckFont(list, dpi);
        CheckFont(SendMessage(list.Handle, 0x101f /* LVM_GETHEADER */, IntPtr.Zero, IntPtr.Zero),
            CONFIG.flFontSize, dpi, "Capture column header");
        float fontFactor = Math.Max(1f, CONFIG.flFontSize / 9.75f);
        Check(viewer.MinimumSize.Width <= (464 * fontFactor + 64) * dpi / 96f
            && viewer.MinimumSize.Height <= (387 * fontFactor + 64) * dpi / 96f,
            "MinimumSize remains near the design minimum, not a previous monitor's window size");
        Check(Math.Abs(viewer.MinimumSize.Width - startupMinimumAt96.Width * dpi / 96f) <= 2
            && Math.Abs(viewer.MinimumSize.Height - startupMinimumAt96.Height * dpi / 96f) <= 2,
            "MinimumSize preserves the startup constraint across DPI round trips");
    }

    private static void MoveTo(frmViewer viewer, Screen screen)
    {
        viewer.Location = new Point(screen.WorkingArea.Left + 20, screen.WorkingArea.Top + 20);
        Pump();
        Check(Screen.FromControl(viewer).DeviceName == screen.DeviceName, "Window moved onto " + screen.DeviceName);
    }

    private static void CheckResize(frmViewer viewer)
    {
        Rectangle area = Screen.FromControl(viewer).WorkingArea;
        int dpi = MonitorDpi(Screen.FromControl(viewer));
        int width = Math.Min((int)(950 * dpi / 96f), area.Width - 100);
        int height = Math.Min((int)(650 * dpi / 96f), area.Height - 100);
        int grow = Math.Max(30, (int)(40 * dpi / 96f));
        var info = new MinMaxInfo();
        GetMinMaxInfo(viewer.Handle, 0x24 /* WM_GETMINMAXINFO */, IntPtr.Zero, ref info);
        Console.WriteLine("  Native minimum tracking size=" + info.MinTrackSize);
        Check(info.MinTrackSize.X <= width && info.MinTrackSize.Y <= height,
            "Native mouse-sizing minimum allows the requested usable size");
        Check(SetWindowPos(viewer.Handle, IntPtr.Zero, 0, 0, width + grow, height + grow, 0x16),
            "Native resize can grow the window");
        Pump();
        Size larger = viewer.Size;
        Check(SetWindowPos(viewer.Handle, IntPtr.Zero, 0, 0, width, height, 0x16),
            "Native resize can shrink the window");
        Pump();
        Console.WriteLine("  Resize: larger=" + larger + ", requested=" + new Size(width, height)
            + ", actual=" + viewer.Size + ", minimum=" + viewer.MinimumSize);
        Check(viewer.Width < larger.Width && viewer.Height < larger.Height,
            "Window shrinks in both directions after moving monitors");
        Check(Math.Abs(viewer.Width - width) <= 2 && Math.Abs(viewer.Height - height) <= 2,
            "Resize is not clamped above the requested usable size");
    }

    private static void ShowInspectors(frmViewer viewer)
    {
        ((TabControl)Find(viewer, "tabsViews")).SelectedTab = (TabPage)Find(viewer, "pageInspectors");
        foreach (Control control in Find(viewer, "pageInspectors").Controls)
            if (control is Label) control.Visible = false;
        Find(viewer, "tabsRequest").Visible = Find(viewer, "tabsResponse").Visible = true;
        Pump();
    }

    private static void PrepareEditors(frmViewer viewer)
    {
        var request = (TabControl)Find(viewer, "tabsRequest");
        var response = (TabControl)Find(viewer, "tabsResponse");
        Type type = Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CAE-Default.dll"))
            .GetType("Extensions.RequestHeaderInspector", true);
        var inspector = (RequestInspectorBase)Activator.CreateInstance(type);
        var headers = new TabPage("Headers") { Tag = inspector };
        request.TabPages.Add(headers);
        var responseHeaders = new TabPage("Headers");
        response.TabPages.Add(responseHeaders);
        request.SelectedTab = headers;
        response.SelectedTab = responseHeaders;
        ShowInspectors(viewer);
        typeof(InspectorBase).GetMethod("EnsureTabReady", Hidden).Invoke(inspector, new object[] { headers });
        Pump();
        request.TabPages.Add(new TabPage("Lazy font probe") { Tag = new FontProbe() });
        var page = (TabPage)Find(viewer, "pageComposer");
        int layouts = 0;
        LayoutEventHandler recordLayout = (sender, e) => layouts++;
        page.Layout += recordLayout;
        ((TabControl)Find(viewer, "tabsViews")).SelectedTab = page;
        Control composer = Find(viewer, "ComposerView");
        Rectangle initialBounds = composer.Bounds;
        float initialFont = composer.Font.SizeInPoints;
        var initialLayout = new Dictionary<string, Rectangle>();
        foreach (string name in new[] { "layoutRequest", "pnlMessage", "cbxMethod", "txtURL", "cbxHTTPVersion", "btnSend" })
            initialLayout.Add(name, Find(composer, name).Bounds);
        Pump();
        page.Layout -= recordLayout;
        Console.WriteLine("  Composer initialization: " + layouts + " parent layouts, bounds=" + initialBounds
            + " -> " + composer.Bounds + ", font=" + initialFont + " -> " + composer.Font.SizeInPoints);
        Check(composer.Bounds == initialBounds && Math.Abs(initialFont - composer.Font.SizeInPoints) < 0.1f,
            "Composer size and font settle synchronously when the tab is selected");
        foreach (KeyValuePair<string, Rectangle> control in initialLayout)
            Check(Find(composer, control.Key).Bounds == control.Value,
                control.Key + " layout settles synchronously on first Composer activation");
        Find(viewer, "txtURL").Text = "https://example.test/api";
        Find(viewer, "txtHeaders").Text = "Content-Type: application/json";
        Find(viewer, "txtBody").Text = "{\"test\":true}";
        foreach (string name in new[] { "txtURL", "txtHeaders", "txtBody" })
            ((TextBox)Find(composer, name)).Select(3, 4);
    }

    private static void CheckComposerTabOrder(Control composer)
    {
        var names = new List<string>();
        Control next = null;
        while ((next = composer.GetNextControl(next, true)) != null)
            if (next.TabStop && (next is TextBoxBase || next is ComboBox || next is Button))
                names.Add(next.Name);
        string actual = string.Join(" -> ", names);
        Check(actual == "cbxMethod -> txtURL -> cbxHTTPVersion -> btnSend -> txtHeaders -> txtBody",
            "Composer retains its logical tab order: " + actual);
    }

    private static void CheckRequestLayout(Control composer, bool? expectedWrap)
    {
        CheckComposerTabOrder(composer);
        var row = (TableLayoutPanel)Find(composer, "layoutRequest");
        Control url = Find(composer, "txtURL");
        bool wrapped = row.GetRow(url) == 1;
        int available = row.ClientSize.Width - row.Padding.Horizontal;
        if (expectedWrap.HasValue) Check(wrapped == expectedWrap.Value, "Composer wraps only below the measured breakpoint");
        Check(row.GetColumnSpan(url) == (wrapped ? 4 : 1), "URL spans the full table only when wrapped");
        Check(wrapped ? url.Width == available - url.Margin.Horizontal : url.Width >= composer.LogicalToDeviceUnits(240),
            "URL has a usable single-row width or fills the wrapped row");
        Control[] controls = { Find(composer, "cbxMethod"), url, Find(composer, "cbxHTTPVersion"), Find(composer, "btnSend") };
        for (int i = 0; i < controls.Length; i++)
        {
            Check(controls[i].Parent == row && controls[i].Visible && controls[i].Width > 0
                && row.ClientRectangle.Contains(controls[i].Bounds), controls[i].Name + " stays usable in the same parent");
            for (int j = i + 1; j < controls.Length; j++)
                Check(!controls[i].Bounds.IntersectsWith(controls[j].Bounds), controls[i].Name + " does not overlap " + controls[j].Name);
        }
        Control send = controls[3];
        Check(send.Width >= send.GetPreferredSize(Size.Empty).Width, "Send retains its preferred content width");
        Control panel = Find(composer, "pnlMessage");
        Check(!row.Bounds.IntersectsWith(panel.Bounds) && composer.ClientRectangle.Contains(panel.Bounds)
            && panel.Height > 0, "Request area and editors remain usable and non-overlapping");
        Check(row.Height < composer.Font.Height * 6, "Composer request area stays compact");
    }

    private static void CheckComposerResize(Control composer)
    {
        var row = (TableLayoutPanel)Find(composer, "layoutRequest");
        DockStyle dock = composer.Dock;
        Rectangle bounds = composer.Bounds;
        string previousContext = context;
        int textEvents = 0;
        EventHandler recordText = (sender, e) => textEvents++;
        var editors = new List<TextBox>();
        var parents = new List<Control>();
        foreach (string name in new[] { "txtURL", "txtHeaders", "txtBody" })
        {
            var text = (TextBox)Find(composer, name);
            editors.Add(text);
            parents.Add(text.Parent);
            text.TextChanged += recordText;
        }
        string method = Find(composer, "cbxMethod").Text;
        string version = Find(composer, "cbxHTTPVersion").Text;
        try
        {
            // Exercise the production view below the main form's minimum without altering its
            // minimum policy. Docking and original geometry are restored before monitor moves.
            composer.Dock = DockStyle.None;
            composer.Width = composer.LogicalToDeviceUnits(1800);
            int overhead = composer.Padding.Horizontal + row.Padding.Horizontal;
            int breakpoint = (int)(row.ColumnStyles[0].Width + row.ColumnStyles[2].Width + row.ColumnStyles[3].Width)
                + composer.LogicalToDeviceUnits(240) + Find(composer, "txtURL").Margin.Horizontal;
            foreach (int width in new[] { breakpoint + 200, breakpoint + 1, breakpoint, breakpoint - 1,
                composer.LogicalToDeviceUnits(300), composer.LogicalToDeviceUnits(180),
                breakpoint + 1, breakpoint - 1, breakpoint + 200 })
            {
                context = previousContext + " / Composer width " + width;
                composer.Width = width + overhead;
                CheckRequestLayout(composer, width < breakpoint);
                var geometry = new Dictionary<Control, Rectangle>();
                foreach (string name in new[] { "layoutRequest", "pnlMessage", "cbxMethod", "txtURL", "cbxHTTPVersion", "btnSend" })
                {
                    Control control = Find(composer, name);
                    geometry.Add(control, control.Bounds);
                }
                Pump();
                composer.PerformLayout();
                foreach (KeyValuePair<Control, Rectangle> item in geometry)
                    Check(item.Key.Bounds == item.Value, item.Key.Name + " resize settles synchronously without later churn");
                for (int i = 0; i < editors.Count; i++)
                    Check(editors[i].Parent == parents[i] && editors[i].SelectionStart == 3 && editors[i].SelectionLength == 4,
                        editors[i].Name + " resize preserves parent and selection");
                Check(Find(composer, "cbxMethod").Text == method && Find(composer, "cbxHTTPVersion").Text == version,
                    "Resize preserves method and HTTP version");
                Check(textEvents == 0, "Responsive layout emits no editor TextChanged events");
                Console.WriteLine("  Composer resize: available=" + width + ", breakpoint=" + breakpoint
                    + ", wrapped=" + (row.GetRow(Find(composer, "txtURL")) == 1));
            }
        }
        finally
        {
            foreach (TextBox editor in editors) editor.TextChanged -= recordText;
            composer.Bounds = bounds;
            composer.Dock = dock;
            context = previousContext;
        }
    }

    private static void CheckEditors(frmViewer viewer)
    {
        int dpi = MonitorDpi(Screen.FromControl(viewer));
        ShowInspectors(viewer);
        foreach (string name in new[] { "tabsRequest", "tabsResponse", "txtFirstLine", "tvNVP" })
            CheckFont(Find(viewer, name), dpi);
        ((TabControl)Find(viewer, "tabsViews")).SelectedTab = (TabPage)Find(viewer, "pageComposer");
        Pump();
        Control composer = Find(viewer, "ComposerView");
        Control panel = Find(composer, "pnlMessage");
        Control row = Find(composer, "layoutRequest");
        var container = (ContainerControl)composer;
        Console.WriteLine("  Composer geometry: client=" + composer.ClientRectangle + ", padding=" + composer.Padding
            + ", panel=" + panel.Bounds + ", row=" + row.Bounds + ", row client=" + row.ClientRectangle
            + ", scale=" + container.AutoScaleDimensions + ", current=" + container.CurrentAutoScaleDimensions);
        Check(composer.ClientRectangle.Contains(panel.Bounds) && panel.Width > composer.Width * 0.8,
            "Composer editors fill the available width without clipping");
        CheckRequestLayout(composer, null);
        CheckComposerResize(composer);
        foreach (string name in new[] { "cbxMethod", "txtURL", "cbxHTTPVersion", "btnSend", "txtHeaders", "txtBody" })
        {
            Control control = Find(composer, name);
            Console.WriteLine("    " + name + ": bounds=" + control.Bounds + ", parent client="
                + control.Parent.ClientRectangle + ", margin=" + control.Margin);
            if (!(control is Button)) CheckFont(control, dpi);
            Check(control.Visible && control.Width > 0 && control.Height > 0
                && control.Parent.ClientRectangle.Contains(control.Bounds), name + " stays inside its parent");
        }
        Check(Find(composer, "txtURL").Text == "https://example.test/api"
            && Find(composer, "txtHeaders").Text == "Content-Type: application/json"
            && Find(composer, "txtBody").Text == "{\"test\":true}", "DPI moves preserve request data");
        CheckExtensions(viewer);
    }

    private static Control PrivateControl(Control view, string field)
    {
        return (Control)view.GetType().GetField(field, Hidden).GetValue(view);
    }

    private static void AttachExtensions(frmViewer viewer)
    {
        Assembly assembly = Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CAE-Default.dll"));
        foreach (string name in new[] { "RawResponseInspector", "HexRequestInspector", "RequestCookieInspector", "DataResponseInspector" })
        {
            var inspector = (InspectorBase)Activator.CreateInstance(assembly.GetType("Extensions." + name, true));
            var page = new TabPage(name) { Tag = inspector };
            ((TabControl)Find(viewer, inspector is RequestInspectorBase ? "tabsRequest" : "tabsResponse")).TabPages.Add(page);
            extensionCases.Add(new ExtensionCase { Name = name, Inspector = inspector, Page = page });
        }
    }

    private static string TreeContent(TreeNodeCollection nodes)
    {
        var text = new System.Text.StringBuilder();
        foreach (TreeNode node in nodes)
            text.Append(node.Text.Length).Append(':').Append(node.Text).Append('[').Append(TreeContent(node.Nodes)).Append(']');
        return text.ToString();
    }

    private static bool BytesMatch(object provider, byte[] expected)
    {
        Type type = provider.GetType();
        if ((long)type.GetProperty("Length").GetValue(provider, null) != expected.Length) return false;
        MethodInfo read = type.GetMethod("ReadByte");
        for (int i = 0; i < expected.Length; i++)
            if ((byte)read.Invoke(provider, new object[] { (long)i }) != expected[i]) return false;
        return true;
    }

    private static void SeedExtension(ExtensionCase test, Control editor)
    {
        byte[] body = System.Text.Encoding.UTF8.GetBytes("{\"message\":\"DPI test\",\"items\":[1,2,3]}");
        var request = new HTTPRequestHeaders("/dpi", new[] { "Content-Type: application/json", "Cookie: dpi=preserved; mode=test" });
        var response = new HTTPResponseHeaders(200, "OK", new[] { "Content-Type: application/json" });
        if (test.Inspector is RequestInspectorBase)
            ((RequestInspectorBase)test.Inspector).Assign(request, body, true);
        else ((ResponseInspectorBase)test.Inspector).Assign(response, body, true);
        if (editor is TextBoxBase)
        {
            var text = (TextBoxBase)editor;
            Check(text.Text.Contains(test.Name == "RequestCookieInspector" ? "preserved" : "DPI test"),
                test.Name + " renders the assigned sample");
            text.Select(5, 4);
            string expected = text.Text;
            bool readOnly = text.ReadOnly;
            var richText = text as RichTextBox;
            if (richText != null)
            {
                richText.Modified = true;
                test.Inspector.SetFontSize(CONFIG.flFontSize * editor.DeviceDpi / 96f);
                Check(richText.Modified, test.Name + " font refresh preserves an already-modified document");
                richText.Modified = false;
                test.Inspector.SetFontSize(CONFIG.flFontSize * editor.DeviceDpi / 96f);
                Check(!richText.Modified, test.Name + " font refresh preserves a clean document");
            }
            test.Preserved = () =>
            {
                bool preserved = text.Text == expected && text.SelectionStart == 5 && text.SelectionLength == 4
                    && text.ReadOnly == readOnly && (richText == null || !richText.Modified);
                if (!preserved) Console.WriteLine(test.Name + " preservation: text=" + (text.Text == expected)
                    + ", selection=" + text.SelectionStart + "/" + text.SelectionLength
                    + ", readOnly=" + text.ReadOnly + "/" + readOnly
                    + ", modified=" + (richText != null && richText.Modified));
                return preserved;
            };
        }
        else if (editor is TreeView)
        {
            var tree = (TreeView)editor;
            Font currentFont = tree.Font;
            test.Inspector.SetFontSize(currentFont.SizeInPoints);
            Check(object.ReferenceEquals(tree.Font, currentFont), "Data equivalent font refresh retains its active font instance");
            Check(tree.Nodes.Count == 1 && tree.Nodes[0].Nodes.Count == 2, "Data inspector renders the assigned JSON tree");
            tree.ExpandAll();
            tree.SelectedNode = tree.Nodes[0].Nodes[0];
            TreeNode selected = tree.SelectedNode;
            string expected = TreeContent(tree.Nodes);
            test.Preserved = () => TreeContent(tree.Nodes) == expected && tree.SelectedNode == selected && tree.Nodes[0].IsExpanded;
        }
        else
        {
            Font currentFont = editor.Font;
            test.Inspector.SetFontSize(currentFont.SizeInPoints);
            Check(object.ReferenceEquals(editor.Font, currentFont), "Hex equivalent font refresh retains its active font instance");
            Type type = editor.GetType();
            PropertyInfo providerProperty = type.GetProperty("ByteProvider");
            object provider = providerProperty.GetValue(editor, null);
            byte[] headers = request.ToByteArray();
            byte[] expected = new byte[headers.Length + body.Length];
            Buffer.BlockCopy(headers, 0, expected, 0, headers.Length);
            Buffer.BlockCopy(body, 0, expected, headers.Length, body.Length);
            PropertyInfo start = type.GetProperty("SelectionStart");
            PropertyInfo length = type.GetProperty("SelectionLength");
            start.SetValue(editor, 5L, null);
            length.SetValue(editor, 4L, null);
            Check(BytesMatch(provider, expected), "Hex inspector renders the assigned header/body bytes");
            test.Preserved = () => providerProperty.GetValue(editor, null) == provider && BytesMatch(provider, expected)
                && (long)start.GetValue(editor, null) == 5 && (long)length.GetValue(editor, null) == 4;
        }
    }

    private static void CheckExtensions(frmViewer viewer)
    {
        if (extensionCases.Count == 0) return;
        string previousContext = context;
        var request = (TabControl)Find(viewer, "tabsRequest");
        var response = (TabControl)Find(viewer, "tabsResponse");
        TabPage requestPage = request.SelectedTab;
        TabPage responsePage = response.SelectedTab;
        // Normal no-selection tab changes clear both inspectors; test preservation before selecting any tab.
        foreach (ExtensionCase test in extensionCases)
            if (test.Preserved != null) Check(test.Preserved(), test.Name + " preserves content and selection across font/DPI changes");
        ShowInspectors(viewer);
        foreach (ExtensionCase test in extensionCases)
        {
            context = previousContext + " / " + test.Name;
            ((TabControl)test.Page.Parent).SelectedTab = test.Page;
            ShowInspectors(viewer);
            typeof(InspectorBase).GetMethod("EnsureTabReady", Hidden).Invoke(test.Inspector, new object[] { test.Page });
            Pump();
            Check(test.Page.Controls.Count == 1, test.Name + " creates one lazy inspector view");
            Control view = test.Page.Controls[0];
            Control editor = test.Name == "RawResponseInspector" ? Find(view, "rtbRaw")
                : PrivateControl(view, test.Name == "HexRequestInspector" ? "hexBox"
                    : test.Name == "RequestCookieInspector" ? "txtCookies" : "tree");
            int dpi = viewer.DeviceDpi;
            Console.WriteLine("  Extension " + test.Name + ": editor=" + editor.GetType().Name + ", font="
                + editor.Font.SizeInPoints + ", DeviceDpi=" + editor.DeviceDpi + ", view=" + view.Bounds);
            Check(view.Visible && test.Page.ClientRectangle.Contains(view.Bounds) && view.Width > test.Page.ClientSize.Width * 0.8,
                test.Name + " fills the visible tab without clipping");
            if (test.FontFamily == null) test.FontFamily = editor.Font.FontFamily.Name;
            Check(editor.Font.FontFamily.Name == test.FontFamily, test.Name + " preserves its configured font face");
            Check(Math.Abs(editor.Font.SizeInPoints - CONFIG.flFontSize * dpi / 96f) < 0.1f,
                test.Name + " paint/managed font matches the configured preference at current DPI");
            // A hidden Data tree can still be handleless with provisional DeviceDpi. Query its
            // native font first, then assert established DPI, without reseeding or refreshing fonts.
            if (test.Name != "HexRequestInspector") CheckFont(editor, dpi);
            Check(editor.DeviceDpi == dpi && GetDpiForWindow(editor.Handle) == dpi,
                test.Name + " managed/native window DPI matches the viewer");
            if (test.Name == "HexRequestInspector")
                using (Graphics graphics = editor.CreateGraphics())
                using (var logicalFont = new Font(editor.Font.FontFamily, CONFIG.flFontSize, editor.Font.Style))
                {
                    float actual = editor.Font.GetHeight(graphics);
                    float expected = logicalFont.GetHeight(dpi);
                    Console.WriteLine("    Hex paint metrics: graphics DPI=" + graphics.DpiY + ", monitor DPI=" + dpi
                        + ", actual height=" + actual + ", expected=" + expected);
                    Check(Math.Abs(actual - expected) <= 1, "Hex GDI+ paint font matches logical points at physical monitor DPI");
                }
            if (test.Name == "RawResponseInspector")
            {
                Control searchBox = Find(view, "txtFind");
                CheckFont(searchBox, dpi);
                Check(searchBox.Visible && searchBox.Width > 0 && searchBox.Height > 0
                    && searchBox.Parent.ClientRectangle.Contains(searchBox.Bounds), "Raw search box stays usable inside its panel");
                Check(!editor.Bounds.IntersectsWith(searchBox.Parent.Bounds), "Raw editor and search row do not overlap");
                Console.WriteLine("    Raw geometry: editor=" + editor.Bounds + ", parent=" + editor.Parent.ClientRectangle
                    + ", bottom=" + Find(view, "pnlBottom").Bounds + ", visible=" + editor.Visible);
            }
            // Assert lifecycle/native font state before Assign/SetFontSize can repair it.
            SeedExtension(test, editor);
            Pump();
            Check(editor.Visible && editor.Width > 0 && editor.Height > 0 && editor.Parent.ClientRectangle.Contains(editor.Bounds),
                test.Name + " content editor stays usable inside its parent");
            Check(test.Preserved(), test.Name + " seeded selection is stable after activation");
        }
        request.SelectedTab = requestPage;
        response.SelectedTab = responsePage;
        ((TabControl)Find(viewer, "tabsViews")).SelectedTab = (TabPage)Find(viewer, "pageComposer");
        foreach (ExtensionCase test in extensionCases)
        {
            Control view = test.Page.Controls[0];
            Control editor = test.Name == "RawResponseInspector" ? Find(view, "rtbRaw")
                : PrivateControl(view, test.Name == "HexRequestInspector" ? "hexBox"
                    : test.Name == "RequestCookieInspector" ? "txtCookies" : "tree");
            SeedExtension(test, editor);
        }
        Pump();
        context = previousContext;
    }

    private static void InvokeConfig(string method)
    {
        typeof(CONFIG).GetMethod(method, Static).Invoke(null, null);
    }

    private static Font OwnedFont(Control view, string name)
    {
        return (Font)view.GetType().GetField(name, Hidden).GetValue(view);
    }

    private static bool FontUsable(Font font)
    {
        if (font == null) return false;
        try { return font.GetHeight(96f) > 0; }
        catch (ArgumentException) { return false; }
        catch (ObjectDisposedException) { return false; }
    }

    private static void CheckInspectorFontNotifications()
    {
        Assembly assembly = Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CAE-Default.dll"));
        foreach (string name in new[] { "RequestCookieInspector", "ResponseCookieInspector",
            "RequestAuthInspector", "ResponseAuthInspector", "Transformer" })
        {
            context = "Inspector font notifications / " + name;
            Font borrowed = null;
            Font finalOwned = null;
            try
            {
                using (var page = new TabPage(name))
                {
                    var inspector = (InspectorBase)Activator.CreateInstance(assembly.GetType("Extensions." + name, true));
                    typeof(InspectorBase).GetMethod("EnsureTabReady", Hidden).Invoke(inspector, new object[] { page });
                    Check(page.Controls.Count == 1, "Lazy initialization creates one view");
                    Control view = page.Controls[0];
                    Control editor = name == "Transformer" ? view
                        : PrivateControl(view, name.Contains("Cookie") ? "txtCookies" : "txtAuth");
                    Check(FontUsable(editor.Font), "Initial font notification leaves a usable active font");
                    if (!FontUsable(editor.Font)) continue;
                    float initial = editor.Font.SizeInPoints;
                    var text = editor as TextBoxBase;
                    if (text != null)
                    {
                        text.Text = "Preserve text and selection";
                        text.Select(3, 4);
                    }
                    bool readOnly = text != null && text.ReadOnly;
                    int textEvents = 0;
                    if (text != null) text.TextChanged += (sender, e) => textEvents++;
                    foreach (float points in new[] { initial, initial, initial + 2.5f, initial + 2.5f, initial })
                    {
                        Font active = editor.Font;
                        Font owned = OwnedFont(view, "viewFont");
                        bool sameSize = active.SizeInPoints == points;
                        inspector.SetFontSize(points);
                        Check(FontUsable(editor.Font) && Math.Abs(editor.Font.SizeInPoints - points) < 0.01f,
                            "Font notification preserves a usable font at the requested size");
                        if (sameSize)
                            Check(object.ReferenceEquals(active, editor.Font)
                                && object.ReferenceEquals(owned, OwnedFont(view, "viewFont")) && FontUsable(owned),
                                "Equal notification retains active and owned font instances");
                        else
                            Check(!FontUsable(owned), "Replacement disposes the superseded owned font");
                        if (text != null)
                            Check(text.Text == "Preserve text and selection" && text.SelectionStart == 3
                                && text.SelectionLength == 4 && text.ReadOnly == readOnly && textEvents == 0,
                                "Font notification preserves content, selection, read-only state and text events");
                    }
                    borrowed = new Font(editor.Font.FontFamily, initial + 1f);
                    editor.Font = borrowed;
                    Font superseded = OwnedFont(view, "viewFont");
                    inspector.SetFontSize(initial + 1f);
                    Check(object.ReferenceEquals(editor.Font, borrowed) && FontUsable(borrowed)
                        && object.ReferenceEquals(OwnedFont(view, "viewFont"), superseded) && FontUsable(superseded),
                        "Equal notification does not adopt or dispose a borrowed active font");
                    inspector.SetFontSize(initial);
                    Check(FontUsable(borrowed) && !FontUsable(superseded),
                        "Replacement disposes only its owned font, not a borrowed active font");
                    finalOwned = OwnedFont(view, "viewFont");
                    editor.Disposed += (sender, e) => Check(FontUsable(finalOwned), "Owned font outlives its consumer");
                }
                Check(!FontUsable(finalOwned), "Disposal releases the final owned font");
                Check(FontUsable(borrowed), "View disposal leaves the borrowed font usable");
            }
            finally { if (borrowed != null) borrowed.Dispose(); }
        }
    }

    private static void RunComponentChecks()
    {
        CheckInspectorFontNotifications();
        using (var composer = new ComposerView())
            foreach (int width in new[] { 1000, 500, 260, 1000 })
            {
                context = "Composer component width " + width;
                composer.Size = new Size(width, 600);
                composer.PerformLayout();
                CheckRequestLayout(composer, null);
            }
        Console.WriteLine("DPI components: " + assertions + " assertions, " + failures + " failures.");
    }

    private static void CheckRawOwnedFonts(ExtensionCase test, Control view, RichTextBox editor)
    {
        context = "Raw owned fonts";
        Control search = Find(view, "txtFind");
        Font activeEditor = editor.Font;
        Font activeSearch = search.Font;
        Font ownedEditor = OwnedFont(view, "editorFont");
        Font ownedSearch = OwnedFont(view, "searchFont");
        float points = activeEditor.SizeInPoints;
        test.Inspector.SetFontSize(points);
        for (int i = 0; i < 3; i++)
        {
            view.Visible = false;
            view.Visible = true;
        }
        Check(object.ReferenceEquals(editor.Font, activeEditor) && object.ReferenceEquals(search.Font, activeSearch),
            "Equivalent Raw notifications and visibility retain active font instances");
        Check(object.ReferenceEquals(OwnedFont(view, "editorFont"), ownedEditor)
            && object.ReferenceEquals(OwnedFont(view, "searchFont"), ownedSearch),
            "Equivalent Raw notifications retain owned font instances");
        test.Inspector.SetFontSize(points + 1f);
        Check(!FontUsable(ownedEditor) && !FontUsable(ownedSearch), "Raw replacement disposes superseded owned fonts");
        if (!object.ReferenceEquals(activeEditor, ownedEditor))
            Check(FontUsable(activeEditor), "Raw replacement does not dispose a Framework-scaled editor font");
        if (!object.ReferenceEquals(activeSearch, ownedSearch))
            Check(FontUsable(activeSearch), "Raw replacement does not dispose a Framework-scaled search font");
        Check(FontUsable(editor.Font) && FontUsable(search.Font), "Replacement Raw fonts remain usable");
        test.Inspector.SetFontSize(points);
    }

    private static void CheckRawEditableLifecycle(frmViewer viewer, Screen start, Screen[] screens,
        List<int> representatives, bool runtimeFonts)
    {
        ExtensionCase test = extensionCases.Find(item => item.Name == "RawResponseInspector");
        ((TabControl)test.Page.Parent).SelectedTab = test.Page;
        ShowInspectors(viewer);
        Control view = test.Page.Controls[0];
        var editor = (RichTextBox)Find(view, "rtbRaw");
        Control search = Find(view, "txtFind");
        CheckRawOwnedFonts(test, view, editor);
        int initialFont = (int)CONFIG.flFontSize;
        foreach (bool dirty in new[] { false, true })
        {
            context = "Raw editable " + (dirty ? "dirty" : "clean");
            var response = new HTTPResponseHeaders(200, "OK", new[] { "Content-Type: text/plain" });
            ((ResponseInspectorBase)test.Inspector).Assign(response,
                System.Text.Encoding.UTF8.GetBytes("Editable DPI document\r\nPreserve undo and selection"), false);
            string original = editor.Text;
            editor.ClearUndo();
            editor.Select(editor.TextLength, 0);
            editor.SelectedText = "!";
            string edited = editor.Text;
            Check(edited == original + "!" && editor.CanUndo, "Raw setup creates an undoable text edit");
            editor.Select(5, 4);
            editor.Modified = dirty;
            int textEvents = 0;
            int readOnlyEvents = 0;
            int modifiedEvents = 0;
            bool redoPending = false;
            EventHandler onText = (sender, e) => textEvents++;
            EventHandler onReadOnly = (sender, e) => readOnlyEvents++;
            EventHandler onModified = (sender, e) => modifiedEvents++;
            editor.TextChanged += onText;
            editor.ReadOnlyChanged += onReadOnly;
            editor.ModifiedChanged += onModified;
            Action<string, Action> transition = (phase, action) =>
            {
                context = "Raw editable " + (dirty ? "dirty" : "clean") + " / " + phase;
                textEvents = readOnlyEvents = modifiedEvents = 0;
                action();
                Pump();
                Check(editor.Text == (redoPending ? original : edited) && editor.SelectionStart == 5 && editor.SelectionLength == 4,
                    "Raw presentation transition preserves text and selection before any refresh");
                Check(!editor.ReadOnly && editor.Modified == dirty,
                    "Raw presentation transition preserves editable and Modified state");
                Check(textEvents == 0 && readOnlyEvents == 0,
                    "Raw presentation transition emits no TextChanged or ReadOnlyChanged events");
                Console.WriteLine("  Raw presentation events " + phase + ": text=" + textEvents
                    + ", readOnly=" + readOnlyEvents + ", modified=" + modifiedEvents);
                int dpi = viewer.DeviceDpi;
                Check(editor.DeviceDpi == dpi && GetDpiForWindow(editor.Handle) == dpi,
                    "Editable Raw managed/native DPI follows its parent");
                CheckFont(editor, dpi);
                if (view.Visible) CheckFont(search, dpi);
                if (redoPending)
                {
                    Check(editor.CanRedo, "Raw presentation transition retains existing redo history");
                    if (editor.CanRedo)
                    {
                        editor.Redo();
                        Check(editor.Text == edited && textEvents > 0, "Redo restores the text edit and still emits TextChanged");
                        Check(editor.CanUndo, "Redone text edit remains undoable");
                        if (editor.CanUndo) editor.Undo();
                        Check(editor.Text == original, "Undo reverses the redone text edit");
                        editor.Select(5, 4);
                        editor.Modified = dirty;
                    }
                }
                else
                {
                    Check(editor.CanUndo, "Raw presentation transition retains undo history");
                    if (editor.CanUndo)
                    {
                        editor.Undo();
                        Check(editor.Text == original && textEvents > 0,
                            "Undo reverses only the text edit and still emits TextChanged");
                        Check(editor.CanRedo, "Raw text edit remains redoable");
                        if (editor.CanRedo) editor.Redo();
                        Check(editor.Text == edited, "Redo after presentation transition restores the text edit");
                        editor.Select(5, 4);
                        editor.Modified = dirty;
                    }
                }
            };
            try
            {
                // RichTextBox.Font can retain a cached pre-move value after undo. Notifications use configured logical points and target DPI.
                transition("equivalent font", () => test.Inspector.SetFontSize(
                    DpiFontUnits.LogicalFontPointsToManagedFontPoints(CONFIG.flFontSize, view.DeviceDpi)));
                if (runtimeFonts)
                {
                    transition("runtime font alternate", () => typeof(frmViewer).GetMethod("actSetFontSize", Hidden)
                        .Invoke(viewer, new object[] { (float)(initialFont == 10 ? 12 : 10) }));
                    transition("runtime font restore", () => typeof(frmViewer).GetMethod("actSetFontSize", Hidden)
                        .Invoke(viewer, new object[] { (float)initialFont }));
                }
                for (int round = 0; round < 2; round++)
                    foreach (int index in representatives)
                    {
                        Screen target = screens[index];
                        transition("hide round " + round, () => view.Visible = false);
                        transition("hidden move " + target.DeviceName, () => MoveTo(viewer, target));
                        transition("show on " + target.DeviceName, () => view.Visible = true);
                        transition("visible return", () => MoveTo(viewer, start));
                    }
                editor.Undo();
                Check(editor.Text == original && editor.CanRedo, "Raw setup retains a pending redo operation");
                redoPending = true;
                editor.Select(5, 4);
                editor.Modified = dirty;
                transition("pending redo equivalent font", () => test.Inspector.SetFontSize(
                    DpiFontUnits.LogicalFontPointsToManagedFontPoints(CONFIG.flFontSize, view.DeviceDpi)));
                if (runtimeFonts)
                {
                    transition("pending redo runtime alternate", () => typeof(frmViewer).GetMethod("actSetFontSize", Hidden)
                        .Invoke(viewer, new object[] { (float)(initialFont == 10 ? 12 : 10) }));
                    transition("pending redo runtime restore", () => typeof(frmViewer).GetMethod("actSetFontSize", Hidden)
                        .Invoke(viewer, new object[] { (float)initialFont }));
                }
                foreach (int index in representatives)
                {
                    Screen target = screens[index];
                    transition("pending redo hide", () => view.Visible = false);
                    transition("pending redo hidden move " + target.DeviceName, () => MoveTo(viewer, target));
                    transition("pending redo show", () => view.Visible = true);
                    transition("pending redo visible return", () => MoveTo(viewer, start));
                }
            }
            finally
            {
                editor.TextChanged -= onText;
                editor.ReadOnlyChanged -= onReadOnly;
                editor.ModifiedChanged -= onModified;
            }
        }
        context = "Raw owned-font disposal";
        Font lastEditor = OwnedFont(view, "editorFont");
        Font lastSearch = OwnedFont(view, "searchFont");
        bool editorDisposed = false;
        bool searchDisposed = false;
        editor.Disposed += (sender, e) =>
        {
            editorDisposed = true;
            Check(FontUsable(lastEditor), "Owned editor font outlives its consumer");
        };
        search.Disposed += (sender, e) =>
        {
            searchDisposed = true;
            Check(FontUsable(lastSearch), "Owned search font outlives its consumer");
        };
        view.Dispose();
        Check(editorDisposed && searchDisposed, "Raw disposal disposes both font consumers");
        Check(!FontUsable(lastEditor) && !FontUsable(lastSearch), "Raw disposal releases remaining owned fonts");
        view.Dispose();
        Check(OwnedFont(view, "editorFont") == null && OwnedFont(view, "searchFont") == null,
            "Repeated Raw disposal leaves no owned fonts");
    }

    private static void CheckLogFont(frmViewer viewer)
    {
        var views = (TabControl)Find(viewer, "tabsViews");
        TabPage previous = views.SelectedTab;
        views.SelectedTab = (TabPage)Find(viewer, "pageLog");
        Pump();
        CheckFont(Find(viewer, "rtbLog"), viewer.DeviceDpi);
        views.SelectedTab = previous;
        Pump();
    }

    private static void ChangeRuntimeFont(frmViewer viewer, int size, bool editorsReady)
    {
        context = "Runtime font " + size + (editorsReady ? " after activation" : " before activation");
        Console.WriteLine(context + ": monitor=" + Screen.FromControl(viewer).DeviceName);
        var log = (RichTextBox)Find(viewer, "rtbLog");
        string logText = log.Text;
        int selectionStart = log.SelectionStart;
        int selectionLength = log.SelectionLength;
        typeof(frmViewer).GetMethod("actSetFontSize", Hidden).Invoke(viewer, new object[] { (float)size });
        Pump();
        Check(log.Text == logText, "Runtime font changes preserve log text");
        Check(log.SelectionStart == selectionStart && log.SelectionLength == selectionLength,
            "Runtime font changes preserve log selection");
        Check(CONFIG.flFontSize == size, "Production command updates the configured logical font size");
        Check(Math.Abs(viewer.Font.SizeInPoints - size * viewer.DeviceDpi / 96f) < 0.1f,
            "Runtime font command uses the current monitor DPI");
        startupMinimumAt96 = new SizeF(viewer.MinimumSize.Width * 96f / viewer.DeviceDpi,
            viewer.MinimumSize.Height * 96f / viewer.DeviceDpi);
        CheckHeadings(viewer, context);
        CheckLogFont(viewer);
        if (editorsReady) CheckEditors(viewer);
        else Check(Find(viewer, "pageComposer").Controls.Count == 0,
            "Changing the font does not initialize lazy Composer controls");
        if (!editorsReady)
            foreach (ExtensionCase test in extensionCases)
                Check(test.Page.Controls.Count == 0, test.Name + " stays uninitialized during runtime font changes");
        foreach (TabPage page in ((TabControl)Find(viewer, "tabsRequest")).TabPages)
            if (page.Tag is FontProbe)
            {
                var probe = (FontProbe)page.Tag;
                Check(probe.AddCalls == 0 && probe.FontCalls == 0,
                    "Runtime font changes do not initialize or notify unready inspectors");
            }
    }

    private static string ConfigurationKey(Screen screen)
    {
        Rectangle bounds = screen.Bounds;
        Rectangle work = screen.WorkingArea;
        return string.Join("/", new string[] {
            MonitorDpi(screen).ToString(CultureInfo.InvariantCulture),
            bounds.Width.ToString(CultureInfo.InvariantCulture), bounds.Height.ToString(CultureInfo.InvariantCulture),
            work.Width.ToString(CultureInfo.InvariantCulture), work.Height.ToString(CultureInfo.InvariantCulture),
            (work.Left - bounds.Left).ToString(CultureInfo.InvariantCulture),
            (work.Top - bounds.Top).ToString(CultureInfo.InvariantCulture), screen.Primary.ToString()
        });
    }

    private static List<int> RepresentativeIndices(Screen[] screens, int preferredIndex)
    {
        var keys = new Dictionary<string, int>(StringComparer.Ordinal);
        var indices = new List<int>();
        for (int i = 0; i < screens.Length; i++)
        {
            string key = ConfigurationKey(screens[i]);
            int position;
            if (!keys.TryGetValue(key, out position))
            {
                keys.Add(key, indices.Count);
                indices.Add(i);
            }
            else if (i == preferredIndex) indices[position] = i;
        }
        return indices;
    }

    private static void Run(int screenIndex, string scenario, int fontSize, string registryPath, bool runtimeFonts, bool extensions)
    {
        CheckFontUnits();
        RunComponentChecks();
        // Run normal frmViewer Load with isolated settings. No script engine, global hotkey,
        // proxy, or extension startup is run; the header inspector is instantiated explicitly.
        typeof(CONFIG).GetField("sRootRegistryKey", Static).SetValue(null, registryPath + "\\");
        var preferences = (PreferenceBag)Activator.CreateInstance(typeof(PreferenceBag), Hidden, null,
            new object[] { null }, null);
        typeof(CONFIG).GetProperty("pbPrefs", Static).SetValue(null, preferences, null);
        preferences.SetStringPref("app.service_traffic", "disallowed");
        preferences.SetBoolPref("app.attach_on_startup", false);
        preferences.SetBoolPref("app.high_resolution_clock", false);
        preferences.SetStringPref("app.paths.commandhistory", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.txt"));
        Screen[] screens = Screen.AllScreens;
        Screen start = screens[screenIndex];
        List<int> representatives = RepresentativeIndices(screens, screenIndex);
        Rectangle area = start.WorkingArea;
        Rectangle savedBounds = new Rectangle(area.Left + 20, area.Top + 20,
            Math.Min(1200, area.Width - 80), Math.Min(850, area.Height - 80));
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(CONFIG.GetRegistryPath("UI")))
        {
            key.SetValue("App_FontSize", fontSize);
            key.SetValue("frmViewer_Left", savedBounds.Left);
            key.SetValue("frmViewer_Top", savedBounds.Top);
            key.SetValue("frmViewer_Width", savedBounds.Width);
            key.SetValue("frmViewer_Height", savedBounds.Height);
        }
        InvokeConfig("RetrieveUrgentSettings");
        Console.WriteLine("Cold start: " + start.DeviceName + ", scenario=" + scenario + ", configured font=" + fontSize);
        using (var viewer = new TracedViewer())
        {
            typeof(CApp).GetField("_frmMain", Static).SetValue(null, viewer);
            context = "Cold startup";
            Console.WriteLine("  Constructed: handle=" + viewer.IsHandleCreated + ", bounds=" + viewer.Bounds
                + ", DeviceDpi=" + viewer.DeviceDpi + ", font=" + viewer.Font.SizeInPoints);
            Check(!viewer.IsHandleCreated, "Production form constructor does not force a window handle");
            bool loaded = false;
            int transitionsAfterLoad = 0;
            viewer.DpiChanged += (sender, e) =>
            {
                if (loaded) transitionsAfterLoad++;
                Console.WriteLine("  DPI event: " + e.DeviceDpiOld + " -> " + e.DeviceDpiNew
                    + ", suggested=" + e.SuggestedRectangle + ", font=" + viewer.Font.SizeInPoints
                    + ", loaded=" + loaded + ", visible=" + viewer.Visible);
            };
            viewer.HandleCreated += (sender, e) =>
            {
                Console.WriteLine("  First handle: monitor=" + Screen.FromControl(viewer).DeviceName
                    + ", DPI=" + GetDpiForWindow(viewer.Handle));
            };
            viewer.Load += (sender, e) =>
            {
                Check(Screen.FromControl(viewer).DeviceName == start.DeviceName,
                    "Saved monitor restoration completes during startup before the form becomes visible");
                loaded = true;
                Console.WriteLine("  Loaded: monitor=" + Screen.FromControl(viewer).DeviceName
                    + ", DPI=" + viewer.DeviceDpi + ", font=" + viewer.Font.SizeInPoints + ", minimum=" + viewer.MinimumSize);
            };
            viewer.Show();
            Pump();
            Check(Screen.FromControl(viewer).DeviceName == start.DeviceName, "Saved layout starts on the requested monitor");
            Check(viewer.Bounds == savedBounds, "Saved pixel bounds are not scaled again by startup font restoration");
            Check(transitionsAfterLoad == 0, "Cold start needs no monitor transition after production Load");
            Check(CApp.isBooted, "Normal frmViewer Load executed");
            startupMinimumAt96 = new SizeF(viewer.MinimumSize.Width * 96f / viewer.DeviceDpi,
                viewer.MinimumSize.Height * 96f / viewer.DeviceDpi);
            SizeF originalMinimumAt96 = startupMinimumAt96;
            CheckHeadings(viewer, "Cold startup");
            if (extensions) AttachExtensions(viewer);
            Screen alternate = start;
            foreach (int index in representatives)
                if (MonitorDpi(screens[index]) != MonitorDpi(start)) { alternate = screens[index]; break; }
            if (alternate == start)
                foreach (int index in representatives)
                    if (index != screenIndex) { alternate = screens[index]; break; }
            if (scenario != "eager") MoveTo(viewer, alternate);
            if (scenario == "roundtrip") MoveTo(viewer, start);
            int otherFontSize = fontSize == 10 ? 12 : 10;
            if (runtimeFonts)
            {
                var log = (RichTextBox)Find(viewer, "rtbLog");
                log.Text = "Runtime font preference\r\nPreserved log text";
                log.Select(8, 6);
                ChangeRuntimeFont(viewer, otherFontSize, false);
                ChangeRuntimeFont(viewer, fontSize, false);
            }
            context = "First editor activation (" + scenario + ")";
            PrepareEditors(viewer);
            CheckEditors(viewer);
            float originalHeaderHeightAt96 = Find(viewer, "txtHeaders").Height * 96f / viewer.DeviceDpi;
            for (int round = 0; round < 2; round++)
            {
                if (runtimeFonts) ChangeRuntimeFont(viewer, round == 0 ? otherFontSize : fontSize, true);
                foreach (int index in representatives)
                {
                    Screen screen = screens[index];
                    MoveTo(viewer, screen);
                    CheckHeadings(viewer, "Round " + round + " on " + screen.DeviceName);
                    CheckResize(viewer);
                    CheckEditors(viewer);
                    if (runtimeFonts) CheckLogFont(viewer);
                }
            }
            MoveTo(viewer, start);
            CheckHeadings(viewer, "Return to startup monitor");
            CheckEditors(viewer);
            if (runtimeFonts)
            {
                Check(Math.Abs(viewer.MinimumSize.Width - originalMinimumAt96.Width * viewer.DeviceDpi / 96f) <= 3
                    && Math.Abs(viewer.MinimumSize.Height - originalMinimumAt96.Height * viewer.DeviceDpi / 96f) <= 3,
                    "Font preference cycles and monitor round trips preserve the original minimum");
                Check(Math.Abs(Find(viewer, "txtHeaders").Height - originalHeaderHeightAt96 * viewer.DeviceDpi / 96f) <= 3,
                    "Font preference cycles and monitor round trips preserve Composer header height");
            }
            if (extensions) CheckRawEditableLifecycle(viewer, start, screens, representatives, runtimeFonts);
            // Dispose without calling the production shutdown handler, which expects a script engine.
            typeof(CApp).GetProperty("isClosing").GetSetMethod(true).Invoke(null, new object[] { true });
            ((Timer)typeof(frmViewer).GetField("timerReportUpdater", Static).GetValue(null)).Stop();
        }
        Console.WriteLine("DPI scaling: " + assertions + " assertions, " + failures + " failures.");
    }

    [STAThread]
    public static int Main(string[] args)
    {
        string registryPath = null;
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // WinForms applies App.config DPI awareness when the first native window is created.
            // Establish that context before Screen caches DPI-virtualized monitor geometry.
            using (var probe = new Form())
            {
                uint dpi = GetDpiForWindow(probe.Handle);
                bool perMonitorV2 = AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), new IntPtr(-4));
                Console.WriteLine("DPI context: PerMonitorV2=" + perMonitorV2 + ", probe DPI=" + dpi);
                if (!perMonitorV2) throw new Exception("Harness must initialize Framework PerMonitorV2 before monitor enumeration");
            }
            if (Array.IndexOf(args, "-ListScreens") >= 0)
            {
                Screen[] screens = Screen.AllScreens;
                List<int> representatives = RepresentativeIndices(screens, -1);
                for (int i = 0; i < screens.Length; i++)
                {
                    Screen screen = screens[i];
                    Console.WriteLine("Physical " + i);
                    Console.WriteLine("Screen " + i + ": " + screen.DeviceName + ", primary=" + screen.Primary
                        + ", DPI=" + MonitorDpi(screen) + ", bounds=" + screen.Bounds + ", work area=" + screen.WorkingArea);
                    string key = ConfigurationKey(screen);
                    int representative = representatives.Find(index => ConfigurationKey(screens[index]) == key);
                    Console.WriteLine("Configuration " + i + ": " + key + ", representative=" + representative);
                }
                foreach (int index in representatives) Console.WriteLine(index);
                Console.WriteLine("Inventory: " + screens.Length + " physical monitors, " + representatives.Count + " unique configurations");
                return 0;
            }
            if (Array.IndexOf(args, "-ComponentsOnly") >= 0)
            {
                CheckFontUnits();
                RunComponentChecks();
                return failures == 0 ? 0 : 1;
            }
            int screenIndex = int.Parse(args[Array.IndexOf(args, "-Screen") + 1], CultureInfo.InvariantCulture);
            string scenario = args[Array.IndexOf(args, "-Scenario") + 1];
            int size = int.Parse(args[Array.IndexOf(args, "-FontSize") + 1], CultureInfo.InvariantCulture);
            registryPath = args[Array.IndexOf(args, "-RegistryPath") + 1];
            Run(screenIndex, scenario, size, registryPath, Array.IndexOf(args, "-RuntimeFonts") >= 0,
                Array.IndexOf(args, "-Extensions") >= 0);
            return failures == 0 ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            if (registryPath != null) Registry.CurrentUser.DeleteSubKeyTree(registryPath, false);
        }
    }
}
