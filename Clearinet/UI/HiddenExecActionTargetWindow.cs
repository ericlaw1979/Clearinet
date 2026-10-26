using System;
using System.Collections.Generic;
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
            // Issue: We could still create the Window but do so with a
            // different title and optionally hotkey. This would allow
            // code to find these windows easily, which might be useful.
            if (CONFIG.isViewerMode) throw new
                    InvalidOperationException("HiddenExecActionTargetWindow is not supported in Viewer mode.");
            window = new CommandWindow();
            container.Add(this);
        }

        /// <summary>
        /// Register on the UI thread; callbacks run on that thread.
        /// </summary>
        internal bool RegisterHotKey(string keyCombo, Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (window.Handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(HiddenExecActionTargetWindow));

            uint modifiers = 0;
            Keys key = Keys.None;
            foreach (string part in (keyCombo ?? string.Empty).Split('+'))
            {
                string token = part.Trim();
                uint modifier;
                switch (token.ToUpperInvariant())
                {
                    case "CTRL":
                    case "CONTROL": modifier = 0x0002; break;
                    case "ALT": modifier = 0x0001; break;
                    case "SHIFT": modifier = 0x0004; break;
                    case "WINDOWS":
                    case "WIN": modifier = 0x0008; break;
                    default:
                        if (key != Keys.None || token.Length == 0 || char.IsDigit(token[0]) || token.Contains(",") ||
                            !Enum.TryParse(token, true, out key) || !Enum.IsDefined(typeof(Keys), key) ||
                            key <= Keys.None || (int)key > 0xFF ||
                            key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu ||
                            key == Keys.LWin || key == Keys.RWin)
                        {
                            CApp.Log.Log($"Invalid global hotkey '{keyCombo}'. Use a key name with Ctrl, Alt, Shift, or Win modifiers (e.g. Ctrl+Alt+F).");
                            return false;
                        }
                        continue;
                }
                if ((modifiers & modifier) != 0)
                {
                    CApp.Log.Log($"Invalid global hotkey '{keyCombo}': repeated modifier '{token}'.");
                    return false;
                }
                modifiers |= modifier;
            }
            if (key == Keys.None)
            {
                CApp.Log.Log($"Invalid global hotkey '{keyCombo}': a non-modifier key is required.");
                return false;
            }
            bool res = window.RegisterHotKey(keyCombo, modifiers, (uint)key, callback);
            CApp.Log.Log($"RegisterHotKey '{keyCombo}' failed. Key already in use?.");

            return res;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) window.Close();
            base.Dispose(disposing);
        }

        private sealed class CommandWindow : NativeWindow
        {
            private const int WM_COPYDATA = 0x004A;
            private const int WM_HOTKEY = 0x0312;
            private const uint MOD_NOREPEAT = 0x4000;
            private readonly Dictionary<int, Action> hotkeyCallbacks = new Dictionary<int, Action>();
            private int nextHotkeyId = 1;
            private static readonly UTF8Encoding CommandEncoding = new UTF8Encoding(false, true);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

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

            internal bool RegisterHotKey(string keyCombo, uint modifiers, uint key, Action callback)
            {
                if (nextHotkeyId > 0xBFFF) throw new InvalidOperationException("No global hotkey IDs remain.");
                int id = nextHotkeyId;
                if (!RegisterHotKey(Handle, id, modifiers | MOD_NOREPEAT, key))
                {
                    int error = Marshal.GetLastWin32Error();  // 1409 (ERROR_HOTKEY_ALREADY_REGISTERED
                    CApp.Log.Log($"Unable to register global hotkey '{keyCombo}': {new Win32Exception(error).Message} (error {error}).");
                    return false;
                }
                hotkeyCallbacks.Add(id, callback);
                ++nextHotkeyId;
                return true;
            }

            internal void Close()
            {
                if (Handle == IntPtr.Zero) return;
                foreach (int id in hotkeyCallbacks.Keys)
                {
                    if (!UnregisterHotKey(Handle, id))
                    {
                        int error = Marshal.GetLastWin32Error();
                        CApp.Log.Log($"Unable to unregister global hotkey {id}: {new Win32Exception(error).Message} (error {error}).");
                    }
                }
                hotkeyCallbacks.Clear();
                DestroyHandle();
            }

            // TODO: This should probably have an event handler `OnMessageReceived` that 
            // the UI subscribes to.
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY && hotkeyCallbacks.TryGetValue(m.WParam.ToInt32(), out Action callback))
                {
                    if (!CApp.isClosing) callback();
                    m.Result = IntPtr.Zero;
                    return;
                }
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
