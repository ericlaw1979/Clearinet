#Requires -Version 5.1
param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Clearinet\bin\Release'))
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
	throw 'Run with powershell.exe -NoProfile -NonInteractive -STA -File .\Test\CustomColumnTypes.Tests.ps1'
}
$core = Join-Path (Resolve-Path -LiteralPath $BuildDirectory).Path 'Clearinet.exe'
$null = [Reflection.Assembly]::LoadFrom($core)
$tests = @'
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Clearinet;

public static class CustomColumnTypesTests
{
	private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
	private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
	private static readonly Type Sources = typeof(Exchange).Assembly.GetType("Clearinet.ExchangeColumnSources");
	private static int assertions;
	private static void Check(bool condition, string message)
	{
		++assertions;
		if (!condition) throw new Exception(message);
	}
	private static string Value(string field, Exchange exchange)
	{
		object source = Sources.GetMethod("Find", PrivateStatic).Invoke(null, new object[] { field });
		Check(source != null, "No source for " + field);
		var provider = (Func<Exchange, string>)source.GetType().GetField("Provider").GetValue(source);
		return provider(exchange);
	}
	private static Exchange Response(params string[] headers)
	{
		Exchange exchange = new Exchange(new HTTPRequestHeaders { HTTPMethod = "GET" }, null);
		exchange.fullUrl = "https://example.test/";
		exchange.ResponseHeaders = new HTTPResponseHeaders();
		for (int i = 0; i < headers.Length; i += 2) exchange.ResponseHeaders.Add(headers[i], headers[i + 1]);
		return exchange;
	}
	private static string Sha256(byte[] data)
	{
		using (SHA256 hasher = SHA256.Create()) return BitConverter.ToString(hasher.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
	}
	private static object Field(object o, string name)
	{
		return o.GetType().GetField(name, PrivateInstance).GetValue(o);
	}
	private static ColumnHeader Column(ExchangeListView list, string title)
	{
		return list.Columns.Cast<ColumnHeader>().FirstOrDefault(c => c.Text == title);
	}
	private static string SortType(ColumnHeader column)
	{
		object info = column.Tag;
		return info.GetType().GetField("SortType").GetValue(info).ToString();
	}

	public static void Run()
	{
		Check(Sources != null && typeof(frmELVAddColumn).GetField("cbxField", PrivateInstance) != null,
			"This Clearinet.exe doesn't include the Timers/Calculated columns change. Rebuild the Release configuration after applying it.");
		TestValues();
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
				TestColumns(viewer.lvExchanges);
			}
		}
		finally
		{
			mainForm.SetValue(null, oldForm);
		}
		Console.WriteLine("Passed {0} custom column type assertions.", assertions);
	}

	private static void TestValues()
	{
		Exchange timed = Response();
		DateTime start = new DateTime(2026, 11, 4, 10, 0, 0);
		Check(Value("@timers.ClientBeginRequest", timed) == "" && Value("@calc.TTFB", timed) == "" && Value("@timers.DNSTime", timed) == "", "Unrecorded timers are not blank.");
		timed.Timers.ClientBeginRequest = start;
		timed.Timers.ProxyDoneRequest = start.AddMilliseconds(20);
		timed.Timers.ServerBeginResponse = start.AddMilliseconds(170);
		timed.Timers.ServerDoneResponse = start.AddMilliseconds(1170);
		timed.Timers.ClientDoneResponse = start.AddMilliseconds(1234);
		timed.Timers.DNSTime = 12;
		Check(Value("@timers.ServerBeginResponse", timed) == "10:00:00.170" && Value("@TIMERS.dnstime", timed) == "12", "Timer values are wrong.");
		Check(Value("@calc.TotalTime", timed) == "1234" && Value("@calc.TTFB", timed) == "150" && Value("@calc.DownloadTime", timed) == "1000", "Durations are wrong.");

		byte[] content = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("hello world ", 1000)));
		byte[] gzipped;
		using (var stream = new MemoryStream())
		{
			using (var gzip = new GZipStream(stream, CompressionMode.Compress)) gzip.Write(content, 0, content.Length);
			gzipped = stream.ToArray();
		}
		Exchange compressed = Response("Content-Encoding", "gzip");
		compressed.ResponseBody = gzipped;
		Check(Value("@calc.DecodedResponseSize", compressed) == (12000).ToString("N0"), "Decoded size is wrong.");
		Check(Value("@calc.CompressionSavings", compressed) == Math.Round(100.0 * (12000 - gzipped.Length) / 12000).ToString("0"), "Compression savings are wrong.");
		Check(Value("@calc.ResponseHash", compressed) == Sha256(content), "Hash is not of the decoded body.");
		compressed.ResponseHeaders.Remove("Content-Encoding");
		compressed.ResponseBody = Encoding.ASCII.GetBytes("abc");
		Check(Value("@calc.DecodedResponseSize", compressed) == "3" && Value("@calc.CompressionSavings", compressed) == "", "Values were not recalculated for a new body.");
		Exchange broken = Response("Content-Encoding", "gzip");
		broken.ResponseBody = Encoding.ASCII.GetBytes("not gzip");
		Check(Value("@calc.DecodedResponseSize", broken) == "" && Value("@calc.ResponseHash", broken) == "", "Undecodable body did not give blank values.");

		Exchange secure = Response("Strict-Transport-Security", "max-age=1", "X-Content-Type-Options", "nosniff", "Cross-Origin-Opener-Policy", "same-origin");
		Check(Value("@calc.SecurityHeaders", secure) == "HSTS, COOP, nosniff", "Security headers are wrong: " + Value("@calc.SecurityHeaders", secure));
		Check(Sources.GetMethod("Find", PrivateStatic).Invoke(null, new object[] { "@calc.Nope" }) == null, "Unknown field found.");
	}

	private static void TestColumns(ExchangeListView list)
	{
		// QuickExec and AddBoundColumn pick the right sort type.
		// "cols save" columns are kept across restarts; "cols add" columns last only for this session.
		Check(CApp.RunExecAction("cols save TTFB @calc.TTFB") && CApp.RunExecAction("cols add Started @timers.ClientBeginRequest"), "cols save/add was not handled.");
		Check(SortType(Column(list, "TTFB")) == "Numeric" && SortType(Column(list, "Started")) == "DateTime", "Wrong sort type.");
		Check(SortType(list.AddBoundColumn("Hash", 80, "@calc.ResponseHash")) == "Text", "Wrong sort type for hash.");

		// The dialog's Timers and Calculated lists.
		using (var dialog = new frmELVAddColumn())
		{
			dialog.Show();
			Application.DoEvents();
			var source = (ComboBox)Field(dialog, "cbxSource");
			var field = (ComboBox)Field(dialog, "cbxField");
			var name = (TextBox)Field(dialog, "txtName");
			var title = (TextBox)Field(dialog, "txtTitle");
			var add = (Button)Field(dialog, "btnAdd");
			Check(source.Items.Count == 5, "Timers and Calculated are not offered.");
			source.SelectedIndex = 4;
			Check(field.Visible && !name.Visible && field.GetItemText(field.Items[0]) == "TotalTime" && title.Text == "Total ms", "Calculated list is wrong.");
			field.SelectedIndex = field.Items.Cast<object>().ToList().FindIndex(i => field.GetItemText(i) == "CompressionSavings");
			Check(title.Text == "Saved %", "Title was not suggested.");
			add.PerformClick();
			Check(Column(list, "Saved %") != null && SortType(Column(list, "Saved %")) == "Numeric", "Dialog did not add the column.");
			source.SelectedIndex = 3;
			Check(field.Items.Count == 16 && title.Text == "ClientConnected", "Timers list is wrong.");
			source.SelectedIndex = 0;
			Check(name.Visible && !field.Visible && title.Text == "", "Switching back to headers did not restore the name box.");
			dialog.Close();
		}

		// Saved and restored like other user columns.
		string saved = (string)typeof(ExchangeListView).GetMethod("GetUserColumns", PrivateInstance).Invoke(list, null);
		Check(saved.Contains("TTFB~@calc.TTFB") && saved.Contains("~@calc.CompressionSavings") && !saved.Contains("Started") && !saved.Contains("ResponseHash"), "Wrong columns saved: " + saved);
	}
}
'@
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.ReferencedAssemblies.AddRange([string[]]@($core, 'System.dll', 'System.Core.dll', 'System.Windows.Forms.dll'))
Add-Type -TypeDefinition $tests -CompilerParameters $compiler
[CustomColumnTypesTests]::Run()
