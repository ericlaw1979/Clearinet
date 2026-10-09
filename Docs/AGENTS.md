### Class & Interface Mappings
- Replace `FiddlerApplication` with `CApp`.
- Replace `Session` with `Exchange` in almost all comments, variable names, class names, etc.
- Replace `IFiddlerExtension` with `IAppExtension`.
- Replace `ProfferFormat` with `OfferFormat`
- Replace `uriContains` with `urlContains`
- Replace timers' `FiddlerGotRequestHeaders` with `ProxyGotRequestHeaders`
- Replace timers' `FiddlerBeginRequest` with `ProxyBeginRequest`
- Replace timers' `ServerGotRequest` with `ProxyDoneRequest`
- Replace timers' `FiddlerGotResponseHeaders` with `ProxyGotResponseHeaders`
- Replace `ProgressCallbackEventArgs` with `ProgressEventArgs`
- Replace `Log.LogString` with `Log.Log`
- Replace `Utilities.IsNullOrEmpty()` with `HasText()` and `HasData()` extension methods on string and byte[] respectively.
- Replace `Utilities.emptyByteArray` with `Array.Empty<byte>()`
- Remove `SessionTimers.EnableHighResolutionTimers` (the app now checks CApp.Prefs.GetBoolPref("app.high_resolution_clock", true) at boot time)
- IHandleExecAction is not yet implemented.
- `IAutoTamper2` and `IAutoTamper3` were merged into the `IAutoTamper` interface. Implementers should leave the Peek methods unimplemented if they aren't needed.

- In Fiddler, each Inspector inherited from Inspector2 (an abstract base class) AND then implemented either 
  IRequestInspector2 or IResponseInspector2 to add an accessor for the proper type of Headers. In Clearinet,
  your Inspector extension simply inherits from either RequestInspectorBase or ResponseInspectorBase. 

### Paths
- Replace `Fiddler.exe` with `Clearinet.exe`.
- Extensions are loaded from the \Extensions\ subfolders of the app's folder and the user's Documents\Clearinet folder (Fiddler used a `Scripts` subfolder)
- Extension DLLs filenames must start with "CAE-" (ClearinetAppExtension) to load.
- Transcoder DLLs filenames must start with "CAT-" (ClearinetAppTranscoder) to load.

### Namespace Mappings
- Replace `using Fiddler;` with `using Clearinet`

### Preference names
- Preference names that start with `fiddler.ui` should be changed to start with `app.ui`

### Threading implications
Fiddler's UI would set `CheckForIllegalCrossThreadCalls=false` which would allow any thread to manipulate UI elements, potentially causing corruption. Clearinet's UI does not set this, meaning that any cross-thread call will result in an immediate exception. 
To fix this, code should call UIInvokeAsync if Winforms UI interaction is needed.

### Other deltas vs. Fiddler Classic
- Raw Inspector allows searching in case-sensitive manner with `exact:` prefix
- Attempting to create a Header with an empty name will now throw an exception. Fiddler would allow this.