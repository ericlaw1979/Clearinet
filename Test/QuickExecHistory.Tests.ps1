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
		Message message = new Message();
		if (!ProcessCmdKey(ref message, key)) throw new Exception("Key was not handled: " + key);
	}

	public void LoadHistoryFile() { InvokeHistory("LoadHistory"); }
	public void SaveHistoryFile() { InvokeHistory("SaveHistory"); }
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

	public static void Run()
	{
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
