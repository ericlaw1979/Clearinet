# ClearINET Web Debugger

Clearinet is a locally-running web debugging application designed to visualize, generate, and modify network traffic transported using Web Protocols (primarily HTTP over TLS, aka HTTPS). Common functionality is built-in ("batteries included") but the tool is built for extensibility, with the aim of easily allowing anyone to customize the tool via powerful and simple-to-use extension models.

## Primary Outputs
- Clearinet App > the full application which runs on Windows
- Clearinet Cap > a lightweight app that is designed to generate SAZ files
- Clearinet Core > A .NET Proxy server engine upon which App and Cap are built. 

### Clearinet App
A .NET WinForms application that has the look-and-feel of an 2010-era Windows application. The UI should be instantly familiar to users of prior tools. The object model and API should be familiar and allow easy porting of extensions and scripts that targeted prior tools.

### Clearinet Cap
A trivial capture app designed to be as simple as possible for a non-technical end user to generate a SAZ capture of web traffic. There are many interesting features we could add that would go beyond what FiddlerCap offered (including e.g. org branding, integrated upload, etc), but the minimum offering only needs to match the basic capture scenario.

### Clearinet Core
This engine should compile correctly for .NET implementations that run across platforms (Mac/Linux) to allow building cross-platform web debuggers that are similar in functionality/spirit to Clearinet App but not limited by its legacy compatibility and UX.

## Philosophy and Prioritization
In the initial incarnation, "Change is bad unless it's great." Our goal is to get a usable tool that is approximately at par with what Progress Software took away from the community, and do so as quickly as possible. After that, we can aim to improve and branch out.

Where possible, we should aim to fix some of the [Fiddler Mistakes](https://textslashplain.com/2024/11/24/fiddler-my-mistakes/) I made when I built Fiddler.

## Commentary and Motivation
This application is the spiritual successor to the Fiddler Web Debugger, a tool which was acquired by Telerik in 2012 with the promise that it would remain free forever. Progress Software acquired Telerik at the end of 2014. In 2016, Fiddler's maintainer left Telerik/Progress and the Fiddler Web Debugger, later renamed to Fiddler Classic, saw few updates over the following 10 years. In 2016, Progress announced that they were reneging on earlier promises and changing the tool's license to preclude any commercial use. They also refuse to offer for sale any license for such use. More discussion can be found in the [Fiddler in 2026 blog post](https://textslashplain.com/2026/08/05/fiddler-in-2026/).

## Links
- [App Website](https://clearinet.app)
- [Fiddler in 2026 blog post](https://textslashplain.com/2026/08/05/fiddler-in-2026/)
