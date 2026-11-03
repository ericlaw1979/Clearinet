#Requires -Version 5.1
param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Clearinet\bin\Release'))
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
	throw 'Run with powershell.exe -NoProfile -NonInteractive -STA -File .\Test\CopyAsCode.Tests.ps1'
}
$core = Join-Path (Resolve-Path -LiteralPath $BuildDirectory).Path 'Clearinet.exe'
$null = [Reflection.Assembly]::LoadFrom($core)
$tests = @'
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Windows.Forms;
using Clearinet;

public static class CopyAsCodeTests
{
	private static readonly Type Export = typeof(Exchange).Assembly.GetType("Clearinet.RequestCodeExport");
	private static int assertions;
	private static void Check(bool condition, string message)
	{
		++assertions;
		if (!condition) throw new Exception(message);
	}
	public static Exchange Request(string method, byte[] body)
	{
		Exchange exchange = new Exchange(new HTTPRequestHeaders { HTTPMethod = method }, body);
		exchange.fullUrl = "https://example.test:8443/private?q=1&literal=$env:TEMP";
		exchange.RequestHeaders.Add("X-Tricky", "quotes \" ' \u2019 backslash \\ dollar $env:TEMP $(Get-Date) ` caret ^ & | < >");
		exchange.RequestHeaders.Add("X-Duplicate", "one");
		exchange.RequestHeaders.Add("x-duplicate", "two");
		exchange.RequestHeaders.Add("X-Empty", "");
		exchange.RequestHeaders.Add("Cookie", "a=1");
		exchange.RequestHeaders.Add("Cookie", "b=2");
		exchange.RequestHeaders.Add("Connection", "keep-alive");
		return exchange;
	}
	private static string Format(string formatter, Exchange exchange)
	{
		string code = (string)Export.GetMethod(formatter, BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { exchange });
		Check(code.IndexOfAny(new[] { '\r', '\n' }) < 0, formatter + " output contains a physical newline.");
		return code;
	}
	public static string PowerShell(Exchange exchange) { return Format("FormatPowerShellCommand", exchange); }
	public static string Fetch(Exchange exchange) { return Format("FormatFetchCall", exchange); }
	public static void Run()
	{
		// fetch(): structure, escaping and bodies.
		string fetch = Fetch(Request("POST", Encoding.ASCII.GetBytes("a=\"1\"")));
		Check(fetch.StartsWith("fetch(\"https://example.test:8443/", StringComparison.Ordinal) && fetch.EndsWith("});", StringComparison.Ordinal), "fetch() call is malformed.");
		Check(fetch.All(c => c >= 0x20 && c <= 0x7e), "fetch() call is not printable ASCII.");
		Check(fetch.Contains("[\"X-Duplicate\", \"one\"], [\"x-duplicate\", \"two\"]"), "fetch() lost a repeated header.");
		Check(fetch.Contains("\\u00e2\\u0080\\u0099"), "fetch() header was not written as UTF-8 bytes.");
		Check(fetch.Contains("body: \"a=\\\"1\\\"\""), "fetch() text body was not escaped.");
		Check(!fetch.Contains("Connection"), "fetch() kept a connection-level header.");
		Check(Fetch(Request("PUT", new byte[] { 0, 255 })).Contains("Uint8Array.from(atob(\"AP8=\")"), "fetch() binary body was not Base64.");
		string fetchGet = Fetch(Request("GET", Encoding.ASCII.GetBytes("body")));
		Check(!fetchGet.Contains("body:") && fetchGet.Contains("4-byte request body omitted"), "fetch() GET body was not omitted.");
		Check(!Fetch(Request("GET", null)).Contains("body"), "fetch() absent body generated a body.");

		// PowerShell: structure and bodies. Quoting and parameter binding are checked by the script below.
		string ps = PowerShell(Request("POST", Encoding.ASCII.GetBytes("x")));
		Check(ps.StartsWith("Invoke-WebRequest -UseBasicParsing -Uri 'https://example.test:8443/", StringComparison.Ordinal), "PowerShell command is malformed.");
		Check(ps.Contains("-Method POST") && ps.Contains("'X-Duplicate' = 'one, two'") && ps.Contains("'Cookie' = 'a=1; b=2'"), "PowerShell headers were not folded.");
		Check(!ps.Contains("Connection"), "PowerShell kept a connection-level header.");
		Check(PowerShell(Request("PROPFIND", null)).Contains("-CustomMethod 'PROPFIND'"), "Custom method was not used.");
		Check(PowerShell(Request("post", null)).Contains("-CustomMethod 'post'"), "Lowercase method was normalized.");
		Check(PowerShell(Request("PUT", new byte[] { 0, 255 })).Contains("-Body ([Convert]::FromBase64String('AP8='))"), "PowerShell binary body was not Base64.");

		// Shared body handling.
		Exchange chunked = Request("POST", Encoding.ASCII.GetBytes("3\r\nabc\r\n0\r\n\r\n"));
		chunked.RequestHeaders.Add("Transfer-Encoding", "chunked");
		Check(PowerShell(chunked).EndsWith("-Body 'abc'", StringComparison.Ordinal) && Fetch(chunked).Contains("body: \"abc\""), "Chunked body was not decoded.");
		Check(chunked.RequestHeaders.Exists("Transfer-Encoding"), "Exchange headers were mutated.");
		Exchange empty = Request("POST", new byte[0]);
		empty.RequestHeaders.Add("Content-Length", "0");
		Check(PowerShell(empty).EndsWith("-Body ''", StringComparison.Ordinal) && Fetch(empty).Contains("body: \"\""), "Explicit empty body was lost.");
		Exchange noMethod = Request("", null);
		try { PowerShell(noMethod); Check(false, "Missing method did not throw."); }
		catch (TargetInvocationException) { ++assertions; }

		// Clipboard actions.
		IDataObject oldClipboard = Clipboard.GetDataObject();
		try
		{
			frmViewer viewer = (frmViewer)FormatterServices.GetUninitializedObject(typeof(frmViewer));
			Exchange request = Request("POST", Encoding.ASCII.GetBytes("x"));
			Clipboard.SetText("unchanged");
			viewer.actCopyPowerShellCommands(new Exchange[0]);
			viewer.actCopyFetchCalls(new Exchange[0]);
			Check(Clipboard.GetText() == "unchanged", "Empty selection changed the clipboard.");
			viewer.actCopyPowerShellCommands(new[] { request, request });
			string[] lines = Clipboard.GetText().Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
			Check(lines.Length == 2 && lines.All(line => line == PowerShell(request)), "PowerShell multi-selection export failed.");
			viewer.actCopyFetchCalls(new[] { request });
			Check(Clipboard.GetText().TrimEnd() == Fetch(request), "fetch() export failed.");
		}
		finally
		{
			if (oldClipboard != null) Clipboard.SetDataObject(oldClipboard, true);
			else Clipboard.Clear();
		}
		Console.WriteLine("Passed {0} PowerShell/fetch() formatting and clipboard assertions.", assertions);
	}
}
'@
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.ReferencedAssemblies.AddRange([string[]]@($core, 'System.dll', 'System.Core.dll', 'System.Windows.Forms.dll'))
Add-Type -TypeDefinition $tests -CompilerParameters $compiler
[CopyAsCodeTests]::Run()

# Run the copied command against a stand-in Invoke-WebRequest to check what PowerShell parses and binds,
# without sending anything.
function Invoke-WebRequest {
	param([switch]$UseBasicParsing, [string]$Uri, [string]$Method, [string]$CustomMethod,
		[string]$UserAgent, [string]$ContentType, [hashtable]$Headers, [object]$Body)
	[pscustomobject]@{ Uri = $Uri; Method = $Method; UserAgent = $UserAgent; Headers = $Headers; Body = $Body }
}
$request = [CopyAsCodeTests]::Request('POST', [Text.Encoding]::ASCII.GetBytes('@body '' " \ $x $(Get-Date) ` ^ & |'))
$request.RequestHeaders.Add('User-Agent', "Agent 'q'")
$bound = & ([scriptblock]::Create([CopyAsCodeTests]::PowerShell($request)))
if ($bound.Uri -cne 'https://example.test:8443/private?q=1&literal=$env:TEMP') { throw "URL was changed: $($bound.Uri)" }
if ($bound.Method -cne 'POST') { throw 'Method was not bound.' }
if ($bound.UserAgent -cne "Agent 'q'") { throw 'User-Agent was not bound.' }
if ($bound.Headers['X-Tricky'] -cne $request.RequestHeaders['X-Tricky']) { throw 'Header value with quotes or expansions was changed.' }
if ($bound.Headers['X-Empty'] -cne '') { throw 'Empty header was lost.' }
if ($bound.Body -cne '@body '' " \ $x $(Get-Date) ` ^ & |') { throw 'Text body was changed.' }
$binary = & ([scriptblock]::Create([CopyAsCodeTests]::PowerShell([CopyAsCodeTests]::Request('PUT', [byte[]](0, 1, 254, 255)))))
if (($binary.Body -join ',') -ne '0,1,254,255') { throw 'Binary body was changed.' }
Remove-Item Function:\Invoke-WebRequest
Write-Host 'Passed PowerShell parameter binding checks without sending requests.'
