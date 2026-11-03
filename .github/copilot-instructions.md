# Copilot Instructions

## Project Guidelines

* User expects JScript.NET-compatible examples for Clearinet scripts; avoid C#-style delegate constructor syntax that does not compile in JScript.NET.
* When providing JScript.NET interop examples, avoid anonymous function-to-delegate assignment because it doesn't work.
* When launching or initializing Clearinet.exe for a test, always include the command line argument `-AutomatedTest` as the first argument to suppress interactive warnings.

## WinForms DPI guidance (.NET Framework 4.8.1)

### Start with Framework layout and the actual lifecycle

* Preserve the application's PerMonitorV2 configuration and normal Framework autoscaling. Use docking, anchoring, AutoSize and layout containers before adding manual scaling. Do not disable autoscaling, reset all minimum sizes, or introduce a global DPI-aware base form to mask a local lifecycle defect.
* Use the target control's current `DeviceDpi`, not the primary display's DPI. Before first handle creation, do not assume it represents the eventual monitor. Trace construction, parenting, handle creation, Load, visibility and parent DPI notifications when diagnosing startup or lazy controls.
* Avoid forcing child handles during construction where possible. Accessing `Handle`, including through native helpers such as `Win32UI.SetCueText`, can create a handle before parenting establishes the target DPI. If an early handle is necessary, validate correction after parenting and monitor changes.
* Apply explicitly assigned configured fonts after parenting lazy controls. `InspectorBase.EnsureTabReady` creates the view and sends the target-DPI font notification before marking it ready; `UpdateFontSize` notifies only initialized views. Preserve this contract rather than creating hidden views just to update fonts.
* Prefer inherited fonts unless a control needs its own face/style. Explicit editor fonts and native font state need separate validation through hidden/visible activation and DPI moves; do not assume a parent font change updates everything.
* Restore saved physical window bounds early enough to establish the target DPI before startup font/layout restoration and first visible rendering. Preserve stored pixel bounds rather than scaling them again. `frmViewer` restores bounds once in `OnHandleCreated` and restores them again after startup font layout; do not copy that form-specific sequence into new forms without tracing their lifecycle.

### Keep scaling units distinct

* Use `Control.LogicalToDeviceUnits(int)` or `Control.LogicalToDeviceUnits(Size)` for canonical 96-DPI logical integer dimensions, not for dimensions that Framework layout already scaled. Framework 4.8.1 has no float overload; never round point-font sizes to integers merely to use this API.
* `CONFIG.flFontSize` is a logical floating-point point size. Use the public pure `DpiFontUnits.LogicalFontPointsToManagedFontPoints(points, deviceDpi)` for Clearinet's explicitly assigned DPI-adjusted managed-font/inspector convention, and `ManagedFontPointsToLogicalFontPoints(points, deviceDpi)` only when receiving that convention. Both require positive supplied target DPI and retain fractional points. Do not apply the adjustment a second time to an already-adjusted value or assume every GDI+/native consumer has the same contract. These helpers do not allocate fonts, access controls/handles, or alter layout or ownership.
* RichEdit character sizes are logical points expressed as twips (20 per point), not DPI-adjusted managed Font sizes. Reuse `Win32UI.SetRichTextFontSize` with logical points for uniform-size Raw/log text. Its size-only `SCF_ALL` operation normalizes all text sizes; it is not a general rich-document formatting API and deliberately does nothing before handle creation.
* Saved desktop bounds are physical pixels. A DPI transition of an existing physical size uses `newDpi / oldDpi`, while a runtime font-size transition uses its font ratio. Neither is a conversion of a canonical logical size. Keep rounding and minimum-size policy explicit; do not substitute `LogicalToDeviceUnits(currentMinimum)`.
* Use built-in DPI-specific metrics such as `SystemInformation.GetVerticalScrollBarWidthForDpi` when available. The Visual Studio SDK's `DpiAwareness` offers float conversions but is a separate dependency, not a missing Framework API; do not add it merely to replace simple arithmetic.

### Native state, dirty state and font ownership

* Reuse `BetterTabControl` and `BetterListView` where their native inherited-font refresh is needed. Their after-parent callbacks resend the final font through the normal font-change path; a numeric converter alone cannot synchronize a native tab/list/header font.
* Call normal base lifecycle handling and keep any corrective work narrowly scoped. The viewer/Raw `AutoScaleDimensions = CurrentAutoScaleDimensions` corrections prevent an evidenced second Font-mode scaling pass; they are not a universal template for every control. Explain the ordering and Framework limitation before adding similar overrides.
* Presentation-only Raw font updates must preserve both clean and already-dirty `Modified` states. `RawRichTextBox.Font` intercepts the automatic Framework font writer because it runs before the before-parent DPI event; capturing the flag in that event is too late. The native helper protects its own separate formatting write. Do not collapse these guards or move the Raw policy into all `RichTextBoxV5` users without equivalent evidence.
* Preserving the final dirty flag does not guarantee event silence, unchanged undo history or stable first painting. Verify those separately when editable documents or event subscribers are involved.
* Track fonts created by application code as owned resources. Never dispose inherited/shared fonts or assume assigning an equal Font replaces the active instance. Compare the active control font with a new candidate; on equality, dispose only the unused candidate. On replacement, assign successfully before disposing a superseded owned font, and dispose remaining owned fonts after their consumers. A Framework-scaled active font may differ from the stored owned reference.
* Avoid unconditional replacement font allocations on repeated visibility/font notifications. Any shared font helper must define ownership and equal-assignment behavior; do not merge resource ownership with Raw dirty-state or RichEdit formatting policy.

### Responsive Composer layout

* Keep Composer request controls in the same `TableLayoutPanel`: content-sized method/version/Send plus remaining-width URL on one row; below the measured breakpoint, span the URL across all four columns on a second row. The minimum single-row URL width is 240 canonical logical pixels, converted with `LogicalToDeviceUnits`; measured text, current margins and preferred control widths are already device units. Include target-DPI dropdown metrics, and compress only dropdown columns proportionally at very narrow widths while retaining Send's preferred width.
* Keep resize updates synchronous and guarded against nested layout; avoid geometry assignments when unchanged. Do not reparent controls or rewrite request data during wrapping. Verify exact breakpoint +/-1, wide/narrow/compressed widths, repeated wraps, first lazy activation, unchanged text/selection/method/version, no editor text events and non-overlapping usable editors at actual DPI/font configurations.

### Evidence, documentation and future abstractions

* Keep shared conversions pure and unit-specific, with names that distinguish logical font points from DPI-adjusted managed font points. Do not hide font assignment, handle creation, layout, dirty-state changes or minimum-size policy inside a generic `Scale` helper. Extract repeated responsibilities only when their contracts genuinely match.
* Add a short local rationale for unintuitive corrections: input/output units, lifecycle ordering, the Framework/native limitation, and why a simpler alternative fails. Document public helper contracts and side effects. Do not rely solely on a temporary audit file or ignored diagnostic logs to explain shipping code.
* Use `Test/DpiScaling.Tests.ps1` and `Test/DpiScalingHarness.cs` for the standalone DPI evidence; absence from Test Explorer does not mean no DPI tests exist. Change and run focused tests for changed behavior, then relevant matrices when production behavior changes. Do not rerun accepted matrices solely to recover an interrupted session or validate documentation-only edits.
* Assert DPI/font state before reseeding content or explicitly refreshing fonts; setup that repairs the control can hide lifecycle failures. Cover cold startup, lazy activation, hidden/visible moves, repeated round trips, runtime font changes before/after initialization, native fonts, usable non-overlapping layout, and owned-font equality/lifetime.
* For Raw/editor changes, preserve text, selection, read-only state and clean/already-dirty editable documents; include undo/redo, event expectations and handle recreation where affected. Do not weaken assertions to accommodate a DPI defect.
* Report tested physical monitors separately from deduplicated configurations. The current retained coverage includes 96/144 DPI and fonts 10/12, not every fractional scale, font, desktop position or primary-monitor arrangement. Add representative cases when broadening the supported behavior.
* The harness bypasses full application startup/scripting/discovery, and form-level paint counting does not prove stable child first painting. Keep full-startup visual checks and actual mouse-drag resizing as explicit manual gates; distinguish retained passing evidence, new results and unrelated failures.

