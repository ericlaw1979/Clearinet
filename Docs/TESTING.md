# Running the regression tests

The regression tests live in `Test`. They are standalone PowerShell scripts,
not Test Explorer projects: run the scripts rather than `dotnet test`. The
`.Tests.cs` files are compiled by their matching `.Tests.ps1` wrappers; do not
run them separately.

All commands below assume the repository root is the current directory.

## Prerequisites and build

- Windows with Visual Studio's **.NET desktop development** workload and the
  **.NET Framework 4.8.1 targeting pack**.
- Windows PowerShell 5.1 (`powershell.exe`) for all assembly-based tests. These
  load .NET Framework assemblies and must not run under PowerShell 7.
- PowerShell 7 (`pwsh.exe`) for `AuthTokenDecoder.Tests.ps1` only. It compiles
  the decoder source directly and does not require a project build.
- An unlocked, interactive Windows desktop for UI tests. Run them sequentially,
  avoid using the mouse/clipboard while they run, and close other Clearinet
  instances, especially before the IPC test. The tests show windows; the header
  menu test moves and restores the cursor. Several tests temporarily replace
  and restore clipboard contents.
- IPv4 and IPv6 loopback support for `MapSocketToProcess.Tests.ps1`.
- The Microsoft Edge WebView2 Evergreen Runtime for the WebView `-Browser`
  mode. Its default document checks and simulated missing-dependency modes do
  not require a working browser runtime.

From a Visual Studio developer PowerShell terminal, build Release outputs:

```powershell
# Restore legacy dependencies on a fresh checkout, or if packages are missing.
nuget restore .\Clearinet\packages.config -PackagesDirectory .\Clearinet\packages
msbuild .\Clearinet\Clearinet.slnx -restore -p:Configuration=Release '-bl:{}'
if ($LASTEXITCODE -ne 0) { throw 'Build failed; do not test stale binaries.' }
```

Building the solution builds Clearinet, Extensions, and Transcoders. The
extension/transcoder post-build steps also copy outputs into
`Documents\Clearinet\Extensions` and `Documents\Clearinet\ImportExport`.
See [CONTRIBUTING.md](CONTRIBUTING.md) for individual-project build details.

## Run one test

Start a fresh process for each invocation. Scripts use `Add-Type`, reflection,
and process-wide application state; reusing one process can retain old
assemblies or conflicting test types after a rebuild.

Include `-AutomatedTest` in the hosting process's arguments as well as when
launching Clearinet directly. Clearinet reads `Environment.GetCommandLineArgs()`
even when loaded as an assembly. With that marker present, unknown host switches
are traced instead of showing invalid-argument dialogs; normal launches retain
those dialogs. After rebuilding Clearinet with this support, the header test
can be run directly with the marker as its first script argument:

```powershell
powershell.exe -NoProfile -NonInteractive -STA -File .\Test\ExchangeHeaderMenu.Tests.ps1 -AutomatedTest -BuildDirectory .\Clearinet\bin\Release
if ($LASTEXITCODE -ne 0) { throw 'Header menu regression failed.' }
```

`-STA` provides single-threaded apartment support for Windows Forms and the
clipboard. `-NonInteractive` disables PowerShell console prompts, not application
windows. PowerShell's own options precede `-File`; `-AutomatedTest` is the first
script argument, so it reaches the hosting process without being interpreted as
an unsupported PowerShell option. This requires a rebuilt Clearinet that
recognizes the marker.

If local execution policy blocks scripts, follow your machine's policy. Where
permitted, a process-local setting can be inherited by the test processes and
by the child processes used in the IPC/WebView tests:

```powershell
# Set only in this terminal; do not change machine-wide execution policy.
$env:PSExecutionPolicyPreference = 'Bypass'
# Run the tests, then restore the previous value (or remove this variable if unset).
```

The process-local environment setting also reaches child test processes.
Organization Group Policy can still prevent script execution.

Most tests accept `-BuildDirectory`. Supply it explicitly to select the output
you just built: `MapSocketToProcess` and `DataViewContextMenu` default to Debug,
while the other assembly-based scripts currently default to Release. For a
Debug build, replace `Release` with `Debug` consistently in the build and test
commands. The directory must contain the matching `Clearinet.exe`, any required
extension/transcoder DLL, and its dependencies; the test does not build them.

## Test inventory

Every row below names a script under `Test` (append `.Tests.ps1`). Use its
listed build directory with the single-test command above.

| Script | Release build directory | What it exercises / special requirements |
|---|---|---|
| `BatchExchangeInsertion` | `Clearinet\bin\Release` | Bulk insertion, ascending/descending sorting, comparison-count threshold, duplicate insertion, partial enumeration failure, file insertion. |
| `CurlExport` | `Clearinet\bin\Release` | CMD/PowerShell quoting, body boundaries, chunk decoding, clipboard export. Invokes CMD for quoting checks but does not execute cURL or send HTTP requests. |
| `ExchangeHeaderMenu` | `Clearinet\bin\Release` | Native header right-clicks, reordered/scrolled columns, full bold title, search prompt and cancellation, duplicate marking, hide/show widths, customization notification, unchanged row menu, disposal. |
| `HiddenExecActionTargetWindow` | `Clearinet\bin\Release` | Hidden IPC receiver, malformed messages, Unicode commands, cross-process senders and bundled `CallExecAction.ps1`. Close other Clearinet instances. `-WindowHandle` is an internal child-runner parameter; omit it for the full suite. |
| `MapSocketToProcess` | `Clearinet\bin\Release` | Socket ownership, IPv4/IPv6 family isolation, cached hits, misses, expiration, ambiguous owners. Opens local TCP listeners; IPv6 is required, not skipped. |
| `MinimizeToTray` | `Clearinet\bin\Release` | Tray minimize/restore, normal/maximized state, clicks, tooltip counts and capture state, disposal. Uses the desktop notification area. |
| `QuickExecHistory` | `Clearinet\bin\Release` | History/autocomplete and navigation, file paste/drop/insertion, persistence, viewer mode, logged I/O failures. Uses clipboard and temporary history files. |
| `ScriptColumnReload` | `Clearinet\bin\Release` | JScript.NET-bound columns across reloads, unique names, values, removal, sort indices, unrelated column preservation. Uses a temporary script. |
| `DataInspector` | `Extensions\bin\Release` | JSON/XML/BSON trees, malformed data, search/expand/collapse, query strings, form bodies, compression/chunk decoding and state clearing. |
| `DataViewContextMenu` | `Extensions\bin\Release` | Copy, Copy Subtree, Ctrl+C, right-click targeting, disabled actions, Textransformer handoff and menu disposal. Uses clipboard and shows tool windows. |
| `ImageInspector` | `Extensions\bin\Release` | Image metadata and exact accounting/preview limits, PNG/JPEG/GIF/WebP/TIFF, EXIF/XMP, malformed inputs, decoding and inspector integration. This is not a dedicated fullscreen-viewer test. |
| `WebViewPreview` | `Extensions\bin\Release` | Passive document generation and inspector state by default; browser lifecycle/restrictions and unavailable fallbacks in the modes below. |
| `HarImport` | `Transcoders\bin\Release` | HAR/JSON parsing, conversion and large-import progress callbacks. Wrapper compiles `HarImport.Tests.cs`. |
| `NetLogImport` | `Transcoders\bin\Release` | Generated NetLog JSON/JSONL, gzip and ZIP fixtures, errors and import behavior. Wrapper compiles `NetLogImport.Tests.cs`; optional `-SamplePath` is described below. |
| `PcapImport` | `Transcoders\bin\Release` | PCAP reader/decoder/importer plus truncation and mutation checks. Compiles reader/decoder source and `PcapImport.Tests.cs`; creates and removes a generated capture in `Test`. |
| `AuthTokenDecoder` | None | NTLM/SPNEGO/Kerberos output, malformed tokens, truncations and deterministic mutated inputs. Requires PowerShell 7; run with the command below. |

### Authentication decoder (PowerShell 7)

```powershell
pwsh.exe -NoProfile -NonInteractive -File .\Test\AuthTokenDecoder.Tests.ps1
if ($LASTEXITCODE -ne 0) { throw 'Authentication decoder regression failed.' }
```

### WebView modes

Run each mode in its own Windows PowerShell process. Modes are mutually
exclusive; the first command runs the default document suite.

```powershell
powershell.exe -NoProfile -NonInteractive -STA -File .\Test\WebViewPreview.Tests.ps1 -AutomatedTest -BuildDirectory .\Extensions\bin\Release
powershell.exe -NoProfile -NonInteractive -STA -File .\Test\WebViewPreview.Tests.ps1 -AutomatedTest -BuildDirectory .\Extensions\bin\Release -Browser
powershell.exe -NoProfile -NonInteractive -STA -File .\Test\WebViewPreview.Tests.ps1 -AutomatedTest -BuildDirectory .\Extensions\bin\Release -MissingRuntime
powershell.exe -NoProfile -NonInteractive -STA -File .\Test\WebViewPreview.Tests.ps1 -AutomatedTest -BuildDirectory .\Extensions\bin\Release -MissingSdk
```

Check `$LASTEXITCODE` after each command, or use the fail-fast run-all example
below. `-Browser` needs the built WebView2 managed assemblies, architecture-
matching native loader and Evergreen Runtime. It opens a loopback listener to
verify that preview content cannot reach it. `-MissingRuntime` temporarily sets
`WEBVIEW2_BROWSER_EXECUTABLE_FOLDER` to a nonexistent location and restores it.
`-MissingSdk` uses a temporary copy without the WebView2 managed assemblies and
a child process; it does not delete SDK files from the build. `-CoreDirectory`
can separately identify the directory containing `Clearinet.exe`; normally it
should be omitted because it defaults to `-BuildDirectory`.

### Optional real NetLog sample

The normal NetLog suite generates its own fixtures. The optional sample path
expects an uncompressed JSONL capture with events, `polledData`, and at least
one traffic Exchange (not a conventional JSON capture, gzip file, or ZIP):

```powershell
powershell.exe -NoProfile -NonInteractive -STA -File .\Test\NetLogImport.Tests.ps1 -AutomatedTest -BuildDirectory .\Transcoders\bin\Release -SamplePath C:\captures\netlog.jsonl
```

Use a capture you are authorized to inspect; real captures can contain secrets.

## Run every script and WebView mode

After building Release and preparing the desktop/runtime prerequisites, paste
this into a PowerShell terminal at the repository root. Each test gets a fresh
process, explicit output directory, and an exit-code check. It stops at the
first failure; it is not a parallel runner.

```powershell
$groups = [ordered]@{
    Clearinet = @(
        'BatchExchangeInsertion', 'CurlExport', 'ExchangeHeaderMenu',
        'HiddenExecActionTargetWindow', 'MapSocketToProcess',
        'MinimizeToTray', 'QuickExecHistory', 'ScriptColumnReload'
    )
    Extensions = @(
        'DataInspector', 'DataViewContextMenu', 'ImageInspector', 'WebViewPreview'
    )
    Transcoders = @('HarImport', 'NetLogImport', 'PcapImport')
}
foreach ($project in $groups.Keys) {
    foreach ($name in $groups[$project]) {
        Write-Host "Running $name"
        & powershell.exe -NoProfile -NonInteractive -STA -File ".\Test\$name.Tests.ps1" -AutomatedTest -BuildDirectory ".\$project\bin\Release"
        if ($LASTEXITCODE -ne 0) { throw "$name failed (exit $LASTEXITCODE)." }
    }
}
foreach ($mode in @('Browser', 'MissingRuntime', 'MissingSdk')) {
    Write-Host "Running WebViewPreview -$mode"
    & powershell.exe -NoProfile -NonInteractive -STA -File .\Test\WebViewPreview.Tests.ps1 -AutomatedTest -BuildDirectory .\Extensions\bin\Release "-$mode"
    if ($LASTEXITCODE -ne 0) { throw "WebViewPreview -$mode failed (exit $LASTEXITCODE)." }
}
& pwsh.exe -NoProfile -NonInteractive -File .\Test\AuthTokenDecoder.Tests.ps1
if ($LASTEXITCODE -ne 0) { throw "AuthTokenDecoder failed (exit $LASTEXITCODE)." }
Write-Host 'All regression scripts and WebView modes passed.'
```

## Results and troubleshooting

- A successful script prints its assertion/pass summary and exits with code 0.
  Exact assertion counts may change as cases are added. Exceptions, compilation
  errors or a nonzero process exit mean failure; retain the full error output.
- `Resolve-Path`/assembly-not-found errors usually mean the required project or
  configuration has not been built. Rebuild and verify `-BuildDirectory` before
  diagnosing a product regression.
- Reflection member-not-found errors often indicate stale binaries. Rebuild,
  then rerun in a fresh process; loaded .NET Framework assemblies cannot simply
  be replaced in the same process.
- Unexpected Options dialogs can come from unrecognized host switches such as
  `-File` or `-ExecutionPolicy`. Ensure `-AutomatedTest` is present in the actual
  hosting process arguments, including any child process that loads Clearinet,
  and rebuild the copy of `Clearinet.exe` used by that test.
- Most generated fixtures are cleaned up in `finally` blocks. Some suites use
  GUID-named preference paths under `HKCU\SOFTWARE\Clearinet\Tests`; do not
  assume every suite removes these keys. Do not run these tests against a
  desktop or user profile containing irreplaceable state.
- The scripts generally load `Clearinet.exe` as an assembly and construct the
  necessary controls, rather than launch the application executable. For any
  additional manual smoke test that launches the app, always put
  **`-AutomatedTest` first**, for example:
  `& .\Clearinet\bin\Release\Clearinet.exe -AutomatedTest`.
