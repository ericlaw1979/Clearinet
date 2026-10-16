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
                if (data.dwData != new UIntPtr(61178)) return;
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

                if (string.IsNullOrWhiteSpace(command) || command.IndexOf('\0') >= 0)
                {
                    CApp.Log.Log("Rejected WM_COPYDATA command: empty command or embedded NUL.");
                    return;
                }

                m.Result = CApp.RunExecAction(command) ? new IntPtr(1) : IntPtr.Zero;
            }
        }
    }
}
