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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Clearinet;

public sealed class BatchExchangeSorter : IComparer
{
	public int Direction;
	public long Comparisons;
	public int Compare(object a, object b)
	{
		Comparisons++;
		return Direction * int.Parse(((ListViewItem)a).Text).CompareTo(int.Parse(((ListViewItem)b).Text));
	}
}

public static class BatchExchangeInsertionTests
{
	private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
	private static readonly MethodInfo AddBatch = typeof(frmViewer).GetMethod("addExchangesToListView", PrivateInstance);
	private static readonly MethodInfo CreateFaux = typeof(frmViewer).GetMethod("CreateFauxExchange", BindingFlags.NonPublic | BindingFlags.Static);
	private static readonly MethodInfo SetClosing = typeof(CApp).GetProperty("isClosing").GetSetMethod(true);
	private static int assertions;

	public static void Run()
	{
		object preferences = Activator.CreateInstance(typeof(PreferenceBag), PrivateInstance,
			null, new object[] { "SOFTWARE\\Clearinet\\Tests\\" + Guid.NewGuid().ToString("N") }, null);
		typeof(CONFIG).GetProperty("pbPrefs", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, preferences, null);
		bool wasClosing = CApp.isClosing;
		try
		{
			foreach (int direction in new[] { 1, -1 }) TestBatch(direction);
			TestFiles();
		}
		finally { SetClosing.Invoke(null, new object[] { wasClosing }); }
		Console.WriteLine("Batch exchange insertion: " + assertions + " assertions passed.");
	}

	private static Exchange Create(string path)
	{
		SetClosing.Invoke(null, new object[] { true });
		try { return (Exchange)CreateFaux.Invoke(null, new object[] { path, "text/plain", Encoding.UTF8.GetBytes("body") }); }
		finally { SetClosing.Invoke(null, new object[] { false }); }
	}

	private static void TestBatch(int direction)
	{
		using (var viewer = new frmViewer())
		{
			IntPtr handle = viewer.lvExchanges.Handle;
			var sorter = new BatchExchangeSorter { Direction = direction };
			viewer.lvExchanges.ListViewItemSorter = sorter;
			Exchange existing = Create("/existing");
			AddBatch.Invoke(viewer, new object[] { new[] { existing } });
			var rows = new List<Exchange>();
			for (int i = 0; i < 1000; i++) rows.Add(Create("/batch/" + i));
			rows.Reverse();
			AddBatch.Invoke(viewer, new object[] { rows });
			Check(viewer.lvExchanges.Items.Count == 1001, "Existing and batched rows retained");
			Check(Object.ReferenceEquals(viewer.lvExchanges.ListViewItemSorter, sorter), "Sorter instance retained");
			Check(sorter.Comparisons < 20000, "Only one bulk sort performed");
			CheckOrder(viewer.lvExchanges, direction);
			Check(existing.ViewItem.Tag == existing && rows[0].ViewItem.Tag == rows[0], "Exchange row associations preserved");
			Check(rows[0].ViewItem.SubItems[5].Text == "/batch/999", "Request path column preserved");
			AddBatch.Invoke(viewer, new object[] { new[] { existing, rows[0] } });
			AddBatch.Invoke(viewer, new object[] { new Exchange[0] });
			Check(viewer.lvExchanges.Items.Count == 1001, "Duplicate and empty batches add no rows");
			Exchange partial = Create("/partial");
			try
			{
				AddBatch.Invoke(viewer, new object[] { FailingBatch(partial) });
				throw new Exception("Expected enumeration failure");
			}
			catch (TargetInvocationException error)
			{
				Check(error.InnerException is InvalidOperationException, "Enumeration failure propagated");
			}
			Check(Object.ReferenceEquals(viewer.lvExchanges.ListViewItemSorter, sorter), "Sorter restored after failure");
			Check(viewer.lvExchanges.Items.Count == 1002 && partial.ViewItem.ListView == viewer.lvExchanges, "Partial batch flushed after failure");
			CheckOrder(viewer.lvExchanges, direction);
			SetClosing.Invoke(null, new object[] { true });
			AddBatch.Invoke(viewer, new object[] { FailingBatch(partial) });
			SetClosing.Invoke(null, new object[] { false });
			Check(viewer.lvExchanges.Items.Count == 1002, "Closing skips batch enumeration");
		}
	}

	private static IEnumerable<Exchange> FailingBatch(Exchange exchange)
	{
		yield return exchange;
		throw new InvalidOperationException("Simulated enumeration failure");
	}

	private static void CheckOrder(ListView list, int direction)
	{
		for (int i = 1; i < list.Items.Count; i++)
			Check(direction * int.Parse(list.Items[i - 1].Text).CompareTo(int.Parse(list.Items[i].Text)) <= 0, "Final order correct");
	}

	private static void TestFiles()
	{
		string path = Path.GetTempFileName();
		try
		{
			File.WriteAllText(path, "file contents", new UTF8Encoding(false));
			using (var viewer = new frmViewer())
			{
				SetClosing.Invoke(null, new object[] { true });
				var factory = typeof(frmViewer).GetMethod("CreateExchangesForFiles", PrivateInstance);
				var rows = new List<Exchange>((IEnumerable<Exchange>)factory.Invoke(viewer, new object[] { new[] { path, path } }));
				SetClosing.Invoke(null, new object[] { false });
				Check(rows.Count == 2 && rows[0].ViewItem == null, "File factory does not insert rows");
				AddBatch.Invoke(viewer, new object[] { rows });
				Check(viewer.lvExchanges.Items.Count == 2, "Multiple files inserted through shared batch helper");
				Check(Encoding.UTF8.GetString(rows[0].ResponseBody) == "file contents", "File body preserved");
				Check(rows[0].RequestHeaders.RequestPath == "/clipboard/" + Path.GetFileName(path), "File request path preserved");
			}
		}
		finally { File.Delete(path); }
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
[BatchExchangeInsertionTests]::Run()
