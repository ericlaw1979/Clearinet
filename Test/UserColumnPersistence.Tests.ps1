#Requires -Version 5.1
param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Clearinet\bin\Release'))
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
	throw 'Run with powershell.exe -NoProfile -NonInteractive -STA -File .\Test\UserColumnPersistence.Tests.ps1'
}
$core = Join-Path (Resolve-Path -LiteralPath $BuildDirectory).Path 'Clearinet.exe'
$null = [Reflection.Assembly]::LoadFrom($core)
$tests = @'
using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Clearinet;

public static class UserColumnPersistenceTests
{
	private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
	private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
	private const string OddTitle = "Odd, title; with ~ and \u00a7 \u00e9";
	private static int assertions;
	private static void Check(bool condition, string message)
	{
		++assertions;
		if (!condition) throw new Exception(message);
	}
	private static object Call(ExchangeListView list, string method, params object[] args)
	{
		return typeof(ExchangeListView).GetMethod(method, PrivateInstance).Invoke(list, args);
	}
	private static ColumnHeader Column(ExchangeListView list, string title)
	{
		return list.Columns.Cast<ColumnHeader>().FirstOrDefault(c => c.Text == title);
	}
	public static void Run()
	{
		object preferences = Activator.CreateInstance(typeof(PreferenceBag), PrivateInstance,
			null, new object[] { "SOFTWARE\\Clearinet\\Tests\\" + Guid.NewGuid().ToString("N") }, null);
		typeof(CONFIG).GetProperty("pbPrefs", PrivateStatic).SetValue(null, preferences, null);
		FieldInfo mainForm = typeof(CApp).GetField("_frmMain", PrivateStatic);
		object oldForm = mainForm.GetValue(null);
		string userColumns, layout;
		try
		{
			// First session: the user adds columns via QuickExec and the dialog's method; a script adds its own.
			using (var viewer = new frmViewer())
			{
				mainForm.SetValue(null, viewer);
				ExchangeListView list = viewer.lvExchanges;
				IntPtr handle = list.Handle;
				Check(CApp.RunExecAction("cols add Server @response.Server"), "cols add was not handled.");
				Call(list, "AddUserColumn", OddTitle, 70, "@request.X-Odd,Name");
				Check(CApp.RunExecAction("cols add Gone @request.Gone") && CApp.RunExecAction("cols remove Gone"), "cols add/remove was not handled.");
				list.AddBoundColumn("ScriptCol", 55, "@response.Via");
				Column(list, "Server").Width = 123;
				Column(list, "Server").DisplayIndex = 1;
				userColumns = (string)Call(list, "GetUserColumns");
				layout = (string)Call(list, "GetColumnLayout");
			}
			Check(userColumns.Contains("Server") && !userColumns.Contains("ScriptCol") && !userColumns.Contains("Gone"), "Wrong columns saved: " + userColumns);

			// Next session: restore as InitializeUIFromPrefs does, then the script adds its column again.
			using (var viewer = new frmViewer())
			{
				mainForm.SetValue(null, viewer);
				ExchangeListView list = viewer.lvExchanges;
				IntPtr handle = list.Handle;
				Call(list, "ApplyUserColumns", userColumns);
				Call(list, "ApplyColumnLayout", layout);
				list.AddBoundColumn("ScriptCol", 55, "@response.Via");
				Check(Column(list, "Server") != null && Column(list, OddTitle) != null, "User columns were not restored.");
				Check(Column(list, "Gone") == null, "Removed column was restored.");
				Check(Column(list, "ScriptCol") != null && Column(list, "ScriptCol.bis") == null, "Script column was duplicated.");
				Check(Column(list, "Server").Width == 123 && Column(list, "Server").DisplayIndex == 1, "Width or position was not restored.");
				Check((string)Call(list, "GetUserColumns") == userColumns, "Saving after restore changed the saved columns.");

				Exchange exchange = new Exchange(new HTTPRequestHeaders { HTTPMethod = "GET" }, null);
				exchange.fullUrl = "https://example.test/";
				exchange.RequestHeaders.Add("X-Odd,Name", "odd");
				exchange.ResponseHeaders = new HTTPResponseHeaders();
				exchange.ResponseHeaders.Add("Server", "nginx");
				Check((string)Call(list, "PopulateColumn", Column(list, "Server").Index, exchange) == "nginx"
					&& (string)Call(list, "PopulateColumn", Column(list, OddTitle).Index, exchange) == "odd", "Restored columns show the wrong data.");

				int count = list.Columns.Count;
				Call(list, "ApplyUserColumns", "");
				Call(list, "ApplyUserColumns", "garbage;a,b;x,10,;,10,y");
				Check(list.Columns.Count == count, "Malformed saved columns created columns.");
			}
		}
		finally
		{
			mainForm.SetValue(null, oldForm);
		}
		Console.WriteLine("Passed {0} user column persistence assertions.", assertions);
	}
}
'@
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.ReferencedAssemblies.AddRange([string[]]@($core, 'System.dll', 'System.Core.dll', 'System.Windows.Forms.dll'))
Add-Type -TypeDefinition $tests -CompilerParameters $compiler
[UserColumnPersistenceTests]::Run()
