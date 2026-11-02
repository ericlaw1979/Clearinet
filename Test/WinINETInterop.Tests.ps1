#Requires -Version 5.1
param(
	[switch]$AutomatedTest,
	[string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Clearinet\bin\Release')
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') {
	throw 'Run with powershell.exe -NoProfile -NonInteractive -File .\Test\WinINETInterop.Tests.ps1 -AutomatedTest'
}
$core = Join-Path (Resolve-Path -LiteralPath $BuildDirectory).Path 'Clearinet.exe'
$null = [Reflection.Assembly]::LoadFrom($core)
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.ReferencedAssemblies.AddRange([string[]]@('System.dll', 'System.Core.dll', $core))
Add-Type -Path (Join-Path $PSScriptRoot 'WinINETInterop.Tests.cs') -CompilerParameters $compiler
[TestExtensions.WinINETInteropTests]::Run()