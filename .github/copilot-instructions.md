# Copilot Instructions

## Project Guidelines

* User expects JScript.NET-compatible examples for Clearinet scripts; avoid C#-style delegate constructor syntax that does not compile in JScript.NET.
* When providing JScript.NET interop examples, avoid anonymous function-to-delegate assignment because it doesn't work.
* When launching or initializing Clearinet.exe for a test, always include the command line argument `-AutomatedTest` as the first argument to suppress interactive warnings.

## WinForms DPI guidance (.NET Framework 4.8.1)

* Preserve PerMonitorV2 and normal Framework autoscaling. Prefer docking, anchoring, AutoSize and layout containers; keep lifecycle corrections local and explain non-obvious Framework workarounds in the affected code.
* Use the target control's established `DeviceDpi`, not the primary display's DPI. Avoid forcing child handles before parenting; apply explicit fonts after parenting lazy views without initializing hidden inspectors just to update fonts.
* Keep logical dimensions, physical window bounds and font points distinct. Use `LogicalToDeviceUnits` only for canonical 96-DPI integer dimensions, not already-scaled sizes or floating-point font sizes. Restore saved physical bounds without scaling them again.
* `CONFIG.flFontSize` is logical font points. Use `DpiFontUnits` for Clearinet's DPI-adjusted managed-font/inspector convention, preserving fractional points and avoiding double adjustment. Keep conversions pure and unit-specific.
* Prefer inherited fonts, but validate explicit editor fonts and native control fonts separately. Reuse the native refresh behavior in `BetterTabControl` and `BetterListView`; use `Win32UI.SetRichTextFontSize` with logical points only for uniform Raw/log text, not general rich documents.
* Presentation-only editor updates must preserve text, selection, read-only state, clean/already-dirty state and undo/redo. Verify event behavior separately; restoring the final dirty flag alone is insufficient. Keep Raw-specific protections local.
* Own and dispose application-created fonts, never inherited/shared fonts. Avoid equal replacements, dispose unused candidates, and release superseded owned fonts only after successful assignment and remaining fonts after their consumers.
* Responsive layout must preserve request data, selection and usable non-overlapping controls across DPI/font changes. Avoid reparenting, redundant geometry writes and nested layout churn.
* Use the standalone `Test/DpiScaling.Tests.ps1` suite; see `Docs/TESTING.md` for commands and coverage limits. Validate startup, lazy activation, monitor transitions and runtime fonts before test setup can repair state. Run relevant coverage for behavior changes, not documentation-only edits, and retain manual startup/painting/mouse-resize checks.
