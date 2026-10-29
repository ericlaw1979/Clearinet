#Requires -Version 5.1
param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Clearinet\bin\Release'))
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
	throw 'Run with powershell.exe -NoProfile -NonInteractive -STA -File .\Test\QuickExecHistory.Tests.ps1'
}
$core = Join-Path (Resolve-Path -LiteralPath $BuildDirectory).Path 'Clearinet.exe'
$null = [Reflection.Assembly]::LoadFrom($core)
$tests = @'
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Clearinet;

public sealed class HistoryTestBox : QuickExecBox
{
	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

	public void Type(string text)
	{
		foreach (char c in text) SendMessage(Handle, 0x0102, new IntPtr(c), IntPtr.Zero);
	}

	public void Key(Keys key)
	{
		if (!TryKey(key)) throw new Exception("Key was not handled: " + key);
	}

	public bool TryKey(Keys key)
	{
		Message message = new Message();
		return ProcessCmdKey(ref message, key);
	}

	public DragDropEffects Drag(IDataObject data, DragDropEffects allowed, int stage)
	{
		var e = new DragEventArgs(data, 0, 0, 0, allowed, DragDropEffects.None);
		if (stage == 0) OnDragEnter(e);
		else if (stage == 1) OnDragOver(e);
		else OnDragDrop(e);
		return e.Effect;
	}

	public void LoadHistoryFile() { InvokeHistory("LoadHistory"); }
	public void SaveHistoryFile() { InvokeHistory("SaveHistory"); }
	public void InsertFiles(string[] filenames)
	{
		typeof(QuickExecBox).GetMethod("InsertFilenames", BindingFlags.NonPublic | BindingFlags.Instance)
			.Invoke(this, new object[] { filenames });
	}
	private void InvokeHistory(string method)
	{
		typeof(QuickExecBox).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(this, null);
	}
}

public static class QuickExecHistoryTests
{
	private static int assertions;
	private static void Check(bool condition, string message)
	{
		++assertions;
		if (!condition) throw new Exception(message);
	}

	private static void ViewerMode(bool enabled)
	{
		typeof(CONFIG).GetProperty("isViewerMode").GetSetMethod(true).Invoke(null, new object[] { enabled });
	}

	private static void CheckNavigation()
	{
		using (HistoryTestBox box = new HistoryTestBox())
		{
			string text = "go a/b\\c  end";
			box.Text = text;
			box.Select(0, 0);
			foreach (int position in new[] { 3, 5, 7, 9, 10, 13 })
			{
				box.Key(Keys.Tab);
				Check(box.Text == text && box.SelectionStart == position && box.SelectionLength == 0,
					"Tab did not advance to the next separator/end: " + position);
			}
			Check(!box.TryKey(Keys.Tab), "Tab at the end prevented normal focus navigation.");
			foreach (int position in new[] { 10, 9, 7, 5, 3, 0 })
			{
				box.Key(Keys.Shift | Keys.Tab);
				Check(box.Text == text && box.SelectionStart == position && box.SelectionLength == 0,
					"Shift+Tab did not return to the previous separator/start: " + position);
			}
			Check(!box.TryKey(Keys.Shift | Keys.Tab), "Shift+Tab at the start prevented normal focus navigation.");
			box.Select(4, 3);
			box.Key(Keys.Tab);
			Check(box.Text == text && box.SelectionStart == 5 && box.SelectionLength == 0,
				"Tab did not navigate from the beginning of a selected suffix.");
			box.Select(6, 3);
			box.Key(Keys.Shift | Keys.Tab);
			Check(box.Text == text && box.SelectionStart == 5 && box.SelectionLength == 0,
				"Shift+Tab did not navigate backward from inside a segment.");
			box.Text = "\u00e9\tword";
			box.Select(0, 0);
			box.Key(Keys.Tab);
			Check(box.SelectionStart == box.TextLength && box.SelectionLength == 0 && box.Text == "\u00e9\tword",
				"Tab treated a nonseparator character as a separator.");
			box.Clear();
			Check(!box.TryKey(Keys.Tab) && !box.TryKey(Keys.Shift | Keys.Tab),
				"Empty QuickExec prevented normal focus navigation.");
		}
	}

	private static void CheckFileTransfer()
	{
		string[] files = { @"C:\folder with spaces\one.txt", "D:\\data\\\u00e9.txt" };
		string inserted = "\"" + files[0] + "\" \"" + files[1] + "\"";
		var data = new DataObject(DataFormats.FileDrop, files);
		using (var box = new HistoryTestBox())
		{
			IntPtr handle = box.Handle;
			Check(box.AllowDrop, "QuickExec did not enable file drops.");
			box.Text = "load draft tail";
			box.Select(5, 5);
			Check(box.Drag(data, DragDropEffects.Copy | DragDropEffects.Move, 0) == DragDropEffects.Copy,
				"File drag did not advertise Copy instead of Move.");
			Check(box.Drag(data, DragDropEffects.Copy, 1) == DragDropEffects.Copy,
				"File drag-over was not accepted.");
			box.Drag(data, DragDropEffects.Copy, 2);
			Check(box.Text == "load " + inserted + " tail" && box.SelectionStart == 5 + inserted.Length && box.SelectionLength == 0,
				"File drop did not replace selected text with quoted, space-delimited paths.");
			string before = box.Text;
			var text = new DataObject(DataFormats.UnicodeText, "not files");
			Check(box.Drag(text, DragDropEffects.Copy, 0) == DragDropEffects.None,
				"Non-file drag was accepted.");
			box.Drag(text, DragDropEffects.Copy, 2);
			Check(box.Text == before, "Non-file drop changed QuickExec.");
			Check(box.Drag(data, DragDropEffects.Move, 0) == DragDropEffects.None,
				"Move-only file drag was accepted.");
			box.Drag(data, DragDropEffects.Move, 2);
			Check(box.Text == before, "Move-only drop changed QuickExec.");
			box.ReadOnly = true;
			Check(box.Drag(data, DragDropEffects.Copy, 1) == DragDropEffects.None,
				"Read-only QuickExec accepted a file drag.");
			box.Drag(data, DragDropEffects.Copy, 2);
			Check(box.Text == before, "File drop changed read-only QuickExec.");
			box.ReadOnly = false;

			IDataObject originalClipboard = Clipboard.GetDataObject();
			try
			{
				var paths = new System.Collections.Specialized.StringCollection();
				paths.AddRange(files);
				Clipboard.SetFileDropList(paths);
				foreach (Keys key in new[] { Keys.Control | Keys.V, Keys.Shift | Keys.Insert })
				{
					box.Text = "load  tail";
					box.Select(5, 0);
					box.Key(key);
					Check(box.Text == "load " + inserted + " tail" && box.SelectionStart == 5 + inserted.Length && box.SelectionLength == 0,
						"File paste shortcut did not insert quoted paths at the caret: " + key);
				}
				box.Text = "load draft tail";
				box.Select(5, 5);
				box.Paste();
				Check(box.Text == "load " + inserted + " tail", "Native Paste did not replace selection with file paths.");
				box.ReadOnly = true;
				before = box.Text;
				box.TryKey(Keys.Control | Keys.V);
				box.Paste();
				Check(box.Text == before, "File paste changed read-only QuickExec.");
				box.ReadOnly = false;
				Clipboard.SetText("ordinary text");
				box.Text = "load draft tail";
				box.Select(5, 5);
				Check(!box.TryKey(Keys.Control | Keys.V), "Ordinary text paste was intercepted as files.");
				box.Paste();
				Check(box.Text == "load ordinary text tail" && box.SelectionStart == 18 && box.SelectionLength == 0,
					"Ordinary text paste behavior changed.");
			}
			finally
			{
				if (originalClipboard == null) Clipboard.Clear();
				else Clipboard.SetDataObject(originalClipboard, true);
			}
		}
	}

	private static void CheckInsertion()
	{
		FieldInfo mainWindow = typeof(CApp).GetField("_frmMain", BindingFlags.NonPublic | BindingFlags.Static);
		object originalWindow = mainWindow.GetValue(null);
		bool wasClosing = CApp.isClosing;
		MethodInfo setClosing = typeof(CApp).GetProperty("isClosing").GetSetMethod(true);
		try
		{
			setClosing.Invoke(null, new object[] { true });
			using (var viewer = new frmViewer())
			using (var box = new HistoryTestBox())
			{
				mainWindow.SetValue(null, viewer);
				viewer.lvExchanges.ListViewItemSorter = null;
				viewer.lvExchanges.Sorting = SortOrder.None;
				IntPtr handle = viewer.lvExchanges.Handle;
				IntPtr boxHandle = box.Handle;
				box.Text = "open draft tail";
				box.Select(5, 5);
				box.Key(Keys.Control | Keys.U);
				Check(box.Text == "open draft tail" && box.SelectionStart == 5 && box.SelectionLength == 5,
					"Ctrl+U without selected Exchanges changed the draft or selection.");
				string first = "https://example.test:8443/path?q=one&x=two";
				string second = "http://other.test/next";
				foreach (string url in new[] { first, "https://unselected.test/", second })
				{
					var exchange = new Exchange(new HTTPRequestHeaders(), new byte[0]);
					exchange.fullUrl = url;
					viewer.lvExchanges.Items.Add(new ListViewItem(url) { Tag = exchange });
				}
				viewer.lvExchanges.Items[0].Selected = true;
				box.Key(Keys.Control | Keys.U);
				Check(box.Text == "open " + first + " tail" && box.SelectionStart == 5 + first.Length && box.SelectionLength == 0,
					"Ctrl+U did not replace selected text with the full URL and position the caret after it. Actual: "
					+ box.Text + "; caret=" + box.SelectionStart + "; selection=" + box.SelectionLength
					+ "; selected Exchanges=" + viewer.GetSelectedExchanges().Length);
				viewer.lvExchanges.Items[2].Selected = true;
				box.Text = "prefix suffix";
				box.Select(7, 0);
				box.Key(Keys.Control | Keys.U);
				string urls = first + " " + second;
				Check(box.Text == "prefix " + urls + "suffix" && box.SelectionStart == 7 + urls.Length && box.SelectionLength == 0,
					"Ctrl+U did not insert only selected URLs in list order with single spaces.");
				box.ReadOnly = true;
				string before = box.Text;
				box.Key(Keys.Control | Keys.U);
				box.Key(Keys.Control | Keys.I);
				Check(box.Text == before, "Insertion shortcuts changed a read-only QuickExec.");
				box.ReadOnly = false;
				box.Text = "load draft tail";
				box.Select(5, 5);
				box.InsertFiles(null);
				box.InsertFiles(new string[0]);
				Check(box.Text == "load draft tail" && box.SelectionStart == 5 && box.SelectionLength == 5,
					"Canceled or empty file selection changed the draft or selection.");
				string filename = @"C:\folder with spaces\one.txt";
				box.InsertFiles(new[] { filename });
				Check(box.Text == "load \"" + filename + "\" tail" && box.SelectionStart == 7 + filename.Length && box.SelectionLength == 0,
					"Single filename insertion did not quote the path, replace selected text, and position the caret.");
				box.Text = "load  tail";
				box.Select(5, 0);
				box.InsertFiles(new[] { filename, "D:\\data\\\u00e9.txt" });
				string files = "\"" + filename + "\" \"D:\\data\\\u00e9.txt\"";
				Check(box.Text == "load " + files + " tail" && box.SelectionStart == 5 + files.Length && box.SelectionLength == 0,
					"Multiple filename insertion did not preserve paths, quoting, spacing, and surrounding text.");
			}
		}
		finally
		{
			mainWindow.SetValue(null, originalWindow);
			setClosing.Invoke(null, new object[] { wasClosing });
		}
	}

	public static void Run()
	{
		CheckNavigation();
		string directory = Path.Combine(Path.GetTempPath(), "ClearinetHistory-" + Guid.NewGuid().ToString("N"));
		string path = Path.Combine(directory, "nested", "history.txt");
		object preferences = Activator.CreateInstance(typeof(PreferenceBag), BindingFlags.Instance | BindingFlags.NonPublic,
			null, new object[] { "SOFTWARE\\Clearinet\\Tests\\" + Guid.NewGuid().ToString("N") }, null);
		typeof(CONFIG).GetProperty("pbPrefs", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, preferences, null);
		bool originalViewerMode = CONFIG.isViewerMode;
		try
		{
			Check(CONFIG.GetPath("CommandHistory") == Path.Combine(CONFIG.GetPath("UserFolder"), "CommandHistory.txt"),
				"Default command history path is incorrect.");
			CApp.Prefs.SetStringPref("app.paths.commandhistory", path);
			Check(CONFIG.GetPath("CommandHistory") == path, "History path preference was ignored.");
			ViewerMode(false);
			CheckInsertion();
			CheckFileTransfer();
			using (HistoryTestBox box = new HistoryTestBox())
			{
				box.LoadHistoryFile();
				string executed = null;
				box.OnExecute += delegate(ExecuteEventArgs e) { executed = e.Command; e.Handled = true; e.AddToHistory = e.Command != "private"; };
				box.Key(Keys.Up);
				Check(box.Text == "", "Empty history changed the draft.");
				box.Text = "   ";
				box.Key(Keys.Enter);
				Check(executed == null && box.Text == "", "Blank command was executed.");
				foreach (string command in new[] { "select image", "cls", "select text", "select image", "private", "find \u00e9\u4e2d" })
				{
					box.Text = "  " + command + "  ";
					box.Key(Keys.Enter);
					Check(executed == command && box.Text == "", "Execution did not trim and clear: " + command);
				}
				box.Key(Keys.Up);
				Check(box.Text == "find \u00e9\u4e2d" && box.SelectionStart == 0 && box.SelectedText == box.Text,
					"Up without a prefix did not select the entire recalled command.");
				box.Key(Keys.Down);
				Check(box.Text == "", "Down did not restore the empty draft.");
				box.Type("se");
				Check(box.Text == "select image" && box.SelectionStart == 2 && box.SelectedText == "lect image",
					"Typing did not offer and select the newest matching suffix.");
				box.Key(Keys.Up);
				Check(box.Text == "select text" && box.SelectionStart == 2 && box.SelectedText == "lect text",
					"First Up did not advance from the autocomplete suggestion to the older matching suffix.");
				box.Key(Keys.Up);
				Check(box.Text == "select text" && box.SelectionStart == 2 && box.SelectedText == "lect text",
					"Up at the oldest match changed the completion selection.");
				box.Key(Keys.Down);
				Check(box.Text == "select image" && box.SelectionStart == 2 && box.SelectedText == "lect image",
					"Down did not replace and select the newer matching suffix.");
				box.Key(Keys.Down);
				Check(box.Text == "se" && box.SelectionStart == 2 && box.SelectionLength == 0,
					"Down did not restore the unselected typed draft.");
				box.Clear();
				box.Type("se");
				box.Key(Keys.Down);
				Check(box.Text == "se" && box.SelectionStart == 2 && box.SelectionLength == 0,
					"Down from an automatic suggestion did not restore the typed draft.");
				box.Key(Keys.Up);
				box.Key(Keys.Up);
				box.Type("x");
				Check(box.Text == "sex" && box.SelectionStart == 3 && box.SelectionLength == 0,
					"Typing after browsing did not replace only the selected suffix.");
				box.Key(Keys.Up);
				Check(box.Text == "sex", "Typing after browsing did not reset the history prefix.");
				box.Text = "SE";
				box.Key(Keys.Up);
				Check(box.Text == "SElect image" && box.SelectionStart == 2 && box.SelectedText == "lect image",
					"History matching did not preserve typed casing and select only the suffix.");
				box.Key(Keys.Down);
				Check(box.Text == "SE", "Draft casing was not preserved.");
				box.Clear();
				box.Type("SE");
				Check(box.Text == "SElect image" && box.SelectedText == "lect image", "Completion changed typed casing.");
				box.Type("x");
				Check(box.Text == "SEx", "Typing did not replace the selected suggestion.");
				box.Key(Keys.Up);
				Check(box.Text == "SEx", "Unmatched draft was changed by Up.");
				box.Text = "select i";
				box.Select(box.TextLength, 0);
				box.Type("m");
				box.Key(Keys.Up);
				Check(box.Text == "select image" && box.SelectionStart == 9 && box.SelectedText == "age",
					"Up with only one matching suggestion changed the completion or selection.");
				box.Type("\b");
				Check(box.Text == "select im", "Backspace did not dismiss the selected completion.");
				box.Type("\b");
				Check(box.Text == "select i", "Backspace immediately reinserted completion.");
				box.Text = "select image";
				box.Select(0, 0);
				box.Type("x");
				Check(box.Text == "xselect image", "Editing in the middle corrupted text.");
				box.SaveHistoryFile();
			}
			string[] expected = { "cls", "select text", "select image", "find \u00e9\u4e2d" };
			Check(string.Join("|", File.ReadAllLines(path)) == string.Join("|", expected),
				"History ordering, deduplication, opt-out, or Unicode persistence was incorrect.");
			using (HistoryTestBox box = new HistoryTestBox())
			{
				box.LoadHistoryFile();
				box.Key(Keys.Up);
				Check(box.Text == expected[3], "Restart did not reload persisted history.");
				box.Text = "cl";
				box.Key(Keys.Up);
				Check(box.Text == "cls", "Reloaded history was not prefix-searchable.");
				box.Clear();
				box.Type("se");
				box.Key(Keys.Tab);
				Check(box.Text == "select image" && box.SelectionStart == 7 && box.SelectionLength == 0,
					"Tab did not advance through an autocomplete suffix without changing it.");
				box.Key(Keys.Shift | Keys.Tab);
				Check(box.Text == "select image" && box.SelectionStart == 0 && box.SelectionLength == 0,
					"Shift+Tab did not return to the start of the completed command.");
				box.Clear();
				box.Type("se");
				box.Key(Keys.Shift | Keys.Tab);
				Check(box.Text == "select image" && box.SelectionStart == 0 && box.SelectionLength == 0,
					"Shift+Tab did not navigate backward with an autocomplete suffix selected.");
				box.Key(Keys.Tab);
				box.Type("new ");
				Check(box.Text == "select new image", "Typing after Tab navigation replaced or corrupted the completion.");
				string executed = null;
				box.OnExecute += delegate(ExecuteEventArgs e) { executed = e.Command; e.Handled = true; };
				box.Clear();
				box.Type("cl");
				box.Key(Keys.Enter);
				Check(executed == "cls" && box.Text == "", "Enter did not execute the completed command.");
			}
			byte[] original = File.ReadAllBytes(path);
			ViewerMode(true);
			using (HistoryTestBox box = new HistoryTestBox())
			{
				box.LoadHistoryFile();
				box.Key(Keys.Up);
				Check(box.Text == expected[3], "ViewerMode did not load history.");
				box.OnExecute += delegate(ExecuteEventArgs e) { e.Handled = true; };
				box.Text = "viewer-only";
				box.Key(Keys.Enter);
				box.SaveHistoryFile();
			}
			Check(Convert.ToBase64String(File.ReadAllBytes(path)) == Convert.ToBase64String(original),
				"ViewerMode modified the history file.");
			string viewerPath = Path.Combine(directory, "viewer", "history.txt");
			CApp.Prefs.SetStringPref("app.paths.commandhistory", viewerPath);
			using (HistoryTestBox box = new HistoryTestBox())
			{
				box.LoadHistoryFile();
				box.SaveHistoryFile();
			}
			Check(!Directory.Exists(Path.GetDirectoryName(viewerPath)), "ViewerMode created a history directory.");
			CApp.Prefs.SetStringPref("app.paths.commandhistory", path);
			ViewerMode(false);
			string log = "";
			EventHandler<LogEventArgs> listener = delegate(object sender, LogEventArgs e) { log += e.LogString + "\n"; };
			CApp.Log.OnLog += listener;
			try
			{
				typeof(SimpleLog).GetMethod("FlushQueue", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(CApp.Log, null);
				using (HistoryTestBox box = new HistoryTestBox())
				{
					using (FileStream locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
					{
						box.LoadHistoryFile();
					}
					Check(log.Contains("Unable to load QuickExec history:"), "History read failure was not logged.");
					box.SaveHistoryFile();
					Check(Convert.ToBase64String(File.ReadAllBytes(path)) == Convert.ToBase64String(original),
						"Failed history load allowed an overwrite of existing history.");
					box.LoadHistoryFile();
					using (FileStream locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
					{
						box.SaveHistoryFile();
					}
					Check(log.Contains("Unable to save QuickExec history:"), "History write failure was not logged.");
				}
			}
			finally { CApp.Log.OnLog -= listener; }
			File.WriteAllLines(path, new[] { "cls", "", "  ", "select text", "cls" });
			using (HistoryTestBox box = new HistoryTestBox())
			{
				box.LoadHistoryFile();
				box.Key(Keys.Up);
				Check(box.Text == "cls", "Reload did not preserve the most recent duplicate.");
				box.Key(Keys.Up);
				Check(box.Text == "select text", "Reload did not skip blank lines or deduplicate.");
				box.Key(Keys.Up);
				Check(box.Text == "select text", "Reload retained an older duplicate.");
			}
			Console.WriteLine("Passed {0} QuickExec history assertions.", assertions);
		}
		finally
		{
			ViewerMode(originalViewerMode);
			if (Directory.Exists(directory)) Directory.Delete(directory, true);
		}
	}
}
'@
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.ReferencedAssemblies.AddRange([string[]]@($core, 'System.dll', 'System.Core.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll'))
Add-Type -TypeDefinition $tests -CompilerParameters $compiler
[QuickExecHistoryTests]::Run()
