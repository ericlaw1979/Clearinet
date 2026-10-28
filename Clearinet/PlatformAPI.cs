using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

namespace Clearinet
{
    /// <summary>
    /// This class contains APIs that will likely vary based on the OS platform.
    /// </summary>
    public class PlatformAPI
    {
        private const ushort IMAGE_FILE_MACHINE_ARM64 = 0xAA64;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsWow64Process2(IntPtr hProcess, out ushort processMachine, out ushort nativeMachine);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        // Windows 10 Version 1709 is Build 16299
        private static readonly bool IsIsWow64Process2Supported =
            Environment.OSVersion.Platform == PlatformID.Win32NT &&
            Environment.OSVersion.Version >= new Version(10, 0, 16299);

        public static bool IsRunningInX64EmulationOnArm64()
        {
            if (!Environment.Is64BitProcess || !IsIsWow64Process2Supported)
            {
                return false;
            }

            if (IsWow64Process2(GetCurrentProcess(), out ushort processMachine, out ushort nativeMachine))
            {
                return nativeMachine == IMAGE_FILE_MACHINE_ARM64;
            }

            return false;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetPhysicalCursorPos(out Point point);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPhysicalPoint(Point point);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern Microsoft.Win32.SafeHandles.SafeProcessHandle OpenProcess(uint access, bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(Microsoft.Win32.SafeHandles.SafeProcessHandle process, uint flags, StringBuilder filename, ref uint size);

        public static string GetProcessInfoAtPhysicalPoint(Point point)
        {
            IntPtr window = WindowFromPhysicalPoint(point);
            if (window == IntPtr.Zero) return "No window at this pixel";
            if (GetWindowThreadProcessId(window, out uint processId) == 0)
            {
                return "Process unavailable: " + new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
            }

            const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
            using (var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId))
            {
                if (process.IsInvalid)
                {
                    return $"Unknown:{processId} - {new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message}";
                }

                StringBuilder filename = new StringBuilder(32768);
                uint size = (uint)filename.Capacity;
                if (!QueryFullProcessImageName(process, 0, filename, ref size))
                {
                    return $"Unknown:{processId} - {new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message}";
                }
                return $"{System.IO.Path.GetFileName(filename.ToString())}:{processId}";
            }
        }

        [DllImport("Kernel32.dll", EntryPoint = "GetTickCount64", CharSet = CharSet.Unicode)]
        public static extern UInt64 GetTickCount64();

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimumWorkingSetSize, IntPtr maximumWorkingSetSize);

        internal static void FreeUpMemory()
        {
            // 1. Force LOH compaction on the next full GC
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

            // 2. Trigger a full blocking GC for all generations (Gen 0, 1, and 2)
            GC.Collect(
                generation: GC.MaxGeneration,
                mode: GCCollectionMode.Forced,
                blocking: true,
                compacting: true);

            // 3. Wait for finalizers to run
            GC.WaitForPendingFinalizers();

            // 4. Collect again to clean up objects that were just finalized
            GC.Collect(
                generation: GC.MaxGeneration,
                mode: GCCollectionMode.Forced,
                blocking: true,
                compacting: true);

            SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
        }

        // TODO: make this robust, with a retry.
        public static bool WriteToClipboard(string sText)
        {
            try
            {
                Clipboard.SetText(sText);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set clipboard text: {ex.Message}");
                return false;
            }
            return true;
        }

        public static bool RunningElevated()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

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
