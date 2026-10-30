using Clearinet.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Clearinet
{
    public delegate void CalculateReportHandler(Exchange[] arrExchanges);

    public static class CApp
    {
        internal static frmViewer _frmMain;
        internal readonly static SimpleLog _Log = new SimpleLog();
        public static SimpleLog Log
        {
            get { return _Log; }
        }

        internal static HiddenExecActionTargetWindow messagingWindow = null;
        internal static GigWorker gwHoneyDew = new GigWorker();

        public static bool isTracing { get; internal set; } = false;

        public static Extensions oExtensions;
        public static Transcoders oTranscoders;


        // TODO: Maybe this should be a facade that exposes only an interface to script/extensions?
        public static JScriptEngine scriptRules;

        internal static ComposerView oComposer;
        internal static FindPanel oFindPanel;
        public static bool isBooted { get; private set; } = false;
        public static bool isClosing { get; private set; } = false;

        public static bool isAttached { get; private set; } = false;

        public static string VersionString
        {
            get
            {
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
                string sFeatures = string.Empty; // Legacy would include " (+SAZ)" but that seems spammy?;
                string sPlatform = "Clearinet";
                string sCPU = PlatformAPI.GetProcessArchitecture();
                return $"{sPlatform}/{fvi.FileMajorPart}.{fvi.FileMinorPart}.{fvi.FileBuildPart}.{fvi.FilePrivatePart} ({sCPU}){sFeatures}";
            }
        }

        public static frmViewer UI
        {
            get
            {
                return _frmMain;
            }
        }

        // Legacy API Name.
        public delegate void SessionStateHandler(Exchange arrExchanges);

        #region Report API
        // A Report is a summary of the currently-selected exchanges in the list.

        // derp derp derp, this needs to be a counter, unless we only allow it to get touched on the UI thread.
        private static bool _bPauseReporting = false;

        internal static bool PauseReporting
        {
            get
            {
                return _bPauseReporting;
            }
            set
            {
                // TODO: Check if InvokeRequired on the mainform and throw if it is.
                _bPauseReporting = value;
                if (!_bPauseReporting)
                {
                    UI.actReportStatistics(true);  // Force update now.
                }
            }
        }

        internal static void OnCalculateReport(Exchange[] arrExchanges)
        {
            if (_bPauseReporting || isClosing) return;
            CalculateReport?.Invoke(arrExchanges);
        }
        public static event CalculateReportHandler CalculateReport;
        #endregion


        /// <summary>
        /// Asynchronously invoke on App's thread unless closing.
        /// </summary>
        internal static void UIInvokeAsync(Delegate oDel, object[] args)
        {
            if (isClosing) return;
            _frmMain.BeginInvoke(oDel, args);
        }
        internal static void UIInvokeAsync(Action oDel)
        {
            if (isClosing) return;
            _frmMain?.BeginInvoke(oDel);
        }

        public static IClearinetPreferences Prefs
        {
            get
            {
                return CONFIG.pbPrefs;
            }
        }
        public static void actAttachProxy()
        {
            if (CONFIG.isViewerMode) return;
            if (CApp.Prefs.GetBoolPref("proxy.ephemeral.show_nyi_dialog", true))
            {
                CApp.alert("Sorry!\n\nTraffic capture is not actually implemented yet.\n\nStay tuned!"); // TODO:
            }
            isAttached = true;
            OnProxyAttach();
        }
        public static void actDetachProxy()
        {
            isAttached = false;
            OnProxyDetach();
        }

        private static void _QuickExecAdjustPrefs(string[] sParams)
        {
            if (sParams.Length < 2)
            {
                MessageBox.Show("set <name> <value>\nshow <name>\nremove <name>\ndump\n\nOr type `about:config` for a friendly editor.", "Available 'prefs' actions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            switch (sParams[1].ToLower())
            {
                case "set":
                    if (sParams.Length != 4)
                    {
                        MessageBox.Show("Correct Syntax is:\n\n\tprefs SET prefName \"new value\"", "Invalid 'prefs set' Parameters", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    try
                    {
                        CApp.Prefs.SetStringPref(sParams[2], sParams[3]);
                        UI.SetStatusText($"Set '{sParams[2]}' to '{sParams[3]}'");
                    }
                    catch (Exception eX)
                    {
                        CApp.ReportException(eX, "Failed", "Unable to set the preference.");
                    }
                    break;

                case "show":
                    if (sParams.Length != 3)
                    {
                        MessageBox.Show("Correct Syntax is:\n\n\tprefs SHOW prefName", "Invalid 'prefs show' parameters", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    MessageBox.Show($"Preference '{sParams[2]}' = '{CApp.Prefs.GetStringPref(sParams[2], "<null>")}'", "Preference Value", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
                case "remove":
                    if (sParams.Length != 3)
                    {
                        MessageBox.Show("Correct Syntax is:\n\n\tprefs REMOVE prefName", "Invalid 'prefs remove' parameters", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    CApp.Prefs.RemovePref(sParams[2]);
                    UI.SetStatusText($"Removed preference '{sParams[2]}'");
                    break;
                case "dump":
                    CApp.Log.Log(CApp.Prefs.ToString(true));
                    UI.actActivateTab("Log");
                    break;

                default:
                    MessageBox.Show("set <name> <value>\nshow <name>\nremove <name>\ndump", "Available 'prefs' actions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
            }
        }

        public static bool RunExecAction(string sCommand)
        {
            string[] arrParams = Utilities.Parameterize(sCommand);

            if (arrParams.Length < 1) return false;
            string sCmd = arrParams[0].ToLower();

            if ("help" == sCmd)
            {
                MessageBox.Show("Built-in commands:\n" +
                    "? <findtext> - Select Exchanges where URL has <findtext>\n" +
                    "= <responsecode|method> - Select Exchanges w/ response code or method\n" +
                    "about:config - Open the Preferences editor\n" +
                    "prefs <set|show|remove|dump> <prefname> <prefval>\n" +
                    "lm - Lists loaded modules\n" +
                    "cls - Clear the Exchanges list\n" +
                    "select <mime>\n" +
                    "tail <#> - Keep only the most recent # of Exchanges\n" +
                    "!dns <hostname> - Log a DNS lookup\n" +
                    "lro <TestLongRunningOperation name>",
                    "QuickExec QuickHelp", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }

            if ("demo" == sCmd)
            {
                UI.TODOAddSampleData();
            }

            if ("prefs" == sCmd)
            {
                _QuickExecAdjustPrefs(arrParams);
                return true;
            }

            if ("about:config" == sCmd)
            {
                PrefView.CreatePrefsTab(UI.tabsViews);
                return true;
            }

            if ("lm" == sCmd)
            {
                _DumpModulesToLog();
                UI.actActivateTab("Log");
                return true;
            }

            if ("gigs" == sCmd)
            {
                CApp.Log.Log(CApp.gwHoneyDew.ToString());
                UI.actActivateTab("Log");
                return true;
            }

            if ("spam" == sCmd)
            {
                uint uiInterval = 1000;
                if (arrParams.Length > 1) UInt32.TryParse(arrParams[1], out uiInterval);
                CApp.gwHoneyDew.createGig("SpamExchanges", () =>
                {
                    DateTime dt = DateTime.Now;
                    Exchange XC = frmViewer.CreateFauxExchange($"/SPAM/{uiInterval}/?{dt:HH-mm-ss.ffff}", "text/plain", Encoding.UTF8.GetBytes("Spam Exchange"));
                    XC.oFlags["ui-backcolor"] = "WhiteSmoke";
                    CApp.UI.addExchangeToListView(XC);

                }, uiInterval);
                return true;
            }
            if ("spamlog" == sCmd)
            {
                uint uiInterval = 1000;
                if (arrParams.Length > 1) UInt32.TryParse(arrParams[1], out uiInterval);
                CApp.gwHoneyDew.createGig("SpamLog", () =>
                {
                    DateTime dt = DateTime.Now;
                    CApp.Log.LogFormat($"/SPAM/{uiInterval}/?{dt:HH-mm-ss.ffff}", "text/plain");
                }, uiInterval);
                return true;
            }

            if ("unspam" == sCmd)
            {
                CApp.gwHoneyDew.cancelGigsNamed("SpamExchanges");
                CApp.gwHoneyDew.cancelGigsNamed("SpamLog");
                return true;
            }

            /*if ("!spew" == sCmd)
            {
                Prefs.SetBoolPref("app.debug.ephemeral.spew". true);
            }*/

            if ("cls" == sCmd)
            {
                UI.lvExchanges.ClearExchanges();
                return true;
            }

            if ("lro" == sCmd) /* TODO: For Testing Purposes Only */
            {
                var thread = new Thread(() =>
                {
                    var sName = (arrParams.Length > 1) ? arrParams[1] : "Test LRO";
                    var lro = new LongRunningOperation(sName, true);
                    for (int i = 0; i < 10; i++)
                    {
                        lro.UpdateProgress((i + 1) * 10, $"Step {i + 1} of 10");
                        Thread.Sleep(1000);
                    }
                    lro.Dispose();
                })
                {
                    IsBackground = true,
                    Name = "LongRunningOperation Test Thread",
                    Priority = ThreadPriority.BelowNormal
                };

                thread.Start();

                return true;
            }

            if (sCmd == "select") // TODO: Fiddler offered more selection options
            {
                if (arrParams.Length < 2)
                {
                    CApp.DoNotifyUser(CApp.UI, "Usage: select <Content-Type>", "Invalid Parameters", MessageBoxIcon.Error);
                    return true;
                }
                string sMIME = arrParams[1];
                UI.actSelectExchangesMatchingCriteria((xc) => xc.MediaType.OICContains(sMIME));
                if (UI.lvExchanges.SelectedCount > 0) UI.lvExchanges.Focus();
                CApp.UI.SetStatusText($"Selected {UI.lvExchanges.SelectedCount} exchanges with Content-Type containing '{sMIME}'");
                return true;
            }

            if (sCmd == "tail")
            {
                if ((arrParams.Length < 2) || !Int32.TryParse(arrParams[1], out int iVal))
                {
                    CApp.DoNotifyUser(CApp.UI, "Usage: tail <#ExchangesToKeep>", "Invalid Parameters", MessageBoxIcon.Error);
                    return true;
                }

                CApp.UI.lvExchanges.Tail(iVal);
                CApp.UI.SetStatusText($"Kept only the most recent {iVal} exchanges");
                return true;
            }

            if (sCmd == "!dns")
            {
                if (arrParams.Length != 2 || string.IsNullOrWhiteSpace(arrParams[1]))
                {
                    CApp.DoNotifyUser(CApp.UI, "Usage: !dns <hostname>", "Invalid Parameters", MessageBoxIcon.Error);
                    return true;
                }

                UI.actActivateTab("Log");
                _ = LookupDnsAsync(arrParams[1]);
                return true;
            }

            if (sCmd == "!script")
            {
                if (arrParams.Length < 2)
                {
                    CApp.DoNotifyUser(CApp.UI, "Usage:\n\t!script <reload|unload>", "Invalid Parameters", MessageBoxIcon.Error);
                    return true;
                }
                if (arrParams[1].OICEquals("reload"))
                {
                    scriptRules?.ReloadScript();
                    CApp.UI.SetStatusText($"Script reloaded.");
                }
                else if (arrParams[1].OICEquals("unload"))
                {
                    scriptRules?.UnloadScript();
                    CApp.UI.SetStatusText($"Script unloaded,");
                }
                else
                {
                    CApp.DoNotifyUser(CApp.UI, "Usage:\n\t!script <reload|unload>", "Invalid Parameters", MessageBoxIcon.Error);
                }
                return true;
            }

            if (sCommand.StartsWith("?")) // Instant search (without hitting enter) lives in QuickSearchBox.cs
            {
                var sFind = sCommand.Substring(1).Trim();
                UI.actSelectExchangesMatchingCriteria((xc) =>
                    xc.fullUrl.OICContains(sFind));   // Issue: Support EXACT: and REGEX: prefixes?

                if (UI.lvExchanges.SelectedCount > 0) UI.lvExchanges.Focus();
                CApp.UI.SetStatusText($"Selected {UI.lvExchanges.SelectedCount} Exchanges with URL containing '{sFind}'");

                return true;
            }

            if (sCommand.StartsWith("="))
            {
                if (Int32.TryParse(sCommand.Substring(1).Trim(), out int iVal))
                {
                    UI.actSelectExchangesMatchingCriteria((xc) => (xc.responseCode == iVal));
                    CApp.UI.SetStatusText($"Selected {UI.lvExchanges.SelectedCount} exchanges with response code '{iVal}'");
                }
                else
                {
                    var sMethod = sCommand.Substring(1).Trim();
                    UI.actSelectExchangesMatchingCriteria((xc) => xc.RequestMethod.OICEquals(sMethod));
                    CApp.UI.SetStatusText($"Selected {UI.lvExchanges.SelectedCount} exchanges with Method '{sMethod}'");

                }
                if (UI.lvExchanges.SelectedCount > 0) UI.lvExchanges.Focus();
                return true;
            }

            if (sCommand.StartsWith(">"))
            {
                Int32.TryParse(sCommand.Substring(1).Trim(), out int iVal);
                UI.actSelectExchangesMatchingCriteria((xc) =>
                { return xc.ResponseBody?.Length > iVal; });
                if (UI.lvExchanges.SelectedCount > 0) UI.lvExchanges.Focus();
                CApp.UI.SetStatusText($"Selected {UI.lvExchanges.SelectedCount} exchanges with response size > '{iVal}'");

                return true;
            }

            if (sCommand.StartsWith("<"))
            {
                Int32.TryParse(sCommand.Substring(1).Trim(), out int iVal);
                UI.actSelectExchangesMatchingCriteria((xc) => { return xc.ResponseBody?.Length < iVal; });
                if (UI.lvExchanges.SelectedCount > 0) UI.lvExchanges.Focus();
                CApp.UI.SetStatusText($"Selected {UI.lvExchanges.SelectedCount} exchanges with response size < '{iVal}'");

                return true;
            }
            if (sCommand.StartsWith("@"))
            {
                string sHost = sCommand.Substring(1).Trim();
                UI.actSelectExchangesMatchingCriteria((xc) => { return xc.hostname.OICEquals(sHost); });
                if (UI.lvExchanges.SelectedCount > 0) UI.lvExchanges.Focus();
                CApp.UI.SetStatusText($"Selected {UI.lvExchanges.SelectedCount} exchanges to host '{sHost}'");
                return true;
            }

            if (null == scriptRules) return false;
            return scriptRules.RunExecAction(arrParams);
        }

        private static async Task LookupDnsAsync(string hostname)
        {
            Log.Log($"Looking up {hostname} in DNS...");
            try
            {
                IPHostEntry entry = await Dns.GetHostEntryAsync(hostname);
                var result = new StringBuilder();
                result.AppendLine($"DNS lookup for {hostname}: {entry.HostName}");
                foreach (string alias in entry.Aliases)
                {
                    result.AppendLine($"  Alias: {alias}");
                }
                foreach (IPAddress address in entry.AddressList)
                {
                    result.AppendLine($"  {address}");
                }
                Log.Log(result.ToString().TrimEnd());
            }
            catch (SocketException ex)
            {
                Log.Log($"DNS lookup for {hostname} failed ({ex.SocketErrorCode}): {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                Log.Log($"DNS lookup for {hostname} failed: {ex.Message}");
            }
        }

        internal static void _DumpModulesToLog()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            List<AssemblyName> nameList = new List<AssemblyName>();

            // Extract AssemblyName objects
            for (int i = 0; i < assemblies.Length; i++)
            {
                AssemblyName name = assemblies[i].GetName();
                if (name.Name != null)
                {
                    nameList.Add(name);
                }
            }

            // Sort alphabetically by Name (case-insensitive)
            nameList.Sort(delegate (AssemblyName x, AssemblyName y)
            {
                return string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
            });

            // Format multiline string
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"\r\n{new string('-', 22)} Currently Loaded Modules {new string('-', 22)}");
            sb.AppendLine($"{"Assembly Name",-30} | {"Version",-10} | CodeBase");
            sb.AppendLine(new string('-', 70));

            for (int i = 0; i < nameList.Count; i++)
            {
                AssemblyName name = nameList[i];
                sb.AppendLine($"{name.Name,-30} | {name.Version,-10} | {name.CodeBase}");
            }

            Log.Log(sb.ToString());
        }

        internal static void CreateScriptEngine()
        {
            scriptRules = new JScriptEngine(CONFIG.GetPath("RulesScript"));
        }

        internal static void OnProxyAttach()
        {
            // TODO: update state of status bar capturing indicator
            ProxyAttach?.Invoke();
            scriptRules?.DoOnAttach();
        }
        internal static void OnProxyDetach()
        {
            // TODO: update state of status bar capturing indicator
            ProxyDetach?.Invoke();
            scriptRules?.DoOnDetach();
        }

        #region LegacyCompatAPIs
        //
        // We want these APIs to stay in the same basic shape for easier compat.
        //
        public static void PlaySound(string sFilename)
        {
            PlatformAPI.PlaySoundFile(sFilename);
        }

        public class NotificationEventArgs : EventArgs
        {
            private readonly string _sText;

            internal NotificationEventArgs(string sText)
            {
                _sText = sText;
            }

            public string NotifyString
            {
                get
                {
                    return _sText;
                }
            }
        }
        public static event EventHandler<NotificationEventArgs> OnNotification;

        public static event Action ProxyAttach;
        public static event Action ProxyDetach;
        public static event Action AppBoot;
        public static event CancelEventHandler BeforeAppShutdown;
        public static event Action AppShutdown;

        public static void alert(string sMessage)
        {
            DoNotifyUser(null, sMessage, "Script Alert", MessageBoxIcon.Information);
        }

        public static void DoNotifyUser(string sMessage, string sTitle)
        {
            DoNotifyUser(null, sMessage, sTitle, MessageBoxIcon.None);
        }

        public static void DoNotifyUser(string sMessage, string sTitle, MessageBoxIcon oIcon)
        {
            DoNotifyUser(null, sMessage, sTitle, oIcon);
        }

        public static void DoNotifyUser(IWin32Window hwndOwner, string sMessage, string sTitle, MessageBoxIcon oIcon)
        {
            if (null != OnNotification)
            {
                NotificationEventArgs oEA = new NotificationEventArgs(String.Format("{0} - {1}", sTitle, sMessage));
                OnNotification(null, oEA);
            }

            if (!CONFIG.isQuietMode)
            {
                MessageBox.Show(hwndOwner, sMessage, sTitle, MessageBoxButtons.OK, oIcon);
            }
        }

        public static void ReportException(Exception eX, string sTitle)
        {
            ReportException(eX, sTitle, null);
        }

        public static void ReportException(Exception eX, string sTitle, string sCallerMessage)
        {
            if (isClosing && (eX is System.Threading.ThreadAbortException))
            {
                return;
            }
            if (CApp.UI != null && CApp.UI.InvokeRequired)
            {
                CApp.UI.Invoke(new Action(() => ReportException(eX, sTitle, sCallerMessage)));
                return;
            }

            Debug.Assert(false, "Assert'ed to enter debugger");
            string sMessage = (sCallerMessage.HasText()) ? sCallerMessage :
            "Clearinet encountered a problem. If you think this is a bug, please copy this message by hitting CTRL+C, and submit a report using the Help menu.";

            sMessage = sMessage + $"\n\n{eX.Message}\n\nType: {eX.GetType()}\nSource: {eX.Source}\n{eX.StackTrace}\n\n{eX.InnerException}\n"
                + $"{CApp.VersionString} [.NET {Environment.Version} on {Environment.OSVersion.VersionString}]";

            if (null != OnNotification)
            {
                NotificationEventArgs oEA = new NotificationEventArgs(String.Format("{0} - {1}", sTitle, sMessage));
                OnNotification(null, oEA);
            }

            MessageBox.Show(CApp.UI, sMessage, sTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        #endregion

        internal static bool OnBeforeShutdown()
        {
            scriptRules.CallMethod("OnBeforeShutdown", null, out object oRes);
            if ((null != oRes) && (false == (bool)oRes)) return false;

            CancelEventHandler evtBeforeShutdown = BeforeAppShutdown;
            if (null != evtBeforeShutdown)
            {
                CancelEventArgs e = new CancelEventArgs(false);
                evtBeforeShutdown(null, e);
                if (e.Cancel) return false;
            }

            return true;
        }

        internal static void OnAppBoot()
        {
            if (!CONFIG.isViewerMode)
            {
                // Currently, Clearinet does not make proper use of async IO, so we
                // need to boost the thread pool sizes to avoid contention.
                int cProcessors = Environment.ProcessorCount;
                ThreadPool.SetMinThreads(Math.Max(16, 5 * cProcessors), cProcessors);
            }

            scriptRules?.DoOnBoot();
            if (null != AppBoot)
            {
                AppBoot();
                AppBoot = null; // We only boot once. No need to hang on to these.
            }

            _Log.OnLog += _Log_OnLog;
            _Log.FlushQueue();

            if (CApp.Prefs.GetBoolPref("app.high_resolution_clock", true)) PlatformAPI.EnableHighResolutionClock();
            CApp.Log.Log($"System Clock Resolution: {PlatformAPI.GetClockResolution()}");
            isBooted = true;
        }

        private static void _Log_OnLog(object sender, LogEventArgs e)
        {
            UIInvokeAsync((MethodInvoker)delegate { _frmMain.UpdateLog(e.LogString); }, null);
        }

        internal static void OnAppShutdown()
        {
            isClosing = true;
            // TODO: Detach proxy
            scriptRules?.DoOnShutdown();
            oExtensions.CallAllOnBeforeUnloads();
            AppShutdown?.Invoke();
            Utilities.CleanupTempDirectories();
            CONFIG.SaveAllSettings(UI);

            // ENSURE THIS STAYS LAST!
            CONFIG.pbPrefs.Close();
        }
    }
}
