#Requires -Version 5.1
param(
	[string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Clearinet\bin\Release'),
	[long]$WindowHandle = 0
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
	throw 'Run with powershell.exe -NoProfile -NonInteractive -STA -File .\Test\HiddenExecActionTargetWindow.Tests.ps1'
}
$core = Join-Path (Resolve-Path -LiteralPath $BuildDirectory).Path 'Clearinet.exe'
$null = [Reflection.Assembly]::LoadFrom($core)
$tests = @'
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Clearinet;

public static class HiddenExecActionTargetWindowTests
{
	[StructLayout(LayoutKind.Sequential)]
	private struct CopyData
	{
		public UIntPtr dwData;
		public uint cbData;
		public IntPtr lpData;
	}

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);
	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
	[DllImport("user32.dll")]
	private static extern bool IsWindow(IntPtr window);
	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(IntPtr window);
	[DllImport("user32.dll")]
	private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr sender, ref CopyData data);
	[DllImport("user32.dll", EntryPoint = "SendMessage")]
	private static extern IntPtr SendNull(IntPtr window, int message, IntPtr sender, IntPtr data);

	private const string PreferenceName = "test.ipc.\u00e9\u4e2d\ud83d\ude00";
	private static int assertions;

	private static void Check(bool condition, string message)
	{
		++assertions;
		if (!condition) throw new Exception(message);
	}

	private static IntPtr FindReceiver()
	{
		IntPtr window = IntPtr.Zero;
		while ((window = FindWindowEx(IntPtr.Zero, window, null, "Clearinet.ExecActionTarget")) != IntPtr.Zero)
		{
			uint process;
			GetWindowThreadProcessId(window, out process);
			if (process == Process.GetCurrentProcess().Id) return window;
		}
		return IntPtr.Zero;
	}

	private static long Send(IntPtr window, byte[] bytes, uint id, uint? length)
	{
		IntPtr payload = bytes == null ? IntPtr.Zero : Marshal.AllocHGlobal(Math.Max(1, bytes.Length));
		try
		{
			if (bytes != null) Marshal.Copy(bytes, 0, payload, bytes.Length);
			CopyData data = new CopyData {
				dwData = new UIntPtr(id),
				cbData = length ?? (uint)(bytes == null ? 0 : bytes.Length),
				lpData = payload
			};
			return SendMessage(window, 0x004A, IntPtr.Zero, ref data).ToInt64();
		}
		finally { if (payload != IntPtr.Zero) Marshal.FreeHGlobal(payload); }
	}

	public static void RunSender(long handle)
	{
		Check(Send(new IntPtr(handle), Encoding.UTF8.GetBytes("prefs remove " + PreferenceName + "\0"), 61178, null) == 1,
			"Cross-process UTF-8 command was not handled.");
	}

	public static void Run(string script, string buildDirectory, string senderScript)
	{
		Assembly core = typeof(CApp).Assembly;
		object preferences = Activator.CreateInstance(typeof(PreferenceBag), BindingFlags.Instance | BindingFlags.NonPublic,
			null, new object[] { "SOFTWARE\\Clearinet\\Tests\\" + Guid.NewGuid().ToString("N") }, null);
		typeof(CONFIG).GetProperty("pbPrefs", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, preferences, null);
		IntPtr window;
		using (frmViewer viewer = new frmViewer())
		{
			typeof(CApp).GetField("_frmMain", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, viewer);
			IContainer container = (IContainer)typeof(frmViewer).GetField("components", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(viewer);
			object receiver = Activator.CreateInstance(core.GetType("Clearinet.HiddenExecActionTargetWindow"),
				BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { container }, null);
			Check(((IComponent)receiver).Site.Container == container, "Receiver is not owned by the viewer component container.");
			window = FindReceiver();
			Check(window != IntPtr.Zero && !IsWindowVisible(window), "Hidden receiver was not created.");
			Check(SendNull(window, 0x004A, IntPtr.Zero, IntPtr.Zero) == IntPtr.Zero, "Null structure was accepted.");
			Check(Send(window, null, 61178, 1) == 0, "Null payload was accepted.");
			Check(Send(window, new byte[0], 61178, null) == 0, "Zero length was accepted.");
			Check(Send(window, new byte[1], 61178, uint.MaxValue) == 0, "Oversized length was accepted.");
			Check(Send(window, new byte[] { 0xC3, 0x28 }, 61178, null) == 0, "Invalid UTF-8 was accepted.");
			Check(Send(window, new byte[] { 0 }, 61178, null) == 0, "Empty command was accepted.");
			Check(Send(window, Encoding.UTF8.GetBytes(" \t\r\n"), 61178, null) == 0, "Whitespace command was accepted.");
			Check(Send(window, Encoding.UTF8.GetBytes("unknown-command"), 61178, null) == 0, "Unhandled command returned success.");

			CApp.Prefs.SetStringPref(PreferenceName, "present");
			byte[] command = Encoding.UTF8.GetBytes("prefs remove " + PreferenceName);
			Check(Send(window, command, 61179, null) == 0, "Wrong dwData was accepted.");
			Check(CApp.Prefs.GetStringPref(PreferenceName, null) == "present", "Wrong dwData executed a command.");
			Check(Send(window, Encoding.UTF8.GetBytes("prefs remove " + PreferenceName + "\0ignored"), 61178, null) == 0,
				"Embedded NUL was accepted.");
			Check(CApp.Prefs.GetStringPref(PreferenceName, null) == "present", "Embedded NUL executed a command.");
			Check(Send(window, command, 61178, null) == 1, "Unterminated UTF-8 command was not handled.");
			Check(CApp.Prefs.GetStringPref(PreferenceName, null) == null, "Command did not reach RunExecAction.");

			Check(FindWindowEx(IntPtr.Zero, IntPtr.Zero, null, "Clearinet.ExecActionTarget") == window,
				"Another Clearinet instance would receive the bundled script commands; close it before running this test.");
			string bundledArguments = "-NoProfile -NonInteractive -File \"" + senderScript + "\"";
			foreach (string arguments in new[] {
				"-NoProfile -NonInteractive -STA -File \"" + script + "\" -BuildDirectory \"" + buildDirectory +
					"\" -WindowHandle " + window.ToInt64(),
				bundledArguments + " -Command \"prefs remove " + PreferenceName + "\"",
				bundledArguments + " prefs remove " + PreferenceName
			})
			{
				CApp.Prefs.SetStringPref(PreferenceName, "present");
				ProcessStartInfo start = new ProcessStartInfo("powershell.exe", arguments);
				start.UseShellExecute = false;
				start.CreateNoWindow = true;
				using (Process sender = Process.Start(start))
				{
					Stopwatch timeout = Stopwatch.StartNew();
					while (!sender.HasExited && timeout.Elapsed < TimeSpan.FromSeconds(30))
					{
						Application.DoEvents();
						Thread.Sleep(10);
					}
					if (!sender.HasExited) { sender.Kill(); throw new Exception("IPC sender timed out."); }
					Check(sender.ExitCode == 0, "Cross-process sender failed.");
				}
				Check(CApp.Prefs.GetStringPref(PreferenceName, null) == null, "Cross-process Unicode command was not executed.");
			}
			GC.KeepAlive(receiver);
		}
		Check(!IsWindow(window), "Disposing the owner did not destroy the hidden window.");
		Check(FindReceiver() == IntPtr.Zero, "Disposed receiver is still discoverable.");
		Console.WriteLine("Passed {0} IPC assertions.", assertions);
	}
}
'@
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.ReferencedAssemblies.AddRange([string[]]@($core, 'System.dll', 'System.Core.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll'))
Add-Type -TypeDefinition $tests -CompilerParameters $compiler
if ($WindowHandle -ne 0) {
	[HiddenExecActionTargetWindowTests]::RunSender($WindowHandle)
} else {
	$senderScript = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\Content\Bundled\CallExecAction.ps1')).Path
	[HiddenExecActionTargetWindowTests]::Run($PSCommandPath, (Resolve-Path -LiteralPath $BuildDirectory).Path, $senderScript)
}
