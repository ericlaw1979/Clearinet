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
    /// TODO: Generally, most of these should be removed in favor of using the Preferences system,
    /// but they exist here for Fiddler compatibility to reduce the burden of porting extensions.
    /// </summary>
    public static class CONFIG
    {
        private static readonly Color COLOR_DEFAULT_DISABLEDEDIT = Color.FromArgb(250, 238, 227);

        internal static string sRootRegistryKey = @"SOFTWARE\Clearinet\App\";

        // TODO: Feed from pref.
        internal static Encoding encodingOfHeaders = Encoding.UTF8;

        /// <summary>
        /// Font-size in pixels for app UI.
        /// </summary>
        public static float flFontSize = 10.0F;
        public static bool isQuietMode { get; set; } = false;
        public static bool isViewerMode { get; internal set; }

        public static int ListenPort { get; internal set; } = 8888;

        /// <summary>
        /// Color for disabled editors app UI.
        /// </summary>
        public static Color colorDisabledEdit = COLOR_DEFAULT_DISABLEDEDIT;

        internal static PreferenceBag pbPrefs
        {
            get; private set;
        }
        private static void ApplyCommandLine()
        {
            foreach (string sArg in Environment.GetCommandLineArgs())
            {
                if (!sArg.OICStartsWithAny("/", "-")) continue;

                if (sArg.IndexOf("port:", StringComparison.OrdinalIgnoreCase) == 1)
                {
                    if (int.TryParse(sArg.Substring(sArg.IndexOf("port:", StringComparison.OrdinalIgnoreCase) + 5),
                        NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out int iPort))
                    {
                        ListenPort = iPort;
                    }
                    continue;
                }

                if (sArg.IndexOf("quiet", StringComparison.OrdinalIgnoreCase) == 1)
                {
                    isQuietMode = true;
                    continue;
                }

                if (sArg.IndexOf("viewer", StringComparison.OrdinalIgnoreCase) == 1)
                {
                    isViewerMode = true;
                    continue;
                }
                if (sArg.IndexOf("?", StringComparison.OrdinalIgnoreCase) == 1)
                {
                    _ShowValidArgs();
                    continue;
                }
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

        public static string GetPath(string sWhich)
        {
            switch (sWhich)
            {
                case "Extensions":
                    return CApp.Prefs.GetStringPref("app.paths.extensions",
                      Application.StartupPath + Path.DirectorySeparatorChar + "Extensions");
                case "Extensions_User":
                    return CApp.Prefs.GetStringPref("app.paths.extensions_user",
                                                GetPath("UserFolder") + "Extensions" + Path.DirectorySeparatorChar);
                case "Transcoders":
                    return CApp.Prefs.GetStringPref("app.paths.extensions",
                      Application.StartupPath + Path.DirectorySeparatorChar + "ImportExport");
                case "Transcoders_User":
                    return CApp.Prefs.GetStringPref("app.paths.extensions_user",
                                                GetPath("UserFolder") + "ImportExport" + Path.DirectorySeparatorChar);
                case "RulesScript":    // TODO: Replace with CustomRules.js file
                    return CApp.Prefs.GetStringPref("app.paths.rulesscript", @"C:\src\cin\Content\poc.js");
                case "Root": return Application.StartupPath;
                case "Scripts":
                    return GetPath("UserFolder") + "Scripts" + Path.DirectorySeparatorChar;
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
            string sValidOptions = "Usage:\n\tcin.exe [options] [Traffic.saz]\n\nOptions:\n\n" +
            "-viewer\t\tOpen a non-proxy 'Viewer' mode\n" +
            "-quiet\t\tShow as little UI as possible\n" +
            "-noattach\t\tDo not attach as the system proxy on boot\n" +
            "-port:####\tUse the specified port to listen\n" +
            "-?\t\tShow this list\n";

            MessageBox.Show(sValidOptions, "Clearinet Options");
        }

        static CONFIG()
        {
            ApplyCommandLine();
            _InitPreferences();
        }

        internal static void RetrieveLayout(frmViewer f)
        {
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
                Debug.WriteLine("[Fiddler Configuration Load Error: " + eX.Message);
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
                oReg.SetValue(f.Name + "_WinState", (int)f.WindowState);

                if (f.WindowState != FormWindowState.Normal)
                {
                    Rectangle rectNormalBounds = f.RestoreBounds;

                    oReg.SetValue(f.Name + "_Top", rectNormalBounds.Top);
                    oReg.SetValue(f.Name + "_Left", rectNormalBounds.Left);
                    oReg.SetValue(f.Name + "_Height", rectNormalBounds.Height);
                    oReg.SetValue(f.Name + "_Width", rectNormalBounds.Width);
                }
                else
                {
                    oReg.SetValue(f.Name + "_Top", f.Top);
                    oReg.SetValue(f.Name + "_Left", f.Left);
                    oReg.SetValue(f.Name + "_Height", f.Height);
                    oReg.SetValue(f.Name + "_Width", f.Width);
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
            throw new NotImplementedException();
        }
    }
}
