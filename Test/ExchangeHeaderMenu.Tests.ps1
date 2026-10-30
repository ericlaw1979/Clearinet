#Requires -Version 5.1
param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Clearinet\bin\Release'))
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
    throw 'Run with Windows PowerShell in STA mode after building Clearinet.'
}
$core = Join-Path (Resolve-Path -LiteralPath $BuildDirectory).Path 'Clearinet.exe'
$null = [Reflection.Assembly]::LoadFrom($core)
$tests = @'
using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Clearinet;

public static class ExchangeHeaderMenuTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private static int assertions;
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hwnd, ref Point point);
    [DllImport("user32.dll", EntryPoint = "SendMessage")]
    private static extern IntPtr GetRect(IntPtr hwnd, int message, IntPtr index, ref Rect rect);
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    private static object Invoke(object target, string name, params object[] args)
    {
        return target.GetType().GetMethod(name, Hidden).Invoke(target, args);
    }
    private static ContextMenuStrip Menu(ExchangeListView list)
    {
        return (ContextMenuStrip)typeof(ExchangeListView).GetField("headerMenu", Hidden).GetValue(list);
    }
    private static IntPtr Pack(Point point)
    {
        return new IntPtr(unchecked((point.Y << 16) | (point.X & 0xffff)));
    }
    private static void Open(ExchangeListView list, int index)
    {
        IntPtr header = SendMessage(list.Handle, 0x101f, IntPtr.Zero, IntPtr.Zero);
        var rect = new Rect();
        GetRect(header, 0x1207, new IntPtr(index), ref rect);
        Point point = new Point((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
        Point screen = point;
        Check(ClientToScreen(header, ref screen), "Header coordinates map to screen");
        Cursor.Position = screen;
        SendMessage(header, 0x0204, new IntPtr(2), Pack(point));
        SendMessage(header, 0x0205, IntPtr.Zero, Pack(point));
        Application.DoEvents();
        Check(Menu(list) != null && Menu(list).Visible, "Native header right-click opens custom menu");
    }
    private static void Check(bool value, string message)
    {
        assertions++;
        if (!value) throw new Exception(message);
    }
    private static void SearchWithPrompt(ExchangeListView list, ColumnHeader column, string text, bool cancel)
    {
        bool shown = false;
        string title = null;
        using (var timer = new Timer { Interval = 20 })
        {
            timer.Tick += (s, e) =>
            {
                foreach (Form form in Application.OpenForms)
                {
                    if (!(form is frmPrompt)) continue;
                    timer.Stop();
                    shown = true;
                    title = form.Text;
                    ((TextBox)typeof(frmPrompt).GetField("txtResponse", Hidden).GetValue(form)).Text = text;
                    form.DialogResult = cancel ? DialogResult.Cancel : DialogResult.OK;
                    return;
                }
            };
            Open(list, column.Index);
            timer.Start();
            Menu(list).Items[1].PerformClick();
        }
        Check(shown && title == "Search " + column.Text, "Search menu opens prompt for clicked column");
        Menu(list).Close();
    }
    public static void Run()
    {
        var preferences = typeof(CONFIG).GetProperty("pbPrefs", Static);
        object oldPreferences = preferences.GetValue(null, null);
        preferences.SetValue(null, Activator.CreateInstance(typeof(PreferenceBag), Hidden, null,
            new object[] { "SOFTWARE\\Clearinet\\Tests\\" + Guid.NewGuid().ToString("N") }, null), null);
        var main = typeof(CApp).GetField("_frmMain", Static);
        object oldMain = main.GetValue(null);
        Point oldCursor = Cursor.Position;
        try
        {
            using (var viewer = new frmViewer())
            {
                main.SetValue(null, viewer);
                viewer.ShowInTaskbar = false;
                viewer.Show();
                viewer.Activate();
                Application.DoEvents();
                var list = viewer.lvExchanges;
                var originalMenu = list.ContextMenuStrip;
                int rowMenuOpenings = 0;
                originalMenu.Opening += (s, e) => rowMenuOpenings++;
                var column = list.Columns[4];
                column.Text = "Full & untruncated column name";
                Open(list, column.Index);
                var menu = Menu(list);
                Check(menu.Items.Count == 7, "Menu contains title, five actions, separator");
                Check(menu.Items[0] is ToolStripLabel && menu.Items[0].Font.Bold && menu.Items[0].Text.Replace("&&", "&") == column.Text,
                    "Title is a bold ToolStripLabel with full column text");
                Check(menu.Items[0].Text == "Full && untruncated column name", "Title escapes mnemonic markers to display ampersands literally");
                string[] labels = { "Search this Column...", "Flag duplicates", "Hide this column", "", "Ensure all columns are visible...", "Customize columns..." };
                for (int i = 0; i < labels.Length; i++) Check(menu.Items[i + 1].Text == labels[i], "Menu item order " + i);
                Check(menu.Items[4] is ToolStripSeparator, "Separator precedes visibility commands");
                Check(rowMenuOpenings == 0 && list.ContextMenuStrip == originalMenu, "Header does not open or replace row menu");
                menu.Close();
                column.DisplayIndex = 0;
                Open(list, column.Index);
                Check(Menu(list).Items[0].Text.Replace("&&", "&") == column.Text, "Reordered column hit test uses logical index");
                Menu(list).Items[3].PerformClick();
                Check(column.Width == 0, "Hide sets clicked reordered column width to zero");
                Menu(list).Close();
                int wideWidth = list.Columns[5].Width = 240;
                list.Columns[2].Width = 12;
                Open(list, 0);
                Menu(list).Items[5].PerformClick();
                foreach (ColumnHeader col in list.Columns) Check(col.Width >= 40, "Every column is at least 40 pixels wide");
                Check(column.Width == 40 && list.Columns[2].Width == 40 && list.Columns[5].Width == wideWidth,
                    "Visibility restores hidden/narrow columns without shrinking wide columns");
                Menu(list).Close();
                SendMessage(list.Handle, 0x1014, new IntPtr(100), IntPtr.Zero);
                Open(list, 3);
                Check(Menu(list).Items[0].Text == list.Columns[3].Text, "Scrolled header hit test identifies correct column");
                Menu(list).Close();
                SendMessage(list.Handle, 0x1014, new IntPtr(-10000), IntPtr.Zero);

                string notification = null;
                EventHandler<CApp.NotificationEventArgs> notify = (s, e) => notification = e.NotifyString;
                bool oldQuiet = CONFIG.isQuietMode;
                CApp.OnNotification += notify;
                try
                {
                    CONFIG.isQuietMode = true;
                    Open(list, column.Index);
                    Menu(list).Items[6].PerformClick();
                    Check(notification == "Customize columns - Not yet implemented.", "Customize notifies that it is not implemented");
                    Menu(list).Close();
                }
                finally
                {
                    CApp.OnNotification -= notify;
                    CONFIG.isQuietMode = oldQuiet;
                }

                string[] values = { "Alpha", "ALPHA", "unique", "", "", "tail" };
                var exchanges = new Exchange[values.Length];
                var closing = typeof(CApp).GetProperty("isClosing").GetSetMethod(true);
                bool oldClosing = CApp.isClosing;
                closing.Invoke(null, new object[] { true });
                try
                {
                    for (int i = 0; i < exchanges.Length; i++)
                        exchanges[i] = (Exchange)typeof(frmViewer).GetMethod("CreateFauxExchange", Static).Invoke(null,
                            new object[] { "/header/" + i, "text/plain", Encoding.UTF8.GetBytes("body") });
                }
                finally { closing.Invoke(null, new object[] { oldClosing }); }
                Invoke(viewer, "addExchangesToListView", new object[] { exchanges });
                for (int i = 0; i < exchanges.Length; i++) exchanges[i].ViewItem.SubItems[4].Text = values[i];
                exchanges[5].ViewItem.Selected = true;
                SearchWithPrompt(list, column, "lPh", true);
                Check(list.SelectedCount == 1 && exchanges[5].ViewItem.Selected, "Cancelling search preserves selection");
                SearchWithPrompt(list, column, "lPh", false);
                Check(list.SelectedCount == 2 && exchanges[0].ViewItem.Selected && exchanges[1].ViewItem.Selected,
                    "Search is case-insensitive substring matching, replacing prior selection");
                Invoke(list, "SearchColumn", column, "absent");
                Check(list.SelectedCount == 0, "Search with no matches clears selection");
                Invoke(list, "SearchColumn", column, "");
                Check(list.SelectedCount == exchanges.Length, "Empty substring matches all values");
                Open(list, column.Index);
                Menu(list).Items[2].PerformClick();
                for (int i = 0; i < exchanges.Length; i++)
                {
                    bool duplicate = i == 0 || i == 1 || i == 3 || i == 4;
                    Check((exchanges[i]["ui-bold"] == "marked") == duplicate, "Only duplicate groups are flagged: " + i);
                    if (duplicate) Check(exchanges[i]["ui-color"] == "blue" && exchanges[i].ViewItem.Font.Bold,
                        "Duplicate uses persistent blue/bold mark: " + i);
                }
                Check(list.SelectedCount == exchanges.Length, "Flagging does not change selection");
                Menu(list).Close();
                Point rowPoint = list.PointToScreen(exchanges[0].ViewItem.Bounds.Location + new Size(5, 5));
                SendMessage(list.Handle, 0x007b, list.Handle, Pack(rowPoint));
                Check(rowMenuOpenings == 1 && originalMenu.Visible, "Row right-click still uses original context menu");
                originalMenu.Close();
                var lastMenu = Menu(list);
                list.Dispose();
                Check(lastMenu.IsDisposed, "Disposing list disposes header menu");
            }
        }
        finally
        {
            main.SetValue(null, oldMain);
            preferences.SetValue(null, oldPreferences, null);
            Cursor.Position = oldCursor;
        }
        Console.WriteLine("Exchange header menu: " + assertions + " assertions passed.");
    }
}
'@
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.ReferencedAssemblies.AddRange([string[]]@($core, 'System.dll', 'System.Core.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll'))
Add-Type -TypeDefinition $tests -CompilerParameters $compiler
[ExchangeHeaderMenuTests]::Run()
