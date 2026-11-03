# Clearinet Help

Welcome, early user! The Help isn't written yet, but I'm excited to add some soon.

For now, the [Fiddler Book](https://fiddlerbook.com) might be useful for your needs, or check out [other docs](https://github.com/ericlaw1979/Clearinet/blob/main/Docs/) or the source.

Or ask your question in the [Clearinet User Group](https://groups.google.com/group/clearinet).

### Log
Log entries are queued while another tab is selected and displayed when you select Log.
Each row starts with a light-gray `HH:mm:ss` timestamp captured when the entry is queued.
A leading `_`, `/`, or `!` is removed and formats that row's text as underlined, italic,
or bold, respectively. New entries scroll into view only if you were already at the bottom;
otherwise your reading position and text selection are preserved. Press Ctrl+X to clear the log.
Use the search box at the bottom to find text in the log. Press Enter to find the next
case-insensitive match, wrapping to the beginning as needed. Ctrl+F while in the Log tab
focuses the search box; Escape clears the search and returns focus to the log.

### Copy as CURL
Copy as CURL only copies text; it never executes cURL or creates files. Each selected Exchange becomes
one command line requiring `curl.exe`. The PowerShell flavor pipes readable UTF-8 cURL configuration
to avoid native argument-quoting differences between PowerShell versions. The Command Prompt flavor
uses native cURL arguments and expects delayed environment-variable expansion to be disabled (the default).
Bodies of at most 1024 printable ASCII bytes are inlined. Other bodies, including multiline text,
reference `YOURREQUESTBODYBINARYFILEHERE.DAT`; create that file yourself with the request body bytes
before running the command. Transfer framing and Content-Length are regenerated; content encoding
is preserved, so the file must contain the content-encoded bytes without transfer framing. HTTPS
certificate validation remains enabled. Shell command-length limits still apply, particularly Command
Prompt's 8191-character limit. Copied commands include credentials and cookies from the request,
so treat them as sensitive.

### Copy as PowerShell and Copy as JavaScript fetch()
Edit > Copy > as PowerShell command and as JavaScript fetch() also only copy text, one line per selected
Exchange. Bodies are embedded in full: printable ASCII as a string, anything else as Base64 that is decoded
back to the exact bytes, so no placeholder file is needed. As with cURL, transfer framing is removed and
content encoding is preserved. Connection-level headers (Host, Content-Length, Transfer-Encoding,
Connection, Keep-Alive, Proxy-Connection, TE, Trailer, Upgrade, Expect) and HTTP/2 pseudo-headers are
omitted because the client generates its own. Both clients follow redirects. Copied code includes
credentials and cookies from the request, so treat it as sensitive.

The PowerShell command uses `Invoke-WebRequest` and runs in Windows PowerShell 5.1 and PowerShell 7+.
Values are single-quoted, so nothing in them is expanded. Methods other than GET, HEAD, POST, PUT,
DELETE, TRACE, OPTIONS, MERGE and PATCH use `-CustomMethod`, which needs PowerShell 7. Repeated headers
are combined into one (Cookie values with `; `, others with `, `) because `-Headers` takes a hashtable.
`Invoke-WebRequest` adds its own User-Agent when the request had none, and a form Content-Type when a
body has none. PowerShell 7 refuses header values containing non-ASCII characters.

The fetch() call works in browser DevTools and in Node.js 18 and later. Headers are passed as a list,
which keeps their order and any repeats, and header text is written so fetch() sends the original
bytes. fetch() cannot send a body with GET or HEAD, so such a body is left out and a comment says so.
Browsers silently drop headers that scripts may not set, such as Cookie, Referer and Accept-Encoding.