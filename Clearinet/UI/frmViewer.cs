using Clearinet.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Clearinet
{
    public partial class frmViewer : Form
    {
        public frmViewer()
        {
            InitializeComponent();
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

        private Label lblInspectorInstruction;
        private MRU mruRecents;


        private static System.Windows.Forms.Timer timerReportUpdater = new System.Windows.Forms.Timer();

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
            throw new NotImplementedException();
        }
        public void actActivateTab(string sTabTitle)
        {
            Win32UI.activateTitledTab(sTabTitle, tabsViews);
        }
        public void actDoFind()
        {
            frmFind.BeginFinding();
        }

        public void actShowTextWizard(string sText)
        {
            frmTextWizard oFrm = new frmTextWizard(sText)
            {
                Left = this.Left + 100,
                Top = this.Top + 100
            };
            oFrm.Show(this);
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
        #endregion ACTIONS

        private void miTextWizard_Click(object sender, EventArgs e)
        {
            actShowTextWizard(null);
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
            // TODO: Get selected exchanges...
            Exchange[] arrEx = GetSelectedExchanges();
            // ... and for each, call RefreshListViewItem to ensure
            // all column data is up-to-date.
        }

        public Exchange[] GetSelectedExchanges()
        {
            int iSelCount = lvExchanges.SelectedItems.Count;
            Exchange[] arrExchanges = new Exchange[iSelCount];
            for (int ix = 0; ix < iSelCount; ++ix)
            {
                arrExchanges[ix] = lvExchanges.SelectedItems[ix].Tag as Exchange;
            }
            return arrExchanges;
        }

        public Exchange[] GetAllExchanges()
        {
            int iCount = lvExchanges.Items.Count;
            Exchange[] arrExchanges = new Exchange[iCount];
            for (int ix = 0; ix < iCount; ++ix)
            {
                arrExchanges[ix] = lvExchanges.Items[ix].Tag as Exchange;
            }
            return arrExchanges;
        }

        /// <summary>
        /// Update the Inspector and Status bar.
        /// LEGACY API. Do not rename.
        /// </summary>
        public void actRefreshUI(bool bBecauseSelectionChanged)
        {
            // TODO: This should ensure that all inspectors are selected properly,
            // any info shown for the current session in the status bar is correct, etc.
            if (CApp.isClosing) return;
            actUpdateInspector(true, true);

            actReportStatistics();

            int cSelected = lvExchanges.SelectedCount;
            if (cSelected == 1) tslInfo.Text = GetFirstSelectedExchange().RequestHeaders?.RequestPath ?? string.Empty;
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
            Application.SetCompatibleTextRenderingDefault(false);   // TODO: Do we want this?
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
                MessageBox.Show("FAILED TO GET MUTEX?. TODO:Activate it!");
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
                    Win32UI.BringTitledWindowToForeground("Clearinet Web Debugger");
                    return;
                }

                CApp._frmMain = new frmViewer();

                frmSplashScreen.SetStatusText("Loading extensions...");
                CApp.oExtensions = new Extensions();
                CApp.oTranscoders = new Transcoders();
                doPopulateInspectorTabs();

                frmSplashScreen.SetStatusText("Loading script engine...");
                CApp.CreateScriptEngine();

                SAZFile.SupplyPassword = () =>
                {
                    var fpo = new frmPrompt.PromptOptions()
                    {
                        Kind = frmPrompt.PromptKind.Password,
                        OwnerWindow = CApp.UI,
                        ReturnNullOnCancel = true,
                        Title = "Password-Protected Session Archive",
                        PromptText = "Enter the password to decrypt this Session Archive:"
                    };
                    return frmPrompt.GetUserString(fpo);
                };

                Application.Run(CApp._frmMain);
                if (bMutexAcquired) oMutex.ReleaseMutex();
            }
        }

        private static void doPopulateInspectorTabs()
        {
            foreach (RequestInspectorBase oRI in CApp.oExtensions.m_RequestInspectors.Values)
            {
                try
                {
                    TabPage oPage = new TabPage
                    {
                        Tag = oRI,
                        Text = oRI.TabTitle
                    };
                    oRI.AddToTab(oPage);  // TODO: We should change this to delay load the UI as UI components will frequently be heavy.
                    CApp.UI.tabsRequest.TabPages.Add(oPage);
                }
                catch (Exception eX) { CApp.ReportException(eX, "Inspector failed"); }
            }

            foreach (ResponseInspectorBase oRI in CApp.oExtensions.m_ResponseInspectors.Values)
            {
                try
                {
                    TabPage oPage = new TabPage
                    {
                        Tag = oRI,
                        Text = oRI.TabTitle
                    };
                    oRI.AddToTab(oPage);  // TODO: We should change this to delay load the UI as UI components will frequently be heavy.
                    CApp.UI.tabsResponse.TabPages.Add(oPage);
                }
                catch (Exception eX) { CApp.ReportException(eX, "Inspector failed"); }
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
        }

        private void frmViewer_Load(object sender, EventArgs e)
        {
            CONFIG.RetrieveLayout(this);

            // TODO: Add event handlers for allow back/forward mouse buttons to change between active tabs
            // TODO: Grab system network config
            // TODO: Start the core Proxy listener

            InitializeUIFromPrefs();
            CApp.Prefs.AddWatcher("app.ui.", OnPrefChange);

            if (CApp.Prefs.GetBoolPref("app.ui.darkmode", false)) ThemeManager.ApplyDarkMode(this);

            rtbStatistics.BackColor = rtbLog.BackColor = CONFIG.colorDisabledEdit;
            CApp.ProxyAttach += HandleProxyAttached;
            CApp.ProxyDetach += HandleProxyDetached;

            frmSplashScreen.CloseSplashScreen();
            CApp.OnAppBoot();

            if (CApp.Prefs.GetBoolPref("app.attach_on_startup", true)
                && !Environment.CommandLine.OICContains("noattach")) { CApp.actAttachProxy(); }

            timerReportUpdater.Interval = 200; // MS. TODO: Make Configurable.
            timerReportUpdater.Tick += new EventHandler(timerReportUpdater_Tick);
            CApp.CalculateReport += UpdateStatisticsTab;

            this.lvExchanges.DragDrop += lvExchanges_DragDrop;
            this.lvExchanges.DragEnter += lvExchanges_DragEnter;
            this.lvExchanges.AllowDrop = true;

            allExchangesToolStripMenuItem.Click += AllExchangesToolStripMenuItem_Click;

            Win32UI.SetCueText(tstxtLookup.Control, CApp.Prefs.GetStringPref("app.ui.toolbar.lookupcuetext", "Search MDN..."));
            ImportAnyStartupArchives();
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
                $"\r\nRequest Headers:\t{cRequestHeaderBytes:N0} bytes" +
                $"\r\nRequest Body:\t{cRequestBodyBytes:N0} bytes" +
                $"\r\nResponse Headers:\t{cResponseHeaderBytes:N0} bytes" +
                $"\r\nResponse Body:\t{cResponseBodyBytes:N0} bytes";
                return;
            }

            rtbStatistics.Text = $"You've selected {arrExchanges.Length} Exchanges\r\n" +
                $"\r\nRequest Headers:\t{cRequestHeaderBytes:N0} bytes" +
                $"\r\nRequest Bodies:\t{cRequestBodyBytes:N0} bytes" +
                $"\r\nResponse Headers:\t{cResponseHeaderBytes:N0} bytes" +
                $"\r\nResponse Bodies:\t{cResponseBodyBytes:N0} bytes";
        }

        private void timerReportUpdater_Tick(object sender, EventArgs e)
        {
            timerReportUpdater.Stop();
            actUpdateReport();
        }

        /// <summary>
        /// Add some data for development purposes.REMOVE THIS
        /// </summary>
        internal void TODOAddSampleData()
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
                    ResponseHeaders = new HTTPResponseHeaders(200, "OK, I guess", new string[] { $"FirstHeader: {iX}", $"SecondHeader: {iX}", $"ThirdHeader: {iX}", "Content-Type: text/plain; charset=utf-16" }),
                };
                hrh["Content-Length"] = x.ResponseBody.Length.ToString();
                x.state = (ExchangeState)(iX);
                addExchangeToListView(x);
            }
        }

        private void miFileLoadSAZ_Click(object sender, EventArgs e)
        {
            var sFilename = Utilities.ObtainOpenFilename("Open SAZ", "SAZ Files (*.saz)|*.saz");
            if (!sFilename.HasText()) return;
            actLoadSessionArchive(sFilename);
        }

        private void miFileImport_Click(object sender, EventArgs e)
        {
            try
            {
                // TODO
                string sFilename = Utilities.ObtainOpenFilename("Import Exchanges...", "Common formats|*.har;*.json|Any file|*.*|Password Protected SAZ|*.saz");
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
                if (2 == iType)
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
                SAZFile.SaveTo(sFilename, arrExchanges, sPassword);
                CApp.UI.mruRecents.PushFile(sFilename);
                CApp.UI.SetStatusText($"{(sPassword.HasText() ? "Encrypted" : "Saved")} {arrExchanges.Length} Exchanges to {PlatformAPI.CompactPath(sFilename, 48)}");
            }
            catch (Exception eX) { CApp.ReportException(eX, "Save failed"); }
            return true;
        }

        private void MiFileSaveSelectedSAZ_Click(object sender, EventArgs e)
        {
            //nyi
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
                foreach (string sPath in arrFiles)
                {
                    if (sPath.OICEndsWith(".saz"))
                        actLoadSessionArchive(sPath);
                    else
                        actImportFile(sPath);
                    e.Effect = DragDropEffects.Copy;
                    return;
                }
            }
        }

        public Exchange GetFirstSelectedExchange()
        {
            return (lvExchanges.SelectedCount == 0) ? null : lvExchanges.SelectedItems[0].Tag as Exchange;
        }

        // Activate Inspectors tab and try to pick the best two.
        public void actInspectSession()
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)actInspectSession);
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

        private void ImportAnyStartupArchives()
        {
            string[] arrTokens = Environment.GetCommandLineArgs();
            for (int i = 1; i < arrTokens.Length; ++i)
            {
                // Filenames have a dot.
                if (!arrTokens[i].Contains(".")) continue;
                if (arrTokens[i].OICEndsWith(".saz"))
                {
                    actLoadSessionArchive(arrTokens[i]);
                    break;
                }
                else
                {
                    actImportFile(arrTokens[i]);
                }
            }
        }

        private void AllExchangesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Exchange[] arrExchanges = GetAllExchanges();
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

                var dictOptions = new Dictionary<string, object>();
                dictOptions.Add("Filename", sFilename);

                var exchImported = oExporter.ExportExchanges(tt.FormatName, arrExchanges, dictOptions,
                                null /*  (s, pcea) => CApp.Log.Log($"Importing {tt.FormatName}: {pcea.CurrentStatus}")*/);

                CApp.UI.SetStatusText($"Exported {arrExchanges.Length} Exchanges to {PlatformAPI.CompactPath(sFilename, 32)} using the '{tt.FormatName}' exporter.");
            }
        }
        private void actImportFile(string sPath)
        {
            if (sPath.OICEndsWith(".saz"))
            {
                actLoadSessionArchive(sPath);
                return;
            }
            // TODO: Can we put importer filenames into the MRU and have the right thing happen?
            if (sPath.OICEndsWithAny(".har", ".json"))
            {
                TranscoderTuple tt = CApp.oTranscoders.GetImporterForExt(Path.GetExtension(sPath));
                if (null == tt)
                {
                    CApp.DoNotifyUser($"No importer found for {Path.GetExtension(sPath)}", "NYI");
                    return;
                }
                IExchangeImporter oImporter = (IExchangeImporter)Activator.CreateInstance(tt.typeTranscoder);

                var dictOptions = new Dictionary<string, object>();
                dictOptions.Add("Filename", sPath);

                var exchImported = oImporter.ImportExchanges(tt.FormatName, dictOptions,
                                null /*  (s, pcea) => CApp.Log.Log($"Importing {tt.FormatName}: {pcea.CurrentStatus}")*/);

                foreach (Exchange x in exchImported)
                {
                    addExchangeToListView(x);
                }
                CApp.UI.SetStatusText($"Imported {exchImported.Length} Exchanges from {PlatformAPI.CompactPath(sPath, 32)} using the '{tt.FormatName}' importer.");
            }
        }

        /// <summary>
        /// LEGACY API. Do not rename. Load a .SAZ file.
        /// </summary>
        /// <param name="sPath"></param>
        public bool actLoadSessionArchive(string sPath)
        {
            try
            {
                using (SAZFile sazFile = SAZFile.LoadFrom(sPath))
                {
                    CApp.Log.Log($"Loaded {sazFile.Exchanges.Count} Exchanges from {PlatformAPI.CompactPath(sPath, 32)}. {sazFile.sComment}");
                    SetStatusText($"Loaded {sazFile.Exchanges.Count} Exchanges from {PlatformAPI.CompactPath(sPath, 32)}");
                    mruRecents.PushFile(sPath);

                    foreach (Exchange x in sazFile.Exchanges)
                    {
                        addExchangeToListView(x);
                    }
                }
                return true;
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "actLoadSessionArchive Failed");
                return false;
            }
        }

        private void addExchangeToListView(Exchange x)
        {
            ListViewItem lvi = new ListViewItem(x.id.ToString())
            {
                Tag = x
            };
            lvi.SubItems.Add(x.responseCode.ToString());
            lvi.SubItems.Add(x.RequestHeaders.HTTPMethod);
            lvi.SubItems.Add(x.host);
            lvi.SubItems.Add(x.RequestHeaders.RequestPath);
            x.ViewItem = lvi;
            lvExchanges.Items.Add(lvi);
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
            CApp.OnAppShutdown();
        }

        internal void UpdateLog(string sLog)
        {
            Debug.Assert(!this.InvokeRequired);
            rtbLog.AppendText(sLog + "\r\n");

            // TODO: Support formatting commands for bold, italics, and underline:  !, /, _
            // TODO: Scroll view to bottom on message add if LOG tab is foremost
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

        private void miEditFind_Click(object sender, EventArgs e)
        {
            frmFind.BeginFinding();
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
            // Disable controls if inapplicable
            miEditPasteAsExchanges.Enabled = Clipboard.ContainsImage() | Clipboard.ContainsText() | Clipboard.ContainsFileDropList();
            miEditRemove.Enabled = miEditFind.Enabled = lvExchanges.Items.Count > 0;
            miEditMark.Enabled = lvExchanges.SelectedCount > 0;
            // TODO: Moar!
        }

        private void miFile_DropDownOpening(object sender, EventArgs e)
        {
            if (lvExchanges.Items.Count > 0)
            {
                saveToolStripMenuItem.Enabled = true; // TODO: Rename!!!
                miFileSaveSelected.Enabled = (lvExchanges.SelectedCount > 0);
                miFileExport.Enabled = true;
            }
            else
            {
                saveToolStripMenuItem.Enabled = false;
                miFileExport.Enabled = false;
            }
        }

        private void lvExchanges_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (CApp.isClosing) return;
            // TODO: Store the most recent item for back/forward nav.
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

        private void actUpdateInspector(bool bUpdateRequest, bool bUpdateResponse)
        {
            // Don't work if it's not visible.
            if (CApp.isClosing) return;
            if (tabsViews.SelectedTab != pageInspectors) return;

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
                RequestInspectorBase ibRequest = tabsRequest.TabPages[tabsRequest.SelectedIndex].Tag as RequestInspectorBase;
                if (null != x) ibRequest.AssignExchange(x); else ibRequest.Clear();
            }

            if (bUpdateResponse && (tabsResponse.SelectedIndex > -1))
            {
                ResponseInspectorBase ibResponse = tabsResponse.TabPages[tabsResponse.SelectedIndex].Tag as ResponseInspectorBase;
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
            if (tabsViews.SelectedTab == pageInspectors) actUpdateInspector(true, true);
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
                            if (!actLoadSessionArchive(sFilename))
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
            actInspectSession();
        }

        public void actSelectAll()
        {
            lvExchanges.SelectAll();
        }
        private void lvExchanges_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter)
            {
                actInspectSession();
                e.SuppressKeyPress = e.Handled = true;
                // TODO: Alt+Enter => Properties; ShifT+Enter =>InspectInTornoff
                return;
            }

            if (e.KeyCode == Keys.Delete)
            {
                if (e.Modifiers == Keys.Shift) lvExchanges.RemoveUnselected(); else lvExchanges.RemoveSelected();
            }

            if (e.Modifiers == Keys.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.X:
                        lvExchanges.ClearExchanges();
                        break;
                }
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

                var headersRequest = new HTTPRequestHeaders($"/clipboard/{DateTime.Now.ToString("H-mm-ss")}.png", new[] { "Host: localhost" });
                var exchNew = new Exchange(headersRequest, Array.Empty<byte>());

                var oMS = new MemoryStream();
                imageToPaste.Save(oMS, ImageFormat.Png);
                var headersResponse = new HTTPResponseHeaders(200, "Pasted", new[] { "Content-Type: image/png", $"Content-Length: {oMS.Length}" });
                exchNew.ResponseHeaders = headersResponse;
                exchNew.ResponseBody = oMS.ToArray();
                exchNew.BitFlags = ExchangeFlags.RequestGeneratedByClearinet | ExchangeFlags.ResponseGeneratedByClearinet
                                    | ExchangeFlags.ImportedFromOtherTool | ExchangeFlags.ServedFromCache;
                exchNew.EnsureID();
                exchNew.state = ExchangeState.Done;
                addExchangeToListView(exchNew);
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Unable to paste image.");
            }
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

                var headersRequest = new HTTPRequestHeaders($"/clipboard/{DateTime.Now.ToString("H-mm-ss")}.txt", new[] { "Host: localhost" });
                var exchNew = new Exchange(headersRequest, Array.Empty<byte>());
                exchNew.ResponseHeaders = new HTTPResponseHeaders(200, "Pasted", null);
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
                    // Paste files
                    // nyi;
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

        private void UpdateStatusBar(bool bNow)
        {
            // If updates are paused, we throttle UI updates and start a background task
            // to periodically post updates to the UI thread.
            if (!bNow && (CApp.PauseReporting || (0 == lvExchanges.SelectedCount)))
            {
                // TODO: BAckground update
                //    ScheduledTasks.ScheduleWork("UpdateStatusBar", 100,
                //      () => { CApp.UIInvokeAsync((MethodInvoker)_UpdateStatusBar, null); });
                //return;
            }

            _UpdateStatusBar();
        }

        private void _UpdateStatusBar()
        {
            int cSelected = lvExchanges.SelectedCount;
            if (cSelected < 1)
            {
                tslSelCount.Text = $"{lvExchanges.TotalCount:N0}";
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
    }
}
