#Requires -Version 5.1
param(
	[string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Transcoders\bin\Release')
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') {
	throw 'Run with powershell.exe -NoProfile -NonInteractive -File .\Test\HarImport.Tests.ps1 (after building Transcoders).'
}
$directory = (Resolve-Path -LiteralPath $BuildDirectory).Path
$core = Join-Path $directory 'Clearinet.exe'
$transcoders = Join-Path $directory 'CAT-Default.dll'
$null = [Reflection.Assembly]::LoadFrom($core)
$null = [Reflection.Assembly]::LoadFrom($transcoders)
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.ReferencedAssemblies.AddRange([string[]]@($core, $transcoders, 'System.dll', 'System.Core.dll'))
Add-Type -Path (Join-Path $PSScriptRoot 'HarImport.Tests.cs') -CompilerParameters $compiler
[TestExtensions.HarImportTests]::Run()
