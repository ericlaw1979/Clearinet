# Contributing to CLeARINET

Thanks for your interest in CLeARINET, an open source HTTP(S) inspection
proxy built as a spiritual successor to the Fiddler Web Debugger. This
document covers what to know before you open an issue or a pull request.

## Clean-room implementation policy

This is the most important rule in this repository, and it exists to keep
the project on solid legal footing.

Fiddler Classic, Fiddler Everywhere, and FiddlerCore are proprietary
software. CLeARINET is built independently, from:

- publicly available documentation (such as the [Fiddler Classic
  docs](https://github.com/telerik/fiddler-docs)),
- publicly released examples or sample code,
- publicly observed behavior (for example, the structure of `.saz` files
  produced by running the real tool and inspecting the output), and
- our own design decisions.

**Contributors must not decompile, disassemble, or reverse-engineer any
Fiddler binary, and must not copy code, comments, or other material from
Fiddler's source (if you have seen it under an NDA or similar) into this
project.** If you have prior exposure to Fiddler's proprietary source
code, please disclose that in your pull request so a maintainer can review
it with that in mind.

Compatibility with Fiddler's documented formats, command syntax, and
scripting hooks is a project goal. Reimplementing *documented, observed
behavior* in your own code is expected and encouraged; copying
implementation code is not.

## No-relicensing commitment

CLeARINET is licensed under the MIT License (see [LICENSE](LICENSE)).

No maintainer, sponsor, or acquirer gets to pull the project behind a paywall
or revoke rights already granted to users. This commitment is a key reason
why this project exists at all.

## Getting started

1. Fork the repository and clone your fork.
2. Install the [.NET SDK](https://dotnet.microsoft.com/download) matching
   the version in `global.json` (once one exists) or the current .NET LTS.
3. `dotnet build` from the repository root should succeed with no
   warnings-as-errors on Windows and macOS; CI checks both.
4. Run the standalone regression scripts described in [Running the tests](TESTING.md).
   These scripts are not discovered by `dotnet test`.

### Building Transcoders on Windows

`Transcoders\Transcoders.csproj` is SDK-style and still targets .NET Framework
4.8.1. Install Visual Studio with the .NET desktop development workload and the
.NET Framework 4.8.1 targeting pack. From a Visual Studio developer terminal at
the repository root, restore the legacy Clearinet dependencies and build:

```powershell
nuget restore .\Clearinet\packages.config -PackagesDirectory .\Clearinet\packages
msbuild .\Transcoders\Transcoders.csproj -restore -p:Configuration=Release '-bl:{}'
```

The output remains `Transcoders\bin\Release\CAT-Default.dll` (or `bin\Debug`
for Debug builds), without a target-framework subfolder. The post-build event
copies the DLL to `Documents\Clearinet\ImportExport`. Assembly metadata and
the minimum compatible Clearinet version remain in
`Transcoders\Properties\AssemblyInfo.cs`.

### Building Extensions on Windows

`Extensions\Extensions.csproj` is SDK-style and targets .NET Framework 4.8.1
with Windows Forms. Use the same Visual Studio workload and targeting pack
described above. From a Visual Studio developer terminal at the repository
root, restore the legacy Clearinet dependencies and build:

```powershell
nuget restore .\Clearinet\packages.config -PackagesDirectory .\Clearinet\packages
msbuild .\Extensions\Extensions.csproj -restore -p:Configuration=Release '-bl:{}'
```

Extensions uses `PackageReference`; MSBuild restores its packages automatically
with `-restore`. Only Clearinet still needs the separate `packages.config`
restore. To build all three projects, use
`msbuild .\Clearinet\Clearinet.slnx -restore -p:Configuration=Release '-bl:{}'`
after restoring Clearinet's packages.

The output remains `Extensions\bin\Release\CAE-Default.dll` (or `bin\Debug`
for Debug builds), without a target-framework subfolder. The output includes
the inspector dependencies and WebView2 loaders under
`runtimes\win-x86\native`, `runtimes\win-x64\native`, and
`runtimes\win-arm64\native`. The post-build event copies the extension,
WebView2 Core and WinForms assemblies, and the `runtimes` directory to
`Documents\Clearinet\Extensions`. Assembly metadata and the minimum compatible
Clearinet version remain in `Extensions\Properties\AssemblyInfo.cs`.

## Pull requests

- Keep pull requests focused on one change. Large, unrelated changes are
  harder to review and more likely to get stuck.
- Add or update tests for anything you change in the proxy core or the
  compatibility adapter.
- Reference the relevant open question or gap from the project plan when
  your change addresses one.
- Be prepared for feedback. Early on especially, the architecture is still
  being worked out, and review comments are how we keep it coherent.

## Reporting bugs and security issues

Open a regular issue for a bug. For a security-sensitive issue (for
example, anything involving the interception root certificate or private
key handling), please do not open a public issue — a `SECURITY.md` with
a private reporting channel will be added before 1.0; until then, contact
a maintainer directly.

## Code of conduct

This project follows the [Code of Conduct](CODE_OF_CONDUCT.md). By
participating, you're expected to uphold it.
