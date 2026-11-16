namespace Clearinet
{
    using System;
    using System.Runtime.InteropServices;

    public readonly struct CurrentProcessMetrics
    {
        public readonly int ThreadCount;
        public readonly int HandleCount;
        public readonly long PrivateBytes; // Commit size (PrivateUsage)
        public readonly long WorkingSetBytes; // Total working set

        public CurrentProcessMetrics(int threadCount, int handleCount, long privateBytes, long workingSetBytes)
        {
            ThreadCount = threadCount;
            HandleCount = handleCount;
            PrivateBytes = privateBytes;
            WorkingSetBytes = workingSetBytes;
        }
    }

    public static class SelfProcessMetrics
    {
        // Pseudo-handle for the current process (-1); never needs CloseHandle
        private static readonly IntPtr CurrentProcessPseudoHandle = new IntPtr(-1);
        private const uint THREAD_QUERY_LIMITED_INFORMATION = 0x0800;
        private const int ProcessVmCounters = 3;

        [StructLayout(LayoutKind.Sequential)]
        private struct VM_COUNTERS_EX
        {
            public IntPtr PeakVirtualSize;
            public IntPtr VirtualSize;
            public uint PageFaultCount;
            public IntPtr PeakWorkingSetSize;
            public IntPtr WorkingSetSize;
            public IntPtr QuotaPeakPagedPoolUsage;
            public IntPtr QuotaPagedPoolUsage;
            public IntPtr QuotaPeakNonPagedPoolUsage;
            public IntPtr QuotaNonPagedPoolUsage;
            public IntPtr PagefileUsage;
            public IntPtr PeakPagefileUsage;
            public IntPtr PrivateUsage;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessHandleCount(IntPtr hProcess, out uint pdwHandleCount);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(
            IntPtr ProcessHandle,
            int ProcessInformationClass,
            out VM_COUNTERS_EX ProcessInformation,
            int ProcessInformationLength,
            out int ReturnLength);

        [DllImport("ntdll.dll")]
        private static extern int NtGetNextThread(
            IntPtr ProcessHandle,
            IntPtr ThreadHandle,
            uint DesiredAccess,
            uint HandleAttributes,
            uint Flags,
            out IntPtr NewThreadHandle);

        public static CurrentProcessMetrics GetMetrics()
        {
            // 1. Handle count: single O(1) syscall
            GetProcessHandleCount(CurrentProcessPseudoHandle, out uint handles);

            // 2. Memory counters: single O(1) syscall, stack-allocated struct
            NtQueryInformationProcess(
                CurrentProcessPseudoHandle,
                ProcessVmCounters,
                out VM_COUNTERS_EX vmCounters,
                Marshal.SizeOf(typeof(VM_COUNTERS_EX)),
                out _);

            // 3. Thread count: walks ONLY the current process's thread list
            int threads = CountCurrentProcessThreads();

            return new CurrentProcessMetrics(
                threads,
                (int)handles,
                vmCounters.PrivateUsage.ToInt64(),
                vmCounters.WorkingSetSize.ToInt64());
        }

        private static int CountCurrentProcessThreads()
        {
            int count = 0;
            IntPtr currentThread = IntPtr.Zero;

            while (NtGetNextThread(
                CurrentProcessPseudoHandle,
                currentThread,
                THREAD_QUERY_LIMITED_INFORMATION,
                0,
                0,
                out IntPtr nextThread) == 0) // STATUS_SUCCESS
            {
                if (currentThread != IntPtr.Zero)
                {
                    CloseHandle(currentThread);
                }

                currentThread = nextThread;
                count++;
            }

            if (currentThread != IntPtr.Zero)
            {
                CloseHandle(currentThread);
            }

            return count;
        }
    }
}
