#Requires -Version 5.1
param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Clearinet\bin\Release'))
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
	throw 'Run with powershell.exe -NoProfile -NonInteractive -STA -File .\Test\ExchangeUIFlags.Tests.ps1'
}
$core = Join-Path (Resolve-Path -LiteralPath $BuildDirectory).Path 'Clearinet.exe'
$null = [Reflection.Assembly]::LoadFrom($core)
$tests = @'
using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Clearinet;

public static class ExchangeUIFlagsTests
{
	private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
	private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
	private static int assertions;
	private static void Check(bool condition, string message)
	{
		++assertions;
		if (!condition) throw new Exception(message);
	}
	private static Exchange Request(params string[] flags)
	{
		Exchange exchange = new Exchange(new HTTPRequestHeaders { HTTPMethod = "GET" }, null);
		exchange.fullUrl = "https://example.test/flags";
		foreach (string flag in flags) exchange[flag] = "test";
		return exchange;
	}
	private static bool Breaks(Exchange exchange, string method)
	{
		return (bool)typeof(Exchange).GetMethod(method, PrivateInstance).Invoke(exchange, null);
	}
	public static void Run()
	{
		// Breakpoint flags, Fiddler aliases, and no breakpoints on hidden Exchanges.
		Check(!Breaks(Request(), "ShouldBreakOnRequest") && !Breaks(Request(), "ShouldBreakOnResponse"), "Unflagged Exchange breaks.");
		Check(Breaks(Request("x-breakrequest"), "ShouldBreakOnRequest") && Breaks(Request("ui-breakrequest"), "ShouldBreakOnRequest"), "Request breakpoint flags ignored.");
		Check(Breaks(Request("x-breakresponse"), "ShouldBreakOnResponse") && Breaks(Request("ui-breakresponse"), "ShouldBreakOnResponse"), "Response breakpoint flags ignored.");
		Check(!Breaks(Request("ui-breakrequest"), "ShouldBreakOnResponse"), "Request breakpoint applied to response.");
		Check(!Breaks(Request("x-breakrequest", "ui-hide"), "ShouldBreakOnRequest"), "Hidden Exchange breaks.");
		Exchange ignored = Request("x-breakresponse");
		ignored.Ignore();
		Check(!Breaks(ignored, "ShouldBreakOnResponse"), "Ignored Exchange breaks.");

		object preferences = Activator.CreateInstance(typeof(PreferenceBag), PrivateInstance,
			null, new object[] { "SOFTWARE\\Clearinet\\Tests\\" + Guid.NewGuid().ToString("N") }, null);
		typeof(CONFIG).GetProperty("pbPrefs", PrivateStatic).SetValue(null, preferences, null);
		FieldInfo mainForm = typeof(CApp).GetField("_frmMain", PrivateStatic);
		object oldForm = mainForm.GetValue(null);
		try
		{
			using (var viewer = new frmViewer())
			{
				mainForm.SetValue(null, viewer);
				IntPtr handle = viewer.lvExchanges.Handle;
				TestList(viewer);
			}
		}
		finally
		{
			mainForm.SetValue(null, oldForm);
		}
		Console.WriteLine("Passed {0} Exchange UI flag assertions.", assertions);
	}
	private static void TestList(frmViewer viewer)
	{
		ExchangeListView list = viewer.lvExchanges;
		MethodInfo update = typeof(frmViewer).GetMethod("UpdateViewItem", PrivateInstance);
		Action<Exchange> Update = x => update.Invoke(viewer, new object[] { x });
		int baseline = list.Items.Count;

		// ui-hide and Ignore() keep an Exchange out of the list.
		Update(Request("ui-hide"));
		Exchange ignored = Request();
		ignored.Ignore();
		Update(ignored);
		Check(list.Items.Count == baseline, "Hidden or ignored Exchange was listed.");

		// ui-hide set after the row is shown (e.g. OnBeforeResponse) removes it; clearing it restores the row.
		Exchange later = Request();
		Update(later);
		Check(list.Items.Count == baseline + 1 && later.ViewItem != null, "Exchange was not listed.");
		later["ui-hide"] = "late";
		Update(later);
		Check(list.Items.Count == baseline && later.ViewItem == null, "Late ui-hide did not remove the row.");
		Update(later);
		Check(list.Items.Count == baseline, "Updating a hidden Exchange listed it again.");
		later["ui-hide"] = null;
		Update(later);
		Check(list.Items.Count == baseline + 1 && later.ViewItem != null && later.ViewItem.ListView == list, "Clearing ui-hide did not restore the row.");

		// Font flags combine, share Font objects, and clear back to the list font.
		Exchange styled = Request("ui-bold", "ui-italic", "ui-strikeout");
		Update(styled);
		Check(styled.ViewItem.Font.Style == (FontStyle.Bold | FontStyle.Italic | FontStyle.Strikeout), "Font flags were not combined.");
		Exchange twin = Request("ui-strikeout", "ui-italic", "ui-bold");
		Update(twin);
		Check(ReferenceEquals(styled.ViewItem.Font, twin.ViewItem.Font), "Rows with the same style do not share a Font.");
		Exchange italic = Request("ui-italic");
		Update(italic);
		Check(italic.ViewItem.Font.Style == FontStyle.Italic, "ui-italic was not applied.");
		styled["ui-italic"] = null;
		styled["ui-strikeout"] = null;
		Update(styled);
		Check(styled.ViewItem.Font.Style == FontStyle.Bold, "Removing flags did not narrow the style.");
		styled["ui-bold"] = null;
		Update(styled);
		Check(ReferenceEquals(styled.ViewItem.Font, list.Font), "Clearing all flags did not restore the list font.");
		typeof(frmViewer).GetMethod("MarkExchanges", PrivateInstance).Invoke(viewer, new object[] { new[] { styled } });
		Check(styled.ViewItem.Font.Bold, "Mark did not bold the row.");
	}
}
'@
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.ReferencedAssemblies.AddRange([string[]]@($core, 'System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll'))
Add-Type -TypeDefinition $tests -CompilerParameters $compiler
[ExchangeUIFlagsTests]::Run()
