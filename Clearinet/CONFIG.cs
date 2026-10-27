using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Clearinet
{
    /// <summary>
    /// Configuration class for the Clearinet application. This class contains static properties 
    /// and methods to manage application settings and configurations. 
    /// 
    /// TODO: Generally, many of these should be removed in favor of using the Preferences system,
    /// but they exist here for Fiddler compatibility to reduce the burden of porting extensions.
    /// </summary>
    public static class CONFIG
    {
        internal static readonly Color COLOR_DEFAULT_DISABLEDEDIT = ColorTranslator.FromHtml("#F0FDF4"); // Slate:#F1F5F9 Greenish: #F0FDF4
        internal static readonly Color COLOR_DATA_RESPONSE = Color.DeepSkyBlue; // Color.OrangeRed; // ColorTranslator.FromHtml("#97F275");
        internal static string sRootRegistryKey = @"SOFTWARE\Clearinet\App\";

        // This-instance-only override settings (set by command line)
        internal struct structOverrides
        {
            internal bool NoAttachOnStartup;
            internal bool DisableScript;
            internal int ListenPort;
        }
        internal static structOverrides Overrides;

        public static Color GetColor(string sPref, Color colorDefault)
        {
            string sColor = CApp.Prefs.GetStringPref(sPref, null);
            if (sColor.HasText()) return Utilities.ParseColor(sColor);
            return colorDefault;
        }

        // TODO: Feed from pref.
        internal static Encoding encodingOfHeaders = Encoding.UTF8;

        /// <summary>
        /// Font-size in pixels for app UI.
        /// </summary>
        public static float flFontSize = 10.0F;
        public static bool isQuietMode { get; set; } = false;
        public static bool isViewerMode { get; internal set; } = false;

        public static int ListenPort
        {
            get
            {
                if (Overrides.ListenPort > 0) return Overrides.ListenPort;
                return pbPrefs.GetInt32Pref("proxy.listen_port", 8888);
            }
            internal set
            {
                if (value < 1 || value > 65535) throw new ArgumentOutOfRangeException("ListenPort must be between 1 and 65535.");
                pbPrefs.SetInt32Pref("proxy.listen_port", value);
                // TODO: If the proxy is running, we should restart it here.
            }
        }

        /// <summary>
        /// Color for disabled editors app UI.
        /// </summary>
        public static Color colorDisabledEdit = COLOR_DEFAULT_DISABLEDEDIT;
        public static Color colorHighlight = Color.Yellow;

        internal static PreferenceBag pbPrefs
        {
            get; private set;
        }
        private static void ApplyCommandLine()
        {
            foreach (string sArg in Environment.GetCommandLineArgs())
            {
                if (!sArg.OICStartsWithAny("/", "-")) continue;

                var sSwitch = sArg.Substring(1);
                if (!sSwitch.HasText()) continue;

                if (sSwitch.IndexOf("port:", StringComparison.OrdinalIgnoreCase) == 1)
                {
                    if (int.TryParse(sArg.Substring(sArg.IndexOf("port:", StringComparison.OrdinalIgnoreCase) + 5),
                        NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out int iPort))
                    {
                        Overrides.ListenPort = iPort;
                    }
                    continue;
                }

                if (sSwitch.OICEquals("quiet"))
                {
                    isQuietMode = true;
                    continue;
                }


                if (sSwitch.OICEquals("viewer"))
                {
                    isViewerMode = true;
                    continue;
                }

                if (sSwitch.OICEquals("noattach"))
                {
                    Overrides.NoAttachOnStartup = true;
                    continue;
                }

                if (sSwitch.OICEquals("noscript"))
                {
                    Overrides.DisableScript = true;
                    continue;
                }

                _ShowValidArgs();
            }
        }

        private static void _InitPreferences()
        {
            pbPrefs = new PreferenceBag(GetRegistryPath("Prefs"));
        }

        public static string GetRegistryPath(string sWhich)
        {
            switch (sWhich)
            {
                case "Prefs": return sRootRegistryKey + @"Prefs\";
                case "Root": return sRootRegistryKey;
                case "UI": return sRootRegistryKey + @"UI\";
                case "MRU": return sRootRegistryKey + @"UI\MRU\";
                default:
                    Debug.Assert(false, "Asked for an undefined path: " + sWhich);
                    return sRootRegistryKey;
            }
        }

        public static string GetURL(string sWhich)
        {
            // TODO: Policy/Pref override of baseURL
            switch (sWhich)
            {
                case "Homepage":
                    return "https://clearinet.app/";
                case "Help":
                    return "https://github.com/ericlaw1979/Clearinet/blob/main/Docs/HELP.md";
                case "HelpPrefs":
                    return "https://github.com/ericlaw1979/Clearinet/blob/main/Docs/PREFERENCES.md";
            }
            Trace.WriteLine($"Failed to find URL `{sWhich}`.");
            return $"https://clearinet.app/?missing_GetURL_code={sWhich}";
        }

        public static string GetPath(string sWhich)
        {
            switch (sWhich)
            {
                case "Editor.Code":
                    return CApp.Prefs.GetStringPref("user.paths.editors.code", "notepad.exe");
                case "Tools.Diff":
                    return CApp.Prefs.GetStringPref("user.paths.tools.diff", "code.exe");
                case "CommandHistory":
                    return CApp.Prefs.GetStringPref("app.paths.commandhistory",
                                               Path.Combine(GetPath("UserFolder"), "CommandHistory.txt"));
                case "Extensions":
                    return CApp.Prefs.GetStringPref("app.paths.extensions",
                     $"{Application.StartupPath}{Path.DirectorySeparatorChar}Extensions{Path.DirectorySeparatorChar}");
                case "Extensions_User":
                    return CApp.Prefs.GetStringPref("app.paths.extensions_user",
                                               $"{GetPath("UserFolder")}Extensions{Path.DirectorySeparatorChar}");
                case "Transcoders":
                    return CApp.Prefs.GetStringPref("app.paths.transcoders",
                      $"{Application.StartupPath}{Path.DirectorySeparatorChar}ImportExport{Path.DirectorySeparatorChar}");
                case "Transcoders_User":
                    return CApp.Prefs.GetStringPref("app.paths.transcoders_user",
                                                $"{GetPath("UserFolder")}ImportExport{Path.DirectorySeparatorChar}");
                case "RulesScript":          // TODO: REMOVE ALPHA for v1.0
                    return CApp.Prefs.GetStringPref("app.paths.rules.script",
                                                $"{GetPath("UserFolder")}CustomRules_0_0_2_6.js");
                case "SampleRulesScript":    // TODO: REMOVE ALPHA for v1.0
                    return CApp.Prefs.GetStringPref("app.paths.rules.sample_script",
                                                $"{Application.StartupPath}{Path.DirectorySeparatorChar}SampleRulesALPHA.js");
                case "Root": return Application.StartupPath;
                //case "Scripts":
                //    return GetPath("UserFolder") + "Scripts" + Path.DirectorySeparatorChar;
                case "Tools":
                    return CApp.Prefs.GetStringPref("app.paths.tools",
                      $"{Application.StartupPath}{Path.DirectorySeparatorChar}Tools{Path.DirectorySeparatorChar}");
                case "UserFolder":
                    return Environment.GetFolderPath(Environment.SpecialFolder.Personal,
                                                     Environment.SpecialFolderOption.DoNotVerify)
                          + Path.DirectorySeparatorChar + "Clearinet" + Path.DirectorySeparatorChar;
                default:
                    Debug.Assert(false, "Asked for an undefined path: " + sWhich);
                    return Application.StartupPath;
            }
        }

        private static void _ShowValidArgs()
        {
            string sValidOptions = "Usage:\n\tclearinet.exe [options] [CaptureToLoad.saz]\n\nOptions:\n\n" +
            "-viewer\t\tOpen a non-proxy 'Viewer' mode\n" +
            "-quiet\t\tShow as little UI as possible\n" +
            "-noattach\tDo not attach as the system proxy on boot\n" +
            "-noscript\t\tDisable the script engine (for troubleshooting)\n" +
            "-port:####\tUse the specified port to listen\n" +
            "-?\t\tShow this list\n";

            MessageBox.Show(sValidOptions, "Clearinet Options");
        }

        static CONFIG()
        {
            ApplyCommandLine();
            _InitPreferences();
        }

        /// <summary>
        /// Settings that must be loaded before we create any forms.
        /// </summary>
        internal static void RetrieveUrgentSettings()
        {
            try
            {
                RegistryKey oReg = Registry.CurrentUser.OpenSubKey(GetRegistryPath("UI"), RegistryKeyPermissionCheck.ReadSubTree);
                if (null != oReg)
                {
                    CONFIG.flFontSize = (float)Utilities.GetRegistryInt(oReg, "App_FontSize", (int)CONFIG.flFontSize);
                    CONFIG.colorDisabledEdit = Utilities.GetRegistryColor(oReg, "App_ColorDisabledEdit", COLOR_DEFAULT_DISABLEDEDIT);
                    CONFIG.colorHighlight = Utilities.GetRegistryColor(oReg, "App_ColorHighlight", Color.Yellow);
                }
            }
            catch (Exception eX)
            {
                Debug.Assert(false);
            }
        }

        internal static void RetrieveLayout(frmViewer f)
        {
            f.Font = new Font(f.Font.FontFamily, CONFIG.flFontSize);
            f.StartPosition = FormStartPosition.Manual;
            try
            {
                RegistryKey oReg = Registry.CurrentUser.OpenSubKey(GetRegistryPath("UI"), RegistryKeyPermissionCheck.ReadSubTree);
                if (null != oReg)
                {
                    f.Bounds = new Rectangle(
                       Utilities.GetRegistryInt(oReg, f.Name + "_Left", f.Left),
                       Utilities.GetRegistryInt(oReg, f.Name + "_Top", f.Top),
                       Utilities.GetRegistryInt(oReg, f.Name + "_Width", f.Width),
                       Utilities.GetRegistryInt(oReg, f.Name + "_Height", f.Height));
                }

                Screen screen = Screen.FromRectangle(f.DesktopBounds);
                Rectangle rectIntersect = Rectangle.Intersect(f.DesktopBounds, screen.WorkingArea);
                if (rectIntersect.IsEmpty || ((rectIntersect.Width * rectIntersect.Height) < (0.25 * f.Width * f.Height)))
                {
                    f.SetDesktopLocation(screen.WorkingArea.Left + 25, screen.WorkingArea.Top + 25);
                }

                if (null != oReg)
                {
                    FormWindowState oFWS = (FormWindowState)oReg.GetValue(f.Name + "_WState", f.WindowState);
                    if (oFWS == FormWindowState.Maximized)
                    {
                        // TODO: fDeferMaximize
                    }
                    oReg.Close();
                }
            }
            catch (Exception eX)
            {
                Debug.WriteLine("[Clearinet] Layout Restoration failed: " + eX.Message);
                Debug.Assert(false);
            }
        }

        internal static void SaveAllSettings(frmViewer f)
        {
            // Don't save settings if we're in Viewer mode, since we don't want to overwrite the user's normal settings.
            if (isViewerMode) return;

            // Open key with Write permissions 
            using (RegistryKey oReg = Registry.CurrentUser.CreateSubKey(GetRegistryPath("UI"), RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                oReg.SetValue("App_FontSize", (int)CONFIG.flFontSize);
                oReg.SetValue("App_ColorDisabledEdit", ColorTranslator.ToHtml(CONFIG.colorDisabledEdit));
                oReg.SetValue("App_ColorHighlight", ColorTranslator.ToHtml(CONFIG.colorHighlight));

                oReg.SetValue(f.Name + "_WinState", (int)f.WindowState);

                if (f.WindowState != FormWindowState.Normal)
                {
                    Rectangle rectRestoredBounds = f.RestoreBounds;

                    oReg.SetValue(f.Name + "_Left", rectRestoredBounds.Left);
                    oReg.SetValue(f.Name + "_Width", rectRestoredBounds.Width);
                    oReg.SetValue(f.Name + "_Top", rectRestoredBounds.Top);
                    oReg.SetValue(f.Name + "_Height", rectRestoredBounds.Height);
                }
                else
                {
                    oReg.SetValue(f.Name + "_Left", f.Left);
                    oReg.SetValue(f.Name + "_Width", f.Width);
                    oReg.SetValue(f.Name + "_Top", f.Top);
                    oReg.SetValue(f.Name + "_Height", f.Height);
                }
            }

            SaveBaseSettings();
        }

        internal static void SaveBaseSettings()
        {
            // Don't save settings if we're in Viewer mode, since we don't want to overwrite the user's normal settings.
            if (isViewerMode) return;
            using (RegistryKey oReg = Registry.CurrentUser.CreateSubKey(sRootRegistryKey))
            {
                // Write values
            }
        }

        internal static void RevertToDefaultAppearance()
        {
            CApp.alert("not yet implemented");
        }

        /// <summary>
        /// The Exchange coloring is mostly based on the response MIME type, although
        /// the request method is also used to remove less interesting exchanges.
        /// </summary>
        /// <param name="exchange"></param>
        /// <returns></returns>
        public static Color GetExchangeColor(Exchange exchange)
        {
            string mediaType = exchange == null ? string.Empty : exchange.MediaType;
            if (exchange.HTTPMethodIs("CONNECT", "HEAD"))
            {
                return Color.Gray;
            }

            if (mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return Color.DarkGray;
            }

            if (mediaType.OICContains("javascript"))
            {
                return Color.DarkGreen;
            }

            if (string.Equals(mediaType, "text/css", StringComparison.OrdinalIgnoreCase))
            {
                return Color.Purple;
            }

            if (mediaType.OICContains("json", "xml", "protobuf", "msgpack", "cbor"))
            {
                return COLOR_DATA_RESPONSE;
            }

            return Color.Blue;
        }
    }
}
