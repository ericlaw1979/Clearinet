# Clearinet's QuickExec Box

The QuickExec box sits below the Web Exchanges list and allows you to rapidly execute commands, including both built-in commands and those added by extensions or your CustomRules script.

<img width="922" height="177" alt="image" src="https://github.com/user-attachments/assets/6c0d8506-c55a-4481-b714-84701cdcb836" />

QuickExec autocompletes from command history as you type. Up/Down browse commands matching your typed prefix, selecting the suggested suffix so typing replaces it. Down past the newest match restores your draft.

Tab moves the cursor just after the next space, `/`, or `\`; Shift+Tab moves back to the previous such position, growing the selection backward while preserving its right edge. With no further separator, they move to the end or beginning of the text. When advancing through a selected history suggestion, Tab accepts the portion it passes over and leaves the remaining suggestion selected, so typing replaces it and can offer another matching completion. Shift+Tab makes the traversed portion uncommitted again, leaving it and the remaining suggestion selected. Tab otherwise clears the selection without changing the text. Both keys keep focus in QuickExec, including at the text boundaries, unless the text is empty or whitespace-only.

- Ctrl+U inserts the full URLs of the selected Exchanges, separated by spaces.
- Ctrl+I opens a file picker allowing one or more files, then inserts their full filenames, each double-quoted and separated by spaces. Canceling leaves the text unchanged.
- Drag files from the shell onto QuickExec, or copy files in the shell and paste with Ctrl+V, Shift+Insert, or the Paste command, to insert their double-quoted, space-delimited paths. Ordinary text paste is unchanged. Files are not moved or opened.

These operations insert at the cursor or replace selected text (including an autocomplete suggestion). They do not add spaces before or after the inserted group.

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
