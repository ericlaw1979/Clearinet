using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Clearinet
{
    /// <summary>
    /// This class contains APIs that will likely vary based on the OS platform.
    /// </summary>
    public class PlatformAPI
    {
        // Win32 API declaration
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool PathCompactPathExW(
            StringBuilder pszOut,
            string pszSrc,
            uint cchMax,
            uint dwFlags
        );

        public static string CompactPath(string sPath, int iMaxLen)
        {
            if (!sPath.HasText()) return string.Empty;

            // Buffer size includes the null terminator
            var sb = new StringBuilder(iMaxLen + 1);

            if (PathCompactPathExW(sb, sPath, (uint)iMaxLen, 0))
            {
                return sb.ToString();
            }

            return sPath;
        }


        [Flags]
        private enum SoundFlags : uint
        {
            SND_SYNC = 0x0000,
            SND_ASYNC = 0x0001,
            SND_NODEFAULT = 0x0002,
            SND_LOOP = 0x0008,
            SND_NOSTOP = 0x0010,
            SND_NOWAIT = 0x00002000,
            SND_ALIAS = 0x00010000,
            SND_FILENAME = 0x00020000,
        }

        [DllImport("winmm.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PlaySound(
            string pszSound,
            IntPtr hMod,
            SoundFlags sf);
        public static void PlaySoundFile(string sFilename)
        {
            PlaySound(sFilename, IntPtr.Zero,
                     SoundFlags.SND_ASYNC | SoundFlags.SND_NOSTOP | SoundFlags.SND_FILENAME | SoundFlags.SND_NODEFAULT);
        }

        #region SystemClockResolution
        private const int ProcessPowerThrottling = 4;
        private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
        private const uint PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION = 0x4;

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_POWER_THROTTLING_STATE
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessInformation(
            IntPtr hProcess,
            int ProcessInformationClass,
            ref PROCESS_POWER_THROTTLING_STATE ProcessInformation,
            uint ProcessInformationSize);

        /// <summary>
        /// Prevent Windows 11+ from decreasing clock resolution when our application window isn't visible.
        /// </summary>
        private static bool DisableBackgroundTimerThrottling()
        {
            var state = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION,
                // 0 means DISABLE the "ignore timer" feature (i.e., FORCE Windows to respect our timer resolution)
                StateMask = 0
            };

            uint size = (uint)Marshal.SizeOf(state);
            IntPtr hProcess = Process.GetCurrentProcess().Handle;

            bool result = SetProcessInformation(hProcess, ProcessPowerThrottling, ref state, size);
            Debug.Assert(result, $"Failed to disable background timer throttling. Error: {Marshal.GetLastWin32Error()}");
            // On earlier Windows versions prior to ProcessPowerThrottling support, SetProcessInformation
            // will return false with Error 87 (ERROR_INVALID_PARAMETER). That is expected and safe to ignore.
            return result;
        }

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint MM_timeBeginPeriod(uint iMS);

        /// Windows' high resolution clock is bad for battery life but good for accuracy of timestamps.
        /// https://learn.microsoft.com/en-us/sysinternals/downloads/clockres shows current clock resolution.
        /// Call this method to enable the 1ms clock. It will be enabled for the lifetime of the process.
        /// 
        /// For higher precision, use System.Diagnostics.Stopwatch, based on the hardware's High-Resolution
        /// Performance Counter (QueryPerformanceCounter) without modifying global system state.
        internal static bool EnableHighResolutionClock()
        {
            uint iRes = MM_timeBeginPeriod(1);
            if (iRes != 0)
            {
                Debug.Assert(false, "Failed to enable high resolution clock.");
            }
            else
            {
                DisableBackgroundTimerThrottling();
            }
            return iRes == 0;
        }

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtQueryTimerResolution(out uint minimumResolution, out uint maximumResolution,
                                                         out uint currentResolution);

        private static (double MinMs, double MaxMs, double CurrentMs) GetResolutions()
        {
            // 1ms = 10,000 * 100-ns)
            NtQueryTimerResolution(out uint min, out uint max, out uint current);
            return (
                MinMs: min / 10_000.0,
                MaxMs: max / 10_000.0,
                CurrentMs: current / 10_000.0
            );
        }

        internal static string GetClockResolution()
        {
            var (min, max, current) = GetResolutions();
            return $"Current: {current:F3}ms; Min: {min:F3}ms; Max: {max:F3}ms";
        }
        #endregion SystemClockResolution

        // Win32 API Flags
        [Flags]
        private enum ExecutionState : uint
        {
            ES_SYSTEM_REQUIRED = 0x00000001,  // Prevents the system from sleeping
            ES_DISPLAY_REQUIRED = 0x00000002, // Prevents the display from turning off
            ES_CONTINUOUS = 0x80000000        // Informs system that state remains in effect until reset
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

        /// <summary>
        /// Prevents the PC (and optionally the display) from going to sleep.
        /// </summary>
        /// <param name="keepDisplayOn">Set to true to also keep the monitor on.</param>
        internal static void PreventSleep(bool keepDisplayOn = false)
        {
            ExecutionState flags = ExecutionState.ES_CONTINUOUS | ExecutionState.ES_SYSTEM_REQUIRED;
            if (keepDisplayOn)
            {
                flags |= ExecutionState.ES_DISPLAY_REQUIRED;
            }

            SetThreadExecutionState(flags);
        }

        /// <summary>
        /// Restores default power management behaviors, allowing the PC to sleep normally.
        /// </summary>
        internal static void RestoreSleep()
        {
            SetThreadExecutionState(ExecutionState.ES_CONTINUOUS);
        }
    }
}
