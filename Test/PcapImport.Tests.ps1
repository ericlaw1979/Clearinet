#Requires -Version 5.1
param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Transcoders\bin\Release'))
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') {
	throw 'Run with powershell.exe -NoProfile -NonInteractive -File .\Test\PcapImport.Tests.ps1 (after building Transcoders Release).'
}
$directory = (Resolve-Path -LiteralPath $BuildDirectory).Path
$core = Join-Path $directory 'Clearinet.exe'
$transcoders = Join-Path $directory 'CAT-Default.dll'
$null = [Reflection.Assembly]::LoadFrom($core)
$null = [Reflection.Assembly]::LoadFrom($transcoders)
$fixture = Join-Path $PSScriptRoot ('.pcap-tests-' + [Guid]::NewGuid().ToString('N') + '.pcap')
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.ReferencedAssemblies.AddRange([string[]]@($core, $transcoders, 'System.dll', 'System.Core.dll'))
try {
	Add-Type -Path @(
		(Join-Path $PSScriptRoot '..\Transcoders\PcapReader.cs'),
		(Join-Path $PSScriptRoot '..\Transcoders\PcapDecoder.cs'),
		(Join-Path $PSScriptRoot 'PcapImport.Tests.cs')
	) -CompilerParameters $compiler
	[TestExtensions.PcapImportTests]::Run($fixture)
}
finally {
	if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture -Force }
}
