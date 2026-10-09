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
            if (string.IsNullOrWhiteSpace(scriptPath))
                throw new ArgumentNullException(nameof(scriptPath));

            _scriptPath = Path.GetFullPath(scriptPath);

            if (!File.Exists(_scriptPath))
                throw new FileNotFoundException("Script file not found.", _scriptPath);

            // Initial compile
            CompileScript();

            // Setup FileSystemWatcher for auto-reloading
            string directory = Path.GetDirectoryName(_scriptPath);
            string fileName = Path.GetFileName(_scriptPath);

            _watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                SynchronizingObject = CApp.UI, // Ensure events are raised on the UI thread
                EnableRaisingEvents = true
            };

            // FileSystemWatcher frequently fires multiple events for a single save.
            // We handle this using a debounced event handler.
            _watcher.Changed += OnScriptFileChanged;
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
                    ClearPriorScriptButtons(CApp.UI.tsToolbar);
                    ClearPriorScriptTabs();
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
                _RunMainMethod();
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

                tsToolbar.Items.Insert(0, tsb);
                htButtonScripts[tsb] = methodToInvoke;
            }


            // Bind each of the BindUITabs.
            foreach (MethodInfo mi in _typeHandlers.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                BindUITab tab = Attribute.GetCustomAttribute(mi, typeof(BindUITab), false) as BindUITab;
                if (null == tab) continue;

                TabPage page = new TabPage
                {
                    Text = tab._sTitle
                };

                // TODO: Support HTML tabs. For now, we just create a RichTextBox tab for each BindUITab.
                RichTextBoxV5 rtb = new RichTextBoxV5
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    BackColor = CONFIG.colorDisabledEdit,
                    WordWrap = false
                };
                page.Controls.Add(rtb);

                CalculateReportHandler reportHandler = (arrExchanges) =>
                {
                    try
                    {
                        object oResult = mi.Invoke(null, new object[] { arrExchanges });
                        rtb.Text = oResult as string ?? String.Empty;
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