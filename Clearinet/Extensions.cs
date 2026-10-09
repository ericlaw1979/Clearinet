using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace Clearinet
{
    public class ExtensionBase
    {
        internal static bool AppMeetsVersionDemand(Assembly a, string sCategory)
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
    public class Transcoders : ExtensionBase
    {
        private Dictionary<string, TranscoderTuple> m_Importers = new Dictionary<string, TranscoderTuple>();
        private Dictionary<string, TranscoderTuple> m_Exporters = new Dictionary<string, TranscoderTuple>();
        internal bool hasImporters => (m_Importers?.Count > 0);
        internal bool hasExporters => (m_Exporters?.Count > 0);

        internal Transcoders()
        {
        }

        /// <summary>
        /// Dotted filename extension
        /// </summary>
        /// <param name="sFilenameExtension"></param>
        /// <returns></returns>
        internal TranscoderTuple GetImporterForExt(string sFilenameExtension)
        {
            EnsureReady();
            if (!hasImporters) return null;
            foreach (TranscoderTuple tt in m_Importers.Values)
            {
                if (tt.HandlesFileExtension(sFilenameExtension)) return tt;
            }
            return null;
        }
        /// <summary>
        /// Dotted 
        /// </summary>
        /// <param name="sFilenameExtension"></param>
        /// <returns></returns>
        internal TranscoderTuple GetExporterForExt(string sFilenameExtension)
        {
            EnsureReady();
            if (!hasExporters) return null;
            foreach (TranscoderTuple tt in m_Exporters.Values)
            {
                if (tt.HandlesFileExtension(sFilenameExtension)) return tt;
            }
            return null;
        }

        private void EnsureReady()
        {
            // Issue: This doesn't allow us to "refresh" without restart.
            if (hasImporters || hasExporters) return;
            InventoryFromPath(CONFIG.GetPath("Transcoders_User"), false);  // Load User folder first to enable "upgrades"
            InventoryFromPath(CONFIG.GetPath("Transcoders"), false);
        }
        private static bool AddToTranscoders(Dictionary<string, TranscoderTuple> dictTranscoders, Type t)
        {
            bool bHasFormatSpecifier = false;
            OfferFormatAttribute[] arrFormats = (OfferFormatAttribute[])Attribute.GetCustomAttributes(t, typeof(OfferFormatAttribute));
            if ((null != arrFormats) && (arrFormats.Length > 0))
            {
                bHasFormatSpecifier = true;
                foreach (OfferFormatAttribute ofa in arrFormats)
                {
                    if (!dictTranscoders.ContainsKey(ofa.FormatName))
                    {
                        dictTranscoders.Add(ofa.FormatName, new TranscoderTuple(ofa, t));
                    }
                    else
                    {
                        CApp.Log.Log($"Duplicate Transcoder found for {ofa.FormatName}");
                    }
                }
            }
            return bHasFormatSpecifier;
        }

        internal void InventoryFromPath(string sPath, bool bNestedDir)
        {
            CApp.Log.LogFormat("Scanning for Transcoders in {0}", sPath);

            try
            {
                if (!Directory.Exists(sPath)) return;

                if (!bNestedDir)
                {
                    DirectoryInfo[] oDirs = new DirectoryInfo(sPath).GetDirectories("*.ext");
                    foreach (DirectoryInfo di in oDirs)
                    {
                        InventoryFromPath(di.FullName, true);
                    }
                }

                FileInfo[] arrDLLFiles = new DirectoryInfo(sPath).GetFiles("*.dll");
                CApp.Log.LogFormat("Loading any Extensions in {0}", sPath);

                foreach (FileInfo fi in arrDLLFiles)
                {
                    if (!fi.Name.OICStartsWith("CAT-")) continue;
                    InventoryFromAssembly(fi);
                }
            }
            catch (Exception eX)
            {
                CApp.ReportException(eX, "Extension Load Failed");
            }
        }

        private void InventoryFromAssembly(FileInfo fi)
        {
            Assembly a;
            try
            {
                a = Assembly.UnsafeLoadFrom(fi.FullName);
            }
            catch (Exception eX)
            {
                CApp.Log.LogFormat("! Failed to load Transcoder assembly {0}: {1}", fi.FullName, eX.Message);
                return;
            }

            try
            {
                if (!AppMeetsVersionDemand(a, "AppExtensions")) return;
                foreach (Type t in a.GetExportedTypes())
                {
                    if (t.IsAbstract && !t.IsPublic && !t.IsClass) continue;

                    if (typeof(IExchangeImporter).IsAssignableFrom(t))
                    {
                        try
                        {
                            AddToTranscoders(m_Importers, t);
                        }
                        catch (Exception eX)
                        {
                            CApp.ReportException(eX, "Adding Importer Failed");
                        }
                    }

                    if (typeof(IExchangeExporter).IsAssignableFrom(t))
                    {
                        try
                        {
                            AddToTranscoders(m_Exporters, t);
                        }
                        catch (Exception eX)
                        {
                            CApp.ReportException(eX, "Adding Exporter Failed");
                        }
                    }

                }
            }
            catch (Exception eX)
            {
                CApp.Log.LogFormat("! Failure loading Extensions from {0}: {1}", fi.FullName, eX.Message);
            }
        }

        public void Dispose()
        {
            if (null != m_Importers) { m_Importers.Clear(); }
            if (null != m_Exporters) { m_Exporters.Clear(); }
            m_Importers = m_Importers = null;
        }
    }

    public class Extensions : ExtensionBase
    {
        private Dictionary<Guid, IAppExtension> m_Extensions = new Dictionary<Guid, IAppExtension>();
        private Dictionary<Guid, IAutoTamper> m_AutoTamperers = new Dictionary<Guid, IAutoTamper>();

        //TODO: Fix accessiblity of these dictionaries. They should be private, but the UI needs to access them to add tabs for each Inspector.
        internal Dictionary<Guid, RequestInspectorBase> m_RequestInspectors = new Dictionary<Guid, RequestInspectorBase>();
        internal Dictionary<Guid, ResponseInspectorBase> m_ResponseInspectors = new Dictionary<Guid, ResponseInspectorBase>();

        internal Extensions()
        {
            ScanAndLoad();
        }
        public override string ToString()
        {
            return ToString(false);
        }

        public string ToString(bool bVerbose)
        {
            StringBuilder sbResult = new StringBuilder(128);
            sbResult.Append($"Extensions: {m_Extensions.Count} loaded: " +
                $"{m_RequestInspectors.Count} Request Inspectors; " +
                $"{m_ResponseInspectors.Count} Response Inspectors; " +
                $"{m_AutoTamperers.Count} AutoTamperers");

            if (bVerbose)
            {
                foreach (IAppExtension extension in m_Extensions.Values)
                {
                    Type type = extension.GetType();
                    Assembly assembly = type.Assembly;
                    string filename = String.IsNullOrEmpty(assembly.Location) ? "<unknown>" : Path.GetFileName(assembly.Location);
                    sbResult.AppendFormat("\r\n{0}, v{1}, {2}", type.FullName, assembly.GetName().Version, filename);
                }
            }

            return sbResult.ToString();
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
            InstantiateFromPath(CONFIG.GetPath("Extensions_User"));  // Load User folder first to enable "upgrades"
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
            if (iae is RequestInspectorBase) m_RequestInspectors.Add(t.GUID, (RequestInspectorBase)iae);
            if (iae is ResponseInspectorBase) m_ResponseInspectors.Add(t.GUID, (ResponseInspectorBase)iae);

            // If extension has loaded and the app is already booted, call OnLoad() immediately.
            // Otherwise, it will be called when the app boots.
            if (CApp.isBooted)
            {
                _FireOnload(iae);
            }
        }
    }
}
