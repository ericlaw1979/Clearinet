# Clearinet's QuickExec Box

The QuickExec box sits below the Web Exchanges list and allows you to rapidly execute commands, including both built-in commands and those added by extensions or your CustomRules script.

<img width="922" height="177" alt="image" src="https://github.com/user-attachments/assets/6c0d8506-c55a-4481-b714-84701cdcb836" />

Eventually, this feature will include autocomplete and a scrollable history list (using the up/down arrow keys), but these [conveniences are `NOTYETIMPLEMENTED`](https://github.com/ericlaw1979/Clearinet/issues/25).

## Built in Commands

### ExchangeList Selection Commands
- `? findtext` select Exchanges whose URL contains the specified _findtext_
- `=method` select Exchanges whose HTTP request method/verb is _method_ (e.g. `=POST`)
- `=responsecode` select Exchanges whose response status code is _responsecode_ (e.g. `=304`)
- `select mime` select Exchanges whose response Content-Type header contains the specified string (e.g. `select image`)

### ExchangeList Removal Commands
- `cls` clears the Exchanges list
- `tail #` - Remove all but the most recent `#` of Exchanges

### Preference Commands
- `about:config` open the about:config tab that allows editing preferences
- `prefs <set|show|remove|dump>` manipulates preferences, but `about:config` is a better choice

### Diagnostic Commands
- `lm` logs all loaded .NET modules to the Log tab
- `!dns hostname` looks up a hostname using the system DNS resolver and switches to the Log tab. The lookup runs without blocking the UI and logs the resolved hostname, any aliases, and all returned IPv4/IPv6 addresses, or an error if resolution fails. Example: `!dns google.com`.
