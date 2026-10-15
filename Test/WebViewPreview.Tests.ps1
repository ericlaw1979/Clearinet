#Requires -Version 5.1
param(
	[string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Extensions\bin\Release'),
	[string]$CoreDirectory = $BuildDirectory,
	[switch]$Browser,
	[switch]$MissingRuntime,
	[switch]$MissingSdk
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
	throw 'Run with powershell.exe -NoProfile -STA -File .\Test\WebViewPreview.Tests.ps1 [-Browser | -MissingRuntime | -MissingSdk]'
}
if (($Browser -and ($MissingRuntime -or $MissingSdk)) -or ($MissingRuntime -and $MissingSdk)) {
	throw 'Select only one browser/failure mode.'
}
$directory = (Resolve-Path -LiteralPath $BuildDirectory).Path
$coreDirectory = (Resolve-Path -LiteralPath $CoreDirectory).Path
$coreAssemblyPath = Join-Path $coreDirectory 'Clearinet.exe'
$temporary = $null
$previousRuntime = $env:WEBVIEW2_BROWSER_EXECUTABLE_FOLDER
try {
	if ($MissingSdk -and (Test-Path -LiteralPath (Join-Path $directory 'Microsoft.Web.WebView2.Core.dll'))) {
		$temporary = Join-Path ([IO.Path]::GetTempPath()) ('Clearinet-WebView-test-' + [Guid]::NewGuid().ToString('N'))
		$null = New-Item -ItemType Directory -Path $temporary
		Get-ChildItem -LiteralPath $directory -File | Where-Object { $_.Name -notlike 'Microsoft.Web.WebView2.*' } | Copy-Item -Destination $temporary
		& (Join-Path $PSHOME 'powershell.exe') -NoProfile -NonInteractive -STA -File $PSCommandPath -BuildDirectory $temporary -CoreDirectory $coreDirectory -MissingSdk
		if ($LASTEXITCODE -ne 0) { throw 'Missing-SDK child process failed.' }
		return
	}
	if ($MissingRuntime) {
		$env:WEBVIEW2_BROWSER_EXECUTABLE_FOLDER = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N'))
	}
	$assemblyPath = Join-Path $directory 'CAE-Default.dll'
	$tests = @'
using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Clearinet;

public static class WebPreviewTests
{
	public static Assembly Extensions;
	public static int Assertions;
	private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

	public static object Invoke(object target, string method, params object[] args)
	{
		Type type = target as Type ?? target.GetType();
		try { return type.GetMethod(method, Members).Invoke(target is Type ? null : target, args); }
		catch (TargetInvocationException ex)
		{
			if (ex.InnerException == null) throw;
			System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
			throw;
		}
	}

	public static object Get(object target, string name)
	{
		Type type = target.GetType();
		PropertyInfo property = type.GetProperty(name, Members);
		return property != null ? property.GetValue(target, null) : type.GetField(name, Members).GetValue(target);
	}

	public static void Check(bool condition, string message)
	{
		Assertions++;
		if (!condition) throw new Exception(message);
	}

	public static object Document(string mime, string content)
	{
		return Document(mime, Encoding.UTF8.GetBytes(content));
	}

	public static object Document(string mime, byte[] content)
	{
		return Invoke(Extensions.GetType("Extensions.WebPreviewDocument"), "Create", mime, content);
	}

	public static void RunDocuments(string assemblyPath)
	{
		Extensions = Assembly.LoadFrom(assemblyPath);
		bool inspectorFound = false;
		foreach (Type exported in Extensions.GetExportedTypes())
			if (exported.FullName == "Extensions.WebViewResponseInspector") inspectorFound = true;
		Check(inspectorFound, "The WebView inspector remains discoverable if optional SDK assemblies are missing.");
		Type type = Extensions.GetType("Extensions.WebPreviewDocument");
		foreach (string mime in new[] { "text/html", "APPLICATION/XHTML+XML", "image/png", "image/svg+xml", "image/avif", "audio/mpeg", "video/mp4", "font/woff2", "application/font-woff2", "text/plain", "text/css", "application/javascript", "application/json", "application/problem+json", "application/xml", "application/atom+xml" })
			Check((bool)Invoke(type, "IsSupported", mime), "Unsupported expected MIME: " + mime);
		foreach (string mime in new[] { "application/pdf", "application/octet-stream", "", "not a MIME", "text/html\r\nSet-Cookie: secret" })
			Check(!(bool)Invoke(type, "IsSupported", mime), "Unexpected supported MIME: " + mime);

		byte[] original = Encoding.UTF8.GetBytes("<html><script>bad()</script><p>preview</p></html>");
		object html = Document("text/html; charset=utf-8", original);
		original[0] = 0;
		Check(((byte[])Get(html, "Content"))[0] == '<', "HTML must snapshot mutable captured bytes.");
		Check((string)Get(html, "ContentType") == "text/html; charset=utf-8", "HTML charset.");
		string headers = (string)Get(html, "ResponseHeaders");
		foreach (string value in new[] { "Content-Security-Policy: sandbox;", "default-src 'none'", "script-src 'none'", "img-src data:", "media-src data:", "font-src data:", "frame-src 'none'", "form-action 'none'", "base-uri 'none'", "Cache-Control: no-store", "X-DNS-Prefetch-Control: off", "Referrer-Policy: no-referrer", "nosniff" })
			Check(headers.Contains(value), "Missing protection: " + value);
		Check(((string)Get(html, "Url")).StartsWith("https://clearinet-preview.invalid/"), "Synthetic origin.");
		Check((string)Get(html, "Url") != (string)Get(Document("text/html", ""), "Url"), "Per-document navigation URL.");
		Check(!headers.Contains("Set-Cookie"), "No captured cookies.");

		string attack = "</pre><script>bad()</script><img src=https://example.test/>";
		foreach (string mime in new[] { "text/plain", "text/javascript", "application/json", "application/xml" })
		{
			string text = Encoding.UTF8.GetString((byte[])Get(Document(mime, attack), "Content"));
			Check(!text.Contains("<script>"), "Escaping: " + mime);
			Check(text.Contains("&lt;script&gt;"), "Readable escaped text: " + mime);
		}
		object latin = Document("text/plain; charset=windows-1252", new byte[] { 0x63, 0x61, 0x66, 0xe9 });
		Check(System.Net.WebUtility.HtmlDecode(Encoding.UTF8.GetString((byte[])Get(latin, "Content"))).Contains("caf\u00e9"), "Declared text charset.");
		byte[] repetitive = new byte[17 * 1024 * 1024];
		for (int i = 0; i < repetitive.Length; i++) repetitive[i] = (byte)'A';
		foreach (var codec in new[] { Tuple.Create("gzip", Utilities.GzipCompress(repetitive)), Tuple.Create("deflate", Utilities.DeflaterCompress(repetitive)), Tuple.Create("br", Utilities.BrotliCompress(repetitive)) })
		{
			byte[] expanded = Exchange.DecodeBodyFromHeaders(new HTTPResponseHeaders(200, "OK", new[] { "Content-Encoding: " + codec.Item1 }), codec.Item2);
			Check(expanded.Length == repetitive.Length && expanded[expanded.Length - 1] == (byte)'A', "Unrestricted supported decompression: " + codec.Item1);
		}
		object large = Document("text/plain", repetitive);
		Check(Encoding.UTF8.GetString((byte[])Get(large, "Content")).Length > 16 * 1024 * 1024, "No decoded or rendered document size cap.");

		string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(1)\"><script>bad()</script></svg>";
		object svgDocument = Document("image/svg+xml", svg);
		Check((string)Get(svgDocument, "ContentType") == "image/svg+xml" && Encoding.UTF8.GetString((byte[])Get(svgDocument, "Content")) == svg, "Preserve SVG as its own document.");
		Check(((string)Get(svgDocument, "ResponseHeaders")).Contains("script-src 'none'"), "Apply the passive document CSP to SVG.");
		string audio = Encoding.UTF8.GetString((byte[])Get(Document("audio/mpeg", new byte[] { 1, 2, 3 }), "Content"));
		Check(audio.Contains("<audio controls preload=\"none\"") && audio.Contains("data:audio/mpeg;base64,AQID"), "Embed audio with user-controlled playback.");
		string video = Encoding.UTF8.GetString((byte[])Get(Document("video/mp4", new byte[] { 1, 2, 3 }), "Content"));
		Check(video.Contains("<video controls preload=\"none\"") && video.Contains("data:video/mp4;base64,AQID"), "Embed video with user-controlled playback.");
		string font = Encoding.UTF8.GetString((byte[])Get(Document("font/woff2", new byte[] { 1, 2, 3 }), "Content"));
		Check(font.Contains("@font-face") && font.Contains("data:font/woff2;base64,AQID") && font.Contains("The quick brown fox"), "Embed WOFF2 in a font sample preview.");

		var inspector = (ResponseInspectorBase)Activator.CreateInstance(Extensions.GetType("Extensions.WebViewResponseInspector"));
		Check(inspector.ScoreForContentType("text/html") > 60, "HTML score.");
		Check(inspector.ScoreForContentType("image/png") < 90, "Preserve image inspector preference.");
		Check(inspector.ScoreForContentType("text/plain") < 60, "Preserve raw inspector preference.");
		using (var tab = new TabPage { Visible = false })
		{
			inspector.AddToTab(tab);
			Control view = tab.Controls[0];
			byte[] bytes = Encoding.UTF8.GetBytes("<p>compressed</p>");
			using (var output = new MemoryStream())
			{
				using (var gzip = new GZipStream(output, CompressionMode.Compress, true)) gzip.Write(bytes, 0, bytes.Length);
				inspector.Assign(new HTTPResponseHeaders(200, "OK", new[] { "Content-Type: text/html", "Content-Encoding: gzip" }), output.ToArray(), true);
			}
			object doc = Get(view, "document");
			Check(Encoding.UTF8.GetString((byte[])Get(doc, "Content")).Contains("<p>compressed</p>"), "Decode compressed body on all supported app versions.");
			Check(Get(view, "renderer") == null, "Hidden inspector must not load WebView2.");
			inspector.Assign(new HTTPResponseHeaders(200, "OK", new[] { "Content-Type: text/html", "Transfer-Encoding: chunked" }), Encoding.ASCII.GetBytes("4\r\ntest\r\n0\r\n\r\n"), true);
			Check(Encoding.UTF8.GetString((byte[])Get(Get(view, "document"), "Content")) == "test", "Decode chunked body.");
			inspector.Assign(new HTTPResponseHeaders(200, "OK", new[] { "Content-Type: application/pdf" }), bytes, true);
			Check(Get(view, "document") == null, "Clear stale document for unsupported MIME.");
			inspector.Clear();
			Check(Get(view, "document") == null && inspector.body == null && inspector.headers == null, "Clear inspector state.");
		}
		Console.WriteLine("Passive document assertions passed: " + Assertions);
	}

	public static void RunUnavailable()
	{
		Exception failure = null;
		using (var form = new Form())
		using (var tab = new TabPage())
		{
			var inspector = (ResponseInspectorBase)Activator.CreateInstance(Extensions.GetType("Extensions.WebViewResponseInspector"));
			inspector.AddToTab(tab);
			Control view = tab.Controls[0];
			tab.Controls.Remove(view);
			form.Controls.Add(view);
			inspector.Assign(new HTTPResponseHeaders(200, "OK", new[] { "Content-Type: text/html" }), Encoding.UTF8.GetBytes("<p>preview</p>"), true);
			form.Shown += async delegate
			{
				try
				{
					for (int i = 0; i < 300 && Get(view, "unavailableReason") == null; i++) await Task.Delay(100);
					string reason = Get(view, "unavailableReason") as string;
					Check(reason != null && reason.Contains("unavailable") && reason.Contains("Other inspectors remain available"), "Explicit unavailable fallback.");
					Check(Get(view, "renderer") == null, "Dispose failed renderer.");
					inspector.Clear();
					inspector.Assign(new HTTPResponseHeaders(200, "OK", new[] { "Content-Type: text/html" }), Encoding.UTF8.GetBytes("<p>next</p>"), true);
					Check(Get(view, "renderer") == null, "Do not repeatedly initialize failed runtime.");
					Console.WriteLine("Unavailable WebView fallback passed.");
				}
				catch (Exception ex) { failure = ex; }
				finally { form.Close(); }
			};
			Application.Run(form);
		}
		if (failure != null) throw failure;
	}
}
'@
	$compiler = New-Object CodeDom.Compiler.CompilerParameters
	$compiler.ReferencedAssemblies.AddRange([string[]]@('System.dll', 'System.Core.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Net.Http.dll', 'System.IO.Compression.dll', $coreAssemblyPath))
	$unsafePath = Join-Path $directory 'System.Runtime.CompilerServices.Unsafe.dll'
	if (!(Test-Path -LiteralPath $unsafePath)) { $unsafePath = Join-Path (Join-Path $PSScriptRoot '..\Extensions\bin\Release') 'System.Runtime.CompilerServices.Unsafe.dll' }
	if (Test-Path -LiteralPath $unsafePath) { $null = [Reflection.Assembly]::LoadFrom($unsafePath) }
	$null = [Reflection.Assembly]::LoadFrom($coreAssemblyPath)
	if (!$Browser) {
		Add-Type -TypeDefinition $tests -CompilerParameters $compiler
		[WebPreviewTests]::RunDocuments($assemblyPath)
		if ($MissingRuntime -or $MissingSdk) { [WebPreviewTests]::RunUnavailable() }
	}
	if ($Browser) {
		$browserTests = @'
namespace BrowserTests
{
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

public static class WebPreviewBrowserTests
{
	private static object renderer;
	private static object expected;
	private static TaskCompletionSource<bool> completion;
	private static CoreWebView2 core;
	private static readonly StringBuilder navigationLog = new StringBuilder();

	private static byte[] Wave()
	{
		const int samples = 800;
		using (var output = new MemoryStream())
		using (var writer = new BinaryWriter(output))
		{
			writer.Write(Encoding.ASCII.GetBytes("RIFF"));
			writer.Write(36 + samples);
			writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
			writer.Write(16);
			writer.Write((short)1);
			writer.Write((short)1);
			writer.Write(8000);
			writer.Write(8000);
			writer.Write((short)1);
			writer.Write((short)8);
			writer.Write(Encoding.ASCII.GetBytes("data"));
			writer.Write(samples);
			for (int i = 0; i < samples; i++) writer.Write((byte)128);
			return output.ToArray();
		}
	}

	public static void Run()
	{
		Exception failure = null;
		using (var form = new Form { Width = 800, Height = 600 })
		{
			renderer = Activator.CreateInstance(WebPreviewTests.Extensions.GetType("Extensions.WebViewRenderer"), true);
			((Control)renderer).Dock = DockStyle.Fill;
			form.Controls.Add((Control)renderer);
			form.Shown += async delegate
			{
				try { await Exercise(); }
				catch (Exception ex) { failure = ex; }
				finally { form.Close(); }
			};
			Application.Run(form);
		}
		if (failure != null) throw failure;
		ExerciseInspectorView();
		Console.WriteLine("Evergreen browser restrictions and inspector UI lifecycle passed. Total assertions: " + WebPreviewTests.Assertions);
	}

	private static void ExerciseInspectorView()
	{
		Exception failure = null;
		using (var form = new Form { Width = 800, Height = 600 })
		using (var tabs = new TabControl { Dock = DockStyle.Fill })
		using (var tab = new TabPage("WebView"))
		{
			tabs.TabPages.Add(tab);
			form.Controls.Add(tabs);
			var inspector = (ResponseInspectorBase)Activator.CreateInstance(WebPreviewTests.Extensions.GetType("Extensions.WebViewResponseInspector"));
			inspector.AddToTab(tab);
			Control view = tab.Controls[0];
			inspector.Assign(new HTTPResponseHeaders(200, "OK", new[] { "Content-Type: text/html" }), Encoding.UTF8.GetBytes("<p id=actual-view>Inspector rendered</p>"), true);
			form.Shown += async delegate
			{
				try
				{
					for (int i = 0; i < 300 && ((Label)WebPreviewTests.Get(view, "message")).Visible; i++) await Task.Delay(100);
					Label message = (Label)WebPreviewTests.Get(view, "message");
					Control rendererView = (Control)WebPreviewTests.Get(WebPreviewTests.Get(view, "renderer"), "View");
					WebPreviewTests.Check(!message.Visible && rendererView.Visible, "Inspector replaces its Rendering status with the rendered WebView.");
					var webView = (WebView2)WebPreviewTests.Get(WebPreviewTests.Get(view, "renderer"), "browser");
					WebPreviewTests.Check((await webView.CoreWebView2.ExecuteScriptAsync("document.getElementById('actual-view').textContent")).Contains("Inspector rendered"), "Inspector's displayed browser rendered the selected response.");
					inspector.Clear();
				}
				catch (Exception ex) { failure = ex; }
				finally { form.Close(); }
			};
			Application.Run(form);
		}
		if (failure != null) throw failure;
	}

	private static void PreviewCompleted(object sender, EventArgs args)
	{
		if (!ReferenceEquals(WebPreviewTests.Get(args, "Document"), expected)) return;
		string error = WebPreviewTests.Get(args, "Error") as string;
		if (error != null) completion.TrySetException(new Exception(error));
		else completion.TrySetResult(true);
	}

	private static async Task Render(object document)
	{
		expected = document;
		completion = new TaskCompletionSource<bool>();
		navigationLog.Clear();
		WebPreviewTests.Invoke(renderer, "Render", document);
		if (await Task.WhenAny(completion.Task, Task.Delay(30000)) != completion.Task) throw new Exception("Preview navigation timed out.\r\n" + navigationLog);
		await completion.Task;
	}

	private static async Task IsTrue(string expression, string description)
	{
		WebPreviewTests.Check(await core.ExecuteScriptAsync(expression) == "true", description);
	}

	private static async Task Exercise()
	{
		await (Task)WebPreviewTests.Invoke(renderer, "InitializeAsync");
		var browser = (WebView2)WebPreviewTests.Get(renderer, "browser");
		core = browser.CoreWebView2;
		core.NavigationStarting += delegate(object sender, CoreWebView2NavigationStartingEventArgs e) { navigationLog.AppendLine("Starting: " + e.Uri + "; canceled=" + e.Cancel + "; user=" + e.IsUserInitiated); };
		core.NavigationCompleted += delegate(object sender, CoreWebView2NavigationCompletedEventArgs e) { navigationLog.AppendLine("Completed: " + e.IsSuccess + "; " + e.WebErrorStatus); };
		core.WebResourceRequested += delegate(object sender, CoreWebView2WebResourceRequestedEventArgs e) { navigationLog.AppendLine("Resource: " + e.Request.Uri + "; status=" + (e.Response == null ? 0 : e.Response.StatusCode)); };
		EventInfo completed = renderer.GetType().GetEvent("PreviewCompleted");
		completed.AddEventHandler(renderer, Delegate.CreateDelegate(completed.EventHandlerType, typeof(WebPreviewBrowserTests).GetMethod("PreviewCompleted", BindingFlags.NonPublic | BindingFlags.Static)));
		CoreWebView2Settings settings = core.Settings;
		WebPreviewTests.Check(!settings.IsScriptEnabled && !settings.IsWebMessageEnabled && !settings.AreHostObjectsAllowed, "No page scripts or host bridge.");
		WebPreviewTests.Check(!settings.AreDevToolsEnabled && !settings.AreDefaultContextMenusEnabled && !settings.AreBrowserAcceleratorKeysEnabled, "No browser escape UI.");
		WebPreviewTests.Check(!settings.IsPasswordAutosaveEnabled && !settings.IsGeneralAutofillEnabled && !settings.AreDefaultScriptDialogsEnabled, "No autofill or dialogs.");
		WebPreviewTests.Check(!browser.AllowExternalDrop && !core.IsMuted, "No dropped files; user-controlled audio can be heard.");
		WebPreviewTests.Check(core.Profile.IsInPrivateModeEnabled, "InPrivate profile.");

		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		try
		{
			string remote = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/blocked";
			string html = "<!doctype html><title>passive</title><meta http-equiv=refresh content='1;url=" + remote + "'>" +
				"<link rel=stylesheet href='" + remote + "'><link rel=preconnect href='" + remote + "'>" +
				"<style>body{background-color:rgb(12,34,56);background-image:url(" + remote + ")}</style>" +
				"<script>window.pageScriptRan=true;document.title='unsafe'</script><body onload='window.pageScriptRan=true'>" +
				"<a id=link href='" + remote + "'>link</a><form id=form action='" + remote + "'><input name=q value=secret></form>" +
				"<img src='" + remote + "'><iframe src='" + remote + "'></iframe>" +
				"<img id=embedded src='data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9WQAAAAASUVORK5CYII='>";
			object document = WebPreviewTests.Document("text/html", html);
			await Render(document);
			await Task.Delay(1500);
			string source = (string)WebPreviewTests.Get(document, "Url");
			WebPreviewTests.Check(core.Source == source, "Block meta refresh.");
			await IsTrue("typeof window.pageScriptRan === 'undefined' && document.title === 'passive'", "Page scripts and event handlers do not execute.");
			await IsTrue("getComputedStyle(document.body).backgroundColor === 'rgb(12, 34, 56)'", "Inline CSS renders.");
			await IsTrue("document.getElementById('embedded').naturalWidth === 1", "Embedded images render.");
			await IsTrue("(function(){try{localStorage.setItem('secret','x');return false;}catch(e){return e.name==='SecurityError';}})()", "Sandbox origin cannot persist DOM storage.");
			await core.ExecuteScriptAsync("document.getElementById('link').click();document.getElementById('form').submit();window.open('" + remote + "');fetch('" + remote + "').catch(function(){});");
			await Task.Delay(500);
			WebPreviewTests.Check(core.Source == source, "Block links, forms, and popups.");
			foreach (string url in new[] { source, remote, "file:///C:/Windows/win.ini", "data:text/html,unsafe", "mailto:preview@example.invalid" })
			{
				core.Navigate(url);
				await Task.Delay(150);
				WebPreviewTests.Check(core.Source == source, "Block external/top-level navigation: " + url);
			}
			WebPreviewTests.Check(!listener.Pending(), "No HTTP or preconnect socket reached the loopback server.");

			core.Navigate(source + "#same-document-fragment");
			await Task.Delay(150);
			WebPreviewTests.Check(core.Source == source + "#same-document-fragment", "Same-document fragments never leave the offline document.");
			WebPreviewTests.Check(!listener.Pending(), "Fragment navigation cannot open a network connection.");
			await Render(WebPreviewTests.Document("text/plain", "<script>window.pageScriptRan=true</script>"));
			await IsTrue("document.querySelectorAll('script').length===0 && document.querySelector('pre').textContent.indexOf('<script>')===0", "Plain text stays inert.");
			string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='20' height='20' onload='window.svgRan=true'><script>window.svgRan=true</script><image href='" + remote + "'/><a href='" + remote + "'><text y='15'>SVG preview</text></a><rect width='2' height='2'/></svg>";
			await Render(WebPreviewTests.Document("image/svg+xml", svg));
			await IsTrue("document.documentElement.localName==='svg' && document.querySelector('image')!==null && typeof window.svgRan==='undefined'", "SVG renders as a document without executing scripts.");
			await core.ExecuteScriptAsync("document.querySelector('a').dispatchEvent(new MouseEvent('click',{bubbles:true,cancelable:true,view:window}));");
			await Task.Delay(300);
			WebPreviewTests.Check(!listener.Pending(), "SVG subresources and hyperlinks cannot reach the network.");
			await Render(WebPreviewTests.Document("audio/wav", Wave()));
			await IsTrue("(function(){var a=document.querySelector('audio');return !!a&&a.controls&&!a.autoplay&&a.paused&&a.getAttribute('src').indexOf('data:audio/wav;base64,')===0&&a.canPlayType('audio/wav')!=='';})()", "WAV renders in a user-controlled, playable audio element.");
			await Render(WebPreviewTests.Document("video/mp4", new byte[] { 0, 0, 0, 24, 102, 116, 121, 112 }));
			await IsTrue("(function(){var v=document.querySelector('video');return !!v&&v.controls&&!v.autoplay&&v.paused&&v.getAttribute('src').indexOf('data:video/mp4;base64,')===0;})()", "Video renders in a user-controlled video element.");
			await Render(WebPreviewTests.Document("font/woff2", new byte[] { 1, 2, 3 }));
			await IsTrue("document.querySelector('.sample')!==null&&getComputedStyle(document.querySelector('.sample')).fontFamily.indexOf('ResponsePreview')>=0", "WOFF2 font sample is rendered using the embedded font family.");
			await Render(WebPreviewTests.Document("text/html", new string(' ', 3 * 1024 * 1024) + "<p id=large>large response</p>"));
			await IsTrue("document.getElementById('large').textContent==='large response'", "No NavigateToString size limit.");
			WebPreviewTests.Invoke(renderer, "Render", WebPreviewTests.Document("text/html", "stale"));
			WebPreviewTests.Invoke(renderer, "Clear");
			await Render(WebPreviewTests.Document("text/html", "<p id=current>latest</p>"));
			await IsTrue("document.getElementById('current').textContent==='latest'", "Rapid assign/clear does not render a stale response.");
			WebPreviewTests.Invoke(renderer, "Clear");
			WebPreviewTests.Check(WebPreviewTests.Get(renderer, "document") == null, "Release current document on clear.");
		}
		finally { listener.Stop(); }
	}
}
}
'@
		foreach ($name in 'Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.WinForms.dll') {
			$path = Join-Path $directory $name
			$null = $compiler.ReferencedAssemblies.Add($path)
			$null = [Reflection.Assembly]::LoadFrom($path)
		}
		Add-Type -TypeDefinition ($tests + [Environment]::NewLine + $browserTests) -CompilerParameters $compiler
		[WebPreviewTests]::RunDocuments($assemblyPath)
		[BrowserTests.WebPreviewBrowserTests]::Run()
	}
}
finally {
	$env:WEBVIEW2_BROWSER_EXECUTABLE_FOLDER = $previousRuntime
	if ($temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force }
}
