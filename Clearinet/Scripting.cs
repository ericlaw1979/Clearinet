using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Clearinet
{
    internal class JScriptEngine : IDisposable
    {
        private readonly string _scriptPath;
        private readonly FileSystemWatcher _watcher;
        // TODO: Do we actually need this? Do we have multi-threading concerns to worry about?
        // The script engine is only called from the main UI thread, and the FileSystemWatcher
        // events are also raised on the main UI thread due to our use of SynchronizingObject.
        private readonly object _syncLock = new object();

        private Assembly _compiledAssembly;
        /// <summary>
        /// The Handlers class from the rules file.
        /// </summary>
        private Type _typeHandlers;
        private bool _isDisposed;


        /// <summary>
        /// The Script is ready after successful compilation.
        /// </summary>
        private bool _isReadyForCalls = false;

        public event RulesBeforeCompileHandler BeforeRulesCompile;

        public event RulesAfterCompileHandler AfterRulesCompile;

        public event RulesCompileFailedHandler RulesCompileFailed;

        /// <summary>
        /// Simple dictionary which maps script-created menu items to their backing fields or methods. 
        /// Used for cleanup when scripts retire.
        /// Note: Values may be null (MenuExt), a FieldInfo, or a MethodInfo. Don't see much benefit in converting to several Dictionary objects...
        /// </summary>
        private static Hashtable htMenuScripts = new Hashtable();

        private static Hashtable htButtonScripts = new Hashtable();

        private static Dictionary<FieldInfo, string> dictFieldBackingPrefs = new Dictionary<FieldInfo, string>();

        private List<BindUITab> listBoundTabs = new List<BindUITab>();
        private List<BindUIColumn> listBoundColumns = new List<BindUIColumn>();

        private List<BindTextCodec> listBoundTextCodecs = new List<BindTextCodec>();
        private List<PreferenceBag.PrefWatcher> listWeaklyHeldWatchers = new List<PreferenceBag.PrefWatcher>();

        private Action<Exchange> _methodOnBeforeRequest;
        private Action<Exchange> _methodOnBeforeResponse;
        // TODO: WebSockets Not yet implemented private Action<WebSocketMessage> _methodOnWebSocketMessage;
        private Action<Exchange> _methodOnPeekAtRequestHeaders;
        private Action<Exchange> _methodOnPeekAtResponseHeaders;
        private Action<Exchange> _methodOnReturningError;
        private Action<Exchange> _methodOnExchangeCompleted;

        private MethodInfo _methodExecAction;

        private static UInt64 tcLatestScriptLoad = 0;

        public JScriptEngine(string scriptPath)
        {
            if (CONFIG.Overrides.DisableScript)
            {
                CApp.Log.Log($"Script engine is disabled by command-line argument.");
                return;
            }
            try
            {
                Debug.Assert(scriptPath.HasText());
                _scriptPath = Path.GetFullPath(scriptPath);
                string directory = Path.GetDirectoryName(_scriptPath);
                string fileName = Path.GetFileName(_scriptPath);

                // If we boot and the script file doesn't exist, copy the sample script to the expected location.
                // Ensure we do this BEFORE trying to set up the watchers to ensure the folders are created or
                // else adding the watcher will fail with a DirectoryNotFoundException.
                if (!File.Exists(_scriptPath))
                {
                    CApp.Log.Log($"Script file not found: {_scriptPath}; creating from default sample.");
                    Directory.CreateDirectory(directory);
                    File.Copy(CONFIG.GetPath("SampleRulesScript"), _scriptPath);
                }

                try
                {
                    _watcher = new FileSystemWatcher(directory, fileName)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                        SynchronizingObject = CApp.UI, // Ensure events are raised on the UI thread
                        EnableRaisingEvents = true
                    };

                    // FileSystemWatcher frequently fires multiple events for a single save.
                    // We handle this using a debounced event handler.
                    _watcher.Changed += OnScriptFileChanged;
                }
                catch (Exception ex)
                {
                    CApp.ReportException(ex, "Failed to initialize script file watcher",
                        $"There was a problem initializing the script file watcher for {PlatformAPI.CompactPath(_scriptPath, 50)}.");
                }

                if (File.Exists(_scriptPath))
                {
                    CompileScript();
                }
            }
            catch (Exception ex)
            {
                CApp.ReportException(ex, "Failed to initialize script engine",
                    $"There was a problem initializing the script engine for {PlatformAPI.CompactPath(_scriptPath, 50)}.");
            }
        }

        private void OnScriptFileChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce rapid successive events.
            if ((Utilities.GetTickCount() - tcLatestScriptLoad) <
                (uint)CApp.Prefs.GetInt32Pref("scripting.msReloadDebounce", 2000))
            {
                return;
            }
            tcLatestScriptLoad = Utilities.GetTickCount();

            // Wait briefly for any editor write handles to release
            Thread.Sleep(50);

            lock (_syncLock)
            {
                try
                {
                    BeforeRulesCompile?.Invoke(_scriptPath);
                    CompileScript();
                    AfterRulesCompile?.Invoke();
                }
                catch (Exception ex)
                {
                    // TODO: Need to extract line/column info from the exception
                    RulesCompileFailed?.Invoke(ex.Message, 0, 0, 0);
                }
            }
        }

        private void CompileScript()
        {
            try
            {
                CApp.Log.Log($"CompileScript(`{_scriptPath}`");
                // Clear any prior script state before compiling the new script.
                if (_isReadyForCalls)
                {
                    _ClearCachedHandlerMethods();
                    ClearPriorScriptButtons(CApp.UI.tsToolbar);
                    ClearPriorScriptTabs();

                    // TODO: The old script engine is still alive and clearing here doesn't
                    // drop the reference to the old script. TODO: Notify any open textransform to refresh?
                    listBoundTextCodecs.Clear();
                    //listWeaklyHeldWatchers.ForEach(w => w.Dispose());
                }
                _isReadyForCalls = false;
                string scriptContent;
                using (var stream = new FileStream(_scriptPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    scriptContent = reader.ReadToEnd();
                }
                // TODO: What if scriptContent is empty? Should we treat that as a valid script with no handlers, or throw an error?

                var provider = new Microsoft.JScript.JScriptCodeProvider();
                var compilerParameters = new System.CodeDom.Compiler.CompilerParameters
                {
                    GenerateInMemory = true,
                    // We must GenerateExecutable for global script statements/functions to compile properly
                    GenerateExecutable = true
                };

                #region ADD_REFERENCES
                // Add all assemblies that Rules can use.
                compilerParameters.ReferencedAssemblies.Add("System.dll");
                compilerParameters.ReferencedAssemblies.Add("System.Windows.Forms.dll");
                compilerParameters.ReferencedAssemblies.Add(Assembly.GetExecutingAssembly().Location); // Clearinet

                // TODO: Document this preference and the SECURITY IMPACT of unqualified paths.
                String[] slAdditionalAssemblies = CApp.Prefs.GetStringPref("scripting.references", String.Empty).Split(';');
                foreach (string sPath in slAdditionalAssemblies)
                {
                    string sPathTrimmed = sPath.Trim();
                    if (String.IsNullOrEmpty(sPathTrimmed)) continue;
                    compilerParameters.ReferencedAssemblies.Add(sPathTrimmed);
                }
                #endregion ADD_REFERENCES

                // Compile the script.
                CompilerResults results = provider.CompileAssemblyFromSource(compilerParameters, scriptContent);

                if (results.Errors.HasErrors)
                {
                    var errorMsg = "JScript Compilation Errors:\n";
                    foreach (System.CodeDom.Compiler.CompilerError error in results.Errors)
                    {
                        errorMsg += $"Line {error.Line}, Col {error.Column}: {error.ErrorText}\n";
                        //RulesCompileFailed?.Invoke(ex.Message, error.Line, error.Column, error.Column + 1); // TODO: Determine end column if possible
                    }

                    throw new InvalidOperationException(errorMsg); // TODO: Somebody needs to catch this!
                }

                _compiledAssembly = results.CompiledAssembly;

                _typeHandlers = _compiledAssembly.GetType("Handlers");
                _isReadyForCalls = (null != _typeHandlers);
                if (_isReadyForCalls)
                {
                    _bindUIElements();
                    _cacheHandlerMethods();

                    CApp.Log.Log($"Script compilation succeeded.");

                    _RunMainMethod();
                }
            }
            catch (Exception ex)
            {
                _isReadyForCalls = false;
                _ClearCachedHandlerMethods();
                CApp.ReportException(ex, "Script Failed to Compile",
                    $"There appears to be an error in your script file ({PlatformAPI.CompactPath(_scriptPath, 50)}).");
            }
        }

        private void _bindUIElements()
        {
            // This method iterates through all public static fields in the Handlers class,
            // looking for functions that have a BindUIButton attribute. For each such function found, it inserts a
            // new button at the start (left) of the CApp.UI toolbar, and binds the button's click event to the specified method in the script.
            // an example looks like:
            //   public static BindUIButton("SingleBrowserMode \uD83D\uDC40")
            //   function LaunchSingleInstance() {
            //   Utilities.LaunchNative('msedge.exe', '--user-data-dir="%temp%\\throwaway" --no-first-run --proxy-server=127.0.0.1:' + CONFIG.ListenPort.ToString() + " about:blank");
            //
            var tsToolbar = CApp.UI.tsToolbar;

            // TODO: iterate the methods only once and look at what attributes they support.
            foreach (MethodInfo mi in _typeHandlers.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var b = Attribute.GetCustomAttribute(mi, typeof(BindTextCodec), false) as BindTextCodec;
                if (null == b) continue;

                MethodInfo methodToInvoke = mi;
                // 1. Validate that the method signature matches: string MethodName(string input)
                if (mi.ReturnType != typeof(string)) continue;

                ParameterInfo[] parameters = mi.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != typeof(string)) continue;

                // 2. Create a strongly-typed delegate matching Func<string, string>
                Func<string, string> CodecMethod =
                    (Func<string, string>)Delegate.CreateDelegate(typeof(Func<string, string>), mi);

                b.Method = CodecMethod;

                listBoundTextCodecs.Add(b);
            }

            foreach (MethodInfo mi in _typeHandlers.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                BindUIColumn b = Attribute.GetCustomAttribute(mi, typeof(BindUIColumn), false) as BindUIColumn;
                if (null == b) continue;

                MethodInfo methodToInvoke = mi;
                // 1. Validate that the method signature matches: string MethodName(Exchange exchange)
                if (mi.ReturnType != typeof(string)) continue;

                ParameterInfo[] parameters = mi.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != typeof(Exchange)) continue;

                // 2. Create a strongly-typed delegate matching Func<Exchange, string>
                Func<Exchange, string> columnProvider =
                    (Func<Exchange, string>)Delegate.CreateDelegate(typeof(Func<Exchange, string>), mi);

                // 3. Register the delegate with your ListView/UI helper
                CApp.UI.lvExchanges.AddBoundColumn(
                    b._sTitle,
                    b._sortNumerically ? ColumnSortType.Numeric : ColumnSortType.Text,
                    columnProvider
                );


                listBoundColumns.Add(b);
            }

            foreach (MethodInfo mi in _typeHandlers.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                BindUIButton b = Attribute.GetCustomAttribute(mi, typeof(BindUIButton), false) as BindUIButton;
                if (null == b) continue;

                MethodInfo methodToInvoke = mi;
                ToolStripButton tsb = new ToolStripButton
                {
                    Text = b._sText,
                    DisplayStyle = ToolStripItemDisplayStyle.Text,
                    Tag = methodToInvoke
                };

                tsb.Click += (sender, args) =>
                {
                    try
                    {
                        methodToInvoke.Invoke(null, null);
                    }
                    catch (Exception eX)
                    {
                        CApp.ReportException(eX, $"Script toolbar action failed: {methodToInvoke.Name}");
                    }
                };

                tsToolbar.Items.Insert(tsToolbar.Items.IndexOf(CApp.UI.tssAfterExt), tsb);
                htButtonScripts[tsb] = methodToInvoke;
            }

            // Bind each of the BindUITabs.
            foreach (MethodInfo mi in _typeHandlers.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                BindUITab tab = Attribute.GetCustomAttribute(mi, typeof(BindUITab), false) as BindUITab;
                if (null == tab) continue;

                TabPage page = new TabPage
                {
                    Text = tab._sTitle,
                    Tag = tab
                };

                // TODO: Support HTML tabs. For now, we just create a RichTextBox tab for each BindUITab.
                RichTextBoxV5 rtb = new RichTextBoxV5
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    BackColor = CONFIG.colorDisabledEdit,
                    WordWrap = false,
                    BorderStyle = BorderStyle.None
                };
                page.Controls.Add(rtb);

                CalculateReportHandler reportHandler = (arrExchanges) =>
                {
                    try
                    {
                        if (page == CApp.UI.tabsViews.SelectedTab)
                        {
                            object oResult = mi.Invoke(null, new object[] { arrExchanges });
                            rtb.Text = oResult as string ?? String.Empty;
                        }
                    }
                    catch (Exception eX)
                    {
                        CApp.ReportException(eX, $"Script tab report failed: {mi.Name}");
                    }
                };

                tab._delegate = reportHandler;
                tab._pageTab = page;
                CApp.CalculateReport += reportHandler;
                CApp.UI.tabsViews.TabPages.Add(page);
                listBoundTabs.Add(tab);
            }

        }

        private void ClearPriorScriptTabs()
        {
            foreach (BindUITab tab in listBoundTabs)
            {
                if (null != tab._delegate) CApp.CalculateReport -= tab._delegate;
                if (null != tab._pageTab)
                {
                    CApp.UI.tabsViews.TabPages.Remove(tab._pageTab);
                    tab._pageTab.Dispose();
                }
            }
            listBoundTabs.Clear();
        }

        private void ClearPriorScriptButtons(ToolStrip tsToolbar)
        {
            // First, clear any existing buttons that were added by a previous script.
            foreach (DictionaryEntry de in htButtonScripts)
            {
                if (de.Key is ToolStripItem tsi)
                {
                    tsToolbar.Items.Remove(tsi);
                    tsi.Dispose();
                }
            }
            htButtonScripts.Clear();
        }

        private void _cacheHandlerMethods()
        {
            _methodOnBeforeRequest = _typeHandlers.GetMethod("OnBeforeRequest")?.CreateDelegate(typeof(Action<Exchange>)) as Action<Exchange>;
            _methodOnBeforeResponse = _typeHandlers.GetMethod("OnBeforeResponse")?.CreateDelegate(typeof(Action<Exchange>)) as Action<Exchange>;
            _methodOnPeekAtRequestHeaders = _typeHandlers.GetMethod("OnPeekAtRequestHeaders")?.CreateDelegate(typeof(Action<Exchange>)) as Action<Exchange>;
            _methodOnPeekAtResponseHeaders = _typeHandlers.GetMethod("OnPeekAtResponseHeaders")?.CreateDelegate(typeof(Action<Exchange>)) as Action<Exchange>;
            _methodOnReturningError = _typeHandlers.GetMethod("OnReturningError")?.CreateDelegate(typeof(Action<Exchange>)) as Action<Exchange>;
            _methodOnExchangeCompleted = _typeHandlers.GetMethod("OnExchangeCompleted")?.CreateDelegate(typeof(Action<Exchange>)) as Action<Exchange>;
            _methodExecAction = _typeHandlers.GetMethod("OnExecAction");
        }

        private void _ClearCachedHandlerMethods()
        {
            _methodExecAction = null;
            _methodOnBeforeRequest = _methodOnBeforeResponse = _methodOnPeekAtRequestHeaders
                = _methodOnPeekAtResponseHeaders = _methodOnReturningError = _methodOnExchangeCompleted = null;
            //_methodOnWebSocketMessage = null;
        }

        internal void RunOnBeforeRequest(Exchange exchange)
        {
            _methodOnBeforeRequest?.Invoke(exchange);
        }
        internal void RunOnBeforeResponse(Exchange exchange)
        {
            _methodOnBeforeResponse?.Invoke(exchange);
        }

        private void _RunMainMethod()
        {
            try
            {
                _typeHandlers.GetMethod("Main")?.Invoke(null, null);
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "JScript main() failed.", "There was a problem with your script.");
            }
        }

        public bool CallMethod(string sMethodName)
        {
            return CallMethod(sMethodName, null, out _);
        }

        public bool CallMethod(string sMethodName, object[] arrArgs, out object oRes)
        {
            oRes = null;
            if (!this._isReadyForCalls) return false;
            Debug.Assert(null != this._typeHandlers);

            try
            {
                MethodInfo method = _typeHandlers.GetMethod(sMethodName);
                if (null == method) return false;  // Didn't find it.

                oRes = method.Invoke(null, arrArgs);
                return true;
            }
            catch (Exception eX)
            {
                CApp.DoNotifyUser("Error in your Script.\n\n" +
                    eX.Message + "\n" + eX.StackTrace + "\n\n" + eX.InnerException, $"Error calling {sMethodName}");
            }
            return false;
        }

        internal bool RunExecAction(string[] arrArgs)
        {
            if (!_isReadyForCalls || (null == _methodExecAction)) return false;
            try
            {
                if (arrArgs.Length > 0)
                {
                    object o = _methodExecAction.Invoke(null, new object[] { arrArgs });
                    if (o is bool v) return v;
                    return false;
                }

                return false;
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "QuickExec failure");
                return false;
            }
        }

        internal string[] AvailableTextCodecs()
        {
            return listBoundTextCodecs.ConvertAll(b => b.Name).ToArray();
        }

        internal bool RunTextCodec(string codecName, string input, out string sOutput, out byte[] arrLatestOutput)
        {
            sOutput = string.Empty;
            arrLatestOutput = null;
            if (!_isReadyForCalls) return false;
            Debug.Assert(null != this._typeHandlers);
            foreach (BindTextCodec b in listBoundTextCodecs)
            {
                if (b.Name.Equals(codecName, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        sOutput = b.Method.Invoke(input);
                        return true;
                    }
                    catch (Exception eX)
                    {
                        CApp.DoNotifyUser("Error in your Script.\n\n" +
                            eX.Message + "\n" + eX.StackTrace + "\n\n" + eX.InnerException, $"Error calling {b.Name}");
                        return false;
                    }
                }
            }
            return false;
        }

        /*
        /// <summary>
        /// Executes a static method defined inside the script file.
        /// </summary>
        /// <param name="methodName">Name of the function/method in the script.</param>
        /// <param name="args">Parameters to pass to the function.</param>
        /// <returns>Return value from the script function.</returns>
        public object ExecuteMethod(string methodName, params object[] args)
        {
            if (!_isReadyForCalls)
                throw new InvalidOperationException("Script is not ready for calls. Ensure it compiled successfully.");
            lock (_syncLock)
            {
                MethodInfo method = _typeHandlers.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                    throw new MissingMethodException($"Method '{methodName}' was not found in the script.");
                return method.Invoke(null, args);
            }
        }*/

        internal void DoOnBoot()
        {
            CallMethod("OnBoot");
        }
        internal void DoOnAttach()
        {
            CallMethod("OnAttach");
        }
        internal void DoOnDetach()
        {
            CallMethod("OnDetach");
        }
        internal void DoOnShutdown()
        {
            CallMethod("OnShutdown");
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _watcher?.Dispose();
                _isDisposed = true;
            }
        }

    }
}