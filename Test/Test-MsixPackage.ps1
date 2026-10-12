#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string] $PackagePath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Drawing
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath).Path)
$passed = 0

function Assert-Package {
    param([string] $Name, [bool] $Condition, [string] $Failure)
    if (-not $Condition) { throw "${Name}: $Failure" }
    $script:passed++
    Write-Output "PASS $Name"
}

function Read-PackageText {
    param([string] $Name)
    $entry = $archive.GetEntry($Name)
    if ($null -eq $entry) { throw "Missing package entry: $Name" }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
}

try {
    $names = @($archive.Entries.FullName)
    $required = @(
        'Clearinet.exe', 'Clearinet.exe.config', 'Extensions/CAE-Default.dll',
        'ImportExport/CAT-Default.dll', 'Svg.dll', 'ExCSS.dll',
        'Ionic.Zip.Reduced.dll', 'BrotliSharpLib.dll', 'ZstdSharp.dll',
        'System.Buffers.dll', 'System.Memory.dll', 'System.Numerics.Vectors.dll',
        'System.Runtime.CompilerServices.Unsafe.dll', 'System.Threading.Tasks.Extensions.dll',
        'System.Resources.Extensions.dll', 'Credits.txt', 'LICENSE',
        'Microsoft.Web.WebView2.Core.dll',
        'Microsoft.Web.WebView2.WinForms.dll', 'runtimes/win-x64/native/WebView2Loader.dll',
        'CBOR.dll', 'Numbers.dll', 'URIUtility.dll',
        'Tools/dwebp.exe', 'Tools/CallExecAction.ps1',
        'AppxManifest.xml', 'AppxBlockMap.xml', 'resources.pri'
    )
    $missing = @($required | Where-Object { $_ -notin $names })
    Assert-Package 'RequiredPayload' ($missing.Count -eq 0) "Missing: $($missing -join ', ')"
    Assert-Package 'UnsignedArtifact' ('AppxSignature.p7x' -notin $names) 'A package signature is present.'
    $forbidden = @($names | Where-Object { $_ -match '\.(js|pfx|pdb)$' })
    Assert-Package 'NoRulesScriptsOrSigningMaterial' ($forbidden.Count -eq 0) "Unexpected files: $($forbidden -join ', ')"

    [xml] $manifest = Read-PackageText 'AppxManifest.xml'
    Assert-Package 'PackageIdentity' (
        $manifest.Package.Identity.Name -eq 'EricLaw1979.Clearinet' -and
        $manifest.Package.Identity.Publisher -eq 'CN=Clearinet Contributors'
    ) 'The package identity or publisher differs from the agreed values.'
    Assert-Package 'X64WindowsMinimum' (
        $manifest.Package.Identity.ProcessorArchitecture -eq 'x64' -and
        $manifest.Package.Dependencies.TargetDeviceFamily.MinVersion -eq '10.0.19044.0'
    ) 'Incorrect package architecture or minimum Windows version.'
    $packageDependencies = @($manifest.SelectNodes("//*[local-name()='PackageDependency']"))
    Assert-Package 'NoWindowsAppSdkRuntime' ($packageDependencies.Count -eq 0) 'Unexpected MSIX framework dependency.'
    $application = $manifest.Package.Applications.Application
    Assert-Package 'FullTrustEntryPoint' (
        $application.Executable -eq 'Clearinet.exe' -and
        $application.EntryPoint -eq 'Windows.FullTrustApplication' -and
        $null -ne $manifest.SelectSingleNode("//*[local-name()='Capability' and @Name='runFullTrust']")
    ) 'The app is not declared as the expected full-trust desktop executable.'
    $saz = $manifest.SelectSingleNode("//*[local-name()='FileTypeAssociation' and @Name='clearinet.saz']")
    $har = $manifest.SelectSingleNode("//*[local-name()='FileTypeAssociation' and @Name='clearinet.har']")
    Assert-Package 'ArchiveAssociations' (
        $null -ne $saz -and $null -ne $har -and
        $saz.Parameters -eq '-noattach "%1"' -and $har.Parameters -eq '"%1"' -and
        $saz.SelectSingleNode(".//*[local-name()='FileType']").InnerText -eq '.saz' -and
        $har.SelectSingleNode(".//*[local-name()='FileType']").InnerText -eq '.har' -and
        $saz.SelectSingleNode(".//*[local-name()='Verb' and @Id='viewer']").Parameters -eq '-viewer "%1"'
    ) 'Archive associations or quoted launch arguments are incorrect.'

    [xml] $config = Read-PackageText 'Clearinet.exe.config'
    Assert-Package 'FrameworkConfiguration' (
        $config.configuration.startup.supportedRuntime.sku -eq '.NETFramework,Version=v4.8.1'
    ) 'The generated executable configuration lost its .NET Framework prerequisite.'

    foreach ($asset in @(
        @{ Name = 'Assets/StoreLogo.png'; Size = 50 },
        @{ Name = 'Assets/Square44x44Logo.png'; Size = 44 },
        @{ Name = 'Assets/Square150x150Logo.png'; Size = 150 },
        @{ Name = 'Assets/SazLogo.png'; Size = 44 }
    )) {
        $entry = $archive.GetEntry($asset.Name)
        if ($null -eq $entry) { throw "Missing visual asset: $($asset.Name)" }
        $stream = $entry.Open()
        $image = [Drawing.Image]::FromStream($stream)
        try {
            Assert-Package "VisualAsset-$($asset.Size)-$([IO.Path]::GetFileNameWithoutExtension($asset.Name))" (
                $image.Width -eq $asset.Size -and $image.Height -eq $asset.Size
            ) 'Incorrect visual asset dimensions.'
        } finally {
            $image.Dispose()
            $stream.Dispose()
        }
    }
    Write-Output "Passed $passed MSIX artifact checks."
} finally {
    $archive.Dispose()
}
