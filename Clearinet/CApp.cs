using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Clearinet
{
    public static class CApp
    {
        internal static frmViewer _frmMain;
        internal readonly static SimpleLog _Log = new SimpleLog();
        public static SimpleLog Log
        {
            get { return _Log; }
        }

        public static Extensions oExtensions;

        internal static JScriptEngine scriptRules;

        public static bool isBooted { get; private set; } = false;
        public static bool isClosing { get;  private set; } = false;

        public static bool isAttached { get; private set; } = false;

        public static string VersionString
        {
            get
            {
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
                string sFeatures = ""; // eventually " (+SAZ)";
                string sPlatform = "Clearinet";
                string sCPU = String.Empty;
                switch (RuntimeInformation.ProcessArchitecture)
                {
                    case Architecture.X64:
                        sCPU = "x64";
                        break;
                    case Architecture.X86:
                        sCPU = "x86";
                        break;
                    case Architecture.Arm:
                        sCPU = "arm";
                        break;
                    case Architecture.Arm64:
                        sCPU = "arm64";
                        break;
                }
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
            _frmMain.BeginInvoke(oDel);
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
                MessageBox.Show("set <name> <value>\r\nshow <name>\r\nremove <name>\r\ndump", "Available 'prefs' actions", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            switch (sParams[1].ToLower())
            {
                case "set":
                    if (sParams.Length != 4)
                    {
                        MessageBox.Show("Correct Syntax is:\r\n\r\n\tprefs SET prefName \"new value\"", "Invalid 'prefs set' Parameters", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                        MessageBox.Show("Correct Syntax is:\r\n\r\n\tprefs SHOW prefName", "Invalid 'prefs show' parameters", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    MessageBox.Show($"Preference '{sParams[2]}' = '{CApp.Prefs.GetStringPref(sParams[2], "<null>")}'", "Preference Value", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;

                case "remove":
                    if (sParams.Length != 3)
                    {
                        MessageBox.Show("Correct Syntax is:\r\n\r\n\tprefs REMOVE prefName", "Invalid 'prefs remove' parameters", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    MessageBox.Show("set <name> <value>\r\nshow <name>\r\nremove <name>\r\ndump", "Available 'prefs' actions", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                MessageBox.Show("One day, this sill show cool things", "TODO: Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }

            if ("prefs" == sCmd)
            {
                _QuickExecAdjustPrefs(arrParams);
                return true;
            }

            if (scriptRules == null) return false;
            return scriptRules.RunExecAction(arrParams);
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

            string sMessage = (sCallerMessage.HasText()) ? sCallerMessage :
            "Clearinet encountered a problem. If you think this is a bug, please copy this message by hitting CTRL+C, and submit a report using the Help menu.";

            sMessage = sMessage + $"\n\n{eX.Message}\n\nType: {eX.GetType()}\nSource: { eX.Source }\n{eX.StackTrace}\n\n{eX.InnerException}\n"
                + $"{CApp.VersionString} [.NET {Environment.Version} on {Environment.OSVersion.VersionString}]";

            if (null != OnNotification)
            {
                NotificationEventArgs oEA = new NotificationEventArgs(String.Format("{0} - {1}", sTitle, sMessage));
                OnNotification(null, oEA);
            }

            MessageBox.Show(sMessage, sTitle, MessageBoxButtons.OK,MessageBoxIcon.Error);
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
            // Currently, Clearinet does not make good use of async IO, so we need to boost the thread pool sizes.
            int cProcessors = Environment.ProcessorCount;
            ThreadPool.SetMinThreads(Math.Max(16, 5 * cProcessors), cProcessors);

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
            AppShutdown?.Invoke();
            CONFIG.SaveAllSettings(UI);

            // ENSURE THIS STAYS LAST!
            CONFIG.pbPrefs.Close();
        }
    }
}
