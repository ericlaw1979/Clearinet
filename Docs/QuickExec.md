# Clearinet's QuickExec Box

The QuickExec box sits below the Web Exchanges list and allows you to rapidly execute commands, including both built-in commands and those added by extensions or your CustomRules script.

<img width="922" height="177" alt="image" src="https://github.com/user-attachments/assets/6c0d8506-c55a-4481-b714-84701cdcb836" />


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
