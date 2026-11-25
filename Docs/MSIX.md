# Unsigned single-project MSIX packaging

Clearinet remains a .NET Framework 4.8.1 WinForms application. The standalone
`Microsoft.Windows.SDK.BuildTools.MSIX` build dependency enables packaging from
the existing app project; no `.wapproj` or Windows App SDK runtime is required.
Ordinary builds stay unpackaged, and the NSIS installer is unchanged.

## Prerequisites

Build on Windows with:

- Visual Studio 2026 or Build Tools, with .NET desktop development and MSIX
  packaging tools, and a compatible .NET SDK (tested with .NET 10).
- The .NET Framework 4.8.1 targeting pack.
- Windows SDK 10.0.26100.0, including MakeAppx and MakePri.

Run the following command from the repository root in a Visual Studio
Developer PowerShell so `MSBuild.exe` is on PATH:

```powershell
MSBuild.exe .\Clearinet\Clearinet.csproj /restore /p:Configuration=Release /p:Platform=x64 /p:EnableMsixTooling=true '/bl:{}'
```

The SDK-style plugin projects are restored automatically before their packaging
builds, using isolated NuGet build imports and intermediates. Restore uses your
configured NuGet sources. If those sources are unavailable, add
`/p:RestoreSources=https://api.nuget.org/v3/index.json` to the MSBuild command
to use only NuGet.org for that invocation. If its v3 endpoint is unavailable,
use `/p:RestoreSources=https://www.nuget.org/api/v2/` instead.

The reported package path is normally
`Clearinet\AppPackages\Clearinet_<version>_x64_Test\Clearinet_<version>_x64.msix`.
Compilation uses separate `bin\msix` and `obj\msix` trees, so it does not
overwrite the normal unpackaged outputs.

## Package contents and identity

The package contains `Clearinet.exe`, its generated binding-redirect
configuration, resolved runtime dependencies, credits/license content, and
visual assets derived from the existing icons. Built-in plugins are placed in
`Extensions\CAE-Default.dll` and `ImportExport\CAT-Default.dll`. Their dependencies
are collected without replacing the app's resolved assemblies with older copies.
Packaging builds do not copy plugins into the developer's Documents folder.
The hex control is compiled into the extension. WebView2 managed dependencies
are included, along with the x64
WebView2 loader in its runtime subdirectory. Web previews still require the
WebView2 Runtime on the target machine; the package does not install that runtime.

- Identity name: `EricLaw1979.Clearinet`.
- Publisher: `CN=Clearinet Contributors`.
- Architecture: x64 only; bundles are not produced.
- Minimum Windows version: `10.0.19044.0` (Windows 10 21H2).
- Runtime prerequisite: .NET Framework 4.8.1 must already be installed.

Update `Package.appxmanifest` and `Properties\AssemblyInfo.cs` together when
changing versions. The build rejects a mismatch between the manifest version
and the app's assembly version.

The manifest declares a full-trust desktop app and `.saz`/`.har` associations.
SAZ activation supports normal open (`-noattach`) and the viewer verb
(`-viewer`), with quoted file arguments. Windows manages association selection;
the package does not force a default or run the NSIS registry writes.

The app project's bundled `Tools\dwebp.exe` and `Tools\CallExecAction.ps1`
are included in the MSIX as well.

## Rules scripting

The MSIX includes the same `SampleRulesAlpha.js` beside `Clearinet.exe` as
the unpackaged build. No user rules file is installed. Both builds load the
sample when no user script exists. By default, Clearinet creates the user
copy only when you choose **Rules > Customize**, before opening it in the
editor. Set `scripting.delay_create` to `false` to create the copy on startup
instead. Existing user scripts are not overwritten.

The default user script is `Documents\Clearinet\CustomRules_0_1_0_0.js`.
Set `app.paths.rules.script` to use a different writable path. `-noscript`
and system policy can disable scripting in both builds.

## Validation and deployment boundaries

Build with Visual Studio MSBuild so WinForms uses classic .NET Framework
resource generation, then run the focused unpackaged scripting tests:

```powershell
MSBuild.exe .\Test\Clearinet.Tests.csproj /restore /p:Configuration=Release '/bl:{}'
dotnet test .\Test\Clearinet.Tests.csproj -c Release --no-build '/bl:{}'
```

After packaging, build and run the same tests against the x64 executable
used in the MSIX, rather than rebuilding the unpackaged app:

```powershell
$packagedApp = (Resolve-Path .\Clearinet\bin\msix\x64\Release\Clearinet.exe).Path
MSBuild.exe .\Test\Clearinet.Tests.csproj /restore /p:Configuration=Release "/p:ClearinetTestAppPath=$packagedApp" '/bl:{}'
dotnet test .\Test\Clearinet.Tests.csproj -c Release --no-build "/p:ClearinetTestAppPath=$packagedApp" '/bl:{}'
```

The alternate test build uses the packaged executable's runtime DLLs and
configuration, with separate intermediates and outputs. These tests check
scripting restrictions, sample fallback, user-script creation, and reload,
but do not register the package or prove
Explorer activation or MSIX registry virtualization.

Check the produced archive without signing, extracting, or installing it:

```powershell
.\Test\Test-MsixPackage.ps1 -PackagePath .\Clearinet\AppPackages\Clearinet_0.1.2.0_x64_Test\Clearinet_0.1.2.0_x64.msix
```

This checks the payload (including the sample and no user rules scripts),
unsigned state, identity, manifest and binary architecture, minimum OS, full-trust entry point,
file associations, generated runtime configuration, and visual asset dimensions.
MakeAppx also validates the package manifest during the build.

MSIX builds explicitly disable signing. Do not add certificate generation,
certificate trust changes, signing secrets, or CI signing steps. An unsigned
artifact is not ready for normal end-user installation.

Building a package does not register it or change Developer Mode. Testing a
loose development layout requires explicit approval on the intended test
machine. Do not use unapproved registration, certificate provisioning, or
automatic uninstall of an existing distribution as part of the build.

Production signing, deployment/servicing validation, Store submission,
App Installer feeds, ARM64/x86 packaging, and CI workflows are out of scope.
