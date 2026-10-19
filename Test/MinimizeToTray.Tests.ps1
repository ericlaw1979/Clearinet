#Requires -Version 5.1
param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Clearinet\bin\Release'))
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
	throw 'Run .\Test\MinimizeToTray.Tests.ps1 in a Windows PowerShell STA console started without command-line switches.'
}
$core = Join-Path (Resolve-Path -LiteralPath $BuildDirectory).Path 'Clearinet.exe'
$null = [Reflection.Assembly]::LoadFrom($core)
$tests = @'
using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;
using Clearinet;

public sealed class TrayTestViewer : frmViewer
{
	protected override void OnLoad(EventArgs e) { }
}

public static class MinimizeToTrayTests
{
	private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
	private static int assertions;
	private static void Check(bool condition, string message)
	{
		++assertions;
		if (!condition) throw new Exception(message);
	}
	private static void CheckTooltip(NotifyIcon icon, string capture, int count)
	{
		string expected = "Clearinet\nCapture: " + capture + "\nSessions: " + count.ToString("N0");
		Check(icon.Text == expected, "Unexpected tooltip: " + icon.Text);
	}
	public static void Run()
	{
		object preferences = Activator.CreateInstance(typeof(PreferenceBag), PrivateInstance,
			null, new object[] { "SOFTWARE\\Clearinet\\Tests\\" + Guid.NewGuid().ToString("N") }, null);
		typeof(CONFIG).GetProperty("pbPrefs", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, preferences, null);
		PropertyInfo attached = typeof(CApp).GetProperty("isAttached");
		NotifyIcon icon;
		bool iconDisposed = false;
		using (TrayTestViewer viewer = new TrayTestViewer())
		{
			icon = (NotifyIcon)typeof(frmViewer).GetField("trayIcon", PrivateInstance).GetValue(viewer);
			icon.Disposed += (sender, e) => iconDisposed = true;
			IContainer container = (IContainer)typeof(frmViewer).GetField("components", PrivateInstance).GetValue(viewer);
			Check(icon.Site.Container == container, "Tray icon is not owned by the form.");
			Check(!icon.Visible, "Tray starts active.");
			viewer.Show();
			ToolStripMenuItem minimize = (ToolStripMenuItem)typeof(frmViewer).GetField("miViewMinimize", PrivateInstance).GetValue(viewer);
			MethodInfo click = typeof(NotifyIcon).GetMethod("OnMouseClick", PrivateInstance);
			foreach (FormWindowState state in new[] { FormWindowState.Normal, FormWindowState.Maximized })
			{
				viewer.WindowState = state;
				minimize.PerformClick();
				Check(!viewer.Visible && icon.Visible, "Menu did not minimize to the tray.");
				CheckTooltip(icon, "Detached", 0);
				minimize.PerformClick();
				click.Invoke(icon, new object[] { new MouseEventArgs(MouseButtons.Right, 1, 0, 0, 0) });
				Check(!viewer.Visible && icon.Visible, "Right click unexpectedly restored the window.");
				click.Invoke(icon, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0) });
				Check(viewer.Visible && viewer.WindowState == state, "Window state was not restored.");
				Check(!icon.Visible, "Tray remained active after restoring.");
			}
			viewer.lvExchanges.Items.Add(new ListViewItem("1"));
			attached.SetValue(null, true, null);
			minimize.PerformClick();
			CheckTooltip(icon, "Attached", 1);
			viewer.lvExchanges.Items.Clear();
			attached.SetValue(null, false, null);
			typeof(frmViewer).GetMethod("UpdateTrayTooltip", PrivateInstance).Invoke(viewer, null);
			CheckTooltip(icon, "Detached", 0);
		}
		Check(iconDisposed, "Tray icon was not disposed with the form.");
		Console.WriteLine("Passed {0} tray assertions.", assertions);
	}
}
'@
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.ReferencedAssemblies.AddRange([string[]]@($core, 'System.dll', 'System.Core.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll'))
Add-Type -TypeDefinition $tests -CompilerParameters $compiler
[MinimizeToTrayTests]::Run()
