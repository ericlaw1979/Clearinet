using Ionic.Zip;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;


namespace Clearinet
{
    public partial class frmViewer : Form
    {
        public frmViewer()
        {
            InitializeComponent();
            if (CONFIG.isViewerMode)
            {
                this.Text = $"Clearinet Viewer";
                tsmiNameViewer.Visible = true;
            }
        }

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
            actRefreshUI();
            // TODO: Get selected exchanges...
            Exchange[] arrEx = GetSelectedExchanges();
            // ... and for each, call RefreshListViewItem to ensure
            // all column data is up-to-date.
        }

        public Exchange[] GetSelectedExchanges()
        {
            // TODO: Get the data!
            Exchange[] arrExchanges = new Exchange[blvExchanges.SelectedItems.Count];
            return arrExchanges;
        }

        /// <summary>
        /// Update the Inspector and Status bar.
        /// LEGACY API. Do not rename.
        /// </summary>
        public void actRefreshUI()
        {
            // TODO: This should ensure that all inspectors are selected properly,
            // any info shown for the current session in the status bar is correct, etc.
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

                frmSplashScreen.SetStatusText("Loading script engine...");
                CApp.CreateScriptEngine();

                Application.Run(CApp._frmMain);
                if (bMutexAcquired) oMutex.ReleaseMutex();
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
                    // case "app.ui.exchangelist.autoscroll"
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
            // TODO: Load Inspectors
            // TODO: Grab system network config
            // TODO: Start the core Proxy listener

            InitializeUIFromPrefs();
            CApp.Prefs.AddWatcher("app.ui.", OnPrefChange);

            if (CApp.Prefs.GetBoolPref("app.ui.darkmode", false)) ThemeManager.ApplyDarkMode(this);

            CApp.ProxyAttach += HandleProxyAttached;
            CApp.ProxyDetach += HandleProxyDetached;

            frmSplashScreen.CloseSplashScreen();
            CApp.OnAppBoot();

            if (CApp.Prefs.GetBoolPref("app.attach_on_startup", true)
                && !Environment.CommandLine.OICContains("noattach")) { CApp.actAttachProxy(); }


            this.blvExchanges.DragDrop += BlvExchanges_DragDrop;
            this.blvExchanges.DragEnter += BlvExchanges_DragEnter;
            this.blvExchanges.AllowDrop = true;

            // TODO: Rename and change hotkey to "O" and set a reasonable default load folder.
            this.loadArchiveToolStripMenuItem.Click += (s, ea) => actLoadSessionArchive(Utilities.ObtainOpenFilename("Open SAZ", "SAZ Files (*.saz)|*.saz"));

            ImportAnyStartupArchives();
        }

        private void BlvExchanges_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Link | DragDropEffects.Copy; return;
            }
            // TODO: support dropping text, other files, Exchanges, etc.
            e.Effect = DragDropEffects.None;
        }

        private void BlvExchanges_DragDrop(object sender, DragEventArgs e)
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
        private void actImportFile(string sPath)
        {
            CApp.DoNotifyUser("Asked to load file: " + sPath, "NYI");
        }

        /// <summary>
        /// LEGACY API. Do not rename. Load a .SAZ file.
        /// </summary>
        /// <param name="sPath"></param>
        public void actLoadSessionArchive(string sPath)
        {
            CApp.DoNotifyUser("Asked to import file: " + sPath, "NYI");
            using (SAZFile sazFile = new SAZFile(sPath)){
                CApp.DoNotifyUser($"SAZ file: {sPath} contained {sazFile.Exchanges.Count} exchanges", "NYI");
            }
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
            // TODO: Name the viewer.
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
    }
}
