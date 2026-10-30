using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Clearinet
{
    // TODO: This is only public because we don't yet have a facade for script/extension access to the Clearinet API.
    // Once we have a proper facade, this class should be internal.
    public class JScriptEngine : IDisposable
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
        /// Tracks script-created menu items and their backing methods for removal on reload.
        /// </summary>
        private static readonly Dictionary<ToolStripItem, MethodInfo> htMenuScripts = new Dictionary<ToolStripItem, MethodInfo>();

        private static readonly Dictionary<ToolStripItem, MethodInfo> htButtonScripts = new Dictionary<ToolStripItem, MethodInfo>();

        private static readonly Dictionary<FieldInfo, string> dictFieldBackingPrefs = new Dictionary<FieldInfo, string>();

        private readonly List<BindUITab> listBoundTabs = new List<BindUITab>();
        private readonly List<ColumnHeader> listBoundColumns = new List<ColumnHeader>();

        private readonly List<BindTextCodec> listBoundTextCodecs = new List<BindTextCodec>();
        private readonly List<PreferenceBag.PrefWatcher> listWeaklyHeldWatchers = new List<PreferenceBag.PrefWatcher>();

        private Action<Exchange> _methodOnBeforeRequest;
        private Action<Exchange> _methodOnBeforeResponse;
        // TODO: WebSockets Not yet implemented private Action<WebSocketMessage> _methodOnWebSocketMessage;
        private Action<Exchange> _methodOnPeekAtRequestHeaders;
        private Action<Exchange> _methodOnPeekAtResponseHeaders;
        private Action<Exchange> _methodOnReturningError;
        private Action<Exchange> _methodOnExchangeCompleted;

        private MethodInfo _methodExecAction;

        private static ulong tcLatestScriptLoad = 0;
        internal JScriptEngine(string scriptPath)
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

        public void ReloadScript()
        {
            tcLatestScriptLoad = Utilities.GetTickCount();

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

        private void OnScriptFileChanged(object sender, FileSystemEventArgs e)
        {
            // Wait briefly for any editor write handles to release
            Thread.Sleep(50);

            // Debounce rapid successive events.
            if ((Utilities.GetTickCount() - tcLatestScriptLoad) <
                (uint)CApp.Prefs.GetInt32Pref("scripting.msReloadDebounce", 2000))
            {
                return;
            }
            ReloadScript();
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
                    ClearPriorScriptColumns();
                    ClearPriorScriptButtons();
                    ClearPriorScriptMenus();
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
                string[] slAdditionalAssemblies = CApp.Prefs.GetStringPref("scripting.references", string.Empty).Split(';');
                foreach (string sPath in slAdditionalAssemblies)
                {
                    string sPathTrimmed = sPath.Trim();
                    if (string.IsNullOrEmpty(sPathTrimmed)) continue;
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
            // looking for functions that have a Bind* attribute. e.g.
            //   public static BindUIButton("SingleBrowserMode \uD83D\uDC40")
            //   function LaunchSingleInstance() {
            //   Utilities.LaunchNative('msedge.exe', '--user-data-dir="%temp%\\throwaway" --no-first-run --proxy-server=127.0.0.1:' + CONFIG.ListenPort.ToString() + " about:blank");
            //

            // Look at each public static method in the Handlers class and bind targets based on the attributes we find.
            foreach (MethodInfo method in _typeHandlers.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                BindTextCodec textCodec = method.GetCustomAttribute<BindTextCodec>(false);
                if (textCodec != null) { doBindTextCodec(method, textCodec); continue; }

                BindUIColumn column = method.GetCustomAttribute<BindUIColumn>(false);
                if (column != null) { doBindUIColumn(method, column); continue; }

                BindUIButton button = method.GetCustomAttribute<BindUIButton>(false);
                if (button != null) { doBindUIButton(method, button); continue; }

                BindUITab tab = method.GetCustomAttribute<BindUITab>(false);
                if (tab != null) { doBindUITab(method, tab); continue; }

                ContextAction contextAction = method.GetCustomAttribute<ContextAction>(false);
                if (contextAction != null) { doBindUIContextAction(method, contextAction); continue; }

                ToolsAction toolsAction = method.GetCustomAttribute<ToolsAction>(false);
                if (toolsAction != null) { doBindUIToolsAction(method, toolsAction); continue; }

                QuickLinkMenu quickLinkMenu = method.GetCustomAttribute<QuickLinkMenu>(false);
                if (quickLinkMenu != null) { doBindUIQuickLinkMenu(method, quickLinkMenu); continue; }
            }
        }

        private void doBindUIQuickLinkMenu(MethodInfo mi, QuickLinkMenu b)
        {
            if ((mi.GetParameters().Length != 2) || ((mi.GetParameters())[0].ParameterType != typeof(string)) ||
                                                    ((mi.GetParameters())[1].ParameterType != typeof(string)))
            {
                CApp.Log.LogFormat($"!Warning: QuickLinkMenu `{b.MenuTitle}`must defined as `function(sText: string, sAction: string)`. Skipping.");
                return;
            }
            QuickLinkItem[] arrQLI = (QuickLinkItem[])Attribute.GetCustomAttributes(mi, typeof(QuickLinkItem));
            if (arrQLI?.Length < 1)
            {
                CApp.Log.LogFormat($"!Warning: QuickLinkMenu `{b.MenuTitle}` has no QuickLinkItem attributes defined on `{mi.Name}`. Skipping.");
                return;
            }
            else
            {
                Array.Sort<QuickLinkItem>(arrQLI);
            }

            ToolStripMenuItem tsmi = new ToolStripMenuItem
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Text = b.MenuTitle,
                Tag = mi
            };

            CApp.UI.AddExtensionTopLevelMenu(tsmi);
            htMenuScripts.Add(tsmi, null);

            ToolStripMenuItem tsmiItem;
            foreach (QuickLinkItem qli in arrQLI)
            {
                tsmiItem = new ToolStripMenuItem(qli.ItemText);
                tsmiItem.Tag = qli.ItemAction;
                tsmi.DropDownItems.Add(tsmiItem);
                htMenuScripts.Add(tsmiItem, mi);
                tsmiItem.Click += new EventHandler(doQuickLinkClick);
            }
        }

        private void doQuickLinkClick(object sender, EventArgs e)
        {
            try
            {
                ToolStripMenuItem tsmi = (ToolStripMenuItem)sender;
                MethodInfo mi = (MethodInfo)htMenuScripts[tsmi];
                mi.Invoke(null, new object[] { tsmi.Text, tsmi.Tag as String });
            }
            catch (Exception eX)
            {
                if ((eX is TargetInvocationException) && (eX.InnerException != null)) eX = eX.InnerException;
                CApp.ReportException(eX, "Your QuickLink function failed.");
            }
        }

        private void doBindUIContextAction(MethodInfo mi, ContextAction b)
        {
            ParameterInfo[] parameters = mi.GetParameters();
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(Exchange[]))
            {
                CApp.Log.Log($"!Warning: BindUIColumn({b._sTitle}) function must be defined as `function(xc: Exchange[]):`. Skipping.");
                return;
            }

            ToolStripMenuItem tsmi = new ToolStripMenuItem
            {
                Text = b._sTitle,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Tag = mi
            };

            tsmi.Click += (sender, ea) =>
            {
                try
                {
                    mi.Invoke(null, new object[] { CApp.UI.GetSelectedExchanges() });
                }
                catch (Exception eX)
                {
                    CApp.ReportException(eX, $"Script ContextAction failed: {mi.Name}");
                }
            };
            CApp.UI.lvExchanges.AddExtensionMenuItem(tsmi);
            htMenuScripts[tsmi] = mi;
        }

        private void doBindUIToolsAction(MethodInfo mi, ToolsAction b)
        {
            ParameterInfo[] parameters = mi.GetParameters();
            b.bPassSelectedExchanges = (parameters.Length == 1 && parameters[0].ParameterType == typeof(Exchange[]));

            ToolStripMenuItem tsmi = new ToolStripMenuItem
            {
                Text = b._sTitle,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Tag = mi
            };

            tsmi.Click += (sender, ea) =>
            {
                try
                {
                    if (b.bPassSelectedExchanges)
                    {
                        mi.Invoke(null, new object[] { CApp.UI.GetSelectedExchanges() });
                    }
                    else
                    {
                        mi.Invoke(null, null);
                    }
                }
                catch (Exception eX)
                {
                    CApp.ReportException(eX, $"Script ToolsAction failed: {mi.Name}");
                }
            };
            CApp.UI.AddExtensionToolsMenuItem(tsmi);
            htMenuScripts[tsmi] = mi;
        }

        private void doBindTextCodec(MethodInfo mi, BindTextCodec b)
        {
            ParameterInfo[] parameters = mi.GetParameters();
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(string) || mi.ReturnType != typeof(string))
            {
                CApp.Log.Log($"!Warning: BindTextCode({b.Name}) function must be defined as `function(from:string):string`. Skipping.");
                return;
            }

            Func<string, string> CodecMethod =
                (Func<string, string>)Delegate.CreateDelegate(typeof(Func<string, string>), mi);

            b.Method = CodecMethod;

            listBoundTextCodecs.Add(b);
        }

        private void doBindUIButton(MethodInfo mi, BindUIButton b)
        {
            ToolStripButton tsb = new ToolStripButton
            {
                Text = b._sText,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Tag = mi
            };

            tsb.Click += (sender, args) =>
            {
                try
                {
                    mi.Invoke(null, null);
                }
                catch (Exception eX)
                {
                    CApp.ReportException(eX, $"Script toolbar action failed: {mi.Name}");
                }
            };
            var tsToolbar = CApp.UI.tsToolbar;
            tsToolbar.Items.Insert(tsToolbar.Items.IndexOf(CApp.UI.tssAfterExt), tsb);
            htButtonScripts[tsb] = mi;
        }

        private void doBindUIColumn(MethodInfo mi, BindUIColumn b)
        {
            ParameterInfo[] parameters = mi.GetParameters();
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(Exchange) || mi.ReturnType != typeof(string))
            {
                CApp.Log.Log($"!Warning: BindUIColumn({b._sTitle}) function must be defined as `function(xc: Exchange):string`. Skipping.");
                return;
            }

            Func<Exchange, string> columnProvider =
                (Func<Exchange, string>)Delegate.CreateDelegate(typeof(Func<Exchange, string>), mi);

            ColumnHeader column = CApp.UI.lvExchanges.AddBoundColumn(
                b._sTitle,
                b._sortNumerically ? ColumnSortType.Numeric : ColumnSortType.Text,
                columnProvider
            );

            listBoundColumns.Add(column);
        }

        private void ClearPriorScriptColumns()
        {
            foreach (ColumnHeader column in listBoundColumns)
            {
                CApp.UI.lvExchanges.RemoveBoundColumn(column);
            }
            listBoundColumns.Clear();
        }

        private void doBindUITab(MethodInfo mi, BindUITab b)
        {
            ParameterInfo[] parameters = mi.GetParameters();
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(Exchange[]) || (mi.ReturnType != typeof(string)))
            {
                CApp.Log.LogFormat($"!Warning: BindUITab `{b._sTitle}` must be defined as `function(arrXC: Exchange[]):string`. Skipping.");
                return;
            }

            listBoundTabs.Add(b);

            // If the tab was marked as hidden, we don't create a tab page, but instead
            // create a menu item that will create the tab on demand.
            if (b._sOptions?.OICContains("<hidden>") == true)
            {
                // Remove the option so we don't keep it hidden if the user asks for it.
                b._sOptions = b._sOptions.Replace("<hidden>", string.Empty);
                b._tsmi = new ToolStripMenuItem(b._sTitle);
                b._tsmi.Click += (s, ea) =>
                {
                    if (null == b._pageTab) doBindUITab(mi, b);
                    CApp.UI.tabsViews.SelectedTab = b._pageTab;
                    return;
                };
                CApp.UI.AddViewExtraTabsMenuItem(b._tsmi);
                return;
            }

            TabPage page = new TabPage
            {
                Text = b._sTitle,
                Tag = b
            };

            RichTextBoxV5 rtb = new RichTextBoxV5
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = CONFIG.colorDisabledEdit,
                BorderStyle = BorderStyle.None
            };
            rtb.WordWrap = !(b._sOptions?.OICContains("<nowrap>") == true);
            if (b._sOptions?.OICContains("<fixedwidth>") == true) rtb.Font = new System.Drawing.Font("Consolas", 10f);
            if (b._sOptions?.OICContains("<nolink>") == true)
                rtb.DetectUrls = false;
            else
                rtb.LinkClicked += (s, e) => Utilities.LaunchHyperlink(e.LinkText);
            if (b._sOptions?.OICContains("<html>") == true) CApp.Log.Log($"!Script tab `{b._sTitle}` is marked as HTML but this is not yet supported.");
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

            b._delegate = reportHandler;
            b._pageTab = page;
            CApp.CalculateReport += reportHandler;
            CApp.UI.tabsViews.TabPages.Add(page);
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
                if (null != tab._tsmi)
                {
                    tab._tsmi.GetCurrentParent()?.Items.Remove(tab._tsmi);
                    tab._tsmi.Dispose();
                }
            }
            listBoundTabs.Clear();
        }

        private void ClearPriorScriptButtons()
        {
            // First, clear any existing buttons that were added by a previous script.
            foreach (ToolStripItem item in htButtonScripts.Keys)
            {
                item.GetCurrentParent()?.Items.Remove(item);
                item.Dispose();
            }
            htButtonScripts.Clear();
        }

        private void ClearPriorScriptMenus()
        {
            foreach (ToolStripItem item in htMenuScripts.Keys)
            {
                item.GetCurrentParent()?.Items.Remove(item);
                item.Dispose();
            }
            htMenuScripts.Clear();
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
        internal void RunOnExchangeCompleted(Exchange exchange)
        {
            _methodOnExchangeCompleted?.Invoke(exchange);
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
            // TODO: Do we tear down the Bound UI, or are we just gonna let the app exit?
            if (!_isDisposed)
            {
                _watcher?.Dispose();
                _isDisposed = true;
            }
        }

    }
}