using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace Clearinet.UI
{
    public class MRU
    {
        private readonly string _registryPath;
        private readonly RegistryKey _hive;
        private readonly int _maxCount;
        private readonly string _mutexName;

        /// <summary>
        /// Initializes a new instance of the RegistryMruList class.
        /// </summary>
        /// <param name="registryPath">Subkey path under the specified hive (e.g., @"Software\MyCompany\MyApp\MRU").</param>
        /// <param name="maxCount">Maximum number of MRU items to keep.</param>
        /// <param name="hive">Registry hive to use (defaults to CurrentUser).</param>
        public MRU(int maxCount, string registryPath)
        {
            if (!registryPath.HasText())
                throw new ArgumentException("Registry path cannot be null or empty.", nameof(registryPath));
            if (maxCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCount), "Max count must be greater than zero.");

            _registryPath = registryPath;
            _maxCount = maxCount;
            _hive = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);

            // Generate a valid, unique global mutex name based on the registry path
            string sanitizedPath = registryPath.Replace('\\', '_').Replace('/', '_');
            _mutexName = $@"Global\MRU_Mutex_{sanitizedPath}";
        }

        /// <summary>
        /// Pushes a file path to the top of the MRU list.
        /// Removes duplicates and truncates to maxCount.
        /// </summary>
        public void PushFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            string normalizedPath = Path.GetFullPath(filePath);

            ExecuteSynchronized(() =>
            {
                List<string> list = ReadListInternal();

                // Remove existing entry if present (case-insensitive)
                list.RemoveAll(item => string.Equals(item, normalizedPath, StringComparison.OrdinalIgnoreCase));

                // Insert at the top
                list.Insert(0, normalizedPath);

                // Trim overflow
                if (list.Count > _maxCount)
                {
                    list = list.Take(_maxCount).ToList();
                }

                WriteListInternal(list);
            });
        }

        /// <summary>
        /// Removes a file path from the MRU list if present.
        /// </summary>
        public void ForgetFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            string normalizedPath = Path.GetFullPath(filePath);

            ExecuteSynchronized(() =>
            {
                List<string> list = ReadListInternal();

                if (list.RemoveAll(item => string.Equals(item, normalizedPath, StringComparison.OrdinalIgnoreCase)) > 0)
                {
                    WriteListInternal(list);
                }
            });
        }

        /// <summary>
        /// Verifies that each file path exists on disk, removing any that do not.
        /// </summary>
        public void Prune()
        {
            ExecuteSynchronized(() =>
            {
                List<string> list = ReadListInternal();
                List<string> existingFiles = list.Where(File.Exists).ToList();

                if (existingFiles.Count != list.Count)
                {
                    WriteListInternal(existingFiles);
                }
            });
        }

        /// <summary>
        /// Deletes the entire registry subkey containing the MRU items.
        /// </summary>
        public void Purge()
        {
            ExecuteSynchronized(() =>
            {
                _hive.DeleteSubKeyTree(_registryPath, throwOnMissingSubKey: false);
            });
        }

        /// <summary>
        /// Retrieves the current list of MRU items.
        /// </summary>
        public List<string> GetFiles()
        {
            List<string> result = null;
            ExecuteSynchronized(() =>
            {
                result = ReadListInternal();
            });
            return result ?? new List<string>();
        }

        #region Internal Registry & Sync Helpers

        private List<string> ReadListInternal()
        {
            var list = new List<string>();

            using (RegistryKey key = _hive.OpenSubKey(_registryPath, writable: false))
            {
                if (key == null) return list;

                int index = 1;
                while (true)
                {
                    // Reading sequential items: File1, File2, File3...
                    object val = key.GetValue($"File{index}");
                    if (val is string path && !string.IsNullOrWhiteSpace(path))
                    {
                        list.Add(path);
                        index++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return list;
        }

        private void WriteListInternal(List<string> list)
        {
            // First delete the existing subkey tree to clear out old entries cleanly
            _hive.DeleteSubKeyTree(_registryPath, throwOnMissingSubKey: false);

            if (list == null || list.Count == 0) return;

            using (RegistryKey key = _hive.CreateSubKey(_registryPath))
            {
                if (key == null) return;

                for (int i = 0; i < list.Count; i++)
                {
                    key.SetValue($"File{i + 1}", list[i], RegistryValueKind.String);
                }
            }
        }

        private void ExecuteSynchronized(Action action)
        {
            bool createdNew;

            // Grant access to all users so non-admin and admin processes can share the mutex
            MutexSecurity mutexSecurity = new MutexSecurity();
            mutexSecurity.AddAccessRule(new MutexAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                MutexRights.FullControl,
                AccessControlType.Allow));

            using (var mutex = new Mutex(false, _mutexName, out createdNew, mutexSecurity))
            {
                bool hasHandle = false;
                try
                {
                    try
                    {
                        hasHandle = mutex.WaitOne(TimeSpan.FromSeconds(5), false);
                        if (!hasHandle)
                        {
                            throw new TimeoutException("Timed out waiting for global synchronization mutex.");
                        }
                    }
                    catch (AbandonedMutexException)
                    {
                        // Another process terminated abruptly without releasing the mutex.
                        // We now own the mutex and can proceed safely.
                        hasHandle = true;
                    }

                    action();
                }
                finally
                {
                    if (hasHandle)
                    {
                        mutex.ReleaseMutex();
                    }
                }
            }
        }

        #endregion
    }
}
