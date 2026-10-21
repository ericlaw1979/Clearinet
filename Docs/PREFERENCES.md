Clearinet is designed to allow easy customization via preferences. 

To see and configure preferences, type `about:config` in the [QuickExec](QuickExec.md) box below the Web Exchanges list. You can
also type `prefs show nameofpref`  or `prefs set nameofpref newvalue`

Below is a list of some preferences, their defaults, and what they control

`app.ui.toolbar.lookupcuetext`, `Search MDN...` => The "Search MDN..." cuetext in the toolbar.

`app.ui.toolbar.lookupurl`, `https://developer.mozilla.org/en-US/search?q=$W$` => The target of the lookup tool in the toolbar.

`app.saz.warn_about_data_protection`, `true` => Whether to warn the user about sharing SAZ files that may contain private information.

`app.high_resolution_clock`, `true` => Whether the app should tell Windows to increase timer resolution for improved accuracy in timing scenarios.

`inspectors.headers.highlighted_names`, `content-type,content-length,...` => change the color of more important headers

`app.copy.curl.flavor", `powershell` => Shell target for Edit > Copy > as CURL command. Set to `cmd` for Windows Command Prompt, or `powershell` for PowerShell.

`user.paths.editors.code`, `notepad` => Default editor for editing your script. `code.exe` is a better choice if you install the free [Visual Studio Code](https://code.visualstudio.com/)

You can find all of the Preferences checked by Clearinet by [Searching the code](https://github.com/search?q=repo%3Aericlaw1979%2Fclearinet+%2FGet.*Pref%5C%28%2F&type=code)
