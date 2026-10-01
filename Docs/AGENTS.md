### Class & Interface Mappings
- Replace `FiddlerApplication` with `CApp`.
- Replace `Session` with `Exchange`.
- Replace `IFiddlerExtension` with `IAppExtension`.
- Replace `ProfferFormat` with `OfferFormat`
- Replace timers `FiddlerGotRequestHeaders` with `ClearinetGotRequestHeaders`
- Replace timers' `FiddlerBeginRequest` with `ClearinetBeginRequest`
- Replace timers' `ServerGotRequest` with `ClearinetEndRequest`
- Replace timers' `FiddlerGotResponseHeaders` with `ClearinetGotResponseHeaders`
- Replace `ProgressCallbackEventArgs` with `ProgressEventArgs`
- Replace `Log.LogString` with `Log.Log`
- Replace `Array.Empty<byte>()` with `Array.Empty<byte>()`
- Remove `SessionTimers.EnableHighResolutionTimers` (the app now checks CApp.Prefs.GetBoolPref("app.high_resolution_clock", true) at boot time)
- IHandleExecAction is not yet implemented.
- `IAutoTamper2` and `IAutoTamper3` were merged into the `IAutoTamper` interface. Implementers should leave the Peek methods unimplemented if they aren't needed.
- 

### Paths
- Replace `Fiddler.exe` with `Clearinet.exe`.
- Extensions are loaded from the \Extensions\ subfolders of the app's folder and the user's Documents\Clearinet folder (Fiddler used a `Scripts` subfolder)
- Extension DLLs filenames must start with "CAE-" (ClearinetAppExtension) to load.

### Namespace Mappings
- Replace `using Fiddler;` with `using Clearinet`

### Preference names
- Preference names that start with "fiddler.ui" should be changed to start with "app.ui"

### Threading implications
Fiddler's UI would set `CheckForIllegalCrossThreadCalls=false` which would allow any thread to manipulate UI elements, potentially causing corruption. Clearinet's UI does not set this, meaning that any cross-thread call will result in an immediate exception. 
To fix this, code should call UIInvokeAsync if Winforms UI interaction is needed.

