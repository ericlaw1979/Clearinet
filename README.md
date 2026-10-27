# ClearINET Web Debugger

Clearinet will be a locally-running web debugging application designed to visualize, generate, and modify network traffic transported using Web Protocols (primarily HTTP over TLS, aka HTTPS). Common functionality is built-in ("batteries included") but the tool is built for extensibility, with the aim of easily allowing anyone to customize the tool via powerful and simple-to-use extension models.

## Primary Outputs
- Clearinet App > the full application which runs on Windows
- Clearinet Cap > a lightweight app that is designed to generate SAZ files
- Clearinet Core > A .NET Proxy server engine upon which App and Cap are built. 

### Clearinet App
#### Status: Development well underway
A .NET WinForms application that has the look-and-feel of an 2010-era Windows application. The UI should be instantly familiar to users of prior tools. The object model and API should be familiar and allow easy porting of extensions and scripts that targeted prior tools.

The request and response **Cookies** inspectors display `Cookie`/`Cookie2` and `Set-Cookie`/`Set-Cookie2` headers in a read-only text view. Each cookie appears separately, with response attributes on individual lines. The request inspector shows the total character count of all cookie header values (including separators, excluding header names) and each cookie's `name=value` character count (excluding surrounding whitespace; quotes and escapes count as displayed).

Right-click a node in the **Data** inspector to **Copy** its displayed text, **Copy Subtree** as tab-indented lines (including collapsed descendants), or **Send to Textransformer** to open the tool with that node's displayed text. Ctrl+C also copies the current node's text.

The NetLog importer accepts Chromium's JSON and JSONL captures, including gzip-compressed files and JSON/JSONL entries in ZIP archives. JSONL records use the same analysis as JSON captures; a capture without a final `end` record can be imported if all records are complete. Malformed JSONL records abort the import with an error.

QuickExec offers inline autocomplete from previous commands as you type. Up/Down browse commands matching your typed prefix (case-insensitive), newest first, keeping your typed prefix unselected and selecting only the completed suffix so you can replace it by typing; Down past the newest match restores your draft. Repeated commands move to the newest position.

The Screenshot toolbar button shows a four-second countdown ending with "cheese!", then captures the full desktop across all monitors and adds it as a generated Exchange with an `image/jpeg` response. The countdown disappears before capture, and the button cannot start another capture while counting down. Closing the viewer cancels the pending capture. Screenshots include visible desktop content, so review them before sharing or saving an archive.

Double-click an image in the Images inspector to open a non-modal, borderless fullscreen viewer on that monitor. Images larger than the window initially zoom out to fit while preserving their aspect ratio; smaller images open at 100% scale. Ctrl+plus/Ctrl+minus (including numeric keypad keys) or the mouse wheel zoom in/out; drag with the left mouse button or use the scrollbars to pan around images larger than the screen. Press Esc to close the viewer. The viewer keeps its own image copy, so changing the selected Exchange does not affect it.

![Top customer requirements list](/Docs/TopCustomerRequirements.png)

### Clearinet Cap
#### Status: Blocked on Clearinet Core
A trivial capture app designed to be as simple as possible for a non-technical end user to generate a SAZ capture of web traffic. There are many interesting features we could add that would go beyond what FiddlerCap offered (including e.g. org branding, integrated upload, etc), but the minimum offering only needs to match the basic capture scenario.

### Clearinet Core
#### Status: Development starting soon

This engine should compile correctly for .NET implementations that run across platforms (Mac/Linux) to allow building cross-platform web debuggers that are similar in functionality/spirit to Clearinet App but not limited by its legacy compatibility and UX.

## Philosophy and Prioritization
In the initial incarnation, "Change is bad unless it's great." Our goal is to get a usable tool that is approximately at par with what Progress Software took away from the community, and do so as quickly as possible. After that, we can aim to improve and branch out.

Where possible, we should aim to fix some of the [Fiddler Mistakes](https://textslashplain.com/2024/11/24/fiddler-my-mistakes/) I made when I built Fiddler.

To aid in AI-powered porting/modernization of code, an [AGENTS.md](/Docs/AGENTS.md) file is available.

## Commentary and Motivation
This application is the _spiritual_ successor to the Fiddler Web Debugger, a tool which was acquired by Telerik in 2012 with the promise that it would remain free forever. Progress Software acquired Telerik at the end of 2014. In 2016, Fiddler's maintainer left Telerik/Progress and the Fiddler Web Debugger, later renamed to Fiddler Classic, saw few updates over the following 10 years. In August 2026, Progress announced that they were reneging on earlier promises and changing the tool's license to preclude any commercial use. They also refuse to offer for sale any license for such use. More discussion can be found in the [Fiddler in 2026 blog post](https://textslashplain.com/2026/08/05/fiddler-in-2026/).

Clearinet is NOT owned/maintained/supported or otherwise sanctioned by Progress Software, Telerik, or any other corporate entity. It's open-source freeware, gifted to the world.

## Get Involved
- [Clearinet Community](https://groups.google.com/group/clearinet)
- [Contribute Code or Content](/Docs/CONTRIBUTING.md)
  
## Links
- [App Website](https://clearinet.app)
- [Fiddler in 2026 blog post](https://textslashplain.com/2026/08/05/fiddler-in-2026/)
