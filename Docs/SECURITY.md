#Security Overview

## Vulnerability Reports
- Vulnerability Reporting: Please report security vulnerabilities directly to maintainers.
- [Compromised machines](https://chromium.googlesource.com/chromium/src/+/lkgr/docs/security/faq.md#Why-arent-compromised_infected-machines-in-Chromes-threat-model) are 
  outside of Clearinet's threat model. It's neither practical nor useful to attempt to prevent attacks on the app if the device has already been compromised. There's further
  discussion of this topic in this [article on runtime signature checks](https://textslashplain.com/2025/03/31/runtime-signature-checking-threat-model/).
  
## Considerations for users
- Clearinet offers full-trust scripting and extensibility models. Scripts and extensions run unsandboxed, so if an attacker can persuade you to install his code, your device
  will be compromised.
- SAZ Files and other archives of web traffic will OFTEN contain credentials. If those credentials include "bearer tokens" (e.g. authentication cookies) or other secrets
  (e.g. HTTPS pages containing your personal data), then the archive MUST be protected (e.g. not shared publicly, not sent without encryption or stored in an insecure location)
  or the information could be compromised. Do NOT send or share SAZ Files with anyone you do not trust. Consider using the "Password-Protected SAZ File" option for any archive which may contain sensitive information.
- Enabling "Allow remote devices to connect" will allow another device to use your device as a proxy, making it appear that traffic is coming from your device. This may
  be abused by an attacker to make it appear as if your device is responsible for illegal behavior. If you enable the "Automatically authenticate" option, traffic flowing
  through Clearinet will have your credentials attached; this may result in abuse of your credentials.
  
