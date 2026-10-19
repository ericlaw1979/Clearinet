#Requires -Version 5.1
param(
    [string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Transcoders\bin\Release'),
    [string]$SamplePath = ''
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw 'Run with powershell.exe -NoProfile -NonInteractive -File .\Test\NetLogImport.Tests.ps1 (after building Transcoders).'
}
$directory = (Resolve-Path -LiteralPath $BuildDirectory).Path
$core = Join-Path $directory 'Clearinet.exe'
$transcoders = Join-Path $directory 'CAT-Default.dll'
$null = [Reflection.Assembly]::LoadFrom($core)
$null = [Reflection.Assembly]::LoadFrom($transcoders)
$fixtureDirectory = Join-Path ([IO.Path]::GetTempPath()) ('clearinet-netlog-tests-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $fixtureDirectory
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.ReferencedAssemblies.AddRange([string[]]@($core, $transcoders, 'System.dll', 'System.Core.dll', 'System.IO.Compression.dll'))
try {
    Add-Type -Path (Join-Path $PSScriptRoot 'NetLogImport.Tests.cs') -CompilerParameters $compiler
    [TestExtensions.NetLogImportTests]::Run($fixtureDirectory, $SamplePath)
}
finally {
    foreach ($name in @('capture.JSONL', 'capture.jsonl.gz', 'capture.zip')) {
        $path = Join-Path $fixtureDirectory $name
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    }
    Remove-Item -LiteralPath $fixtureDirectory
}
