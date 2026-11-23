 A "Web Exchange" represents a HTTP Request and Response pair and the associated metadata (timers, flags, etc). (Legacy Fiddler called these "Sessions")

 Clearinet shows captured and loaded Web Exchanges in the list at the left hand side of the UI. Data from each Exchange is shown in the columns of the list; you can add [Custom Columns](CustomColumns.md) if you like.

## Suggested filenames

`Exchange.SuggestedFilename` is a read-only property. Without response headers it returns `{id}.txt`. With a response, it prefers the decoded `Content-Disposition` `filename*` parameter, then `filename`, then the decoded final URL path segment (excluding query and fragment). If none supplies a name, it uses `{id}` plus an extension based on `MediaType`. Missing Content-Type uses `.txt`; unrecognized binary types use `.bin`.

Wrapping single quotes on a `filename` parameter are removed; internal or unmatched apostrophes are preserved.

Names are limited to 96 characters, preserving an extension when possible. Windows-invalid filename characters and trailing dots/spaces become `_`; reserved device names are prefixed with `_`.

## Dragging response files

Drag selected Exchanges to a file-drop target, such as Windows Explorer, to copy the decoded response bodies of HTTP 200 Exchanges. Each file uses its Exchange's suggested filename; duplicate names receive a numbered suffix. Other response statuses are omitted. Dragging to Composer still uses the selected Exchanges.

To support ordinary file-drop targets, Clearinet stages these response bodies in a uniquely named folder under the user's temporary directory when the drag begins. The folder is scheduled for deletion when Clearinet exits, even if the drag is cancelled. Staged files may contain sensitive response data and can remain if Clearinet terminates unexpectedly or cleanup fails.
