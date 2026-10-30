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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Clearinet;

public static class ScriptColumnReloadTests
{
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private static int assertions;

    public static void Run()
    {
        object preferences = Activator.CreateInstance(typeof(PreferenceBag), PrivateInstance,
            null, new object[] { "SOFTWARE\\Clearinet\\Tests\\" + Guid.NewGuid().ToString("N") }, null);
        typeof(CONFIG).GetProperty("pbPrefs", PrivateStatic).SetValue(null, preferences, null);
        string path = Path.GetTempFileName();
        FieldInfo mainForm = typeof(CApp).GetField("_frmMain", PrivateStatic);
        object oldForm = mainForm.GetValue(null);
        try
        {
            using (var viewer = new frmViewer())
            {
                mainForm.SetValue(null, viewer);
                IntPtr handle = viewer.lvExchanges.Handle;
                TestReload(viewer, path);
            }
        }
        finally
        {
            mainForm.SetValue(null, oldForm);
            File.Delete(path);
        }
        Console.WriteLine("Script column reload: " + assertions + " assertions passed.");
    }

    private static ColumnHeader AddColumn(ExchangeListView list, string title, string value)
    {
        MethodInfo add = typeof(ExchangeListView).GetMethod("AddBoundColumn", PrivateInstance);
        Type sortType = add.GetParameters()[1].ParameterType;
        return (ColumnHeader)add.Invoke(list, new object[] {
            title, Enum.Parse(sortType, "Text"), new Func<Exchange, string>(x => value) });
    }

    private static void Click(ExchangeListView list, int index)
    {
        typeof(ExchangeListView).GetMethod("OnColumnClick", PrivateInstance)
            .Invoke(list, new object[] { new ColumnClickEventArgs(index) });
    }

    private static int SortColumn(ExchangeListView list)
    {
        object sorter = list.ListViewItemSorter;
        return (int)sorter.GetType().GetProperty("SortColumn", PrivateInstance).GetValue(sorter, null);
    }

    private static void TestReload(frmViewer viewer, string path)
    {
        var list = viewer.lvExchanges;
        int builtInCount = list.Columns.Count;
        MethodInfo setClosing = typeof(CApp).GetProperty("isClosing").GetSetMethod(true);
        bool wasClosing = CApp.isClosing;
        Exchange exchange;
        setClosing.Invoke(null, new object[] { true });
        try
        {
            exchange = (Exchange)typeof(frmViewer).GetMethod("CreateFauxExchange", PrivateStatic)
                .Invoke(null, new object[] { "/reload", "text/plain", Encoding.UTF8.GetBytes("body") });
        }
        finally { setClosing.Invoke(null, new object[] { wasClosing }); }
        typeof(frmViewer).GetMethod("addExchangesToListView", PrivateInstance)
            .Invoke(viewer, new object[] { new[] { exchange } });
        string originalHost = exchange.ViewItem.SubItems[4].Text;
        ColumnHeader unrelated = AddColumn(list, "Script", "extension");
        File.WriteAllText(path, Script("old"));
        using (var engine = (JScriptEngine)Activator.CreateInstance(typeof(JScriptEngine), PrivateInstance,
            null, new object[] { path }, null))
        {
            ((FileSystemWatcher)typeof(JScriptEngine).GetField("_watcher", PrivateInstance)
                .GetValue(engine)).EnableRaisingEvents = false;
            Check(list.Columns.Count == builtInCount + 3, "Two script columns added");
            var oldColumns = new[] { list.Columns[builtInCount + 1], list.Columns[builtInCount + 2] };
            Check(oldColumns[0].Text != unrelated.Text && oldColumns[0].Text != oldColumns[1].Text,
                "Duplicate column titles remain unique");
            Check(exchange.ViewItem.SubItems[oldColumns[0].Index].Text.StartsWith("old"),
                "New script columns populate existing exchanges");
            ColumnHeader tail = AddColumn(list, "Tail", "tail");
            Click(list, tail.Index);

            for (int i = 0; i < 3; i++)
            {
                File.WriteAllText(path, Script("new"));
                engine.ReloadScript();
                Check(list.Columns.Count == builtInCount + 4, "Reload does not accumulate columns");
                Check(exchange.ViewItem.SubItems.Count == list.Columns.Count, "Row and header counts match");
                Check(exchange.ViewItem.SubItems[tail.Index].Text == "tail", "Later unrelated column data retained");
                Check(SortColumn(list) == tail.Index, "Sort index follows surviving column");
                Check(exchange.ViewItem.SubItems[list.Columns.Count - 1].Text.StartsWith("new"),
                    "Replacement providers populate existing exchanges");
                typeof(frmViewer).GetMethod("UpdateViewItem", PrivateInstance).Invoke(viewer, new object[] { exchange });
            }
            foreach (ColumnHeader oldColumn in oldColumns)
                Check(!list.Columns.Contains(oldColumn) && oldColumn.Tag == null, "Old header removed and provider released");

            Click(list, list.Columns.Count - 1);
            File.WriteAllText(path, "class NoHandlers {}");
            engine.ReloadScript();
            Check(list.Columns.Count == builtInCount + 2, "Removing bindings removes all script columns");
            Check(SortColumn(list) == 0, "Removing sorted column resets to ID sorting");
            Check(exchange.ViewItem.SubItems.Count == list.Columns.Count, "Removed column values deleted");
            Check(list.Columns.Contains(unrelated) && exchange.ViewItem.SubItems[unrelated.Index].Text == "extension",
                "Same-title unrelated column preserved");
            Check(exchange.ViewItem.SubItems[4].Text == originalHost, "Built-in data preserved");

            File.WriteAllText(path, Script("again"));
            engine.ReloadScript();
            Check(list.Columns.Count == builtInCount + 4, "Bindings can return after a script without Handlers");
            typeof(JScriptEngine).GetField("_isReadyForCalls", PrivateInstance).SetValue(engine, false);
            File.WriteAllText(path, "class NoHandlers {}");
            engine.ReloadScript();
            Check(list.Columns.Count == builtInCount + 2, "Cleanup also runs when previous script is not ready");
        }
    }

    private static string Script(string value)
    {
        return "import Clearinet; class Handlers { " +
            "public static BindUIColumn(\"Script\") function First(x: Exchange): String { return \"" + value + "1\"; } " +
            "public static BindUIColumn(\"Script\", true) function Second(x: Exchange): String { return \"" + value + "2\"; } }";
    }

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }
}
'@
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.ReferencedAssemblies.AddRange([string[]]@($core, 'System.dll', 'System.Core.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll'))
Add-Type -TypeDefinition $tests -CompilerParameters $compiler
[ScriptColumnReloadTests]::Run()
