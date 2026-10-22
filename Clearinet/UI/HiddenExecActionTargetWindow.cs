using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Clearinet
{
    /// <summary>
    /// Hidden top-level window discoverable via FindWindow(null, "Clearinet.ExecActionTarget").
    /// Send WM_COPYDATA with dwData 61178 and cbData bytes of UTF-8 in lpData.
    /// A trailing NUL is optional. SendMessage returns the RunExecAction handled result.
    /// 
    /// The `CallExecAction.ps1` script bundled in the installer exercises this code.
    /// </summary>
    internal sealed class HiddenExecActionTargetWindow : Component
    {
        private readonly CommandWindow window;

        internal HiddenExecActionTargetWindow(IContainer container)
        {
            window = new CommandWindow();
            container.Add(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) window.DestroyHandle();
            base.Dispose(disposing);
        }

        private sealed class CommandWindow : NativeWindow
        {
            private const int WM_COPYDATA = 0x004A;
            private static readonly UTF8Encoding CommandEncoding = new UTF8Encoding(false, true);

            [StructLayout(LayoutKind.Sequential)]
            private struct COPYDATASTRUCT
            {
                public UIntPtr dwData;
                public uint cbData;
                public IntPtr lpData;
            }

            internal CommandWindow()
            {
                CreateHandle(new CreateParams { Caption = "Clearinet.ExecActionTarget" });
            }

            // TODO: This should probably have an event handler `OnMessageReceived` that 
            // the UI subscribes to.
            protected override void WndProc(ref Message m)
            {
                if (m.Msg != WM_COPYDATA)
                {
                    base.WndProc(ref m);
                    return;
                }

                m.Result = IntPtr.Zero;
                if (CApp.isClosing) return;
                if (m.LParam == IntPtr.Zero)
                {
                    CApp.Log.Log("Rejected WM_COPYDATA command: missing COPYDATASTRUCT.");
                    return;
                }

                COPYDATASTRUCT data = Marshal.PtrToStructure<COPYDATASTRUCT>(m.LParam);

                // "Magic" token value, chosen by random dice roll ;)
                if (data.dwData != new UIntPtr(61177) && data.dwData != new UIntPtr(61178)) return;

                if (data.cbData == 0 || data.cbData > int.MaxValue || data.lpData == IntPtr.Zero)
                {
                    CApp.Log.Log("Rejected WM_COPYDATA command: invalid payload length or pointer.");
                    return;
                }

                // COPYDATA memory is valid only until this WndProc returns.
                byte[] bytes = new byte[(int)data.cbData];
                Marshal.Copy(data.lpData, bytes, 0, bytes.Length);
                int length = bytes.Length;
                if (bytes[length - 1] == 0) --length;

                string command;
                try
                {
                    command = CommandEncoding.GetString(bytes, 0, length);
                }
                catch (DecoderFallbackException)
                {
                    CApp.Log.Log("Rejected WM_COPYDATA command: invalid UTF-8.");
                    return;
                }

                if (!command.HasText()) return; // Brought to front by previous instance without args.
                if (command.IndexOf('\0') >= 0)
                {
                    CApp.Log.Log("Rejected WM_COPYDATA command: embedded NUL.");
                    return;
                }

                if (data.dwData == new UIntPtr(61178))
                {
                    m.Result = CApp.RunExecAction(command) ? new IntPtr(1) : IntPtr.Zero;
                }
                else
                {
                    var tokens = command.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    CApp.Log.LogFormat($"Received {tokens.Length} arguments from second instance:\n```\n{command}\n```");
                    CApp.UI.LoadAnyFiles(tokens);
                    m.Result = new IntPtr(1);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CopyData
        {
            public UIntPtr dwData;
            public uint cbData;
            public IntPtr lpData;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string title);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr sender,
            ref CopyData data, uint flags, uint timeout, out UIntPtr result);

        internal static bool SendToRunningInstance(string sMessage)
        {
            IntPtr window = FindWindow(null, "Clearinet.ExecActionTarget");
            if (window == IntPtr.Zero) return false;
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(sMessage + "\0");
            IntPtr payload = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, payload, bytes.Length);
                CopyData data = new CopyData
                {
                    dwData = new UIntPtr(61177),
                    cbData = (uint)bytes.Length,
                    lpData = payload
                };
                UIntPtr result;
                // Abort after 15 seconds if the UI is busy; never retry a potentially executed command.
                if (SendMessageTimeout(window, 0x004A, IntPtr.Zero, ref data, 0x0003, 15000, out result) == IntPtr.Zero)
                    return false; // Didn't send
                if (result == UIntPtr.Zero) return false;   // Sent, but not handled
            }
            finally
            {
                Marshal.FreeHGlobal(payload);
            }
            return true;
        }
    }
}
