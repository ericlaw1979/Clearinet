#Requires -Version 5.1
param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Clearinet\bin\Release'))
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
	throw 'Run with powershell.exe -NoProfile -NonInteractive -STA -File .\Test\CurlExport.Tests.ps1'
}
$core = Join-Path (Resolve-Path -LiteralPath $BuildDirectory).Path 'Clearinet.exe'
$null = [Reflection.Assembly]::LoadFrom($core)
$tests = @'
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Windows.Forms;
using Clearinet;

public static class CurlExportTests
{
	private static readonly MethodInfo Format = typeof(frmViewer).GetMethod("FormatCURLCommand", BindingFlags.NonPublic | BindingFlags.Static);
	private static int assertions;
	private static void Check(bool condition, string message)
	{
		++assertions;
		if (!condition) throw new Exception(message);
	}
	public static Exchange Request(byte[] body)
	{
		Exchange exchange = new Exchange(new HTTPRequestHeaders { HTTPMethod = "POST" }, body);
		exchange.fullUrl = "https://example.test:8443/private?q=1&literal=%PATH%";
		exchange.RequestHeaders.Add("X-Tricky", "quotes \" ' backslash \\ dollar $env:TEMP ` caret ^ & | < > %PATH% ! ( ) \u00e9");
		exchange.RequestHeaders.Add("X-Duplicate", "one");
		exchange.RequestHeaders.Add("X-Duplicate", "two");
		exchange.RequestHeaders.Add("X-Empty", "");
		return exchange;
	}
	public static string Command(Exchange exchange, bool cmd)
	{
		string command = (string)Format.Invoke(null, new object[] { exchange, cmd });
		Check(command.IndexOfAny(new[] { '\r', '\n' }) < 0, "Command contains a physical newline.");
		Check(!command.Contains("GetTempFileName") && !command.Contains("[IO.") && !command.Contains("Start-Process"), "Command creates files or uses an execution wrapper.");
		return command;
	}
	[DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr CommandLineToArgvW(string commandLine, out int count);
	[DllImport("kernel32.dll")]
	private static extern IntPtr LocalFree(IntPtr memory);
	private static void CheckCmdEscaping()
	{
		Exchange request = Request(null);
		string tricky = "quotes \" ' \\" + " trailing\\ ^ & | < > %PATH% ! ( )";
		request.RequestHeaders["X-Tricky"] = tricky;
		request.RequestHeaders.Add("X-Trailing", "backslash\\");
		ProcessStartInfo start = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec"),
			"/d /v:off /c \"echo " + Command(request, true) + "\"");
		start.UseShellExecute = false;
		start.CreateNoWindow = true;
		start.RedirectStandardOutput = true;
		string echoed;
		using (Process process = Process.Start(start))
		{
			echoed = process.StandardOutput.ReadToEnd().TrimEnd('\r', '\n');
			process.WaitForExit();
			Check(process.ExitCode == 0, "CMD echo failed.");
		}
		int count;
		IntPtr argv = CommandLineToArgvW(echoed, out count);
		Check(argv != IntPtr.Zero, "Native argument parsing failed.");
		try
		{
			string[] args = new string[count];
			for (int i = 0; i < count; ++i) args[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size));
			Check(args.Contains("X-Tricky: " + tricky), "CMD changed shell-sensitive characters.");
			Check(args.Contains("X-Trailing: backslash\\"), "CMD changed a trailing backslash.");
		}
		finally { LocalFree(argv); }
	}
	public static void Run()
	{
		CheckCmdEscaping();
		foreach (bool cmd in new[] { false, true })
		{
			Exchange request = Request(Encoding.ASCII.GetBytes("@data ' \" \\ %PATH% & | < > ^ $x ` !"));
			string command = Command(request, cmd);
			Check(command.Contains("data-raw") && !command.Contains("YOURREQUESTBODY"), "Short text was not inlined.");
			foreach (byte[] body in new[] { new byte[] { 0, 255 }, Encoding.ASCII.GetBytes("two\r\nlines"), new byte[1025] })
			{
				Check(Command(Request(body), cmd).Contains("@YOURREQUESTBODYBINARYFILEHERE.DAT"), "Non-plaintext body did not reference the placeholder.");
			}
			Check(!Command(Request(Encoding.ASCII.GetBytes(new string('x', 1024))), cmd).Contains("YOURREQUESTBODY"), "1024-byte plaintext was not inlined.");
			Check(Command(Request(Encoding.ASCII.GetBytes(new string('x', 1025))), cmd).Contains("YOURREQUESTBODY"), "1025-byte plaintext was inlined.");
			Exchange chunked = Request(Encoding.ASCII.GetBytes("3\r\nabc\r\n0\r\n\r\n"));
			chunked.RequestHeaders.Add("Transfer-Encoding", "chunked");
			Check(Command(chunked, cmd).Contains("abc"), "Chunked body was not decoded before export.");
			Check(chunked.RequestHeaders.Exists("Transfer-Encoding"), "Exchange headers were mutated.");
			Check(!Command(Request(null), cmd).Contains("data-"), "Absent body generated a body option.");
			Exchange empty = Request(new byte[0]);
			empty.RequestHeaders.Add("Content-Length", "0");
			Check(Command(empty, cmd).Contains("data-raw"), "Explicit empty body was lost.");
			Exchange head = Request(Encoding.ASCII.GetBytes("body"));
			head.RequestHeaders.HTTPMethod = "HEAD";
			string headCommand = Command(head, cmd);
			Check(!headCommand.Contains("--head ") && !headCommand.EndsWith("--head", StringComparison.Ordinal) && !headCommand.Contains("'head'"), "HEAD body conflicts with --head.");
		}
		PropertyInfo prefsProperty = typeof(CONFIG).GetProperty("pbPrefs", BindingFlags.NonPublic | BindingFlags.Static);
		object oldPrefs = prefsProperty.GetValue(null, null);
		IDataObject oldClipboard = Clipboard.GetDataObject();
		try
		{
			object preferences = Activator.CreateInstance(typeof(PreferenceBag), BindingFlags.Instance | BindingFlags.NonPublic,
				null, new object[] { "SOFTWARE\\Clearinet\\Tests\\" + Guid.NewGuid().ToString("N") }, null);
			prefsProperty.SetValue(null, preferences, null);
			frmViewer viewer = (frmViewer)FormatterServices.GetUninitializedObject(typeof(frmViewer));
			Exchange request = Request(null);
			Clipboard.SetText("unchanged");
			viewer.actCopyCURLCommands(new Exchange[0]);
			Check(Clipboard.GetText() == "unchanged", "Empty selection changed the clipboard.");
			viewer.actCopyCURLCommands(new[] { request, request });
			string[] lines = Clipboard.GetText().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
			Check(lines.Length == 2 && lines.All(line => line == Command(request, false)), "Default PowerShell multi-selection export failed.");
			CApp.Prefs.SetStringPref("app.copy.curl.flavor", "CMD");
			viewer.actCopyCURLCommands(new[] { request });
			Check(Clipboard.GetText().TrimEnd() == Command(request, true), "CMD preference was not honored.");
		}
		finally
		{
			prefsProperty.SetValue(null, oldPrefs, null);
			if (oldClipboard != null) Clipboard.SetDataObject(oldClipboard, true);
			else Clipboard.Clear();
		}
		Console.WriteLine("Passed {0} cURL formatting and clipboard assertions.", assertions);
	}
}
'@
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.ReferencedAssemblies.AddRange([string[]]@($core, 'System.dll', 'System.Core.dll', 'System.Windows.Forms.dll'))
Add-Type -TypeDefinition $tests -CompilerParameters $compiler
[CurlExportTests]::Run()

# Inspect PowerShell's parsed configuration without executing curl or creating files.
function curl.exe { process { $_ } }
$request = [CurlExportTests]::Request([Text.Encoding]::ASCII.GetBytes('@body '' " \\ %PATH% & | < > ^ $x ` !'))
$lines = @(& ([scriptblock]::Create([CurlExportTests]::Command($request, $false))))
foreach ($expected in @('globoff', 'path-as-is', 'http1.1', 'header = "X-Duplicate: one"', 'header = "X-Duplicate: two"', 'header = "X-Empty;"', 'header = "Accept:"', 'header = "User-Agent:"', 'header = "Content-Type:"', 'header = "Expect:"')) {
	if ($lines -notcontains $expected) { throw "Missing cURL configuration: $expected" }
}
if (-not ($lines | Where-Object { $_ -like 'url = "https://example.test:8443/*' })) { throw 'HTTPS URL was changed.' }
if ($lines -contains 'insecure') { throw 'Certificate validation was disabled.' }
if (-not ($lines | Where-Object { $_ -like 'data-raw = "@body*' })) { throw 'Literal leading @ was not preserved.' }
Remove-Item Function:\curl.exe
Write-Host 'Passed PowerShell configuration parsing checks without executing cURL.'
