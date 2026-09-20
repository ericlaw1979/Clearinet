// Documentation at http://fiddler.wikidot.com/prefs
// This sample is provided "AS IS" and confers no warranties.
// You are granted a non-exclusive, worldwide, royalty-free license to reproduce this code,
// prepare derivative works, and distribute it or any derivative works that you create.
//

// TODO:
// Add an internal version of the indexer that will allow skipping notification of Event handlers and setting of Internal Prefs
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Threading;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Collections;
using System.Text;
 
namespace Clearinet
{
    #region EventArgs
    
    /// <summary>
    /// EventArgs for preference-change events.  See http://msdn.microsoft.com/en-us/library/ms229011.aspx.
    /// </summary>
    public class PrefChangeEventArgs : EventArgs
    {
        private readonly string _prefName;
        private readonly string _prefValueString;
 
        internal PrefChangeEventArgs(string prefName, string prefValueString)
        {
            _prefName = prefName;
            _prefValueString = prefValueString;
        }
 
        /// <summary>
        /// The name of the preference
        /// </summary>
        public string PrefName
        {
            get
            {
                return _prefName;
            }
        }
 
        /// <summary>
        /// The string value of the preference
        /// </summary>
        public string ValueString
        {
            get
            {
                return _prefValueString;
            }
        }
 
        /// <summary>
        /// Returns TRUE if ValueString=="true", case-insensitively
        /// </summary>
        public bool ValueBool
        {
            get
            {
                return ("True".Equals(_prefValueString, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
    #endregion EventArgs  
 
    /// <summary>
    /// The IClearinetPreferences Interface
    /// </summary>
    public interface IClearinetPreferences
    {
        /// <summary>
        /// Store a boolean value for a preference
        /// </summary>
        /// <param name="sPrefName">The named preference</param>
        /// <param name="bValue">The boolean value to store</param>
        void SetBoolPref(string sPrefName, bool bValue);
 
        /// <summary>
        /// Store an Int32 value for a preference
        /// </summary>
        /// <param name="sPrefName">The named preference</param>
        /// <param name="iValue">The int32 value to store</param>
        void SetInt32Pref(string sPrefName, Int32 iValue);
 
        /// <summary>
        /// Store a string value for a preference
        /// </summary>
        /// <param name="sPrefName">The named preference</param>
        /// <param name="sValue">The string value to store</param>
        void SetStringPref(string sPrefName, string sValue);
 
        /// <summary>
        /// Get a preference's value as a boolean
        /// </summary>
        /// <param name="sPrefName">The Preference Name</param>
        /// <param name="bDefault">The default value for missing or invalid preferences</param>
        /// <returns>A Boolean</returns>
        bool GetBoolPref(string sPrefName, bool bDefault);
 
        /// <summary>
        /// Gets a preference's value as a string
        /// </summary>
        /// <param name="sPrefName">The Preference Name</param>
        /// <param name="sDefault">The default value for missing preferences</param>
        /// <returns>A string</returns>
        string GetStringPref(string sPrefName, string sDefault);
 
        /// <summary>
        /// Gets a preference's value as a 32-bit integer
        /// </summary>
        /// <param name="sPrefName">The Preference Name</param>
        /// <param name="iDefault">The default value for missing or invalid preferences</param>
        /// <returns>An integer</returns>
        Int32 GetInt32Pref(string sPrefName, Int32 iDefault);
 
        /// <summary>
        /// Removes a named preference from storage
        /// </summary>
        /// <param name="sPrefName">The name of the preference to remove</param>
        void RemovePref(string sPrefName);
 
        /// <summary>
        /// Add a Watcher which will be notified when a value has changed
        /// </summary>
        /// <param name="sPrefixFilter">The prefix of preferences for which changes are interesting</param>
        /// <param name="pcehHandler">The Event handler to notify</param>
        /// <returns>Returns the Watcher object added to the notification list</returns>
        PreferenceBag.PrefWatcher AddWatcher(string sPrefixFilter, EventHandler<PrefChangeEventArgs> pcehHandler);
 
        /// <summary>
        /// Removes a previously-created preference Watcher from the notification queue
        /// </summary>
        /// <param name="wliToRemove">The Watcher to remove</param>
        void RemoveWatcher(PreferenceBag.PrefWatcher wliToRemove);
 
        /// <summary>
        /// Indexer. Returns the value of the preference as a string
        /// </summary>
        /// <param name="sName">The Preference Name</param>
        /// <returns>The Preference value as a string, or null</returns>
        string this[string sName] {get; set; }
    }
 
    /// <summary>
    /// The PreferenceBag is used to maintain a threadsafe Key/Value list of preferences, persisted in the registry, and with appropriate eventing when a value changes.
    /// </summary>
    public class PreferenceBag : IClearinetPreferences
    {
        #region Fields
        // NB: Using Dictionary<string, string> rather than a StringDictionary results in different behavior, like
        // the indexer throwing an exception when looking up a non-existent key. 
        // StringDictionary is also case-insensitive, which is desirable.
        private readonly StringDictionary _dictPrefs = new StringDictionary();
        private readonly List<PrefWatcher> _listWatchers = new List<PrefWatcher>();
 
        // TODO: Migrate to ReaderWriterLockSlim for .NET 3.5/4.0
        //http://www.bluebytesoftware.com/blog/PermaLink,guid,54bf6c16-ce42-49d5-94db-aafbd7cb0c27.aspx
        private readonly ReaderWriterLock _RWLockPrefs = new ReaderWriterLock();
        private readonly ReaderWriterLock _RWLockWatchers = new ReaderWriterLock();
 
        private string _sRegistryPath;
        private string _sCurrentProfile = ".default";
        #endregion  
 
        internal PreferenceBag(string sRegPath)
        {
            _sRegistryPath = sRegPath;
            ReadRegistry();
            Debug.WriteLine("created preferences object; loaded prefs: " + _dictPrefs.Count.ToString());
        }
 
        #region PrefNameValidation
        static char[] _arrForbiddenChars = { '*', ' ', '$', '%', '@', '?', '!' };
        private bool isValidName(string sName)
        {
            // TODO: Use RegEx to enforce A-z0-9-_
            return ((!String.IsNullOrEmpty(sName)) &&
                (256 > sName.Length) &&                                              // Prior to .NETv4, regkey names are limited to 255 characters.
                (0 > sName.IndexOf("internal", StringComparison.OrdinalIgnoreCase)) &&
                (0 > sName.IndexOfAny(_arrForbiddenChars)));
        }
        #endregion PrefNameValidation
 
        #region Profiles
        /// <summary>
        /// Returns a string naming the current profile
        /// </summary>
        public string CurrentProfile
        {
            get
            {
                return _sCurrentProfile;
            }
        }
 
        #endregion
 
        #region RegistryPersistence
        /// <summary>
        /// Load the existing preferences from the registry into the Preferences bag.
        /// </summary>
        private void ReadRegistry()
        {
            if (null == _sRegistryPath)
            {
                return;
            }
            RegistryKey oReg = Registry.CurrentUser.OpenSubKey(_sRegistryPath + @"\" + _sCurrentProfile, false);
            if (null == oReg) return; 
            string[] arrPrefs = oReg.GetValueNames();
 
            try
            {
                _RWLockPrefs.AcquireWriterLock(Timeout.Infinite);
                foreach (string sPref in arrPrefs)
                {
                    if (sPref.Length < 1) continue; 
                    if (sPref.Contains("ephemeral")) continue;
                    try
                    {
                        _dictPrefs[sPref] = (string)oReg.GetValue(sPref, string.Empty);
                    }
                    catch (Exception eX)
                    {
                        // Registry key was probably of the wrong type...
                        Debug.WriteLine(eX);
                    }
                }
                // System.Windows.Forms.MessageBox.Show(String.Join("\r\n", GetPrefArray()));
            }
            finally
            {
                _RWLockPrefs.ReleaseWriterLock();
                oReg.Close();
            }
        }
 
        /// <summary>
        /// Serialize the existing preferences to the Registry.
        /// </summary>
        private void WriteRegistry()
        {
            if (CONFIG.bIsViewOnly)
            {
                return;
            }
            if (null == _sRegistryPath) 
            {
                return;
            }
            RegistryKey oReg = Registry.CurrentUser.CreateSubKey(_sRegistryPath);
            if (null == oReg)
            {
                Debug.Assert(false, "Cannot serialize Preference bag!");
                return;
            }
 
            try
            {
                _RWLockPrefs.AcquireReaderLock(Timeout.Infinite);
           
                // Wipe the old values
                oReg.DeleteSubKey(_sCurrentProfile, false);
 
                // Bail at this point if there are no preferences
                if (_dictPrefs.Count < 1) { return; }
 
                // Create the Profile subkey
                oReg = oReg.CreateSubKey(_sCurrentProfile);
 
                // Write the new values
                foreach (DictionaryEntry dePrefVal in _dictPrefs)
                {
                    string sKey = (string)dePrefVal.Key;
                    if (sKey.Contains("ephemeral")) continue;
                    oReg.SetValue(sKey, dePrefVal.Value);
                }
            }
            finally
            {
                _RWLockPrefs.ReleaseReaderLock();
                oReg.Close();
            }
        }
        #endregion RegistryPersistence
        
        #region Indexer
        /// <summary>
        /// Indexer into the Preference collection.
        /// </summary>
        /// <param name="sPrefName">The name of the Preference to update/create or return.</param>
        /// <returns>The string value of the preference, or null.</returns>
        public string this[string sPrefName]
        {
            get
            {               
                try
                {
                    _RWLockPrefs.AcquireReaderLock(Timeout.Infinite);
                    return _dictPrefs[sPrefName];
                }
                finally
                {
                    _RWLockPrefs.ReleaseReaderLock();
                } 
            }
            set
            {
                if (!isValidName(sPrefName))
                {
                    throw new ArgumentException(String.Format("Preference name must contain 1 or more characters from the set A-z0-9-_ and may not contain the word Internal.\nYou tried to set: \"{0}\"", sPrefName));
                }
 
                if (value == null)
                {
                    RemovePref(sPrefName);
                    return;
                }
 
                bool _bNotifyChange = false;
                try
                {
                    // Determine if the value existed & had the same value
                    _RWLockPrefs.AcquireWriterLock(Timeout.Infinite);
                    _bNotifyChange = (!_dictPrefs.ContainsKey(sPrefName) || (_dictPrefs[sPrefName] != value));
 
                    // Write the preference. TODO: Should we skip the next line if we already know it's unchanged?
                    _dictPrefs[sPrefName] = value;
                }
                finally
                {
                    _RWLockPrefs.ReleaseWriterLock();
                }
                if (_bNotifyChange)
                {
                    PrefChangeEventArgs oArgs = new PrefChangeEventArgs(sPrefName, value);
                    AsyncNotifyWatchers(oArgs);
                }
            }
        }
        #endregion Indexer
 
        /// <summary>
        /// Get a string array of the preferences
        /// </summary>
        /// <returns>string[] of preference values</returns>
        public string[] GetPrefArray()
        {
            try
            {
                _RWLockPrefs.AcquireReaderLock(Timeout.Infinite);
                string[] arrResult = new String[_dictPrefs.Count];
                _dictPrefs.Keys.CopyTo(arrResult, 0);
                return arrResult;
            }
            finally
            {
                _RWLockPrefs.ReleaseReaderLock();
            }
        }
 
        #region GetterHelpers
        /// <summary>
        /// Return a string preference.
        /// </summary>
        /// <param name="sPrefName"></param>
        /// <param name="sDefault"></param>
        /// <returns></returns>
        public string GetStringPref(string sPrefName, string sDefault)
        {
            string sRet = this[sPrefName];
            Debug.WriteLineIf(null == sRet, "[ClearinetPref] Lookup for non-existent preference " + sPrefName);
            return (sRet ?? sDefault);
        }
 
        /// <summary>
        /// Return a bool preference.
        /// </summary>
        /// <param name="sPrefName"></param>
        /// <param name="bDefault"></param>
        /// <returns></returns>
        public bool GetBoolPref(string sPrefName, bool bDefault)
        {
            string sRet = this[sPrefName];
            if (sRet == null)
            {
                Debug.WriteLine("[ClearinetPref] Lookup for non-existent preference " + sPrefName);
                return bDefault;
            }
            else
            {
                bool bRet;
                if (bool.TryParse(sRet, out bRet))      // Case-insensitive compare of "TRUE"/"FALSE"
                {
                    return bRet;
                }
                else
                {
                    return bDefault;
                }
            }
        }
 
        /// <summary>
        /// Return an Int32 Preference.
        /// </summary>
        /// <param name="sPrefName"></param>
        /// <param name="iDefault"></param>
        /// <returns></returns>
        public Int32 GetInt32Pref(string sPrefName, Int32 iDefault)
        {
            string sRet = this[sPrefName];
            if (sRet == null) 
            {
                Debug.WriteLine("[ClearinetPref] Lookup for non-existent preference " + sPrefName);
                return iDefault;
            } 
            else
            {
                int iRet;
                if (Int32.TryParse(sRet, out iRet))
                {
                    return iRet;
                }
                else
                {
                    return iDefault;
                }
            }
        }
        #endregion GetterHelpers
 
        #region SetterHelpers
        /// <summary>
        /// Update or create a string preference.
        /// </summary>
        /// <param name="sPrefName"></param>
        /// <param name="sValue"></param>
        public void SetStringPref(string sPrefName, string sValue)
        {
            this[sPrefName] = sValue;
        }
 
        /// <summary>
        /// Update or create a Int32 Preference
        /// </summary>
        /// <param name="sPrefName"></param>
        /// <param name="iValue"></param>
        public void SetInt32Pref(string sPrefName, Int32 iValue)
        {
            this[sPrefName] = iValue.ToString();
        }
 
        /// <summary>
        /// Update or create a Boolean preference.
        /// </summary>
        /// <param name="sPrefName"></param>
        /// <param name="bValue"></param>
        public void SetBoolPref(string sPrefName, bool bValue)
        {
            this[sPrefName] = bValue.ToString();
        }
        #endregion SetterHelpers
 
        /// <summary>
        /// Delete a Preference from the collection.
        /// </summary>
        /// <param name="sPrefName">The name of the Preference.</param>
        public void RemovePref(string sPrefName)
        {
            bool _bNotifyChange = false;
            try
            {
                _RWLockPrefs.AcquireWriterLock(Timeout.Infinite);
                _bNotifyChange = _dictPrefs.ContainsKey(sPrefName);
                _dictPrefs.Remove(sPrefName);
            }
            finally
            {
                _RWLockPrefs.ReleaseWriterLock();
            }
            if (_bNotifyChange)
            {
                PrefChangeEventArgs oArgs = new PrefChangeEventArgs(sPrefName, String.Empty);
                AsyncNotifyWatchers(oArgs);
            }
        }
 
        /// <summary>
        /// Remove all watchers and write the registry.
        /// </summary>
        public void Close()
        {
            _listWatchers.Clear();
            WriteRegistry();
        }
 
        /// <summary>
        /// Return a short description of the contents of the preference bag
        /// </summary>
        /// <returns>Single-line string</returns>
        public override string ToString()
        {
            return ToString(false);
        }
 
        /// <summary>
        /// Return a string-based serialization of the Preferences settings.
        /// </summary>
        /// <param name="bVerbose">TRUE for a multi-line format with all preferences</param>
        /// <returns>String</returns>
        internal string ToString(bool bVerbose)
        {
            StringBuilder sbResult = new StringBuilder(128);
            try
            {
                _RWLockPrefs.AcquireReaderLock(Timeout.Infinite);
                sbResult.AppendFormat("PreferenceBag [{0} Preferences. {1} Watchers.]", _dictPrefs.Count, _listWatchers.Count);
                if (bVerbose)
                {
                    sbResult.Append("\r\n");
                    foreach (DictionaryEntry dePrefVal in _dictPrefs)
                    {
                        sbResult.AppendFormat("{0}:\t{1}\r\n", dePrefVal.Key, dePrefVal.Value);
                    }
                }
            }
            finally
            {
                _RWLockPrefs.ReleaseReaderLock();
            }
            return sbResult.ToString();
        }
 
        internal string FindMatches(string sFilter)
        {
            StringBuilder sbResult = new StringBuilder(128);
            try
            {
                _RWLockPrefs.AcquireReaderLock(Timeout.Infinite);
                foreach (DictionaryEntry dePrefVal in _dictPrefs)
                {
                    if (((string)dePrefVal.Key).IndexOf(sFilter, StringComparison.OrdinalIgnoreCase) > -1)
                    {
                        sbResult.AppendFormat("{0}:\t{1}\r\n", dePrefVal.Key, dePrefVal.Value);
                    }
                }
            }
            finally
            {
                _RWLockPrefs.ReleaseReaderLock();
            }
            return sbResult.ToString();
        }
 
        #region Watchers
 
        /// <summary>
        /// A simple struct which contains a Branch identifier and EventHandler
        /// </summary>
        public struct PrefWatcher
        {
            internal PrefWatcher(string sPrefixFilter, EventHandler<PrefChangeEventArgs> fnHandler)
            {
                sPrefixToWatch = sPrefixFilter;
                fnToNotify = fnHandler;
            }
            
            internal readonly EventHandler<PrefChangeEventArgs> fnToNotify;
            internal readonly string sPrefixToWatch;
        }
 
        /// <summary>
        /// Add a watcher for changes to the specified preference or preference branch.
        /// </summary>
        /// <param name="sPrefixFilter">Preference branch to monitor</param>
        /// <param name="pcehHandler">The EventHandler accepting PrefChangeEventArgs to notify</param>
        public PreferenceBag.PrefWatcher AddWatcher(string sPrefixFilter, EventHandler<PrefChangeEventArgs> pcehHandler)
        {
            // TODO: Validate sBranchFilter?            
            PrefWatcher wliNew = new PrefWatcher(sPrefixFilter.ToLower(), pcehHandler);
            _RWLockWatchers.AcquireWriterLock(Timeout.Infinite);
            try
            {
                _listWatchers.Add(wliNew);
            }
            finally
            {
                _RWLockWatchers.ReleaseWriterLock();
            }
            return wliNew;
        }
 
        /// <summary>
        /// Remove a previously attached Watcher
        /// </summary>
        /// <param name="wliToRemove">The previously-specified Watcher</param>
        public void RemoveWatcher(PreferenceBag.PrefWatcher wliToRemove)
        {
            _RWLockWatchers.AcquireWriterLock(Timeout.Infinite);
            try
            {
                _listWatchers.Remove(wliToRemove);
            }
            finally
            {
                _RWLockWatchers.ReleaseWriterLock();
            }
        }
 
        /// <summary>
        /// This function executes on a single background thread and notifies any registered
        /// Watchers of changes in preferences they care about.
        /// </summary>
        /// <param name="objThreadState">A string containing the name of the Branch that changed</param>
        private void _NotifyThreadExecute(object objThreadState)
        {
            PrefChangeEventArgs oArgs = (PrefChangeEventArgs)objThreadState;
            string sBranch = oArgs.PrefName;
 
            List<EventHandler<PrefChangeEventArgs>> listToNotify = null;
            try
            {
                #region BuildListofInterestedWatchers
                _RWLockWatchers.AcquireReaderLock(Timeout.Infinite);
                try
                {
                    foreach (PrefWatcher wliEntry in _listWatchers)
                    {
                        if (sBranch.StartsWith(wliEntry.sPrefixToWatch, StringComparison.Ordinal))
                        {
                            if (null == listToNotify)
                            {
                                listToNotify = new List<EventHandler<PrefChangeEventArgs>>();
                            }
 
                            listToNotify.Add(wliEntry.fnToNotify);
                        }
                    }
                }
                finally
                {
                    _RWLockWatchers.ReleaseReaderLock();
                }
                #endregion BuildListofInterestedWatchers
 
                #region NotifyEachInterestedWatcher
                if (null != listToNotify)
                {
                    foreach (EventHandler<PrefChangeEventArgs> oEach in listToNotify)
                    {
                        try
                        {
                            oEach(this, oArgs);
                        }
                        catch (Exception eX)
                        {
                            ClearinetApp.ReportException(eX);
                        }
                    }
                } // else NobodyWatchingThisBranch...
                #endregion NotifyEachInterestedWatcher
            }
            catch (Exception eX)
            {
                ClarinetApp.ReportException(eX);
            }
        }
 
        /// <summary>
        /// Spawn a background thread to notify any interested Watchers of changes to the Target preference branch.
        /// </summary>
        /// <param name="oNotifyArgs">The arguments to pass to the interested Watchers</param>
        private void AsyncNotifyWatchers(PrefChangeEventArgs oNotifyArgs)
        {
            ThreadPool.UnsafeQueueUserWorkItem(_NotifyThreadExecute, oNotifyArgs);
        }
        #endregion Watchers
    }
}