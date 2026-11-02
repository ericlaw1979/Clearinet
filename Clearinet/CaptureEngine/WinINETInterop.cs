using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Clearinet.CaptureEngine
{
    /// <summary>
    /// WinINET interop used to read and modify per-connection proxy settings.
    /// </summary>
    internal static class WinINETInterop
    {
        private const int INTERNET_OPTION_PER_CONNECTION_OPTION = 75;
        private const int INTERNET_OPTION_PROXY_SETTINGS_CHANGED = 95;

        private const int INTERNET_PER_CONN_FLAGS = 1;
        private const int INTERNET_PER_CONN_PROXY_SERVER = 2;
        private const int INTERNET_PER_CONN_PROXY_BYPASS = 3;
        private const int INTERNET_PER_CONN_AUTOCONFIG_URL = 4;
        private const int INTERNET_PER_CONN_AUTODISCOVERY_FLAGS = 5;
        private const int INTERNET_PER_CONN_FLAGS_UI = 10;

        //
        // PER_CONN_FLAGS
        //
        private const int PROXY_TYPE_DIRECT = 0x00000001; // direct to net
        private const int PROXY_TYPE_PROXY = 0x00000002; // via named proxy
        private const int PROXY_TYPE_AUTO_PROXY_URL = 0x00000004; // autoproxy URL
        private const int PROXY_TYPE_AUTO_DETECT = 0x00000008; // use autoproxy detection

        //
        // PER_CONN_AUTODISCOVERY_FLAGS
        //
        private const int AUTO_PROXY_FLAG_USER_SET = 0x00000001;        // user changed this setting
        private const int AUTO_PROXY_FLAG_ALWAYS_DETECT = 0x00000002;   // force detection even when its not needed
        private const int AUTO_PROXY_FLAG_DETECTION_RUN = 0x00000004;   // detection has been run
        private const int AUTO_PROXY_FLAG_MIGRATED = 0x00000008;        // migration has just been done
        private const int AUTO_PROXY_FLAG_DONT_CACHE_PROXY_RESULT = 0x00000010; // don't cache result of host=proxy name
        private const int AUTO_PROXY_FLAG_CACHE_INIT_RUN = 0x00000020;      // don't initalize and run unless URL expired
        private const int AUTO_PROXY_FLAG_DETECTION_SUSPECT = 0x00000040;   // results of detection are suspect

        [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool InternetQueryOption(
            IntPtr hInternet,
            int dwOption,
            IntPtr lpBuffer,
            ref int lpdwBufferLength);

        [DllImport("wininet.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool InternetSetOption(
            IntPtr hInternet,
            int dwOption,
            IntPtr lpBuffer,
            int dwBufferLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct INTERNET_PER_CONN_OPTION_LIST
        {
            public int dwSize;
            public IntPtr pszConnection;
            public int dwOptionCount;
            public int dwOptionError;
            public IntPtr pOptions;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INTERNET_PER_CONN_OPTION
        {
            public int dwOption;
            public INTERNET_PER_CONN_OPTION_VALUE Value;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INTERNET_PER_CONN_OPTION_VALUE
        {
            [FieldOffset(0)]
            public int dwValue;

            [FieldOffset(0)]
            public IntPtr pszValue;

            [FieldOffset(0)]
            public System.Runtime.InteropServices.ComTypes.FILETIME ftValue;
        }

        private struct ProxySettingsSnapshot
        {
            public int FlagsUi;
            public string ProxyServer;
            public string ProxyBypass;
            public string AutoConfigUrl;
            public int AutoDiscoveryFlags;
        }

        /// <summary>
        /// Reads current system proxy settings and returns a serialized snapshot string suitable for restore.
        /// </summary>
        internal static string CaptureCurrentProxySettings()
        {
            ProxySettingsSnapshot snapshot = QueryCurrentSettings();
            return SerializeSnapshot(snapshot);
        }

        /// <summary>
        /// Configures WinINET to use an explicit proxy server and bypass list.
        /// </summary>
        internal static void SetProxyForClearinet(string proxyServer, string proxyBypassList)
        {
            if (string.IsNullOrWhiteSpace(proxyServer))
            {
                throw new ArgumentException("Proxy server must be provided.", nameof(proxyServer));
            }

            if (string.IsNullOrWhiteSpace(proxyBypassList))
            {
                throw new ArgumentException("Proxy bypass list must be provided.", nameof(proxyBypassList));
            }

            SetPerConnectionOptions(
                new INTERNET_PER_CONN_OPTION
                {
                    dwOption = INTERNET_PER_CONN_FLAGS,
                    Value = new INTERNET_PER_CONN_OPTION_VALUE { dwValue = PROXY_TYPE_PROXY }
                },
                new INTERNET_PER_CONN_OPTION
                {
                    dwOption = INTERNET_PER_CONN_PROXY_SERVER,
                    Value = new INTERNET_PER_CONN_OPTION_VALUE { pszValue = Marshal.StringToHGlobalUni(proxyServer) }
                },
                new INTERNET_PER_CONN_OPTION
                {
                    dwOption = INTERNET_PER_CONN_PROXY_BYPASS,
                    Value = new INTERNET_PER_CONN_OPTION_VALUE { pszValue = Marshal.StringToHGlobalUni(proxyBypassList) }
                });
        }

        /// <summary>
        /// Restores system proxy settings from a prior CaptureCurrentProxySettings snapshot.
        /// </summary>
        internal static void RestoreProxySettings(string serializedSettings)
        {
            ProxySettingsSnapshot snapshot = DeserializeSnapshot(serializedSettings);

            SetPerConnectionOptions(
                new INTERNET_PER_CONN_OPTION
                {
                    dwOption = INTERNET_PER_CONN_FLAGS,
                    Value = new INTERNET_PER_CONN_OPTION_VALUE { dwValue = snapshot.FlagsUi }
                },
                new INTERNET_PER_CONN_OPTION
                {
                    dwOption = INTERNET_PER_CONN_PROXY_SERVER,
                    Value = new INTERNET_PER_CONN_OPTION_VALUE { pszValue = Marshal.StringToHGlobalUni(snapshot.ProxyServer ?? string.Empty) }
                },
                new INTERNET_PER_CONN_OPTION
                {
                    dwOption = INTERNET_PER_CONN_PROXY_BYPASS,
                    Value = new INTERNET_PER_CONN_OPTION_VALUE { pszValue = Marshal.StringToHGlobalUni(snapshot.ProxyBypass ?? string.Empty) }
                },
                new INTERNET_PER_CONN_OPTION
                {
                    dwOption = INTERNET_PER_CONN_AUTOCONFIG_URL,
                    Value = new INTERNET_PER_CONN_OPTION_VALUE { pszValue = Marshal.StringToHGlobalUni(snapshot.AutoConfigUrl ?? string.Empty) }
                },
                new INTERNET_PER_CONN_OPTION
                {
                    dwOption = INTERNET_PER_CONN_AUTODISCOVERY_FLAGS,
                    Value = new INTERNET_PER_CONN_OPTION_VALUE { dwValue = snapshot.AutoDiscoveryFlags }
                });
        }

        private static ProxySettingsSnapshot QueryCurrentSettings()
        {
            INTERNET_PER_CONN_OPTION[] options = QueryPerConnectionOptions(
                INTERNET_PER_CONN_FLAGS_UI,
                INTERNET_PER_CONN_PROXY_SERVER,
                INTERNET_PER_CONN_PROXY_BYPASS,
                INTERNET_PER_CONN_AUTOCONFIG_URL,
                INTERNET_PER_CONN_AUTODISCOVERY_FLAGS);

            return new ProxySettingsSnapshot
            {
                FlagsUi = options[0].Value.dwValue,
                ProxyServer = PtrToString(options[1].Value.pszValue),
                ProxyBypass = PtrToString(options[2].Value.pszValue),
                AutoConfigUrl = PtrToString(options[3].Value.pszValue),
                AutoDiscoveryFlags = options[4].Value.dwValue
            };
        }

        private static INTERNET_PER_CONN_OPTION[] QueryPerConnectionOptions(params int[] optionIds)
        {
            int optionSize = Marshal.SizeOf(typeof(INTERNET_PER_CONN_OPTION));
            IntPtr optionsPtr = IntPtr.Zero;
            IntPtr listPtr = IntPtr.Zero;

            try
            {
                optionsPtr = Marshal.AllocCoTaskMem(optionSize * optionIds.Length);

                for (int i = 0; i < optionIds.Length; i++)
                {
                    var option = new INTERNET_PER_CONN_OPTION { dwOption = optionIds[i] };
                    Marshal.StructureToPtr(option, IntPtr.Add(optionsPtr, i * optionSize), false);
                }

                var list = new INTERNET_PER_CONN_OPTION_LIST
                {
                    dwSize = Marshal.SizeOf(typeof(INTERNET_PER_CONN_OPTION_LIST)),
                    pszConnection = IntPtr.Zero,
                    dwOptionCount = optionIds.Length,
                    dwOptionError = 0,
                    pOptions = optionsPtr
                };

                int listSize = Marshal.SizeOf(typeof(INTERNET_PER_CONN_OPTION_LIST));
                listPtr = Marshal.AllocCoTaskMem(listSize);
                Marshal.StructureToPtr(list, listPtr, false);

                if (!InternetQueryOption(IntPtr.Zero, INTERNET_OPTION_PER_CONNECTION_OPTION, listPtr, ref listSize))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "InternetQueryOption failed.");
                }

                var result = new INTERNET_PER_CONN_OPTION[optionIds.Length];
                for (int i = 0; i < optionIds.Length; i++)
                {
                    result[i] = (INTERNET_PER_CONN_OPTION)Marshal.PtrToStructure(
                        IntPtr.Add(optionsPtr, i * optionSize),
                        typeof(INTERNET_PER_CONN_OPTION));
                }

                return result;
            }
            finally
            {
                if (optionsPtr != IntPtr.Zero)
                {
                    for (int i = 0; i < optionIds.Length; i++)
                    {
                        var option = (INTERNET_PER_CONN_OPTION)Marshal.PtrToStructure(
                            IntPtr.Add(optionsPtr, i * optionSize),
                            typeof(INTERNET_PER_CONN_OPTION));

                        if (IsStringOption(option.dwOption) && option.Value.pszValue != IntPtr.Zero)
                        {
                            GlobalFree(option.Value.pszValue);
                        }
                    }

                    Marshal.FreeCoTaskMem(optionsPtr);
                }

                if (listPtr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(listPtr);
                }
            }
        }

        private static void SetPerConnectionOptions(params INTERNET_PER_CONN_OPTION[] options)
        {
            int optionSize = Marshal.SizeOf(typeof(INTERNET_PER_CONN_OPTION));
            IntPtr optionsPtr = IntPtr.Zero;
            IntPtr listPtr = IntPtr.Zero;

            try
            {
                optionsPtr = Marshal.AllocCoTaskMem(optionSize * options.Length);

                for (int i = 0; i < options.Length; i++)
                {
                    Marshal.StructureToPtr(options[i], IntPtr.Add(optionsPtr, i * optionSize), false);
                }

                var list = new INTERNET_PER_CONN_OPTION_LIST
                {
                    dwSize = Marshal.SizeOf(typeof(INTERNET_PER_CONN_OPTION_LIST)),
                    pszConnection = IntPtr.Zero,
                    dwOptionCount = options.Length,
                    dwOptionError = 0,
                    pOptions = optionsPtr
                };

                int listSize = Marshal.SizeOf(typeof(INTERNET_PER_CONN_OPTION_LIST));
                listPtr = Marshal.AllocCoTaskMem(listSize);
                Marshal.StructureToPtr(list, listPtr, false);

                if (!InternetSetOption(IntPtr.Zero, INTERNET_OPTION_PER_CONNECTION_OPTION, listPtr, listSize))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "InternetSetOption(INTERNET_OPTION_PER_CONNECTION_OPTION) failed.");
                }

                if (!InternetSetOption(IntPtr.Zero, INTERNET_OPTION_PROXY_SETTINGS_CHANGED, IntPtr.Zero, 0))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "InternetSetOption(INTERNET_OPTION_PROXY_SETTINGS_CHANGED) failed.");
                }
            }
            finally
            {
                for (int i = 0; i < options.Length; i++)
                {
                    if (IsStringOption(options[i].dwOption) && options[i].Value.pszValue != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(options[i].Value.pszValue);
                    }
                }

                if (optionsPtr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(optionsPtr);
                }

                if (listPtr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(listPtr);
                }
            }
        }

        private static bool IsStringOption(int option)
        {
            return option == INTERNET_PER_CONN_PROXY_SERVER ||
                   option == INTERNET_PER_CONN_PROXY_BYPASS ||
                   option == INTERNET_PER_CONN_AUTOCONFIG_URL;
        }

        private static string PtrToString(IntPtr ptr)
        {
            return ptr == IntPtr.Zero ? string.Empty : (Marshal.PtrToStringUni(ptr) ?? string.Empty);
        }

        private static string SerializeSnapshot(ProxySettingsSnapshot snapshot)
        {
            return string.Join("|", new[]
            {
                snapshot.FlagsUi.ToString(CultureInfo.InvariantCulture),
                snapshot.AutoDiscoveryFlags.ToString(CultureInfo.InvariantCulture),
                ToBase64(snapshot.ProxyServer),
                ToBase64(snapshot.ProxyBypass),
                ToBase64(snapshot.AutoConfigUrl)
            });
        }

        private static ProxySettingsSnapshot DeserializeSnapshot(string serialized)
        {
            if (string.IsNullOrWhiteSpace(serialized))
            {
                return new ProxySettingsSnapshot
                {
                    FlagsUi = PROXY_TYPE_AUTO_DETECT | PROXY_TYPE_DIRECT,
                    AutoDiscoveryFlags = 0,
                    ProxyServer = null,
                    ProxyBypass = null,
                    AutoConfigUrl = null
                };
            }

            string[] parts = serialized.Split('|');
            if (parts.Length != 5)
            {
                throw new FormatException("Serialized proxy settings are invalid.");
            }

            return new ProxySettingsSnapshot
            {
                FlagsUi = int.Parse(parts[0], CultureInfo.InvariantCulture),
                AutoDiscoveryFlags = int.Parse(parts[1], CultureInfo.InvariantCulture),
                ProxyServer = FromBase64(parts[2]),
                ProxyBypass = FromBase64(parts[3]),
                AutoConfigUrl = FromBase64(parts[4])
            };
        }

        private static string ToBase64(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string FromBase64(string value)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty));
        }
    }
}