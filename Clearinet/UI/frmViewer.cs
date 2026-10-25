using Clearinet.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Clearinet
{
    public partial class frmViewer : Form
    {
        public frmViewer()
        {
            InitializeComponent();
            Win32UI.SetCueText(txtLogSearch, "Search log (Enter to find next)");

            // TODO: This is a hack. Eventually we will be smarter.
            lvExchanges.ConfigureColumn(colID.Index, ColumnSortType.Numeric);
            lvExchanges.ConfigureColumn(colResult.Index, ColumnSortType.Numeric);
            lvExchanges.ConfigureColumn(colResponseSize.Index, ColumnSortType.Numeric);

            mruRecents = new MRU(CApp.Prefs.GetInt32Pref("ui.mru.max", 12), CONFIG.GetRegistryPath("MRU"));
            if (CONFIG.isViewerMode)
            {
                this.Text = $"Clearinet Viewer";
                tsmiNameViewer.Visible = true;
                this.lvExchanges.EmptyText = "No Exchanges are loaded";
                // TODO: Automatically name this viewer if another is already running.
                tssbCapture.Enabled = miFileAttach.Enabled = false;
            }
        }

        /// <summary>
        /// Prevent UI flashing. The degree to which this helps is AMAZING.
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }

        /// <summary>
        /// Used to tell the user to Select one or more Exchanges to inspect
        /// </summary>
        private Label lblInspectorInstruction;
        private readonly MRU mruRecents;
        private NotifyIcon trayIcon;
        private FormWindowState trayRestoreState;

        private readonly static System.Windows.Forms.Timer timerReportUpdater = new System.Windows.Forms.Timer();

        private void miFileExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void miHelpAbout_Click(object sender, EventArgs e)
        {
            frmAbout f = new frmAbout();
            f.ShowDialog(this);
        }

        #region ACTIONS
        private void actMinimizeToTray()
        {
            if (trayIcon == null)
            {
                trayIcon = new NotifyIcon(components) { Icon = this.Icon };
                trayIcon.MouseClick += trayIcon_MouseClick;
            }
            if (!trayIcon.Visible)
            {
                trayRestoreState = WindowState == FormWindowState.Minimized ? FormWindowState.Normal : WindowState;
            }
            UpdateTrayTooltip();
            trayIcon.Visible = true;
            WindowState = FormWindowState.Minimized;
            Hide();
        }

        private void UpdateTrayTooltip()
        {
            trayIcon.Text = $"Clearinet Web Debugger\nCapture: {(CApp.isAttached ? "Attached" : "Not Attached")}\nExchanges: {lvExchanges.TotalCount:N0}";
        }

        private void trayIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            actActivateMainWindow();
        }

        private void actActivateMainWindow()
        {
            if (trayIcon != null && trayIcon.Visible)
            {
                Show();
                WindowState = trayRestoreState;
                trayIcon.Visible = false;
            }
            if (!Win32UI.BringWindowToForeground(Handle))
                CApp.Log.Log("Unable to bring the main Clearinet window to the foreground.");
        }

        /// <summary>
        /// Flashes the window once without activating it.
        /// </summary>
        public void actFlashWindow()
        {
            // TODO: Should we have a variant of this that calls FlashWindowEx to demand the user activate the window to stop the flashing?
            // fwi.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG; // Flash taskbar & caption until window comes to foreground
            Win32UI.FlashWindow(Handle, true);
        }

        public void actActivateTab(string sTabTitle)
        {
            Win32UI.activateTitledTab(sTabTitle, tabsViews);
        }

        public void actSelectExchangesMatchingCriteria(Func<Exchange, bool?> oCriteriaChecker, int iMatchAtMost = Int32.MaxValue, bool bClearSelectionFirst = true)
        {
            CApp.PauseReporting = true;
            lvExchanges.BeginUpdate();
            if (bClearSelectionFirst) lvExchanges.SelectedItems.Clear();
            int iMatches = 0;
            foreach (ListViewItem lvi in lvExchanges.Items)
            {
                Exchange x = lvi.Tag as Exchange;
                if (oCriteriaChecker(x) == true)
                {
                    ++iMatches;
                    lvi.Selected = true;
                    if (iMatches >= iMatchAtMost) break;
                }
            }
            lvExchanges.EndUpdate();

            CApp.PauseReporting = false;
            actUpdateInspector(true, true);
        }

        public void actShowTextransform(string sText)
        {
            frmTextWizard oFrm = new frmTextWizard(sText)
            {
                Left = this.Left + 100,
                Top = this.Top + 100
            };
            oFrm.Show(this);
        }
        // Old name for backwards compatibility with scripts that call this method.
        public void actShowTextWizard(string sText)
        {
            actShowTextransform(sText);
        }
        public void actSpawnViewer()
        {
            // Respawn the currently-executing application in Viewer mode.
            Process.Start(Application.ExecutablePath, "-viewer");
        }
        public void actShowOptions()
        {
            frmOptions oFrm = new frmOptions
            {
                Left = this.Left + 100,
                Top = this.Top + 100
            };
            oFrm.ShowDialog(this);
        }

        public void actCopyCURLCommands(Exchange[] arrExch)
        {
            if (arrExch.Length < 1) return;
            try
            {
                string flavor = CApp.Prefs.GetStringPref("app.copy.curl.flavor", "powershell");
                if (!flavor.OICEquals("powershell") && !flavor.OICEquals("cmd"))
                {
                    flavor = "powershell";
                }
                StringBuilder commands = new StringBuilder();
                foreach (Exchange exch in arrExch)
                {
                    commands.AppendLine(FormatCURLCommand(exch, flavor.OICEquals("cmd")));
                }
                Clipboard.SetText(commands.ToString());
            }
            catch (Exception eX) { CApp.ReportException(eX, "Copy as CURL failed"); }
        }

        private static string FormatCURLCommand(Exchange exch, bool forCommandPrompt)
        {
            HTTPRequestHeaders headers = exch.RequestHeaders;
            if (headers == null || !headers.HTTPMethod.HasText())
            {
                throw new InvalidDataException($"Exchange #{exch.id} has no request headers or method.");
            }
            StringBuilder command = new StringBuilder("curl.exe --disable");
            StringBuilder config = new StringBuilder("& { $OutputEncoding = New-Object Text.UTF8Encoding; @(");
            bool firstOption = true;
            void AddOption(string name, string value = null)
            {
                command.Append(" --").Append(name);
                if (value != null) command.Append(' ').Append(QuoteCURLCmdArgument(value));
                if (!firstOption) config.Append(", ");
                firstOption = false;
                string line = name;
                if (value != null)
                {
                    line += " = \"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"")
                        .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
                }
                config.Append('\'').Append(line.Replace("'", "''")).Append('\'');
            }
            AddOption("globoff");
            AddOption("path-as-is");
            AddOption("url", exch.fullUrl);
            AddOption("request", headers.HTTPMethod);
            switch (headers.HTTPVersion)
            {
                case "HTTP/1.0": AddOption("http1.0"); break;
                case "HTTP/1.1": AddOption("http1.1"); break;
                case "HTTP/2":
                case "HTTP/2.0": AddOption("http2"); break;
                case "HTTP/3":
                case "HTTP/3.0": AddOption("http3-only"); break;
                default: throw new InvalidDataException($"Unsupported HTTP version: {headers.HTTPVersion}");
            }
            bool isHead = headers.HTTPMethod.OICEquals("HEAD");
            if (isHead && !exch.RequestBody.HasData()) AddOption("head");

            foreach (HTTPHeaderItem header in headers)
            {
                if (header.Name.OICEquals("Transfer-Encoding")) continue;
                if (header.Name.OICEquals("Content-Length") && !isHead) continue;
                AddOption("header", header.Name + (header.Value.Length == 0 ? ";" : ": " + header.Value));
            }
            // Suppress headers that curl would otherwise add to the request.
            foreach (string name in new[] { "Accept", "User-Agent", "Content-Type", "Expect" })
            {
                if (!headers.Exists(name)) AddOption("header", name + ":");
            }

            byte[] body = exch.RequestBody ?? Array.Empty<byte>();
            if (headers.Exists("Transfer-Encoding"))
            {
                HTTPRequestHeaders transferHeaders = headers.Clone();
                transferHeaders.Remove("Content-Encoding");
                body = Exchange.DecodeBodyFromHeaders(transferHeaders, body);
            }
            if (body.Length > 0 || (!isHead && headers.ExistsAny("Content-Length", "Transfer-Encoding")))
            {
                bool shortPlaintext = body.Length <= 1024;
                for (int i = 0; shortPlaintext && i < body.Length; ++i)
                {
                    shortPlaintext = body[i] >= 0x20 && body[i] <= 0x7e;
                }
                AddOption(shortPlaintext ? "data-raw" : "data-binary",
                    shortPlaintext ? Encoding.ASCII.GetString(body) : "@YOURREQUESTBODYBINARYFILEHERE.DAT");
            }
            config.Append(") | curl.exe --disable --config - }");
            return forCommandPrompt ? command.ToString() : config.ToString();
        }

        private static string QuoteCURLCmdArgument(string value)
        {
            if (value.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0)
            {
                throw new InvalidDataException("CURL command arguments cannot contain NUL characters or line breaks.");
            }
            StringBuilder quoted = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char c in value)
            {
                if (c == '\\')
                {
                    ++backslashes;
                    continue;
                }
                quoted.Append('\\', c == '"' ? backslashes * 2 + 1 : backslashes).Append(c);
                backslashes = 0;
            }
            quoted.Append('\\', backslashes * 2).Append('"');
            StringBuilder escaped = new StringBuilder();
            foreach (char c in quoted.ToString())
            {
                if ("^&|<>()\"%".IndexOf(c) >= 0) escaped.Append('^');
                escaped.Append(c);
            }
            return escaped.ToString();
        }
        #endregion ACTIONS

        private void miTextWizard_Click(object sender, EventArgs e)
        {
            actShowTextransform(null);
        }

        private void miViewStayOnTop_CheckedChanged(object sender, EventArgs e)
        {
            CApp.Prefs.SetBoolPref("app.ui.stayontop", miViewStayOnTop.Checked);
        }
        private void miViewAutoScroll_Click(object sender, EventArgs e)
        {
            CApp.Prefs.SetBoolPref("app.ui.exchangelist.autoscroll", miViewAutoScroll.Checked);
        }
        private void miViewSquish_Click(object sender, EventArgs e)
        {
            actToggleSquish();
        }
        private void miViewMinimize_Click(object sender, EventArgs e)
        {
            actMinimizeToTray();
        }
        private void miViewStatistics_Click(object sender, EventArgs e)
        {
            Win32UI.activateTitledTab("Statistics", tabsViews);
        }

        private void miViewInspectors_Click(object sender, EventArgs e)
        {
            Win32UI.activateTitledTab("Inspectors", tabsViews);
        }
        private void miViewComposer_Click(object sender, EventArgs e)
        {
            Win32UI.activateTitledTab("Composer", tabsViews);
        }
        private void miViewRefresh_Click(object sender, EventArgs e)
        {
            actRefreshUI(false);
            Exchange[] arrEx = GetSelectedExchanges();
            foreach (Exchange xc in arrEx)
            {
                Trace.WriteLine("Updating Inspector for Exchange #" + xc.id);
                UpdateViewItem(xc);
            }
        }

        public Exchange[] GetSelectedExchanges()
        {
            return lvExchanges.GetSelectedExchanges();
        }

        public Exchange[] GetAllExchanges()
        {
            return lvExchanges.GetAllExchanges();
        }

        /// <summary>
        /// Update the Inspector and Status bar.
        /// LEGACY API. Do not rename.
        /// </summary>
        public void actRefreshUI(bool bBecauseSelectionChanged)
        {
            // TODO: This should ensure that all inspectors are selected properly,
            // any info shown for the current Exchange in the status bar is correct, etc.
            if (CApp.isClosing) return;
            actUpdateInspector(true, true);

            actReportStatistics();

            int cSelected = lvExchanges.SelectedCount;
            if (cSelected == 1) tslInfo.Text = GetFirstSelectedExchange()?.fullUrl ?? string.Empty;
        }

        private void miFileNewViewer_Click(object sender, EventArgs e)
        {
            actSpawnViewer();
        }

        private void miToolsOptions_Click(object sender, EventArgs e)
        {
            actShowOptions();
        }

        private void miToolsMezer_Click(object sender, EventArgs e)
        {
            // TODO: Integrate native tool
            Utilities.LaunchHyperlink("https://mezer.app");
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] arrArgs)
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(frmViewer.UnhandledExceptionHandler);
            frmViewer.RunApp(arrArgs);
        }

        private static void RunApp(string[] arrArgs)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            CONFIG.RetrieveUrgentSettings();
            frmSplashScreen.CreateAndShow();
            if (!CONFIG.isQuietMode)
            {
                #region SafeMode
                if (Keys.Shift == Control.ModifierKeys)
                {
                    if (DialogResult.Yes ==
                        MessageBox.Show("The SHIFT Key is down. Would you like to reset Clearinet's appearance?", "Clearinet Safe Mode",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2))
                    {
                        CONFIG.RevertToDefaultAppearance();
                    }
                }
                #endregion SafeMode
            }

            bool bMutexAcquired;
            Mutex oMutex;

            frmSplashScreen.SetStatusText("Checking for running instance...");
            try
            {
                string sMutexName = CONFIG.isViewerMode ? $"Global\\ClearinetViewer_{Environment.TickCount}" : $"Global\\ClearinetUser_{Environment.UserName}";
                oMutex = new Mutex(false, sMutexName);
            }
            catch (Exception eX)
            {
                frmSplashScreen.CloseSplashScreen();
                CApp.ReportException(eX, "Clearinet", "Failed to obtain mutex for single-instance enforcement.");
                return;
            }
            using (oMutex)
            {
                bMutexAcquired = true;
                if (!oMutex.WaitOne(200))
                {
                    bMutexAcquired = false;
                    frmSplashScreen.CloseSplashScreen();
                    activatePriorInstance(arrArgs);
                    Win32UI.BringTitledWindowToForeground("Clearinet Web Debugger");
                    return;
                }

                CApp._frmMain = new frmViewer();

                frmSplashScreen.SetStatusText("Loading extensions...");
                CApp.oExtensions = new Extensions();
                CApp.oTranscoders = new Transcoders();
                doPopulateInspectorTabs(CApp.UI.tabsRequest, CApp.UI.tabsResponse, "MainWindow");

                frmSplashScreen.SetStatusText("Loading script engine...");
                CApp.CreateScriptEngine();

                SAZFile.SupplyPassword = (string sPath) =>
                {
                    var fpo = new frmPrompt.PromptOptions()
                    {
                        Kind = frmPrompt.PromptKind.Password,
                        OwnerWindow = CApp.UI,
                        ReturnNullOnCancel = true,
                        Title = "Password-Protected Session Archive",
                        PromptText = $"The archive '{PlatformAPI.CompactPath(sPath, 50)}' is encrypted.\r\n\r\nEnter the password to load it:"
                    };
                    return frmPrompt.GetUserString(fpo);
                };

                Application.Run(CApp._frmMain);
                if (bMutexAcquired) oMutex.ReleaseMutex();
            }
        }

        private static void activatePriorInstance(string[] arrArgs)
        {
            string sMessage = string.Join("\n", arrArgs);
            if (!HiddenExecActionTargetWindow.SendToRunningInstance(sMessage))
            {
                Trace.WriteLine("[Clearinet] Failed to send command to running instance.");
            }
        }

        // Called by both the main UI *and* in InspectInNewWindow

        private static void AddInspectorPageInOrder(List<KeyValuePair<int, TabPage>> pages, int order, TabPage page)
        {
            int index = 0;
            while (index < pages.Count && pages[index].Key <= order)
            {
                index++;
            }
            pages.Insert(index, new KeyValuePair<int, TabPage>(order, page));
        }

        internal static void doPopulateInspectorTabs(TabControl tabsRequest, TabControl tabsResponse, string Context)
        {
            var requestPages = new List<KeyValuePair<int, TabPage>>();
            foreach (Type tRI in CApp.oExtensions.m_RequestInspectors.Values)
            {
                try
                {
                    RequestInspectorBase oRI = (RequestInspectorBase)Activator.CreateInstance(tRI);
                    if ((oRI.GetFlags().HasFlag(InspectorFlags.HideInNewWindow) &&
                        Context == "NewWindow"))
                    {
                        continue;
                    }
                    // Create a Tab with the title, but don't tell the Inspector about
                    // its tab yet. We'll only do that if the user actively selects that tab.
                    TabPage oPage = new TabPage
                    {
                        Tag = oRI,
                        Text = oRI.TabTitle
                    };
                    AddInspectorPageInOrder(requestPages, oRI.GetOrder(), oPage);
                }
                catch (Exception eX) { CApp.ReportException(eX, "Inspector failed"); }
            }
            foreach (KeyValuePair<int, TabPage> page in requestPages)
            {
                tabsRequest.TabPages.Add(page.Value);
            }

            var responsePages = new List<KeyValuePair<int, TabPage>>();
            foreach (Type tRI in CApp.oExtensions.m_ResponseInspectors.Values)
            {
                try
                {
                    ResponseInspectorBase oRI = (ResponseInspectorBase)Activator.CreateInstance(tRI);
                    if ((oRI.GetFlags().HasFlag(InspectorFlags.HideInNewWindow) &&
                        Context == "NewWindow") ||
                        (oRI.GetFlags().HasFlag(InspectorFlags.HideInAutoResponder) &&
                        Context == "AutoResponder"))
                    {
                        continue;
                    }
                    // Create a Tab with the title, but don't tell the Inspector about
                    // its tab yet. We'll only do that if the user actively selects that tab.
                    TabPage oPage = new TabPage
                    {
                        Tag = oRI,
                        Text = oRI.TabTitle
                    };
                    AddInspectorPageInOrder(responsePages, oRI.GetOrder(), oPage);
                }
                catch (Exception eX) { CApp.ReportException(eX, "Inspector failed"); }
            }
            foreach (KeyValuePair<int, TabPage> page in responsePages)
            {
                tabsResponse.TabPages.Add(page.Value);
            }
        }

        static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            MessageBox.Show($"An unhandled exception occurred:\r\n\r\n{ex.Message}\r\n\r\n{ex.StackTrace}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>
        /// React to changes in the "app.ui" namespace.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="pceaChange"></param>
        private void OnPrefChange(object sender, PrefChangeEventArgs pceaChange)
        {
            if (CApp.isClosing) return;

            // Note: This event handler is called back on a threadpool thread, so if
            // you hope to manipulate the UI, you need to do so from an Invoked delegate
            switch (pceaChange.PrefName)
            {
                case "app.ui.stayontop":
                    CApp.UIInvokeAsync(() => miViewStayOnTop.Checked = TopMost = pceaChange.ValueBool);
                    break;
                case "app.ui.toolbar.lookupcuetext":
                    CApp.UIInvokeAsync(() => Win32UI.SetCueText(tstxtLookup.Control, pceaChange.ValueString));
                    break;
            }
        }

        /// <summary>
        /// Set initial UI states based on preferences.
        /// TODO: Do we need to do it this way, or could we attach the watcher earlier?
        /// </summary>
        private void InitializeUIFromPrefs()
        {
            miViewStayOnTop.Checked = CApp.Prefs.GetBoolPref("app.ui.stayontop", false);
            if (miViewStayOnTop.Checked) TopMost = true;

            miViewAutoScroll.Checked = CApp.Prefs.GetBoolPref("app.ui.exchangelist.autoscroll", true);

            splitRequestResponse.SplitPosition =
                CApp.Prefs.GetInt32Pref("app.ui.inspector.splitter", splitRequestResponse.SplitPosition);
            splitterMain.SplitPosition = CApp.Prefs.GetInt32Pref("app.ui.main.splitter", splitterMain.SplitPosition);

            CApp.Prefs.AddWatcher("app.ui.", OnPrefChange);

            if (CApp.Prefs.GetBoolPref("app.ui.darkmode", false)) ThemeManager.ApplyDarkMode(this);

        }

        private void frmViewer_Load(object sender, EventArgs e)
        {
            try
            {
                CONFIG.RetrieveLayout(this);
                SuspendLayout();
                InitializeUIFromPrefs();

                // one-offs
                rtbLog.Font = new Font("Consolas", CONFIG.flFontSize);
            }
            finally
            {
                ResumeLayout();
            }


            // TODO: Add event handlers for allow back/forward mouse buttons to change between active tabs
            // TODO: Grab system network config
            // TODO: Start the core Proxy listener


            rtbStatistics.BackColor = rtbLog.BackColor = CONFIG.colorDisabledEdit;
            CApp.ProxyAttach += HandleProxyAttached;
            CApp.ProxyDetach += HandleProxyDetached;

            frmSplashScreen.CloseSplashScreen();
            CApp.OnAppBoot();

            if (CApp.Prefs.GetBoolPref("app.attach_on_startup", false)  // TODO: Change after we have a proxy
                && !CONFIG.Overrides.NoAttachOnStartup) { CApp.actAttachProxy(); }

            timerReportUpdater.Interval = 200; // MS. TODO: Make Configurable.
            timerReportUpdater.Tick += new EventHandler(timerReportUpdater_Tick);
            CApp.CalculateReport += UpdateStatisticsTab;

            this.lvExchanges.DragDrop += lvExchanges_DragDrop;
            this.lvExchanges.DragEnter += lvExchanges_DragEnter;
            this.lvExchanges.AllowDrop = true;

            Win32UI.SetCueText(tstxtLookup.Control, CApp.Prefs.GetStringPref("app.ui.toolbar.lookupcuetext", "Search MDN..."));


            CApp.gwHoneyDew.createGig("UpdateStatusBar", () => UpdateStatusBar(false), 250);
            CApp.gwHoneyDew.createGig("UpdateMemoryCounter", () => UpdateMemoryCounter(), 5000);

            // =========================== Move TODOs ===========================
            // Today, I only use the VS Designer on a 100% DPI device because otherwise
            // the VS designer corrupts the layout. So when working on other devices, I
            // have to manually add these event handlers until I can move them later.
            btnRunToComplete.Click += (s, ea) =>
            {
                if (GetFirstSelectedExchange() is Exchange x)
                {
                    x.oFlags.TryRemove("x-breakresponse", out _);
                    ++x.state;
                    actUpdateInspector(true, true);
                }
            };
            btnBreakAtResponse.Click += (s, ea) =>
            {
                if (GetFirstSelectedExchange() is Exchange x)
                {
                    x["x-breakresponse"] = "BreakAtButton";
                    ++x.state;
                    actUpdateInspector(true, true);
                }
            };
            // =========================== /End Move TODOs ===========================

            CApp.Log.Log($"[{DateTime.Now}] {CApp.VersionString} ready. OS: {Environment.OSVersion}. .NET: {Environment.Version}.\nUser: {Environment.UserName}. PID: {Process.GetCurrentProcess().Id}.\nCommandLine: {Environment.CommandLine}");
            txtQuickExec.LoadHistory();
            tslVersion.Text = CApp.VersionString.TrimBefore("/");
            LoadAnyFiles(Environment.GetCommandLineArgs(), bSkipFirst: true);

            Task.Run(async () => await UpdateChecker.CheckForUpdatesAsync());
            createHotkeyIPCWindow();
        }

        private void createHotkeyIPCWindow()
        {
            if (CONFIG.isViewerMode) return; // Hotkeys and IPC are not supported in Viewer mode.
            var messagingWindow = new HiddenExecActionTargetWindow(components);
            string globalHotkey = CApp.Prefs.GetStringPref("app.keys.global_hotkey", "Ctrl+Alt+F");
            if (!string.IsNullOrWhiteSpace(globalHotkey))
            {
                messagingWindow.RegisterHotKey(globalHotkey, actActivateMainWindow);
            }
        }

        private void miHelpViewHelp_Click(object sender, EventArgs e)
        {
            Utilities.LaunchHyperlink(CONFIG.GetURL("Help"));
        }

        private void actDiffExchanges()
        {
            if (lvExchanges.SelectedCount != 2) return;
            Exchange x1 = lvExchanges.SelectedItems[0].Tag as Exchange;
            Exchange x2 = lvExchanges.SelectedItems[1].Tag as Exchange;
            if (null == x1 || null == x2) return;
            CApp.UI.SetStatusText($"Diffing Exchange #{x1.id} and Exchange #{x2.id}...");
            Utilities.DiffExchanges(x1, x2);
        }

        private void actInspectInNewWindow()
        {
            Exchange[] xc = GetSelectedExchanges();
            if (xc?.Length < 1) return;
            int iXOffset = 0;
            int iYOffset = 0;

            // "Tile" the windows so that they don't all appear in the same location. Limit the number of windows to avoid overwhelming the user.
            for (int x = 0; x < Math.Min(xc.Length, CApp.Prefs.GetInt32Pref("app.limits.max_new_inspect_windows", 5)); ++x)
            {
                Form f = new frmInspect(xc[x]);
                f.Show();
                if (x == 0)
                {
                    iXOffset = f.Left + 40;
                    iYOffset = f.Top + 40;
                }
                else
                {
                    f.Left = iXOffset;
                    f.Top = iYOffset;
                    iXOffset += 40;
                    iYOffset += 40;
                }
            }
        }

        private async void miHelpCheckForUpdates_Click(object sender, EventArgs e)
        {
            await UpdateChecker.CheckForUpdatesAsync(true);
        }

        private void UpdateStatisticsTab(Exchange[] arrExchanges)
        {
            // TODO: Put more interesting statistics here.
            var cRequestHeaderBytes = 0;
            var cRequestBodyBytes = 0;
            var cResponseHeaderBytes = 0;
            var cResponseBodyBytes = 0;
            foreach (Exchange x in arrExchanges)
            {
                bool isCONNECT = x.HTTPMethodIs("CONNECT"); // Data in the Connect body is "fake"
                cRequestHeaderBytes += x.RequestHeaders?.ToString().Length ?? 0;
                if (!isCONNECT) cRequestBodyBytes += x.RequestBody?.Length ?? 0;
                cResponseHeaderBytes += x.ResponseHeaders?.ToString().Length ?? 0;
                if (!isCONNECT) cResponseBodyBytes += x.ResponseBody?.Length ?? 0;
            }

            if (0 == arrExchanges.Length)
            {
                rtbStatistics.Text = "Select one or more Exchanges in the list at the left to see information here.";
                return;
            }
            if (1 == arrExchanges.Length)
            {
                string sPriorDayWarning = string.Empty;
                DateTime dtFirst = arrExchanges[0].Timers.ClientBeginRequest;
                if ((dtFirst.Ticks > 0) && (dtFirst.Date != DateTime.Today))
                {
                    sPriorDayWarning = $"This Exchange was captured on {dtFirst.Date.ToLongDateString()}.\r\n\r\n";
                }

                rtbStatistics.Text = $"{sPriorDayWarning}Exchange Timers\r\n{arrExchanges[0].Timers.ToString(true)}\r\n" +
                $"\r\nRequest Headers:  {cRequestHeaderBytes:N0} bytes" +
                $"\r\nRequest Body:     {cRequestBodyBytes:N0} bytes" +
                $"\r\nResponse Headers: {cResponseHeaderBytes:N0} bytes" +
                $"\r\nResponse Body:    {cResponseBodyBytes:N0} bytes";
                return;
            }

            rtbStatistics.Text = $"You've selected {arrExchanges.Length} Exchanges\r\n" +
                $"\r\nRequest Headers:  {cRequestHeaderBytes:N0} bytes" +
                $"\r\nRequest Bodies:   {cRequestBodyBytes:N0} bytes" +
                $"\r\nResponse Headers: {cResponseHeaderBytes:N0} bytes" +
                $"\r\nResponse Bodies:  {cResponseBodyBytes:N0} bytes";
        }

        private void timerReportUpdater_Tick(object sender, EventArgs e)
        {
            timerReportUpdater.Stop();
            actUpdateReport();
        }

        /// <summary>
        /// Add some data for development purposes. TODO:REMOVE THIS
        /// </summary>
        internal void TODOAddSampleData()
        {
            addExchangesToListView(CreateSampleExchanges());
        }

        private IEnumerable<Exchange> CreateSampleExchanges()
        {
            for (int iX = 0; iX < 15; iX++)
            {
                HTTPRequestHeaders hrh = new HTTPRequestHeaders($"/Item#{iX}", new string[] { $"FirstHeader: {iX}", $"SecondHeader: {iX}{iX}", $"Host: {iX}.com" })
                {
                    HTTPMethod = "POST",
                };
                Exchange x = new Exchange(hrh, Encoding.UTF8.GetBytes($"This is the request body for Exchange #{iX}"))
                {
                    ResponseBody = Encoding.UTF8.GetBytes($"This is the response body for Exchange #{iX}"),
                    ResponseHeaders = new HTTPResponseHeaders(200, "OK, I guess", new string[] { $"FirstHeader: {iX}", $"SecondHeader: {iX}", $"ThirdHeader: {iX}",
                                                        $"FourthLong-{new String('a',512)}z: value{new String('c', 512)}z", "Content-Type: text/plain; charset=utf-16" }),
                };
                hrh["Content-Length"] = x.ResponseBody.Length.ToString();
                x.state = (ExchangeState)(iX);
                yield return x;
            }
        }

        private void miFileLoadSAZ_Click(object sender, EventArgs e)
        {
            var sFilenames = Utilities.ObtainOpenFilenames("Open SAZ", "SAZ Files (*.saz)|*.saz");
            if ((sFilenames?.Length ?? 0) == 0) return;
            foreach (var sFilename in sFilenames)
            {
                actLoadExchangeArchive(sFilename);
            }
        }

        private void miFileImport_Click(object sender, EventArgs e)
        {
            try
            {
                // TODO: This may not scale well, and doesn't handle the old Fiddler-style calling pattern whereby
                // the user picked a format first, then it could prompt or not for a file. I imagined that this might
                // be useful if an Importer could e.g. pull from a database or cloud storage or whatever...
                string sFilename = Utilities.ObtainOpenFilename("Import Exchanges...",
                    $"All Importer formats|{CApp.oTranscoders.GetAllImportFilenameExtensions()}|Any file|*.*");
                if (!sFilename.HasText()) return;
                actImportFile(sFilename);
            }
            catch (Exception eX) { CApp.ReportException(eX, "Import failed"); }
        }

        private void miFileSaveSAZ_Click(object sender, EventArgs e)
        {
            actSaveExchanges(GetAllExchanges());
        }

        private bool actSaveExchanges(Exchange[] arrExchanges)
        {
            if (arrExchanges?.Length < 1) return false;

            try
            {
                (string sFilename, int iType) = Utilities.ObtainSaveFilenameAndType("Save All Exchanges...", "SAZ file|*.saz|Password Protected SAZ|*.saz");
                if (!sFilename.HasText()) return false;
                string sPassword = null;
                if (2 == iType) // Password protected?
                {
                    sPassword = frmPrompt.GetUserString(new frmPrompt.PromptOptions()
                    {
                        Kind = frmPrompt.PromptKind.Password,
                        OwnerWindow = CApp.UI,
                        ReturnNullOnCancel = true,
                        Title = "Password-Protect Session Archive",
                        PromptText = "Enter a password to encrypt this Session Archive:"
                    });
                    if (null == sPassword) return false; // Cancel Save
                    if (!sPassword.HasText()) sPassword = null;
                }
                else
                {
                    if (!CApp.Prefs.GetBoolPref("app.saz.warn_about_data_protection", false))
                    {
                        // TODO: This is important enough to have a custom dialog with a link to a help topic.
                        CApp.DoNotifyUser(
                            "Captured Network Data may contain passwords and other private data. Protect access and share only with people you trust.\n\nTo encrypt a SAZ file when supplying the file's name, use the 'Save As Type' dropdown to select 'Password Protected SAZ'.",
                            "Security and Privacy", MessageBoxIcon.Warning);
                        CApp.Prefs.SetBoolPref("app.saz.warn_about_data_protection", true);
                    }
                }
                using (LongRunningOperation lro = new LongRunningOperation("Saving Exchanges...", true))
                {
                    SAZFile.SaveTo(sFilename, arrExchanges, sPassword, lro: lro);
                    CApp.UI.mruRecents.PushFile(sFilename);
                    CApp.UI.SetStatusText($"{(sPassword.HasText() ? "Encrypted" : "Saved")} {arrExchanges.Length} Exchanges to {PlatformAPI.CompactPath(sFilename, 48)}");
                }
            }
            catch (Exception eX) { CApp.ReportException(eX, "Save failed"); }
            return true;
        }

        private void lvExchanges_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Link | DragDropEffects.Copy; return;
            }
            // TODO: support dropping text, other files, Exchanges, etc.
            e.Effect = DragDropEffects.None;
        }

        private void lvExchanges_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] arrFiles = (string[])e.Data.GetData("FileDrop", false);
                if (null == arrFiles) return;

                e.Effect = DragDropEffects.Copy;

                // For performance reasons, we'll batch the addition of unprocessed (non-archive)/ordinary files
                // so their rows are sorted only once.
                var paths = new List<string>();
                foreach (string sPath in arrFiles)
                {
                    bool isArchive = sPath.OICEndsWith(".saz");
                    bool hasImporter = !isArchive && null != CApp.oTranscoders.GetImporterForExt(Path.GetExtension(sPath));
                    if (isArchive || hasImporter)
                    {
                        // To preserve the drop ordering, insert any pending ordinary files before loading Exchanges from this file.
                        if (paths.Count > 0)
                        {
                            addExchangesToListView(CreateExchangesForFiles(paths));
                            paths.Clear();
                        }
                        // Archive Importers will batch their own Exchanges.
                        if (isArchive) actLoadExchangeArchive(sPath);
                        else actImportFile(sPath);
                        continue;
                    }

                    // Unrecognized file types are added to the Exchanges list as synthetic Exchanges
                    // that contain the file's bytes as the response body.
                    paths.Add(sPath);
                }
                // Flush any remaining ordinary files after the last imported file.
                if (paths.Count > 0) addExchangesToListView(CreateExchangesForFiles(paths));

                CApp.UI.SetStatusText($"Pasted {arrFiles.Length} files from the clipboard as Exchanges.");
            }
        }

        public Exchange GetFirstSelectedExchange()
        {
            return (lvExchanges.SelectedCount == 0) ? null : lvExchanges.SelectedItems[0].Tag as Exchange;
        }

        // Legacy API Name
        public void actInspectSession()
        {
            actInspectExchange();
        }
        // Activate Inspectors tab and try to pick the best two.
        public void actInspectExchange()
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)actInspectExchange);
                return;
            }
            Exchange exch = GetFirstSelectedExchange();
            if (null == exch) return;

            /* todo: Call anyone syncing the DoBeforeInspect event.*/
            SelectBestInspector(tabsRequest, exch);
            SelectBestInspector(tabsResponse, exch);

            tabsViews.SelectedTab = this.pageInspectors;
        }

        private static void SelectBestInspector(TabControl tabs, Exchange exch)
        {
            TabPage bestPage = null;
            int bestScore = int.MinValue;
            foreach (TabPage page in tabs.TabPages)
            {
                if (page.Tag is InspectorBase inspector)
                {
                    int score = inspector.ScoreForExchange(exch);
                    if (null == bestPage || score > bestScore)
                    {
                        bestPage = page;
                        bestScore = score;
                    }
                }
            }

            if (null != bestPage) tabs.SelectedTab = bestPage;
        }

        internal void LoadAnyFiles(string[] arrTokens, bool bSkipFirst = false)
        {
            for (int i = bSkipFirst ? 1 : 0; i < arrTokens.Length; ++i)
            {
                if (arrTokens[i].StartsWith("-") || arrTokens[i].StartsWith("/")) continue;
                if (!arrTokens[i].Contains(".")) continue;
                if (arrTokens[i].OICEndsWith(".saz"))
                {
                    actLoadExchangeArchive(arrTokens[i]);
                }
                else
                {
                    actImportFile(arrTokens[i]);
                }
            }
        }

        private void miFileExportAll_Click(object sender, EventArgs e)
        {
            Exchange[] arrExchanges = GetAllExchanges();
            actExportExchanges(arrExchanges);
        }

        private void miFileExportSelected_Click(object sender, EventArgs e)
        {
            Exchange[] arrExchanges = GetSelectedExchanges();
            actExportExchanges(arrExchanges);
        }

        private void actExportExchanges(Exchange[] arrExchanges)
        {
            string sFilename = Utilities.ObtainSaveFilename("Export Exchanges to HAR...", "HAR format|*.har|Any file|*.*");
            if (!sFilename.HasText()) return;

            if (sFilename.OICEndsWithAny(".har", ".json"))
            {
                TranscoderTuple tt = CApp.oTranscoders.GetExporterForExt(Path.GetExtension(sFilename));
                if (null == tt)
                {
                    CApp.DoNotifyUser($"No exporter found for {Path.GetExtension(sFilename)}", "NYI");
                    return;
                }
                IExchangeExporter oExporter = (IExchangeExporter)Activator.CreateInstance(tt.typeTranscoder);

                var dictOptions = new Dictionary<string, object>
                {
                    { "Filename", sFilename }
                };

                using (var lro = new LongRunningOperation($"Exporting {tt.FormatName}...", true))
                {
                    var exchImported = oExporter.ExportExchanges(tt.FormatName, arrExchanges, dictOptions,
                                (s, pcea) =>
                                {
                                    if (pcea.CompletionPercentage > 0) lro.UpdateProgress(pcea.CompletionPercentage);
                                    CApp.Log.Log($"Exporting {tt.FormatName}: {pcea.CurrentStatus} {pcea.CompletionPercentage}%");
                                });

                    CApp.UI.SetStatusText($"Exported {arrExchanges.Length} Exchanges to {PlatformAPI.CompactPath(sFilename, 32)} using the '{tt.FormatName}' exporter.");
                }
            }
        }

        internal void actImportFile(string sPath)
        {
            if (sPath.OICEndsWith(".saz"))
            {
                actLoadExchangeArchive(sPath);
                return;
            }
            // TODO: Can we put importer filenames into the MRU and have the right thing happen?
            TranscoderTuple tt = CApp.oTranscoders.GetImporterForExt(Path.GetExtension(sPath));
            if (null == tt)
            {
                CApp.DoNotifyUser($"No importer found for {Path.GetExtension(sPath)}", "NYI");
                return;
            }
            IExchangeImporter oImporter = (IExchangeImporter)Activator.CreateInstance(tt.typeTranscoder);

            var dictOptions = new Dictionary<string, object>
            {
                { "Filename", sPath }
            };

            using (var lro = new LongRunningOperation($"Importing {tt.FormatName}...", true))
            {
                var exchImported = oImporter.ImportExchanges(tt.FormatName, dictOptions,
                            (s, pcea) =>
                            {
                                if (pcea.CompletionPercentage > 0) lro.UpdateProgress(pcea.CompletionPercentage);
                                CApp.Log.Log($"Importing {tt.FormatName}: {pcea.CurrentStatus} {pcea.CompletionPercentage}%");

                                Application.DoEvents();
                            });

                if (exchImported == null) return;

                addExchangesToListView(exchImported);

                CApp.UI.SetStatusText($"Imported {exchImported.Length} Exchanges from {PlatformAPI.CompactPath(sPath, 32)} using the '{tt.FormatName}' importer.");
                // If we're an unnamed viewer instance, show the file path
                if (CONFIG.isViewerMode && tsmiNameViewer.Text == "Viewer")
                {
                    tsmiNameViewer.Text = Path.GetFileNameWithoutExtension(sPath);
                    tsmiNameViewer.ToolTipText = sPath;
                }
            }
        }

        /// <summary>
        /// LEGACY API. Do not rename. Load a .SAZ file.
        /// </summary>
        /// <param name="sPath"></param>
        public bool actLoadSessionArchive(string sPath)
        {
            return actLoadExchangeArchive(sPath);
        }

        public bool actLoadExchangeArchive(string sPath)
        {
            try
            {
                using (var lro = new LongRunningOperation($"Loading {PlatformAPI.CompactPath(sPath, 32)}...", false))
                {
                    using (SAZFile sazFile = SAZFile.LoadFrom(sPath))
                    {
                        CApp.Log.Log($"Loaded {sazFile.Exchanges.Count} Exchanges from {PlatformAPI.CompactPath(sPath, 32)}. {sazFile.sComment}");
                        SetStatusText($"Loaded {sazFile.Exchanges.Count} Exchanges from {PlatformAPI.CompactPath(sPath, 32)}");
                        mruRecents.PushFile(sPath);

                        // If we're a viewer instance, show the SAZ file path
                        if (CONFIG.isViewerMode && tsmiNameViewer.Text == "Viewer")
                        {
                            tsmiNameViewer.Text = Path.GetFileNameWithoutExtension(sPath);
                            tsmiNameViewer.ToolTipText = sPath;
                        }

                        addExchangesToListView(sazFile.Exchanges);
                    }
                    return true;
                }
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "actLoadExchangeArchive Failed");
                return false;
            }
        }

        internal void UpdateViewItem(Exchange xc)
        {
            updateLVI(xc);
        }

        // TODO: CLEANUP updateLVI and addExchangeToListView ************************************
        // **************************************************************************************
        private void updateLVI(Exchange xc)
        {
            if (CApp.isClosing) return;

            if (null == xc.ViewItem)
            {
                addExchangeToListView(xc);
                return;
            }

            ListViewItem lvi = xc.ViewItem;
            lvi.SubItems[1].Text = xc.bHasResponse ? xc.responseCode.ToString() : "-";
            lvi.SubItems[2].Text = xc.RequestHeaders.HTTPMethod;
            lvi.SubItems[3].Text = xc.RequestHeaders?.UriScheme;
            lvi.SubItems[4].Text = xc.host;
            lvi.SubItems[5].Text = xc.RequestHeaders.RequestPath;
            lvi.SubItems[6].Text = xc.ResponseBody?.Length.ToString("N0") ?? "-";
            lvi.SubItems[7].Text = xc.MediaType ?? "-";
            lvi.SubItems[8].Text = xc["x-processinfo"] ?? String.Empty;
            lvi.SubItems[9].Text = xc["ui-comments"] ?? String.Empty;

            for (int i = 10; i < lvExchanges.Columns.Count; ++i)
            {
                lvi.SubItems[i].Text = lvExchanges.PopulateColumn(i, xc);
            }

            AddFormatting(lvi, xc);
        }

        private void addExchangesToListView(IEnumerable<Exchange> exchanges)
        {
            if (CApp.isClosing) return;
            bool bAtBottom = lvExchanges.IsScrolledToBottom();

            var sorter = lvExchanges.ListViewItemSorter;
            lvExchanges.BeginUpdate();
            try
            {
                lvExchanges.ListViewItemSorter = null;
                foreach (Exchange exchange in exchanges)
                {
                    addExchangeToListView(exchange);
                }
            }
            finally
            {
                // Flush deferred inserts before restoring sorting.
                // TODO: Handle KeepOnly
                // int TrimTo = CApp.Prefs.GetInt32Pref("app.ui.exchangelist.keeponly", 0);
                lvExchanges.EndUpdate();
                lvExchanges.ListViewItemSorter = sorter;
            }
            onExchangeAdded(bAtBottom);
        }

        private void onExchangeAdded(bool bWasPreviouslyAtBottom)
        {
            if (bWasPreviouslyAtBottom &&
                CApp.Prefs.GetBoolPref("app.ui.exchangelist.autoscroll", true))
            {
                int iLatest = this.lvExchanges.Items.Count - 1;
                if (iLatest > -1)
                {
                    lvExchanges.EnsureVisible(iLatest);
                }
            }

            // TODO: Where should this method live? CApp.OnExchangesAdded?.Invoke(this, EventArgs.Empty);
        }

        internal void addExchangeToListView(Exchange xc)
        {
            if (CApp.isClosing) return;
            if (xc.ViewItem != null) return; // Already added.

            if (lvExchanges.InvokeRequired)
            {
                lvExchanges.Invoke((Action<Exchange>)addExchangeToListView, xc);
                return;
            }
            ListViewItem lvi = new ListViewItem(xc.id.ToString())
            {
                Tag = xc
            };
            xc.ViewItem = lvi;

            lvi.SubItems.Add(xc.bHasResponse ? xc.responseCode.ToString() : "-");
            lvi.SubItems.Add(xc.RequestHeaders?.HTTPMethod ?? String.Empty);
            lvi.SubItems.Add(xc.RequestHeaders?.UriScheme ?? String.Empty);
            lvi.SubItems.Add(xc.host);
            lvi.SubItems.Add(xc.RequestHeaders?.RequestPath ?? String.Empty);
            lvi.SubItems.Add(xc.ResponseBody?.Length.ToString("N0") ?? "-");
            lvi.SubItems.Add(xc.MediaType ?? "-");
            lvi.SubItems.Add(xc["x-processinfo"] ?? String.Empty);
            lvi.SubItems.Add(xc["ui-comments"] ?? String.Empty);
            for (int i = 10; i < lvExchanges.Columns.Count; ++i)
            {
                lvi.SubItems.Add(lvExchanges.PopulateColumn(i, xc));
            }

            AddFormatting(lvi, xc);
            lvExchanges.Items.Add(lvi);

            // TODO: deal with delayed scroll/trim
        }

        private void AddFormatting(ListViewItem lvi, Exchange xc)
        {
            {
                if (xc["ui-backcolor"] is string sColor)
                {
                    try
                    {
                        Color c = ColorTranslator.FromHtml(sColor);
                        lvi.BackColor = c;
                    }
                    catch { }
                }
            }
            {
                if (xc["ui-color"] is string sColor)
                {
                    try
                    {
                        Color c = ColorTranslator.FromHtml(sColor);
                        lvi.ForeColor = c;
                    }
                    catch { }
                }
                else if (xc.bHasResponse)
                {
                    lvi.ForeColor = CONFIG.GetResponseColor(xc);
                }
            }
            if (xc["ui-strikeout"] != null) lvi.Font = new Font(lvExchanges.Font, FontStyle.Strikeout);
            if (xc["ui-bold"] != null) lvi.Font = new Font(lvExchanges.Font, FontStyle.Bold);
        }

        private void HandleProxyAttached()
        {
            miFileAttach.Checked = true;
            tssbCapture.Text = "Attached";
        }
        private void HandleProxyDetached()
        {
            miFileAttach.Checked = false;
            tssbCapture.Text = "(Detached)";
        }

        private void frmViewer_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!CApp.OnBeforeShutdown()) { e.Cancel = true; return; }
            timerReportUpdater.Stop();
            CApp.gwHoneyDew.Dispose();
            // TODO: Freeze anything else we can.

            CApp.Prefs.SetInt32Pref("app.ui.inspector.splitter", splitRequestResponse.SplitPosition);
            CApp.Prefs.SetInt32Pref("app.ui.main.splitter", splitterMain.SplitPosition);

            txtQuickExec.SaveHistory();
            CApp.OnAppShutdown();
        }

        private readonly Queue<Tuple<DateTime, string>> queuedLogEntries = new Queue<Tuple<DateTime, string>>();

        internal void UpdateLog(string sLog)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((Action<string>)UpdateLog, sLog);
                return;
            }
            Debug.Assert(!this.InvokeRequired);
            queuedLogEntries.Enqueue(Tuple.Create(DateTime.Now, sLog));
            FlushLogQueue();
        }

        private void FlushLogQueue()
        {
            if (tabsViews.SelectedTab != pageLog || queuedLogEntries.Count == 0) return;

            bool bAtBottom = Win32UI.IsScrolledToBottom(rtbLog);
            Point scrollPosition = Win32UI.GetScrollPosition(rtbLog);
            int selectionStart = rtbLog.SelectionStart;
            int selectionLength = rtbLog.SelectionLength;
            try
            {
                rtbLog.Select(rtbLog.TextLength, 0);
                while (queuedLogEntries.Count > 0)
                {
                    var entry = queuedLogEntries.Peek();
                    string timestamp = entry.Item1.ToString("HH:mm:ss") + " ";
                    foreach (string line in entry.Item2.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                    {
                        rtbLog.SelectionFont = rtbLog.Font;
                        rtbLog.SelectionColor = Color.LightGray;
                        rtbLog.SelectedText = timestamp;

                        FontStyle style = FontStyle.Regular;
                        string text = line;
                        if (line.Length > 0)
                        {
                            switch (line[0])
                            {
                                case '_': style = FontStyle.Underline; break;
                                case '/': style = FontStyle.Italic; break;
                                case '!': style = FontStyle.Bold; break;
                            }
                            if (style != FontStyle.Regular) text = line.Substring(1);
                        }
                        using (var font = new Font(rtbLog.Font, style))
                        {
                            rtbLog.SelectionFont = font;
                            rtbLog.SelectionColor = rtbLog.ForeColor;
                            rtbLog.SelectedText = text + "\r\n";
                        }
                    }
                    queuedLogEntries.Dequeue();
                }
            }
            finally
            {
                if (bAtBottom)
                {
                    Win32UI.ScrollToBottom(rtbLog);
                }
                else
                {
                    rtbLog.Select(selectionStart, selectionLength);
                    Win32UI.SetScrollPosition(rtbLog, scrollPosition);
                }
            }
        }

        /// <summary>
        /// Set the status text on the Status Strip at the bottom of the main window.
        /// </summary>
        public void SetStatusText(string sStatus)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((Action<string>)SetStatusText, sStatus);
                return;
            }
            tslInfo.Text = sStatus;
        }

        private void txtQuickExec_OnExecute(ExecuteEventArgs ea)
        {
            string sCommand = ea.Command;
            ea.Handled = CApp.RunExecAction(sCommand);
        }

        private void splitterMain_DoubleClick(object sender, EventArgs e)
        {
            //TODO: Auto-position the splitter based on the expected width of the text.
        }

        private void miHelpDiscuss_Click(object sender, EventArgs e)
        {
            Utilities.LaunchHyperlink("https://groups.google.com/g/clearinet"); // TODO: Use Redirector
        }

        private void miHelpBug_Click(object sender, EventArgs e)
        {
            Utilities.LaunchHyperlink("https://github.com/ericlaw1979/Clearinet/issues"); // TODO: Use Redirector
        }

        /// <summary>
        /// Resize the Exchanges listview.
        /// Legacy API. Do not rename.
        /// </summary>
        public void actToggleSquish()
        {
            if (pnlLeft.Width == 60)
            {
                miViewSquish.Checked = false;
                pnlLeft.Width = Math.Max(CApp.Prefs.GetInt32Pref("app.ui.ephemeral.exchangelist.unsquishedwidth", 350), 350);
            }
            else
            {
                miViewSquish.Checked = true;
                CApp.Prefs.SetInt32Pref("app.ui.ephemeral.exchangelist.unsquishedwidth", pnlLeft.Width);
                pnlLeft.Width = 60;
            }
        }

        private void tsmiNameViewer_Click(object sender, EventArgs e)
        {
            string sNewName = frmPrompt.GetUserString(new frmPrompt.PromptOptions
            {
                Title = "Name this Viewer",
                PromptText = "Enter a name for this viewer:",
                DefaultValue = tsmiNameViewer.Text,
                ReturnNullOnCancel = true,
                Kind = frmPrompt.PromptKind.Password
            });
            if (sNewName.HasText()) tsmiNameViewer.Text = sNewName;
        }

        private void tssbCapture_ButtonClick(object sender, EventArgs e)
        {
            if (CONFIG.isViewerMode) return;
            if (CApp.isAttached) { CApp.actDetachProxy(); return; }
            CApp.actAttachProxy();
        }

        private void miFileAttach_Click(object sender, EventArgs e)
        {
            miFileAttach.Checked = !miFileAttach.Checked;
            if (miFileAttach.Checked) { CApp.actAttachProxy(); } else { CApp.actDetachProxy(); }
        }

        public void actBeginFinding(string sFindText = "")
        {
            if (null == CApp.oFindPanel)
            {
                CApp.oFindPanel = new FindPanel();
                SuspendLayout();
                pnlLeftBottom.Controls.Add(CApp.oFindPanel);
                CApp.oFindPanel.Dock = DockStyle.Fill;
                pnlLeftBottom.Height = 100;
                pnlLeftBottom.Visible = true;
                ResumeLayout();
            }
            if (sFindText.HasText()) CApp.oFindPanel.txtToFind.Text = sFindText;
            CApp.oFindPanel.txtToFind.Focus();
        }

        private void miEditFind_Click(object sender, EventArgs e)
        {
            actBeginFinding();
        }
        private void miEditRemoveSelected_Click(object sender, EventArgs e)
        {
            lvExchanges.RemoveSelected();
        }
        private void miEditRemoveUnselected_Click(object sender, EventArgs e)
        {
            lvExchanges.RemoveUnselected();
        }

        private void tstxtLookup_KeyDown(object sender, KeyEventArgs e)
        {
            // Prevent the beep.
            if (e.KeyCode == Keys.Enter) { e.Handled = e.SuppressKeyPress = true; }
        }

        private void tstxtLookup_KeyUp(object sender, KeyEventArgs e)
        {
            // We need this in KeyUp to prevent a beep.
            if (e.KeyCode == Keys.Enter)
            {
                Utilities.LaunchHyperlink(CApp.Prefs.GetStringPref("app.ui.toolbar.searchurl",
                   "https://developer.mozilla.org/en-US/search?q=$W$").Replace("$W$", tstxtLookup.Text.Trim()));
                tstxtLookup.Clear();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F) && pageLog.ContainsFocus)
            {
                txtLogSearch.Focus();
                txtLogSearch.SelectAll();
                return true;
            }
            // This ridiculousness is needed because otherwise the toolstrip control
            // uses ESC to mean "unfocus this text box"
            if (keyData == Keys.Escape && tstxtLookup.Focused)
            {
                tstxtLookup.Clear();
                return true; // indicate key was handled.
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void miEdit_DropDownOpening(object sender, EventArgs e)
        {
            // Requires 1+ Exchange in the list
            miEditSelectAll.Enabled = miEditCopy.Enabled = miEditMark.Enabled = miEditRemoveSelected.Enabled = (lvExchanges.SelectedCount > 0);
            // Requires 1+ *selected* Exchange in the list
            miEditUnlock.Enabled = miEditRemove.Enabled = miEditFind.Enabled =
                miEditCopyForAI.Enabled = miEditCopyExchange.Enabled = miEditCopyHeaders.Enabled =
                miEditCopySummary.Enabled = miEditCopyTerseSummary.Enabled = miEditCopyURL.Enabled =
                (lvExchanges.Items.Count > 0);

            miEditPasteAsExchanges.Enabled = Clipboard.ContainsImage() | Clipboard.ContainsText() | Clipboard.ContainsFileDropList();

            miEditUndelete.Enabled = lvExchanges.CanUndelete();
        }

        private void miFile_DropDownOpening(object sender, EventArgs e)
        {
            if (lvExchanges.Items.Count > 0)
            {
                miFileSave.Enabled = true;
                miFileSaveSelected.Enabled = (lvExchanges.SelectedCount > 0);
                miFileExport.Enabled = true;
            }
            else
            {
                miFileSave.Enabled = false;
                miFileExport.Enabled = false;
            }
        }

        private void lvExchanges_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (CApp.isClosing) return;
            lvExchanges.UpdateActiveItem();
            actRefreshUI(true);
        }

        private void tabsRequest_SelectedIndexChanged(object sender, EventArgs e)
        {
            actUpdateInspector(true, false);
        }

        private void ShowSelectOne()
        {
            pageInspectors.SuspendLayout();
            pnlTamper.Visible = tabsRequest.Visible = splitRequestResponse.Visible = tabsResponse.Visible = false;
            if (null == lblInspectorInstruction)
            {
                lblInspectorInstruction = new Label
                {
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font(CApp.UI.Font, FontStyle.Italic),
                    Text = "Please select a single Exchange to inspect",
                    Parent = pageInspectors,
                    ForeColor = Color.FromKnownColor(KnownColor.ControlDarkDark),
                    Dock = DockStyle.Fill
                };
            }
            else
            {
                lblInspectorInstruction.Visible = true;
            }
            pageInspectors.ResumeLayout();
        }

        public void actUpdateInspector(bool bUpdateRequest, bool bUpdateResponse)
        {
            // Don't work if it's not visible.
            if (CApp.isClosing) return;
            if (tabsViews.SelectedTab != pageInspectors) return;

            if (lvExchanges.SelectedItems.Count == 0)
            {
                // CLEAR Inspectors.
                if (tabsRequest.SelectedIndex >= 0) { (tabsRequest.TabPages[tabsRequest.SelectedIndex].Tag as RequestInspectorBase)?.Clear(); }
                if (tabsResponse.SelectedIndex >= 0) { (tabsResponse.TabPages[tabsResponse.SelectedIndex].Tag as ResponseInspectorBase)?.Clear(); }
                ShowSelectOne();
                return;
            }
            if (lvExchanges.SelectedItems.Count != 1)
            {
                ShowSelectOne();
                return;
            }

            Exchange x = lvExchanges.SelectedItems[0].Tag as Exchange;

            pageInspectors.SuspendLayout();
            if (null != lblInspectorInstruction) lblInspectorInstruction.Visible = false;
            tabsRequest.Visible = splitRequestResponse.Visible = tabsResponse.Visible = true;

            if ((ExchangeState.HandTamperRequest == x.state) || (ExchangeState.HandTamperResponse == x.state))
            {
                btnBreakAtResponse.Visible = (x.state == ExchangeState.HandTamperRequest);
                pnlTamper.Visible = true;
            }
            else
            {
                pnlTamper.Visible = false;
            }

            if (bUpdateRequest && (tabsRequest.SelectedIndex > -1))
            {
                var page = tabsRequest.TabPages[tabsRequest.SelectedIndex];
                RequestInspectorBase ibRequest = page.Tag as RequestInspectorBase;
                ibRequest.EnsureTabReady(page);
                if (null != x) ibRequest.AssignExchange(x); else ibRequest.Clear();
            }

            if (bUpdateResponse && (tabsResponse.SelectedIndex > -1))
            {
                var page = tabsResponse.TabPages[tabsResponse.SelectedIndex];
                ResponseInspectorBase ibResponse = page.Tag as ResponseInspectorBase;
                ibResponse.EnsureTabReady(page);
                if (null != x) ibResponse.AssignExchange(x); else ibResponse.Clear();
            }
            pageInspectors.ResumeLayout();
        }

        private void tabsResponse_SelectedIndexChanged(object sender, EventArgs e)
        {
            actUpdateInspector(false, true);
        }

        private void miInspectorScreenshot_Click(object sender, EventArgs e)
        {
            if (mnuInspectors.SourceControl is TabControl tabControl &&
                tabControl.SelectedTab?.Tag is InspectorBase inspector)
            {
                inspector.CopyAsImage(tabControl.SelectedTab);
            }
        }

        private void miInspectorAbout_Click(object sender, EventArgs e)
        {
            if (mnuInspectors.SourceControl is TabControl tabControl &&
                tabControl.SelectedTab?.Tag is InspectorBase inspector)
            {
                inspector.ShowAboutBox();
            }
        }

        private void tabsViews_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabsViews.SelectedTab == pageLog)
            {
                FlushLogQueue();
                return;
            }
            if (tabsViews.SelectedTab == pageInspectors)
            {
                actUpdateInspector(true, true);
                return;
            }
            if (tabsViews.SelectedTab == pageComposer)
            {
                // Delay-load the composer UI
                if (0 == pageComposer.Controls.Count)
                {
                    CApp.oComposer = new ComposerView();
                    pageComposer.Controls.Add(CApp.oComposer);
                    CApp.oComposer.Dock = DockStyle.Fill;
                }
                return;
            }
            if (tabsViews.SelectedTab?.Tag is BindUITab bindtab)
            {
                bindtab.RunDelegate(lvExchanges.GetSelectedExchanges());
            }
        }


        private void mnuFileMRU_DropDownOpening(object sender, EventArgs e)
        {
            try
            {
                mnuFileMRU.DropDownItems.Clear();
                var filenames = mruRecents.GetFiles();

                var items = new List<ToolStripItem>(16);

                if (filenames.Count < 1)
                {
                    items.Add(new ToolStripMenuItem("<empty") { Enabled = false });
                }
                else
                {
                    int ix = 0;
                    foreach (string f in filenames)
                    {
                        var newItem = new ToolStripMenuItem($"&{ix:x}. {PlatformAPI.CompactPath(f, 50)}")
                        {
                            Tag = f
                        };

                        newItem.MouseHover += (s, ea) => CApp.UI.SetStatusText((s as ToolStripMenuItem).Tag as String);
                        newItem.Click += (s, ea) =>
                        {
                            string sFilename = (s as ToolStripMenuItem).Tag as String;
                            if (!actLoadExchangeArchive(sFilename))
                            {
                                if (!File.Exists(sFilename))
                                {
                                    if (DialogResult.Yes == MessageBox.Show(
                                        "That file is not available. It may have been moved or deleted, or may be on a disconnected drive.\n\nWould you like to remove this file from the list?",
                                        "File Not Found", MessageBoxButtons.YesNo, MessageBoxIcon.Question))
                                    {
                                        mruRecents.ForgetFile(sFilename);
                                    }
                                }
                            }
                        };
                        items.Add(newItem);
                        ix++;
                    }
                }

                items.Add(new ToolStripSeparator());
                items.Add(new ToolStripMenuItem("&Prune Obsolete", image: null, (s, ea) => mruRecents.Prune()));
                items.Add(new ToolStripMenuItem("Clear this &List", image: null, (s, ea) => mruRecents.Purge()));
                mnuFileMRU.DropDownItems.AddRange(items.ToArray());
            }
            catch (Exception eX) { CApp.ReportException(eX, "oops"); }
        }

        private void lvExchanges_DoubleClick(object sender, EventArgs e)
        {
            actInspectExchange();
        }

        public void actSelectAll()
        {
            lvExchanges.SelectAll();
        }
        private void lvExchanges_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Oemplus)
            {
                actAddFauxSplitter();
                e.Handled = e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.D1 && e.Modifiers == Keys.Control)
            {
                MarkExchanges(lvExchanges.GetSelectedExchanges());
                e.SuppressKeyPress = e.Handled = true;
                return;
            }
            if (e.KeyData == Keys.Enter)
            {
                actInspectExchange();
                e.SuppressKeyPress = e.Handled = true;
                return;
            }

            if (e.Modifiers == Keys.Alt)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    actInspectInNewWindow();
                    e.SuppressKeyPress = e.Handled = true;
                    return;
                }
                return;
            }

            if (e.KeyCode == Keys.Delete)
            {
                if (e.Modifiers == Keys.Shift) lvExchanges.RemoveUnselected(); else lvExchanges.RemoveSelected();
                if (null != lvExchanges.FocusedItem)
                {
                    lvExchanges.FocusedItem.Selected = true;
                    lvExchanges.FocusedItem.EnsureVisible();
                }
            }

            if (e.Modifiers == Keys.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.A:
                        actSelectAll();
                        e.SuppressKeyPress = e.Handled = true;
                        break;

                    case Keys.C:
                        e.SuppressKeyPress = e.Handled = true;
                        miEditCopyExchange.PerformClick();
                        break;

                    case Keys.W:
                        e.SuppressKeyPress = e.Handled = true;
                        miExchangeDiffer.PerformClick();
                        break;

                    case Keys.X:
                        lvExchanges.ClearExchanges();
                        e.SuppressKeyPress = e.Handled = true;
                        break;

                    case Keys.Z:
                        lvExchanges.UndeleteItems();
                        e.SuppressKeyPress = e.Handled = true;
                        break;

                    case Keys.Oemplus:
                    case Keys.Add:
                        {
                            float flSize = Math.Min((float)(CONFIG.flFontSize + 1.0), 36f);
                            actSetFontSize(flSize);
                            e.SuppressKeyPress = e.Handled = true;
                            break;
                        }

                    case Keys.OemMinus:
                    case Keys.Subtract:
                        {
                            float flSize = Math.Max((float)(CONFIG.flFontSize - 1.0), 6f);
                            actSetFontSize(flSize);
                            e.SuppressKeyPress = e.Handled = true;
                            break;
                        }

                    case Keys.D0:
                    case Keys.NumPad0:
                        actSetFontSize(10f);
                        e.SuppressKeyPress = e.Handled = true;
                        break;
                }

                return; // We don't want CTRL+ other keys below to be processed.
            }

            switch (e.KeyCode)
            {
                case Keys.Back:
                    e.SuppressKeyPress = e.Handled = true;
                    lvExchanges.ActivatePreviousItem();
                    break;

                case Keys.R:
                    e.SuppressKeyPress = e.Handled = true;
                    actReplaySelected(e.Modifiers == Keys.Shift);
                    break;

                case Keys.U:
                    e.SuppressKeyPress = e.Handled = true;
                    actReplaySelected(e.Modifiers == Keys.Shift, bUnconditional: true);
                    break;

                case Keys.D:
                    e.SuppressKeyPress = e.Handled = true;
                    actSelectDuplicates();
                    break;

                case Keys.M:
                    e.SuppressKeyPress = e.Handled = true;
                    // Actual handling happens in Keyup to avoid beep from the new dialog
                    break;
            }   // switch
        }

        private void lvExchanges_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyData)
            {
                case Keys.M:
                    e.SuppressKeyPress = e.Handled = true;
                    actSetComment();
                    break;
            }
        }

        private void actSelectDuplicates()
        {
            // TODO: Support multiple?
            Exchange xc = CApp.UI.GetFirstSelectedExchange();
            if (null == xc) return;
            string sURL = xc.fullUrl;
            string sMethod = xc.RequestMethod;
            actSelectExchangesMatchingCriteria((x) =>
                x.RequestMethod.OICEquals(sMethod) && x.fullUrl.OICEquals(sURL));
            CApp.UI.SetStatusText($"Selected {lvExchanges.SelectedCount} duplicate Exchanges.");
        }
        private void miExchangeSelectDuplicates_Click(object sender, EventArgs e)
        {
            actSelectDuplicates();
        }

        private void actAddFauxSplitter()
        {
            DateTime dt = DateTime.Now;
            var XC = AddFauxExchange($"/SPLITTER/?{dt:HH-mm-ss.ffff}", "text/plain", Encoding.UTF8.GetBytes($"User hit = to create a split line at\n{dt.ToLongTimeString()}"));
            XC.oFlags["ui-strikeout"] = "splitter";
            XC.oFlags["ui-backcolor"] = "OrangeRed";
        }

        private void actAddDesktopScreenshot()
        {
            try
            {
                Rectangle bounds = SystemInformation.VirtualScreen;
                using (var screenshot = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb))
                using (var graphics = Graphics.FromImage(screenshot))
                using (var stream = new MemoryStream())
                using (var encoderParameters = new EncoderParameters(1))
                {
                    graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                    var jpegEncoder = Array.Find(ImageCodecInfo.GetImageEncoders(), codec => codec.FormatID == ImageFormat.Jpeg.Guid);
                    if (jpegEncoder == null)
                    {
                        throw new InvalidOperationException("The JPEG image encoder is not available.");
                    }
                    encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 95L);
                    screenshot.Save(stream, jpegEncoder, encoderParameters);
                    AddFauxExchange($"/screenshot/{DateTime.Now:yyyy-MM-dd-HH-mm-ss.ffff}.jpg", "image/jpeg", stream.ToArray());
                }
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Unable to capture desktop screenshot.");
            }
        }

        private void MarkExchanges(Exchange[] exchanges)
        {
            foreach (var xc in exchanges)
            {
                xc.oFlags["ui-bold"] = "marked";
                xc.oFlags["ui-color"] = "blue";
                updateLVI(xc);
            }
        }

        internal void actSetFontSize(float v)
        {
            // TODO: Should this be resizing the window bounds? Probably not, but it does.
            CONFIG.flFontSize = v;
            this.Font = new Font(this.Font.FontFamily, CONFIG.flFontSize, this.Font.Style);
        }

        private void actReplaySelected(bool bBreakOnRequest, bool bUnconditional = false)
        {
            Exchange[] arrXC = lvExchanges.GetSelectedExchanges();
            if (arrXC.Length < 1) return;
            CApp.UI.SetStatusText($"Replaying {arrXC.Length} Exchanges...");
            Task.Run(async () =>
            {
                for (var ix = 0; ix < arrXC.Length; ++ix)
                {
                    HTTPRequestHeaders rhNew = arrXC[ix].RequestHeaders.Clone();
                    if (bUnconditional)
                    {
                        rhNew.Remove("If-Modified-Since", "If-Unmodified-Since", "If-Match", "If-None-Match", "If-Range");
                    }
                    Exchange XC = new Exchange(rhNew, arrXC[ix].RequestBody.FastClone());
                    XC.BitFlags = ExchangeFlags.RequestGeneratedByClearinet;
                    XC.EnsureID();
                    XC.state = ExchangeState.AutoTamperRequestBefore;
                    if (bBreakOnRequest) XC["x-breakrequest"] = "Shift+R replay";
                    XC.state = ExchangeState.HandTamperRequest;
                }
            });
        }

        private IEnumerable<Exchange> CreateExchangesForFiles(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                Exchange exResult = null;
                try
                {
                    byte[] body = File.ReadAllBytes(path);
                    string contentType = Utilities.MimeTypeFromFilename(path);
                    exResult = CreateFauxExchange($"/clipboard/{Path.GetFileName(path)}", contentType, body);
                }
                catch (Exception eX)
                {
                    exResult = CreateFauxExchange($"/clipboard/{Path.GetFileName(path)}", "text/plain; charset=utf-8",
                        Encoding.UTF8.GetBytes($"Unable to create Exchange for file '{path}': {eX.Message}."));
                }
                yield return exResult;
            }
        }

        private void doPasteFilesAsExchanges()
        {
            try
            {
                var arrFiles = Clipboard.GetFileDropList();
                if (null == arrFiles || arrFiles.Count < 1)
                {
                    CApp.DoNotifyUser("The clipboard did not contain any files.", "Paste Failed");
                    return;
                }
                var paths = new List<string>();
                foreach (string path in arrFiles)
                {
                    if (File.Exists(path)) paths.Add(path);
                }
                addExchangesToListView(CreateExchangesForFiles(paths));
                CApp.UI.SetStatusText($"Pasted {paths.Count} files from the clipboard as Exchanges.");
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Unable to paste files.");
            }
        }

        private void doPasteImageAsExchange()
        {
            try
            {
                Image imageToPaste = Clipboard.GetImage();

                if (null == imageToPaste)
                {
                    CApp.DoNotifyUser("The clipboard did not contain a pasteable image.", "Paste Failed");
                    return;
                }

                var oMS = new MemoryStream();
                imageToPaste.Save(oMS, ImageFormat.Png);
                AddFauxExchange($"/clipboard/{DateTime.Now:H-mm-ss}.png", "image/png", oMS.ToArray());
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Unable to paste image.");
            }
        }

        internal Exchange AddFauxExchange(string sRequestPath, string sContentType, byte[] arrBody)
        {
            Exchange exchange = CreateFauxExchange(sRequestPath, sContentType, arrBody);
            addExchangeToListView(exchange);
            return exchange;
        }

        internal static Exchange CreateFauxExchange(string sRequestPath, string sContentType, byte[] arrBody)
        {
            var headersRequest = new HTTPRequestHeaders(sRequestPath, new[] { "Host: localhost" });
            var exchNew = new Exchange(headersRequest, Array.Empty<byte>());

            var headersResponse = new HTTPResponseHeaders(200, "Pasted", new[] { $"Content-Type: {sContentType}", $"Content-Length: {arrBody.Length}" });
            exchNew.ResponseHeaders = headersResponse;
            exchNew.ResponseBody = arrBody.FastClone();
            exchNew.BitFlags = ExchangeFlags.RequestGeneratedByClearinet | ExchangeFlags.ResponseGeneratedByClearinet
                                | ExchangeFlags.ImportedFromOtherTool | ExchangeFlags.ServedFromCache;
            exchNew.EnsureID();
            exchNew.state = ExchangeState.Done;
            exchNew.Timers.AssignNowToAll();
            return exchNew;
        }

        // Paste the clipboard text as a new Exchange; if the text is a data URL, parse it.
        private void doPasteTextAsExchange()
        {
            try
            {
                string sText = Clipboard.GetText(TextDataFormat.UnicodeText);
                if (!sText.HasText())
                {
                    CApp.DoNotifyUser("The clipboard did not contain any text.", "Paste Failed");
                    return;
                }

                // TODO: Can we handle paste of CURL Commands?

                var headersRequest = new HTTPRequestHeaders($"/clipboard/{DateTime.Now:H-mm-ss}.txt", new[] { "Host: localhost" });
                var exchNew = new Exchange(headersRequest, Array.Empty<byte>())
                {
                    ResponseHeaders = new HTTPResponseHeaders(200, "Pasted", null)
                };
                if (sText.TrimStart().StartsWith("data:"))
                {
                    _FillExchangeFromDataURL(exchNew, sText);
                }
                else
                {
                    var headersResponse = new HTTPResponseHeaders(200, "Pasted", new[] { "Content-Type: text/plain; charset=utf-8", $"Content-Length: {sText.Length}" });
                    exchNew.ResponseHeaders = headersResponse;
                    exchNew.ResponseBody = Encoding.UTF8.GetBytes(sText);
                }

                exchNew.BitFlags = ExchangeFlags.RequestGeneratedByClearinet | ExchangeFlags.ResponseGeneratedByClearinet
                                    | ExchangeFlags.ImportedFromOtherTool | ExchangeFlags.ServedFromCache;
                exchNew.EnsureID();
                exchNew.Timers.AssignNowToAll();
                exchNew.state = ExchangeState.Done;
                addExchangeToListView(exchNew);
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Unable to paste text.");
            }
        }

        // TODO: We can make this smarter and trim leading and trailing stuff, improving the UX
        // if the user sloppily copies e.g. "<img src=data:.... width=400/>" and then tries to paste it.
        private void _FillExchangeFromDataURL(Exchange exchNew, string sClipboardText)
        {
            // data:[<mediatype>][;base64],<data>
            string payload = sClipboardText.TrimBefore("data:").Trim();
            int comma = payload.IndexOf(',');
            if (comma < 0)
            {
                exchNew.ResponseBody = Array.Empty<byte>();
                exchNew.ResponseHeaders["Content-Type"] = "text/plain";
                exchNew.ResponseHeaders["Content-Length"] = "0";
                return;
            }

            string meta = payload.Substring(0, comma);
            string dataPart = payload.Substring(comma + 1);
            bool isBase64 = meta.EndsWith(";base64", StringComparison.OrdinalIgnoreCase);
            string mimeType = isBase64
                ? meta.Substring(0, meta.Length - ";base64".Length)
                : meta;

            if (!mimeType.HasText()) mimeType = "application/octet-stream";

            byte[] bodyBytes = isBase64
                ? Convert.FromBase64String(dataPart)
                : Encoding.UTF8.GetBytes(Uri.UnescapeDataString(dataPart));

            exchNew.ResponseBody = bodyBytes;
            exchNew.ResponseHeaders["Content-Type"] = mimeType;
            exchNew.ResponseHeaders["Content-Length"] = bodyBytes.Length.ToString();
        }

        private void miEditPasteAsExchanges_Click(object sender, EventArgs e)
        {
            try
            {
                if (Clipboard.ContainsImage())
                {
                    doPasteImageAsExchange();
                    return;
                }
                if (Clipboard.ContainsFileDropList())
                {
                    doPasteFilesAsExchanges();
                    return;
                }
                doPasteTextAsExchange();
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Failed Paste");
            }
        }

        private void miEditSelectAll_Click(object sender, EventArgs e)
        {
            lvExchanges.SelectAll();
        }

        private void miEditRemoveAll_Click(object sender, EventArgs e)
        {
            lvExchanges.ClearExchanges();
        }

        private void frmViewer_KeyDown(object sender, KeyEventArgs e)
        {
            // Focus QuickExec (Ctrl/Alt + Q or Semicolon)
            if (((e.Modifiers == Keys.Alt) || (e.Modifiers == Keys.Control)) &&
                ((e.KeyCode == Keys.Q) || e.KeyCode == Keys.OemSemicolon))
            {
                txtQuickExec.Focus();
                e.SuppressKeyPress = e.Handled = true;
                return;
            }
            // Focus Exchange List (Alt+S)
            if (e.KeyData == (Keys.Alt | Keys.S))
            {
                lvExchanges.Focus();
                e.SuppressKeyPress = e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.H && e.Modifiers == Keys.Control)
            {
                Win32UI.activateTitledTab("Headers", tabsRequest);
                Win32UI.activateTitledTab("Headers", tabsResponse);
                tabsViews.SelectedTab = pageInspectors;
                e.SuppressKeyPress = e.Handled = true;
                return;
            }

            // TODO: Support fontsize adjustment with CTRL+Plus, Ctrl+Minus, and so on
        }

        public void actReportStatistics()
        {
            actReportStatistics(false);
        }
        internal void actReportStatistics(bool bNow)
        {
            UpdateStatusBar(false);

            if (CApp.PauseReporting) return;
            if (bNow)
            {
                actUpdateReport();
            }
            else
            {
                if (!timerReportUpdater.Enabled) timerReportUpdater.Start();
            }
        }

        private void UpdateMemoryCounter()
        {
            double mb = Process.GetCurrentProcess().PrivateMemorySize64 / (1024.0 * 1024.0);
            CApp.UIInvokeAsync((MethodInvoker)(() => { tslMemory.Text = $"{mb:N1}mb"; }), null);
        }

        private void UpdateStatusBar(bool bNow)
        {
            // If updates are paused, we throttle UI updates and start a background task
            // to periodically post updates to the UI thread.
            if (!bNow && (CApp.PauseReporting))
            {
                // TODO: Background update
                //    ScheduledTasks.ScheduleWork("UpdateStatusBar", 100,
                //      () => { CApp.UIInvokeAsync((MethodInvoker)_UpdateStatusBar, null); });
                //return;
            }

            if (CApp.UI.InvokeRequired)
            {
                CApp.UIInvokeAsync((MethodInvoker)_UpdateStatusBar, null);
                return;
            }

            _UpdateStatusBar();
        }


        private void _UpdateStatusBar()
        {
            Debug.Assert(!CApp.UI.InvokeRequired);
            int cSelected = lvExchanges.SelectedCount;
            if (cSelected < 1)
            {
                tslSelCount.Text = $"{lvExchanges.TotalCount:N0}{(lvExchanges.IsScrolledToBottom() ? "**TODO: BOTTOM**" : string.Empty)}";
            }
            else
            {
                tslSelCount.Text = $"{cSelected:N0} / {lvExchanges.TotalCount:N0}";
            }
        }

        public void actUpdateReport()
        {
            CApp.OnCalculateReport(GetSelectedExchanges());
        }

        private void miFileSaveSelectedSAZ_Click(object sender, EventArgs e)
        {
            actSaveExchanges(GetSelectedExchanges());
        }

        private void lvExchanges_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.XButton1)
            {
                lvExchanges.ActivatePreviousItem();
            }
        }

        private void txtLogSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (txtLogSearch.Text.Length > 0)
                {
                    txtLogSearch.Clear();
                }
                else
                {
                    txtLogSearch.Clear();
                    rtbLog.Focus();
                }
                e.SuppressKeyPress = e.Handled = true;
                return;
            }

            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = e.Handled = true;

            string searchText = txtLogSearch.Text;
            if (searchText.Length == 0)
            {
                SetStatusText("Enter text to search the log.");
                return;
            }

            int start = rtbLog.SelectionStart + rtbLog.SelectionLength;
            int match = rtbLog.Find(searchText, start, RichTextBoxFinds.None);
            if (match < 0 && start > 0)
            {
                match = rtbLog.Find(searchText, 0, RichTextBoxFinds.None);
            }
            if (match < 0)
            {
                SetStatusText("No matching text found in the log.");
                return;
            }
            rtbLog.ScrollToCaret();
            SetStatusText("Log match found. Press Enter to find the next match.");
        }

        private void rtbLog_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.A && e.Modifiers == Keys.Control)
            {
                rtbLog.SelectAll();
                e.SuppressKeyPress = e.Handled = true;
                return;
            }
            if (e.KeyCode == Keys.X && e.Modifiers == Keys.Control)
            {
                queuedLogEntries.Clear();
                rtbLog.Clear();
                e.SuppressKeyPress = e.Handled = true;
                return;
            }

        }

        private void miFileSaveRequestFull_Click(object sender, EventArgs e)
        {
            try
            {
                Exchange[] arrExchanges = GetSelectedExchanges();
                if (arrExchanges.Length < 1) { CApp.DoNotifyUser("No Exchanges were selected.", "Nothing to Save"); return; }
                for (int iX = 0; iX < arrExchanges.Length; ++iX)
                {
                    Exchange exch = arrExchanges[iX];
                    string sFilename = Utilities.ObtainSaveFilename($"Export Exchange {exch.id} Request", "Any file|*.*");
                    if (!sFilename.HasText()) return;
                    File.WriteAllBytes(sFilename, exch.FullRequestBytes());
                }
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Request dumping failed");
            }
        }

        private void miFileSaveRequestBody_Click(object sender, EventArgs e)
        {
            try
            {
                Exchange[] arrExchanges = GetSelectedExchanges();
                if (arrExchanges.Length < 1) { CApp.DoNotifyUser("No Exchanges were selected.", "Nothing to Save"); return; }
                for (int iX = 0; iX < arrExchanges.Length; ++iX)
                {
                    Exchange exch = arrExchanges[iX];
                    string sFilename = Utilities.ObtainSaveFilename($"Export Exchange {exch.id} Request Body", "Any file|*.*");
                    if (!sFilename.HasText()) return;
                    File.WriteAllBytes(sFilename, exch.RequestBody);
                }
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Request dumping failed");
            }
        }

        private void miFileSaveResponseFull_Click(object sender, EventArgs e)
        {
            try
            {
                Exchange[] arrExchanges = GetSelectedExchanges();
                if (arrExchanges.Length < 1) { CApp.DoNotifyUser("No Exchanges were selected.", "Nothing to Save"); return; }
                for (int iX = 0; iX < arrExchanges.Length; ++iX)
                {
                    Exchange exch = arrExchanges[iX];
                    string sFilename = Utilities.ObtainSaveFilename($"Export Exchange {exch.id} Response", "Any file|*.*");
                    if (!sFilename.HasText()) return;
                    File.WriteAllBytes(sFilename, exch.FullResponseBytes());
                }
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Response dumping failed");
            }
        }

        private void miFileSaveResponseBody_Click(object sender, EventArgs e)
        {
            try
            {
                Exchange[] arrExchanges = GetSelectedExchanges();
                if (arrExchanges.Length < 1) { CApp.DoNotifyUser("No Exchanges were selected.", "Nothing to Export"); return; }
                for (int iX = 0; iX < arrExchanges.Length; ++iX)
                {
                    Exchange exch = arrExchanges[iX];
                    string sFilename = Utilities.ObtainSaveFilename($"Export Exchange {exch.id} Response Body", "Any file|*.*");
                    if (!sFilename.HasText()) return;
                    File.WriteAllBytes(sFilename, exch.ResponseBody);
                }
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Response dumping failed");
            }
        }

        private void miEditUndelete_Click(object sender, EventArgs e)
        {
            lvExchanges.UndeleteItems();
        }

        private void mnuExchange_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            miExchangeReplay.Enabled = miExchangeProperties.Enabled =
                mnuExchangeCopy.Enabled = miExchangeSelect.Enabled = (lvExchanges.SelectedCount > 0);
            miExchangeDiffer.Enabled = lvExchanges.SelectedCount == 2;
        }

        private void miExchangeCopyUrl_Click(object sender, EventArgs e)
        {
            Exchange[] arrExch = GetSelectedExchanges();
            if (arrExch.Length < 1) return;
            if (arrExch.Length == 1)
            {
                Clipboard.SetText(arrExch[0].fullUrl);
                return;
            }
            StringBuilder sbAllUrls = new StringBuilder();
            for (var ix = 0; ix < arrExch.Length; ++ix)
            {
                sbAllUrls.AppendLine(arrExch[ix].fullUrl);
            }
            Clipboard.SetText(sbAllUrls.ToString());
        }

        private void miExchangeCopyHeaders_Click(object sender, EventArgs e)
        {
            Exchange[] arrExch = GetSelectedExchanges();
            if (arrExch.Length < 1) return;
            StringBuilder sbAllHeaders = new StringBuilder();
            for (var ix = 0; ix < arrExch.Length; ++ix)
            {
                sbAllHeaders.AppendLine(arrExch[ix].RequestHeaders?.ToString());
                sbAllHeaders.AppendLine(arrExch[ix].ResponseHeaders?.ToString());
                sbAllHeaders.AppendLine("==================================");
            }
            Clipboard.SetText(sbAllHeaders.ToString());
        }

        private void miEditCopyForAI_Click(object sender, EventArgs e)
        {
            Exchange[] arrExch = GetSelectedExchanges();
            if (arrExch.Length < 1) return;
            StringBuilder sbAllHeaders = new StringBuilder();
            for (var ix = 0; ix < arrExch.Length; ++ix)
            {
                sbAllHeaders.AppendLine("```http");
                sbAllHeaders.AppendLine(arrExch[ix].RequestHeaders?.ToString());
                sbAllHeaders.AppendLine(arrExch[ix].ResponseHeaders?.ToString());
                sbAllHeaders.AppendLine("```");
            }
            Clipboard.SetText(sbAllHeaders.ToString());
        }

        private void miEditCopyExchange_Click(object sender, EventArgs e)
        {
            Exchange[] xc = GetSelectedExchanges();
            if (xc.Length < 1) return;

            StringBuilder sbAllExchanges = new StringBuilder();
            for (int i = 0; i < xc.Length; i++)
            {
                sbAllExchanges.AppendLine(xc[i].ToLongString());
                if (i < xc.Length - 1) sbAllExchanges.AppendLine("---------------------");
            }
            Clipboard.SetText(sbAllExchanges.ToString());
        }

        private void miEditCopyURL_Click(object sender, EventArgs e)
        {
            Exchange[] xc = GetSelectedExchanges();
            if (xc.Length < 1) return;
            if (xc.Length == 1)
            {
                Clipboard.SetText(xc[0].fullUrl);
                return;
            }
            StringBuilder sbAllUrls = new StringBuilder();
            for (int i = 0; i < xc.Length; i++)
            {
                sbAllUrls.AppendLine(xc[i].fullUrl);
            }
            Clipboard.SetText(sbAllUrls.ToString());
        }

        private void miEditCopySummary_Click(object sender, EventArgs e)
        {
            Exchange[] arrXC = GetSelectedExchanges();
            if (arrXC.Length < 1) return;

            // Create a stringbuilder, and copy to it a tab-delimited set of columns. On the first line,
            // copy the column titles, and on each subsequent line, copy the values in that column for each exchange.
            StringBuilder sbSummary = new StringBuilder();
            foreach (ColumnHeader column in lvExchanges.Columns)
            {
                sbSummary.Append(column.Text).Append('\t');
            }
            sbSummary.AppendLine();
            foreach (var xc in arrXC)
            {
                ListViewItem oLVI = xc.ViewItem;
                if (null == oLVI) continue;
                for (var x = 0; x < oLVI.SubItems.Count; x++)
                {
                    sbSummary.Append(oLVI.SubItems[x].Text).Append('\t');
                }
                sbSummary.AppendLine();
            }
            Clipboard.SetText(sbSummary.ToString());
        }

        private void miEditCopyTerseSummary_Click(object sender, EventArgs e)
        {
            Exchange[] xc = GetSelectedExchanges();
            if (xc.Length < 1) return;
            Clipboard.SetText(SummarizeExchanges(xc));
        }

        private static string SummarizeExchanges(Exchange[] xc)
        {
            StringBuilder sbSummary = new StringBuilder();
            for (int i = 0; i < xc.Length; i++)
            {
                sbSummary.AppendLine($"{xc[i].RequestHeaders?.HTTPMethod} {xc[i].fullUrl}");

                sbSummary.AppendLine($"<- {xc[i].SummarizeResponse()}");
                sbSummary.AppendLine();
            }
            return sbSummary.ToString();
        }

        private void miRulesCustomize_Click(object sender, EventArgs e)
        {
            Utilities.LaunchNative(CONFIG.GetPath("Editor.Code"), $"\"{CONFIG.GetPath("RulesScript")}\"");
        }

        private void tsbComment_Click(object sender, EventArgs e)
        {
            actSetComment();
        }

        private void actSetComment(string sNewComment = null)
        {
            Exchange[] arrExch = GetSelectedExchanges();
            if (arrExch.Length < 1) return;
            if (null == sNewComment)
            {
                string sExistingComment = arrExch[0]["ui-comments"];
                frmPrompt.PromptOptions po = new frmPrompt.PromptOptions
                {
                    Title = "Add Comment",
                    PromptText = "Enter a comment for the selected Exchanges:",
                    DefaultValue = sExistingComment,
                    ReturnNullOnCancel = true,
                };
                sNewComment = frmPrompt.GetUserString(po);
            }
            foreach (Exchange exch in arrExch)
            {
                exch["ui-comments"] = sNewComment;
                updateLVI(exch);
            }
        }

        private void tsmiRemoveAll_Click(object sender, EventArgs e)
        {
            lvExchanges.ClearExchanges();
        }

        private void tsmiRemoveDuplicateResponseBodies_Click(object sender, EventArgs e)
        {
            CApp.PauseReporting = true;
            lvExchanges.BeginUpdate();
            lvExchanges.SelectedItems.Clear();
            var hasherMD5 = MD5.Create();
            HashSet<string> setHashes = new HashSet<string>();
            Exchange[] arrXC = GetAllExchanges();
            // For each exchange, if there's a response body, calculate its MD5 hash (collision is wildly implausible and
            // basically harmless) and store it in a HashSet. If we see it again, then we found a duplicate and we
            // should select it for removal.
            foreach (Exchange exch in arrXC)
            {
                if (exch.ResponseBody?.Length > 0)
                {
                    byte[] hash = hasherMD5.ComputeHash(exch.ResponseBody);
                    string hashString = Convert.ToBase64String(hash);

                    if (!setHashes.Add(hashString))
                    {
                        ListViewItem lvi = exch.ViewItem;
                        if (null != lvi) lvi.Selected = true;
                    }
                }
            }

            lvExchanges.RemoveSelected();
            lvExchanges.EndUpdate();
            CApp.PauseReporting = false;
            actUpdateInspector(true, true);
            CApp.Log.Log("Removed Exchanges with duplicate response bodies.");
        }

        private void tsmiRemoveImages_Click(object sender, EventArgs e)
        {
            actSelectExchangesMatchingCriteria((exch) => exch.MediaType.OICStartsWith("image/"));
            lvExchanges.RemoveSelected();
            CApp.Log.Log("Removed Exchanges with image responses.");
        }

        private void tsmiNon200Response_Click(object sender, EventArgs e)
        {
            actSelectExchangesMatchingCriteria((exch) => exch.responseCode != 200);
            lvExchanges.RemoveSelected();
            CApp.Log.Log("Removed Exchanges with non-HTTP/200 responses.");
        }

        private void tsmiRemoveConnects_Click(object sender, EventArgs e)
        {
            actSelectExchangesMatchingCriteria((exch) => exch.RequestHeaders?.HTTPMethod.OICEquals("CONNECT"));
            lvExchanges.RemoveSelected();
            CApp.Log.Log("Removed Exchanges with HTTP CONNECT requests.");
        }

        private void miEditCopyCurl_Click(object sender, EventArgs e)
        {
            actCopyCURLCommands(GetSelectedExchanges());
        }

        private void miEditMarkStrikeout_Click(object sender, EventArgs e)
        {

        }

        private void miEditMarkRed_Click(object sender, EventArgs e)
        {
            MarkExchanges(GetSelectedExchanges()); // TODO:Color
        }

        private void miEditMarkBlue_Click(object sender, EventArgs e)
        {
            MarkExchanges(GetSelectedExchanges());
        }

        private void miEditMarkGold_Click(object sender, EventArgs e)
        {
            MarkExchanges(GetSelectedExchanges()); // TODO:Color
        }

        private void miEditMarkUnmark_Click(object sender, EventArgs e)
        {
            // TODO: MarkExchanges(GetSelectedExchanges());
        }

        private void tabsViews_DragOver(object sender, DragEventArgs e)
        {
            // If dragging a file or Exchanges over the tabset, switch to the tab underneath.
            try
            {
                Point ptOver = tabsViews.PointToClient(new Point(e.X, e.Y));
                if (ptOver.Y < tabsViews.DisplayRectangle.Top) // Only switch if the mouse is over the tab strip, not the tab content area.
                {
                    TabPage tp = Win32UI.GetPageFromPoint(tabsViews, ptOver);
                    if ((null != tp) && (tp != tabsViews.SelectedTab))
                    {
                        tabsViews.SelectedTab = tp;
                        // TODO: If we have multiple rows, things get weird.
                    }
                }
            }
            catch
            {
                // Eat it
            }
        }

        private void miExchangeProperties_Click(object sender, EventArgs e)
        {
            actInspectInNewWindow();
        }

        private void miExchangeDiffer_Click(object sender, EventArgs e)
        {
            actDiffExchanges();
        }

        private void miReplayReplay_Click(object sender, EventArgs e)
        {
            actReplaySelected(Control.ModifierKeys == Keys.Shift);
        }

        private void miReplayWithComposer_Click(object sender, EventArgs e)
        {
            Exchange exch = GetFirstSelectedExchange();
            if (null == exch) return;
            actActivateTab("Composer");
            if (CApp.oComposer != null)
            {
                CApp.oComposer.PopulateFromExchange(exch);
            }
        }

        private void miReplayUnconditionally_Click(object sender, EventArgs e)
        {
            actReplaySelected(false, true);
        }

        private void miReplayWithBreakpoint_Click(object sender, EventArgs e)
        {
            actReplaySelected(true);
        }

        private void miExchangeInsertSplitter_Click(object sender, EventArgs e)
        {
            actAddFauxSplitter();
        }

        private void miExchangeReplayBrowser_Click(object sender, EventArgs e)
        {
            Utilities.LaunchHyperlink(GetFirstSelectedExchange()?.fullUrl);
        }

        private void miExchangeSelectMatchingColumn_Click(object sender, EventArgs e)
        {
            var (lvi, ixCol) = lvExchanges.GetItemAndColumnAt(lvExchanges.PointToClient(mnuExchange.Location));

            if (null == lvi || ixCol < 0)
            {
                CApp.UI.SetStatusText("Column not found.");
                return;
            }
            lvExchanges.selectMatchingColumn(lvi, ixCol, Control.ModifierKeys == Keys.Control);
        }

        private void miExchangeSelect_DropDownOpening(object sender, EventArgs e)
        {
            var (lvi, ixCol) = lvExchanges.GetItemAndColumnAt(lvExchanges.PointToClient(mnuExchange.Location));
            if (null == lvi || ixCol < 0)
            {
                miExchangeSelectMatchingColumn.Text = "(no column found)";
                miExchangeSelectMatchingColumn.Enabled = false;
            }
            else
            {
                miExchangeSelectMatchingColumn.Enabled = true;
                miExchangeSelectMatchingColumn.Text = $"'{lvExchanges.Columns[ixCol].Text}'='{lvi.SubItems[ixCol].Text.Ellipsize(16)}'";
            }
        }

        private void miExchangeCopyColumn_Click(object sender, EventArgs e)
        {
            var (lvi, ixCol) = lvExchanges.GetItemAndColumnAt(lvExchanges.PointToClient(mnuExchange.Location));
            if (null == lvi || ixCol >= 0)
            {
                Clipboard.SetText(lvi.SubItems[ixCol].Text);
            }
        }

        private void mnuExchangeCopy_DropDownOpening(object sender, EventArgs e)
        {
            var (lvi, ixCol) = lvExchanges.GetItemAndColumnAt(lvExchanges.PointToClient(mnuExchange.Location));
            if (null == lvi || ixCol < 0)
            {
                miExchangeCopyColumn.Text = "(no column found)";
                miExchangeCopyColumn.Enabled = false;
            }
            else
            {
                miExchangeCopyColumn.Enabled = true;
                miExchangeCopyColumn.Text = $"'{lvi.SubItems[ixCol].Text.Ellipsize(24)}'";
            }
        }
        public void AddExtensionToolsMenuItem(ToolStripMenuItem tsmi)
        {
            // TODO: put this in the right place rather than just shoving it at the top.
            miTools.DropDownItems.Insert(0, tsmi);
        }

        private async void tsbScreenshot_Click(object sender, EventArgs e)
        {
            if (!tsbScreenshot.Enabled) return;

            string originalText = tsbScreenshot.Text;
            ToolStripItemDisplayStyle originalDisplayStyle = tsbScreenshot.DisplayStyle;
            tsbScreenshot.Enabled = false;
            if (Control.ModifierKeys == Keys.Control)
            {
                actAddDesktopScreenshot();
                tsbScreenshot.Enabled = true;
                return;
            }
            try
            {
                tsbScreenshot.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
                for (int remaining = 4; remaining > 0; --remaining)
                {
                    tsbScreenshot.Text = (remaining > 1) ? $"( {remaining}... )" : "** say cheese! **";
                    tsToolbar.Refresh();
                    await Task.Delay(1000);
                    if (IsDisposed || Disposing || tsbScreenshot.IsDisposed) return;
                }
                tsbScreenshot.DisplayStyle = originalDisplayStyle;
                tsbScreenshot.Text = originalText;
                tsToolbar.Refresh();
                actAddDesktopScreenshot();
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Screenshot failed.");
            }
            finally
            {
                if (!IsDisposed && !Disposing && !tsbScreenshot.IsDisposed)
                {
                    tsbScreenshot.Text = originalText;
                    tsbScreenshot.DisplayStyle = originalDisplayStyle;
                    tsbScreenshot.Enabled = true;
                }
            }
        }
    }

    public class LongRunningOperation : IDisposable
    {
        DateTime dtFirstProgress = DateTime.MinValue;
        private bool PreventSleepOrExit { get; set; }

        public string OperationName { get; private set; }

        public string StatusText { get; private set; }

        private ToolStripProgressBar ProgressUI;

        private readonly CancelEventHandler ceh;

        public LongRunningOperation(string sOperationName, bool bPreventSleepOrExit)
        {
            OperationName = sOperationName;
            PreventSleepOrExit = bPreventSleepOrExit;
            if (PreventSleepOrExit)
            {
                ceh = (s, e) =>
                {
                    CApp.UI.SetStatusText($"Please wait for the operation ('{OperationName}') to complete before exiting.");
                    e.Cancel = true;
                };
                //                Stopwatch oSW = Stopwatch.StartNew();
                PlatformAPI.PreventSleep(keepDisplayOn: false);
                //                Trace.WriteLine($"LongRunningOperation calling PreventSleep took {oSW.ElapsedMilliseconds}ms.");
                CApp.BeforeAppShutdown += ceh;
            }
            CreateUI();
        }

        private void CreateUI()
        {
            if (CApp.UI.InvokeRequired)
            {
                CApp.UI.Invoke((Action)(() => { CreateUI(); }));
                return;
            }
            ProgressUI = new ToolStripProgressBar()
            {
                Maximum = 100,
                ToolTipText = OperationName,
                Style = ProgressBarStyle.Marquee
            };

            CApp.UI.tsToolbar.Items.Insert(0, ProgressUI);
            Application.DoEvents();
        }

        public void UpdateProgress(int iPercent, string sStatusText = null)
        {
            if (dtFirstProgress == DateTime.MinValue) dtFirstProgress = DateTime.Now;
            if (sStatusText.HasText()) StatusText = sStatusText;
            if (iPercent >= 100)
            {
                iPercent = 100;
                //Dispose();
                return;
            }
            if (null != ProgressUI)
            {
                UpdateUIForProgress(iPercent);
            }
        }

        private void UpdateUIForProgress(int iPercent)
        {
            if (CApp.UI.InvokeRequired)
            {
                CApp.UI.Invoke((Action)(() => { UpdateUIForProgress(iPercent); }));
                return;
            }
            if (null == ProgressUI) return;

            ProgressUI.Style = ProgressBarStyle.Blocks;
            ProgressUI.Value = iPercent;
            ProgressUI.ToolTipText = $"{OperationName}: {StatusText} ({iPercent:N0}%)";
            Application.DoEvents();
        }

        public void Dispose()
        {
            if (CApp.UI.InvokeRequired)
            {
                CApp.UI.Invoke((Action)(() => { Dispose(); }));
                return;
            }
            CApp.Log.Log($"Long-running operation '{OperationName}' completed.");
            if (dtFirstProgress != DateTime.MinValue && DateTime.Now - dtFirstProgress > TimeSpan.FromSeconds(5))
            {
                CApp.UI.actFlashWindow();
            }
            CApp.UI.tsToolbar.Items.Remove(ProgressUI);
            ProgressUI.Dispose(); ProgressUI = null;
            if (PreventSleepOrExit)
            {
                PlatformAPI.RestoreSleep();
                CApp.BeforeAppShutdown -= ceh;
            }
        }
    }
}
