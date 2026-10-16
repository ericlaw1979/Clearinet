#Requires -Version 5.1
<#
.SYNOPSIS
Sends a QuickExec command to a running Clearinet instance.
.DESCRIPTION
Targets the first Clearinet instance found.
Quote the complete command when it contains quoted QuickExec arguments.
.EXAMPLE
.\CallExecAction.ps1 select text/html
.EXAMPLE
.\CallExecAction.ps1 'prefs set example.message "hello world"'
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory = $true, Position = 0, ValueFromRemainingArguments = $true)]
	[ValidateNotNullOrEmpty()]
	[string[]]$Command
)
$ErrorActionPreference = 'Stop'
$commandText = $Command -join ' '
if ([string]::IsNullOrWhiteSpace($commandText) -or $commandText.IndexOf([char]0) -ge 0) {
	throw 'The command must not be empty or contain a NUL character.'
}
if ($env:OS -ne 'Windows_NT') {
	throw 'Clearinet command IPC requires Windows.'
}
if (-not ('Clearinet.CommandLine.ExecActionSender' -as [type])) {
	Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Clearinet.CommandLine
{
	public static class ExecActionSender
	{
		[StructLayout(LayoutKind.Sequential)]
		private struct CopyData
		{
			public UIntPtr dwData;
			public uint cbData;
			public IntPtr lpData;
		}

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr FindWindow(string className, string title);

		[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr sender,
			ref CopyData data, uint flags, uint timeout, out UIntPtr result);

		public static void Send(string command)
		{
			IntPtr window = FindWindow(null, "Clearinet.ExecActionTarget");
			if (window == IntPtr.Zero)
				throw new InvalidOperationException("No Clearinet command window was found. Start Clearinet before sending commands.");

			byte[] bytes = new UTF8Encoding(false, true).GetBytes(command + "\0");
			IntPtr payload = Marshal.AllocHGlobal(bytes.Length);
			try
			{
				Marshal.Copy(bytes, 0, payload, bytes.Length);
				CopyData data = new CopyData {
					dwData = new UIntPtr(61178), cbData = (uint)bytes.Length, lpData = payload
				};
				UIntPtr result;
				// Abort after 5 seconds if the UI is busy; never retry a potentially executed command.
				if (SendMessageTimeout(window, 0x004A, IntPtr.Zero, ref data, 0x0003, 5000, out result) == IntPtr.Zero)
					throw new Win32Exception(Marshal.GetLastWin32Error(),
						"Clearinet command delivery failed or timed out. The command may still execute. If Clearinet is elevated, run this script elevated too.");
				if (result == UIntPtr.Zero)
					throw new InvalidOperationException("Clearinet did not handle the command.");
			}
			finally
			{
				Marshal.FreeHGlobal(payload);
			}
		}
	}
}
'@
}
[Clearinet.CommandLine.ExecActionSender]::Send($commandText)
