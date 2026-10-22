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
builds, using isolated NuGet build imports and intermediates.
`NuGet.Config` limits restore to the public NuGet feed. If that feed's v3
endpoint is unavailable in your environment, its official v2 endpoint can be
selected explicitly with MSBuild's `/p:RestoreSources=https://www.nuget.org/api/v2/`.

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

## No installed rules script

Neither the package nor first launch installs, generates, or copies
`CustomRulesALPHA.js`, `SampleRules.js`, `SampleRulesAlpha.js`, or `poc.js`. A missing script or script
directory is a supported state, including normal startup, built-in features,
and shutdown cancellation through app events.

Users can supply a script at `Documents\Clearinet\CustomRules_0_0_2_4.js` or
override `app.paths.rules.script`. The earlier `app.paths.rulesscript` override
is honored if the new preference is not set. An empty override or the
`-noscript` command-line option disables scripting.
User-supplied scripts are not overwritten. Invalid or unreadable scripts
still surface errors. See [preferences](PREFERENCES.md).

## Validation and deployment boundaries

Run focused scripting regressions independently of packaging:

```powershell
dotnet test .\Test\Clearinet.Tests.csproj -c Release '/bl:{}'
```

After packaging, run the same regressions against the x64 executable used
in the MSIX, rather than rebuilding the unpackaged app:

```powershell
$packagedApp = (Resolve-Path .\Clearinet\bin\msix\x64\Release\Clearinet.exe).Path
dotnet test .\Test\Clearinet.Tests.csproj -c Release "/p:ClearinetTestAppPath=$packagedApp" '/bl:{}'
```

The alternate test build uses the packaged executable's runtime DLLs and
configuration, with separate intermediates and outputs. These tests check
scripting absence, lifecycle cancellation, user-supplied scripting, and
invalid/unreadable script errors, but do not register the package or prove
Explorer activation or MSIX registry virtualization.

Check the produced archive without signing, extracting, or installing it:

```powershell
.\Test\Test-MsixPackage.ps1 -PackagePath .\Clearinet\AppPackages\Clearinet_0.0.2.4_x64_Test\Clearinet_0.0.2.4_x64.msix
```

This checks the payload, unsigned state, absence of rules scripts/signing
material, identity, manifest and binary architecture, minimum OS, full-trust entry point,
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
