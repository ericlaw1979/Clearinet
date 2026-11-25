(This documentation page is referenced directly by the "Add Columns..." dialog box)

The Exchanges List at the left side of the user interface provides a list of captured (or loaded) [Web Exchanges](EXCHANGES.md). By default, the columns of this list show the most useful attributes of the Exchange, including the URL, request method, response size and content-type and so on. However, depending on your needs, you may find it useful to add new columns.

To do so, you can:

- Add a new column by right-clicking the column headers and choosing `Add Columns...` and then choosing what data to show in your new column: a Request Header, a Response Header, a Flag on the Exchange, a Timer, or a Calculated value. For Timers and Calculated values, pick the field from the list; hover over it for a description.
- Type `cols save <title> <field>` in [QuickExec](QuickExec.md), e.g. `cols save Server @response.Server` or `cols save TTFB @calc.TTFB`. Use `cols add` instead for a column that lasts only until Clearinet closes, and `cols remove <title>` to remove a column.
- Bind a script function to a new column; click Rules > Customize Rules and look for the `BindUIColumn` attribute to find a sample of doing this.

Columns you add with `Add Columns...` or QuickExec `cols save` are kept when Clearinet restarts, along with their position and width. Columns added by scripts are not saved, because the script adds them again each time it loads.

## Fields

| Field | Shows |
|---|---|
| `@request.Name` | The named request header |
| `@response.Name` | The named response header |
| `name` | The named Exchange flag, e.g. `ui-comments` or `x-processinfo` |
| `@timers.Name` | A timer: `ClientConnected`, `ClientBeginRequest`, `ProxyGotRequestHeaders`, `ClientDoneRequest`, `ServerConnected`, `ProxyBeginRequest`, `ProxyDoneRequest`, `ServerBeginResponse`, `ProxyGotResponseHeaders`, `ServerDoneResponse`, `ClientBeginResponse` or `ClientDoneResponse` as a time of day (`HH:mm:ss.fff`), or `GatewayDeterminationTime`, `DNSTime`, `TCPConnectTime` or `HTTPSHandshakeTime` in milliseconds. Blank if not recorded. |
| `@calc.TotalTime` | Milliseconds from `ClientBeginRequest` to `ClientDoneResponse` |
| `@calc.TTFB` | Milliseconds the server took to start responding: `ProxyDoneRequest` to `ServerBeginResponse` |
| `@calc.DownloadTime` | Milliseconds from `ServerBeginResponse` to `ServerDoneResponse` |
| `@calc.RequestBodySize` | Request body size in bytes |
| `@calc.DecodedResponseSize` | Response body size in bytes after removing Content-Encoding compression |
| `@calc.CompressionSavings` | Percentage of the response body saved by Content-Encoding compression. Blank if the response isn't compressed. |
| `@calc.ResponseHash` | SHA-256 hash (hex) of the response body after removing compression, so identical content matches even if it was sent differently. Blank for an empty body. |
| `@calc.SecurityHeaders` | Security response headers present: `HSTS`, `CSP`, `CSP-Report-Only`, `XFO` (X-Frame-Options), `Referrer-Policy`, `Permissions-Policy`, `COOP`, `COEP`, `CORP`, and `nosniff` (X-Content-Type-Options: nosniff) |

Sizes, milliseconds and percentages sort numerically, and times of day sort chronologically. Field names are not case-sensitive.
