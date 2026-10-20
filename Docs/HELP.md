# Clearinet Help

Welcome, early user! The Help isn't written yet, but I'm excited to add some soon.

For now, the [Fiddler Book](https://fiddlerbook.com) might be useful for your needs, or check out [other docs](https://github.com/ericlaw1979/Clearinet/blob/main/Docs/) or the source.

Or ask your question in the [Clearinet User Group](https://groups.google.com/group/clearinet).



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