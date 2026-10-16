An Inspector is used to display (and optionally tamper with) the content of a HTTP request or response message.

By default, your Inspector object should simply offer its name and respond to queries about how interested it is in
a given message. For instance an Image inspector will score responses with "Content-Type: image/jpeg" highly, while
responses of "Content-Type: text/html" will return a low score. The App will then attempt to select the most suitable
Inspector when the user hits Enter or double-clicks on an Exchange.

Your Inspector will be provided a tab into which its UI will be rendered. This is typically one of the tabs on the
Inspectors page in the main app UI, but it may be in the AutoResponder editor or in a standalone "Inspect in New Window" window.

For performance reasons, you should avoid doing anything expensive (e.g. loading dependent DLLs, creating heavy UI components) until
you've been told to do so with the `.AddToTab` call.

In many cases, your Inspector may be asked to inspect exchanges in rapid sequence; this could happen:

   - Intentionally: the user is scrolling through the list rapidly looking for e.g. a particular image, or
   - Unintentionally: the user is scrolling through the list and not even looking at the inspector at all.

Unfortunately, these scenarios are in conflict: we could "debounce" updates to prevent unwanted updates and allow the UI to be
more responsive, but doing so will cause problems for the user who is intentionally rapidly cycling between entries. Handling of
this situation will probably be decided for you at the app level; we'll only call Assign() on you when we want you to inspect the
Exchange as quickly as possible.

## Data request and response inspectors

The read-only Data tab displays XML and JSON bodies in a tree, including XML attributes and text, JSON properties,
array indices, and primitive values. Both inspectors score 75 for `application/json`, `application/xml`, and media types
ending in `+xml`, except `image/svg+xml`. Content-Type matching is case-insensitive and accepts charset parameters.
Other content types can be inspected manually; without a recognized type, the body is detected as XML or JSON.
HTTP content and transfer encodings are decoded before parsing. HTTP charsets and byte-order marks are honored;
XML without an HTTP charset also honors its encoding declaration. Parsing uses only .NET Framework readers.
XML DTDs and external entity resolution are disabled. Invalid documents display a parse error instead of stale data.

Type in the Find box at the bottom to select and scroll to the first matching node, including collapsed descendants.
Matching is case-insensitive. Enter or Next advances to the next match; Shift+Enter goes to the previous match,
wrapping at either end. Ctrl+F focuses the search box. The match count (or No matches) appears beside it.
The footer's Expand All and Collapse All buttons expand or collapse every node in the document tree.

Run the Data inspector regressions with Windows PowerShell after building the Extensions project:
`powershell.exe -NoProfile -STA -File .\Test\DataInspector.Tests.ps1`.

## WebView response inspector

The WebView tab uses the system Microsoft Edge WebView2 Evergreen Runtime, initialized only when a supported response is viewed.
If the runtime or SDK components cannot load, the tab displays an explanation; other inspectors continue to work.
The SDK assemblies and x86/x64/ARM64 native loaders are shipped with the default extensions, but the Evergreen Runtime is not bundled.

HTML and SVG are rendered as passive documents; SVG scripts and external subresources remain disabled. Images are embedded as data URLs.
Audio and video responses are placed in native media elements with controls and no autoplay, so playback requires user action.
WOFF2 responses are embedded as data fonts in a sample page. Text, JSON, XML, CSS, and JavaScript are displayed as escaped text.
Compressed and chunked responses are decoded first. PDF and plugins are not supported. WebView is preferred for HTML and SVG;
the existing image and raw inspectors retain their higher scores for other images and plain text.

This is an offline preview, not a browser: captured origins, cookies, authentication, and active response headers are never replayed.
Only the selected response is served, in memory, from a random URL on the reserved `clearinet-preview.invalid` host.
All other resource requests are denied. CSP permits only embedded data images, media, and fonts; external dependencies do not load.
Same-document `#fragment` links may update the preview URL but never leave the offline document. Inline CSS is allowed.
A restrictive HTTP Content-Security-Policy sandbox provides an additional boundary. Responses and decompressed/browser documents are not size-capped;
very large responses can consume substantial memory. Scripts, host objects, web messages, forms, hyperlinks, redirects, popups, downloads,
external protocols, browser shortcuts, devtools, context menus, permissions, authentication prompts, and dropped files are disabled or blocked.
Each viewer uses an InPrivate profile in a separate Clearinet WebView data folder under Local AppData, never the user's Edge profile.
Keep the Evergreen Runtime up to date: rendering untrusted content still depends on the browser engine's security.

Run the WebView preview regressions with Windows PowerShell after building the Extensions project:
`powershell.exe -NoProfile -STA -File .\Test\WebViewPreview.Tests.ps1 -Browser`.