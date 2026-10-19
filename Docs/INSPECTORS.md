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

The read-only Data tab displays XML, JSON, and BSON bodies in a tree, including XML attributes and text, document properties,
array indices, and primitive values. Both inspectors score 75 for `application/json`, `application/xml`, `application/bson`,
`application/x-bson`, and media types ending in `+xml`, except `image/svg+xml`. Content-Type matching is case-insensitive and accepts charset parameters.
Other content types can be inspected manually; without a recognized type, the body is detected as XML or JSON.
HTTP content and transfer encodings are decoded before parsing. HTTP charsets and byte-order marks are honored for text;
XML without an HTTP charset also honors its encoding declaration. XML and JSON parsing uses .NET Framework readers.
XML DTDs and external entity resolution are disabled. Invalid documents display a parse error instead of stale data.

BSON is parsed directly from decoded bytes when its Content-Type is declared; no external libraries are required.
Nested documents and arrays use the same search and expansion controls as JSON. BSON-specific values retain their types:
integers, doubles, ObjectIds, binary subtypes and hex payloads, regexes, timestamps, JavaScript (including scope), symbols,
DBPointers, undefined, and MinKey/MaxKey. Dates show signed milliseconds since the Unix epoch; Decimal128 values show
all 16 bytes as little-endian hex to preserve their exact representation. Code is displayed, never executed.
Invalid lengths, terminators, UTF-8, array indices, booleans, trailing bytes, and unknown element types produce errors.
BSON nesting is limited to 128 document levels.

Type in the Find box at the bottom to select and scroll to the first matching node, including collapsed descendants.
Matching is case-insensitive. Enter or Next advances to the next match; Shift+Enter goes to the previous match,
wrapping at either end. Ctrl+F focuses the search box. The match count (or No matches) appears beside it.
The footer's Expand All and Collapse All buttons expand or collapse every node in the document tree.

Run the Data inspector regressions with Windows PowerShell after building the Extensions project:
`powershell.exe -NoProfile -STA -File .\Test\DataInspector.Tests.ps1`.

## Image response inspector

The Images tab shows file metadata in the scrollable textbox beside the image. JPEG, PNG/APNG, GIF, WebP, and classic TIFF
are analyzed from the original file bytes after HTTP content/transfer decoding, before WebP conversion. It lists the file size,
pixel dimensions, and color depth. The non-rendering metadata byte total and percentage are shown only when the total exceeds
512 bytes. Indexed images report bits per palette index; other depths include all channels, including alpha. For other renderable
formats, dimensions and decoded pixel depth are shown, and byte accounting is explicitly marked unsupported.

Accounting includes known optional metadata and its segment/chunk/extension framing, without double-counting overlapping ranges.
It excludes compressed pixel data, palettes, transparency, animation controls, color profiles, and Adobe JPEG color transforms.
Exif with a non-default orientation retains its container framing and orientation entry; known optional fields and thumbnails
within it are still counted. Orientation-bearing XMP packets are conservatively retained as a whole. TIFF accounting includes
optional tag entries and their out-of-line values, not primary image directories or pixels. Unknown PNG ancillary chunks are
retained conservatively rather than assumed to be removable.

APNG and GIF frame counts and progressive JPEG encoding are identified. Embedded Exif, JFIF/JFXX, and Photoshop thumbnails
show byte sizes and absolute offsets (decimal and hexadecimal) within the HTTP-decoded image file. Exif GPS and XMP location
coordinates are displayed as signed decimal `[latitude, longitude]`. JPEG COM, PNG text (including compressed/international text),
GIF comments, Exif descriptions/user comments, XMP descriptions, and IPTC captions are previewed. The combined preview is limited
to the first 1,024 text bytes, with `...` when truncated; compressed text is bounded during decompression. XMP parsing disables
DTDs/external entities and is limited to 1 MB. Malformed or incomplete metadata produces an explicit notice and no misleading
complete byte total; metadata remains available even when the image renderer cannot open the file.

Run the Image inspector regressions after building the Extensions project:
`powershell.exe -NoProfile -STA -File .\Test\ImageInspector.Tests.ps1`.

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