# Customer Requirements

The following are a list of customer requirements for Clearinet

- OSS and Free Forever (no chance of a corporate "rug pull" like Progress/Telerik)
- Offline mode: No CAPTURE data sent to any web service (Required for compliance)
- Import and export of .SAZ ("Session Archive Zip") traffic captures
- Ability to import HAR format traffic
- Ability to import Chromium Netlog format traffic
- Ability to replay previously-archived traffic ("Autorespond")
- Easy way to visualize size of request headers (e.g. "Cookie")
- Lightweight capture client that can be used by non-technical users

Stretch goals in order of cost
- Support TLS/1.3
- Support for HTTP/2
- Support for traffic capture without proxy (e.g. use WFP to intercept/redirect)
- Support for non-Windows OS (e.g. .NET Standard cross-platform proxy engine and  MAUI-based UI)

Sugar (Not strictly required but very useful)
- Mezer-Tools style color-picker and UI sizer
- Extensible TextWizard applet
