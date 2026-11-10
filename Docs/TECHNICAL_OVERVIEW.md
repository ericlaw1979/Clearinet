# Project Overview

Clearinet is a HTTP(S) web debugging tool used primarily to collect and examine
web traffic originating from browsers, browser-like applications (e.g. Electron),
and native code applications that utilize web protocols for communication.

Clearinet aims to provide a similar API and capabilities to Telerik Fiddler
Classic, a debugging proxy whose license now precludes most usages.

The tool runs locally on the user's PC and exposes a HTTP endpoint that accepts
requests and creates a new Exchange object for each. The inbound connection's
originating process is determined using the MapSocketToProcess class in Clearinet.
The client optionally modifies the request, then determines its target. Normally,
the target is determined using the Host header on the request, but this can be
overridden by an "X-OverrideHost" string in the Flags object on the Exchange. If
the user's configuration suggests that requests should be sent to an upstream
gateway (a previously configured proxy), the connection should be made to that
proxy instead.

After connecting to the request's target, Clearinet sends the request. 
The client then reads the response from the server, optionally modifies it, 
then returns the response to the requesting client. Each Exchange (a 
request/response pair) moves through a state machine and each step will update
the headers/body objects, the ExchangeTimers, the Exchange's display in the UI,
and will fire events or otherwise allow extensions or the UI to update.

By default, requests and responses are "buffered". This means that requests are
read completely from the client before processing continues, and that responses
are completely read from the server before processing continues. However, an
Exchange can also be configured for "streaming" mode, meaning that after the response's
headers are read from the server, each read() returned from the server results
in the buffer data being immediately returned through the client connection via send().
This streaming allows smooth streaming of video and other data where waiting
for the whole response would be impractical or unnecessary. It also allows optional
cancelation of the reading of the response (e.g. if the client closes the connection
because the user has navigated their browser away from the video, the proxy core
can close the connection to the upstream server before the respone has ended).

## Code
The project is written targeting the .NET Framework 4.8.1 for the front-end, and
.NET Standard for the proxy core. 

There is a clear separation between the frontend UI and the backend proxy core;
the proxy core in particular does not directly reference the frontend UI, 
instead firing events that allow the UI to respond to changes in the Exchange
objects as they are created and processed. Code for the Proxy Engine lives
inside the existing CaptureEngine subfolder.

The proxy core is designed as an embeddable component which can be used 
in other .NET applications. The UI targets WinForms and thus only runs 
on Windows, but the embedded proxy core can be used by applications 
targeting cross-platform UI frameworks like Maui or Avalonia.

### Libraries
This code is released under the MIT License and cannot use anything not
compatible with that. 

Use of 3rd-party libraries should be avoided entirely in the proxy core.
For the rest of the application, adding new 3rd-party dependencies should
be minimized and should only be used in cases where the component is
entirely self-contained and not practical to rewrite (e.g. compression and
encryption libraries, and file format parsers).

### Code Style and Commenting
In general, code should contain explanatory comments explaining why it is
doing whatever it is doing, showing simple examples where it makes sense, e.g.
a function `string TrimTo(string s, int len)` could have a comment like
`Truncates the string at an offset. Example: TrimTo("a long string", 5) returns "a lon".`

Simple code should be written succinctly (e.g. ternary operations are fine) but
complicated code should be written in a way that it is easy to read. Advanced
features like LINQ can be used when they do not impact performance and only in
cases where they are significantly more readable than using more basic language
features.

### Capturing Traffic from Clients
When "attached", Clearinet will use the InternetSetOptionEx API with the Internet_PER_CONN_OPTION setting to direct most Windows network stacks
to route its traffic through its proxy, which listens on local port 8888
by default. When it detaches, Clearinet restores the user's original system
proxy setting using the same InternetSetOptionEx API.

Traffic can also be sent directly to the listening proxy by manual configuration,
e.g. launching a Chromium-based browser with a command line argument of
`--proxy-server=127.0.0.1:8888`.

The capture engine listener is attach-gated: it starts only after Clearinet
successfully attaches as the system proxy, and it stops when Clearinet detaches
or shuts down. The listener address is loopback-only unless remote connections
are explicitly enabled. Accepted sockets are associated with their originating
process when Windows exposes a matching TCP owner record.

HTTP/1.x request headers and bodies are read with bounded buffering. Both
Content-Length and chunked request framing are supported; chunked bodies remain
wire-framed when forwarded. Malformed requests are rejected by aborting the
client connection rather than attempting to continue on a desynchronized stream.

In the future, we will support a mode where traffic from a process can be
transparently proxied by using a Windows Filtering Platform callout to
retarget traffic that was targeted to a remote endpoint so that it is
instead sent to Clearinet, but that feature will be delivered in a future
update.

### Socket Pools
By default, Clearinet will reuse connections to a server for a configurable
lifetime. After a response is completed from a server, the connection is
returned to a "socket pool" and is eligible for reuse for a configurable term
defaulting to 5 minutes. Idle connections are not pooled if they were closed
by the server after the response was read, or if the HTTP response headers
specified "Connection: close", or if the protocol version was "HTTP/1.0"
and the server did not specify "Connection: keep-alive" on the response.

A "Janitor" running on a periodic schedule will purge any sockets in the pool
which have exceeded their idle time limit or whose connections were closed by
the server.

Pools are keyed by destination hostname, port, and whether the upstream
connection uses TLS. The idle and maximum-age defaults are both five minutes and
are configurable with `proxy.socketpool.idle_ms` and
`proxy.socketpool.max_age_ms`.

### TLS Decryption
TLS decryption is provided by a "man-in-the-middle" approach where Clearinet
presents to the client a self-generated certificate that contains the target
site's hostname. This certificate chains to a unique per-machine root certificate
that is generated the first time it is needed and subsequently stored in the 
Windows Trusted root certificate store. By default, these certificates are
generated using the CertEnroll API in Windows, but Clearinet consumes certificate
generation using an ICertificateProvider API that allows replacing this logic
with your own, including, for example, an implementation based on the BouncyCastle
library.

By default, Clearinet uses the .NET Framework's SslStream class for securing
socket connections using TLS, but in the future this will be replaced with 
another approach (likely either the BouncyCastle library or PInvoke calls to
Windows SChannel).

CONNECT requests use one of two modes. With HTTPS decryption disabled, or when
an Exchange has the `x-no-decrypt` flag, Clearinet establishes a blind TCP tunnel
and copies bytes in both directions. With decryption enabled, Clearinet sends the
CONNECT success response, authenticates the client side with a generated leaf
certificate, authenticates an upstream TLS connection, and then processes the
decrypted HTTP/1.x traffic. The certificate provider caches generated leaf
certificates and exposes cache cleanup through its abstraction.

ServerPipe reuse is controlled by `proxy.serverpipe.binding_policy`: `Free`
allows any request to rent a pipe, `Process` (the default) requires the same
known originating process, and `PipeBound` requires the same ClientPipe. A pipe
identity includes a stable ID and whether its current stream is TLS-wrapped.

The initial MITM implementation does not negotiate HTTP/2 because .NET Framework
SslStream does not expose the required ALPN support. Certificate generation and
trust operate in the current user's Windows certificate stores; deployments that
cannot modify those stores must supply another `ICertificateProvider`.

### Protocol Support
In the initial implementation, Clearinet's proxy engine supports only HTTP/1.0
and HTTP/1.1, because negotiating HTTP/2 requires configuration of the ALPN
TLS extension and .NET's SslStream class does not support doing so.

The capture engine is the default transport path. The earlier HttpClientBridge
transport remains available for compatibility and diagnostics through the
ephemeral `proxy.ephemeral.use_httpclientbridge_transport` preference, but is
not selected by default by the Exchange state machine.

In a future implementation, Clearinet will detect when a client is attempting
to construct a connection to be used for WebSocket traffic and will handle
these connections by processing the WebSocket messages and exposing them
to the user via UI and extensions via APIs. For now, Clearinet will simply
proxy the bytes on a WebSocket connection blindly from client to server without
attempting to interpret them.

## Sample Exchange Flow

The following is a representative flow for an HTTP/1.x Web Exchange through
the default capture engine. `ExchangeState` defines the lifecycle states used
to describe this flow; see `Clearinet/ExchangeEnums.cs`.

1. **Accept the client connection.** `TcpCaptureEngineListener.AcceptLoopAsync`
   accepts a TCP connection and starts `HandleClientAsync` for it. The handler
   also asks `MapSocketToProcess` for the originating process so that process
   metadata can be attached to the Exchange
   (`Clearinet/CaptureEngine/CaptureEngineRuntime.cs`).

2. **Read and parse the request.** The `ClientPipe` owns the client socket,
   stream, and a `HttpStreamReader`. The reader preserves read-ahead bytes,
   validates `Content-Length`/chunked framing, and rejects ambiguous framing so
   a pipelined request cannot be consumed as part of the preceding body.

3. **Create and publish the Exchange.** `BuildExchange` creates an `Exchange`
   containing the request headers and body, assigns its ID, records client and
   request timers, adds client/process flags, and sets its state to
   `ReadingRequest`. `PublishExchange` then schedules a UI update so the Web
   Exchange is visible while work continues.

4. **Allow request alteration.** The state-machine sequence is
   `AutoTamperRequestBefore`, `HandTamperRequest`, and
   `AutoTamperRequestAfter` (`Exchange.state` in `Clearinet/Exchange.cs`). In
   the automatic stage, `Extensions.CallAutoTamperRequestBefore` invokes each
   `IAutoTamper` extension and the script rule's `OnBeforeRequest` handler.
   Setting `x-breakrequest` holds the Exchange in `HandTamperRequest`; inspector
   edits are copied back to `RequestHeaders` and `RequestBody` by
   `InspectorBase.CommitAnyChanges` in `Clearinet/InspectorAPI.cs` when the user
   continues it.

5. **Choose the HTTP or CONNECT path.** A normal HTTP request is sent through
   `ForwardUsingServerPipeAsync`. The target authority is resolved from the
   absolute-form request target or `Host`; a `CONNECT` request instead becomes
   either a blind byte tunnel or a decrypted TLS tunnel in
   `HandleBlindConnectTunnelAsync` or `HandleMitmConnectTunnelAsync`.

6. **Send the request and read the response.** The engine rents a compatible
   `ServerPipe` or resolves DNS and opens a TCP socket, wrapping HTTPS sockets
   in a retained `SslStream`. `HttpStreamReader` reads the correctly framed
   response. The whole pipe, including its TLS state and reader, is returned to
   the pool only when the protocol permits reuse.

7. **Allow response alteration.** Assigning
   `AutoTamperResponseBefore` invokes every `IAutoTamper` extension and the
   script rule's `OnBeforeResponse` handler. The subsequent
   `HandTamperResponse` state can expose editable response inspectors when
   `x-breakresponse` is set, after which inspector changes are committed to
   `ResponseHeaders` and `ResponseBody`. These hooks can also generate a local
   response, for example by setting `X-ReplyWithFile`, instead of preserving the
   server's response.

8. **Return the response to the client.** The capture engine serializes the
   response headers and body, flushes the `ClientPipe`, and records client
   response timers. If the client connection remains open and both sides allow
   persistence, the ClientPipe waits in the reuse queue for up to one minute;
   a subsequent request repeats the exchange flow. Blind CONNECT and
   WebSocket-style tunnels instead use `PumpStreamAsync` to copy bytes in both
   directions without interpreting individual HTTP Exchanges.

9. **Complete or abort.** A successful Exchange enters `Done` and is published
   once more so the UI shows its final status and timing. Parse, TLS, socket, or
   other transport failures are converted to a generated error response where
   possible by `HandleCaptureFailureAsync`, and the Exchange enters `Aborted`.
   `Exchange.state` raises `OnStateChanged` and schedules UI refreshes as the
   state changes.

The state enum describes the complete alteration contract, but transport
coverage is still being completed. At present, decrypted HTTPS inner Exchanges
enter the request-tampering states explicitly, while the ordinary HTTP capture
path forwards after `ReadingRequest`; transport writers also do not yet await a
manual response-break continuation before writing. Extensions should therefore
treat the `HandTamper*` states as the lifecycle contract while accounting for
the current transport limitations.

## Threading and Asynchronous Behavior
As a proxy, many Exchanges will be processing at the same time, but from
a pure technical perspective, most of the time each Exchange will be waiting
for something to happen (e.g. awaiting an inbound connection to the listening
proxy socket, performing a DNS lookup, waiting for a TCP/IP connect/send/receive
operation, or waiting for the user to manually modify a request or response in the
HandTamperRequest or HandTamperResponse state). Therefore, Clearinet is
written using asynchronous patterns that avoid leaving threads blocked and
idle, using threads from the thread pool only when running code (e.g.
notifying an extension that a given event has changed state and allowing
that extension to run code in response).

## Memory Usage
Web Traffic can consume a lot of memory, especially when dealing with large
responses from servers (e.g. video files may be hundreds of megabytes). Because
Clearinet holds onto captured Exchanges (refering to each using an item in
the ExchangeListView's Items), this can result in high memory usage. We should
avoid making unnecessary copies of data, and check for memory leaks.

For now, we will not redefine Clearinet's RequestBody and ResponseBody objects
to use a type other than byte[], meaning that these bodies have an upper limit
of 2GB, but in the future we will move to using a different approach, e.g.
a "Custom chunked byte buffer class to store data larger than 2GB using List<byte[]>."