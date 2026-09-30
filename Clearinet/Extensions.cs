using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Clearinet
{
    public class Extensions
    {
        private Dictionary<Guid, IAppExtension> m_Extensions = new Dictionary<Guid, IAppExtension>();
        private Dictionary<Guid, IAutoTamper> m_AutoTamperers = new Dictionary<Guid, IAutoTamper>();

        internal Extensions()
        {
            ScanAndLoad();
        }

        internal void CallAllOnLoads()
        {
            foreach (IAppExtension oFE in m_Extensions.Values)
            {
                _FireOnload(oFE);
            }
        }

        private static void _FireOnload(IAppExtension iae)
        {
            try
            {
                iae.OnLoad();
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Extension threw up OnLoad");
            }
        }

        private void ScanAndLoad()
        {
            InstantiateFromPath(CONFIG.GetPath("Extensions_User"));
            InstantiateFromPath(CONFIG.GetPath("Extensions"));

            // If we found any extensions, ensure that we call OnLoad for each when boot completes.
            if (m_Extensions.Count > 0)
            {
                CApp.AppBoot += () => CallAllOnLoads();
            }
        }


        // Scan the Extensions folder for assemblies.
        // For each assembly, check for RequiredVersionAttribute, and if the app's version is lower than the required version, skip it.
        // For each valid assembly, find types that implement IAppExtension, and instantiate them.
        private void InstantiateFromPath(string sPath, bool bNestedDir = false)
        {
            CApp.Log.LogFormat("Scanning for extensions in {0}", sPath);

            try
            {
                if (!Directory.Exists(sPath)) return;

                if (!bNestedDir)
                {
                    DirectoryInfo[] oDirs = new DirectoryInfo(sPath).GetDirectories("*.ext");
                    foreach (DirectoryInfo di in oDirs)
                    {
                        InstantiateFromPath(di.FullName, true);
                    }
                }

                FileInfo[] arrDLLFiles = new DirectoryInfo(sPath).GetFiles("*.dll");

                // CApp.Prefs.GetBoolPref("app.debug.extensions.verbose", false);
                CApp.Log.LogFormat("Loading any Extensions in {0}", sPath);

                foreach (FileInfo fi in arrDLLFiles)
                {
                    if (!fi.Name.OICStartsWith("CAE-")) continue;
                    InstantiateFromFile(fi);
                }
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Extension Load Failed");
            }
        }

        private void InstantiateFromFile(FileInfo fi)
        {
            Assembly a;
            try
            {
                a = Assembly.UnsafeLoadFrom(fi.FullName);
            }
            catch (Exception eX)
            {
                CApp.Log.LogFormat("! Failed to load extension assembly {0}: {1}", fi.FullName, eX.Message);
                return;
            }

            try
            {
                if (!AppMeetsVersionDemand(a, "AppExtensions")) return;
                foreach (Type t in a.GetExportedTypes())
                {
                    if (!t.IsAbstract && t.IsPublic && t.IsClass)
                    {
                        if (typeof(IAppExtension).IsAssignableFrom(t))
                        {
                            try
                            {
                                InstantiateExtension(t);
                            }
                            catch (Exception eX)
                            {
                                CApp.Log.LogFormat("! Failed to instantiate extension {0} from {1}: {2}", t.Name, fi.FullName, eX.Message);
                            }
                        }
                    }
                }
            }
            catch (Exception eX)
            {
                CApp.Log.LogFormat("! Failure loading Extensions from {0}: {1}", fi.FullName, eX.Message);
            }
        }

        private void InstantiateExtension(Type t)
        {
            if (m_Extensions.ContainsKey(t.GUID)) return;

            IAppExtension iae = (IAppExtension)Activator.CreateInstance(t);
            m_Extensions.Add(t.GUID, iae);
            if (iae is IAutoTamper) m_AutoTamperers.Add(t.GUID, (IAutoTamper)iae);

            // If extension has loaded and the app is already booted, call OnLoad() immediately.
            // Otherwise, it will be called when the app boots.
            if (CApp.isBooted)
            {
                _FireOnload(iae);
            }
        }

        private static bool AppMeetsVersionDemand(Assembly a, string sCategory)
        {
            var attrRequirement = (RequiredVersionAttribute)Attribute.GetCustomAttribute(
                                   a, typeof(RequiredVersionAttribute), inherit: false);
            if (attrRequirement == null)
            {
                CApp.Log.LogFormat("! File '{0}' is named as if it's an extension. However, it does not specify a RequiredVersionAttribute, so it will be ignored.", a.FullName);
                return false;
            }

            if (!Version.TryParse(attrRequirement.RequiredVersion, out var verRequired))
            {
                Debug.Assert(false, "Invalid RequiredVersion string format");
                return false;
            }

            bool bOutdated = (Assembly.GetExecutingAssembly().GetName().Version < verRequired);
            if (bOutdated)
            {
                string location = string.IsNullOrEmpty(a.Location) ? a.FullName : a.Location;
                CApp.DoNotifyUser(
                    $"The {sCategory} in {location} require Clearinet v{attrRequirement.RequiredVersion} or later. (You have v{Assembly.GetExecutingAssembly().GetName().Version})\n\n" +
                    $"Please install the latest version of Clearinet from https://clearinet.app.\n",
                    "Extension Not Loaded");
                return false;
            }

            return true;
        }
    }
}
