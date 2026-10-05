using Clearinet;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Clearinet
{
    /// <summary>
    /// This class holds helper methods related to Win32 UI features that are not exposed in .NET.
    /// </summary>
    public class Win32UI
    {
        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_RESTORE = 9;
        public static bool BringTitledWindowToForeground(string windowTitle)
        {
            IntPtr hWnd = FindWindow(null, windowTitle);
            if (hWnd == IntPtr.Zero) return false;
            if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);
            return SetForegroundWindow(hWnd);
        }


        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public static void UseDarkTitleBar(IntPtr h)
        {
            if (Environment.OSVersion.Version.Major >= 10)
            {
                int useDarkMode = 1; // 1 = Dark Mode, 0 = Light Mode
                                     // DWMWA_USE_IMMERSIVE_DARK_MODE attribute (20 for Windows 11 / 10 20H1+, 19 for older Win10 builds)
                int attribute = 20;
                DwmSetWindowAttribute(h, attribute, ref useDarkMode, sizeof(int));
            }
        }

        #region CueText
        // CueText is the "ghost text" that appears in a textbox or combobox when it is empty.
        // This is a Win32 feature that is not exposed in .NET, so we have to call SendMessage() to set it.
        private const int CB_SETCUEBANNER = 0x1703;
        private const int EM_SETCUEBANNER = 0x1501;
        public static void SetCueText(Control oCtl, string sCueText)
        {
             SendMessage(oCtl.Handle, (oCtl is ComboBox) ? CB_SETCUEBANNER : EM_SETCUEBANNER, IntPtr.Zero, sCueText);
        }
        #endregion

        // Note: This method doesn't use any PInvoke APIs, but this class is a handy place to put it.
        internal static void activateTitledTab(string title, TabControl tabSet)
        {
            for (int i = 0; i < tabSet.TabPages.Count; i++)
            {
                if (tabSet.TabPages[i].Text.OICEquals(title))
                {
                    tabSet.SelectedTab = tabSet.TabPages[i];
                    return;
                }
            }
        }
    }

    internal static class Win32ListViewAPI
    {
        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        internal static extern IntPtr SendMessage(IntPtr hWnd, UInt32 msg, IntPtr wParam, IntPtr lParam);

        internal const Int32 LVN_FIRST = -100;
        internal const Int32 LVN_GETEMPTYMARKUP = LVN_FIRST - 87;

        const int LVS_EX_BORDERSELECT = 0x8000;
        const int LVM_SETEXTENDEDLISTVIEWSTYLE = 0x1036;

        [StructLayout(LayoutKind.Sequential)]
        public struct NMHDR
        {
            public IntPtr hwndFrom;
            public IntPtr idFrom;
            public Int32 code;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NMLVEMPTYMARKUP
        {
            public NMHDR hdr;
            public UInt32 dwFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2084)]
            public String szMarkup;
        }

        /// <summary>
        /// Prevent ugly overlay on image.
        /// </summary>
        internal static void DontOverlayImage(ListView lvTarget)
        {
            SendMessage(lvTarget.Handle, LVM_SETEXTENDEDLISTVIEWSTYLE, (IntPtr)LVS_EX_BORDERSELECT, (IntPtr)LVS_EX_BORDERSELECT);
        }
    }

    public class BetterTreeView : TreeView
    {
        const int TVS_EX_DOUBLEBUFFER = 0x0004;
        const int TVS_NOTOOLTIPS = 0x80;
        const int TVM_SETEXTENDEDSTYLE = 0x112c;

        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        private static extern IntPtr SendMessage(IntPtr hWnd, UInt32 msg, IntPtr wParam, IntPtr lParam);

        private void EnableDoubleBuffer()
        {
            SendMessage(Handle, TVM_SETEXTENDEDSTYLE, (IntPtr)TVS_EX_DOUBLEBUFFER, (IntPtr)TVS_EX_DOUBLEBUFFER);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            EnableDoubleBuffer();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cpBase = base.CreateParams;
                cpBase.Style |= TVS_NOTOOLTIPS;
                return cpBase;
            }
        }
    }

    public class RichTextBoxV5 : RichTextBox
    {

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr LoadLibrary(string lpFileName);

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cparams = base.CreateParams;
                if (LoadLibrary("msftedit.dll") != IntPtr.Zero)
                {
                    cparams.ClassName = "RICHEDIT50W";
                }
                return cparams;
            }
        }

        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        private const int WM_VSCROLL = 0x0115;
        public void ScrollUp()
        {
            SendMessage(Handle, WM_VSCROLL, 0, 0);
        }

        public void ScrollDown()
        {
            SendMessage(Handle, WM_VSCROLL, 1, 0);
        }


        [DllImport("user32.dll")]
        private static extern bool LockWindowUpdate(IntPtr hWndLock);
        public void Search(string sFind, bool bMarkAll)
        {
            if (TextLength < 1) return;

            RichTextBoxFinds rtbFindOption = RichTextBoxFinds.None;
            if (sFind.OICStartsWith("exact:"))
            {
                sFind = sFind.Substring(6);
                rtbFindOption = RichTextBoxFinds.MatchCase;
            }

            if (!bMarkAll)
            {
                // Easy mode: Find the next match
                Find(sFind, 
                    Math.Min(SelectionStart + 1, TextLength),  // Start offset
                    RichTextBoxFinds.None);
            }

            // Hard mode: Highlight all matches
            try
            {
                LockWindowUpdate(Handle);

                // Clear old matches
                SelectAll();
                SelectionBackColor = BackColor;
                SelectionStart = SelectionLength = 0;

                if (!sFind.HasText()) return;

                int ixLastFound = Find(sFind, 0, rtbFindOption);

                int ixFirstSeen = ixLastFound;
                while (ixLastFound > -1)
                {
                    ixLastFound++;
                    SelectionBackColor = Color.Yellow;  // TODO: Control via Preferences

                    if (ixLastFound >= TextLength) break;

                    ixLastFound = Find(sFind, ixLastFound, rtbFindOption);
                }

                // If we found any, go to the first one.
                if (ixFirstSeen >= 0)
                {
                    SelectionLength = 0;
                    SelectionStart = ixFirstSeen;
                }
            }
            finally
            {
                LockWindowUpdate(IntPtr.Zero);
            }
        }
    }

    public class BetterListView : System.Windows.Forms.ListView
    {
        /// <summary>
        /// Text to show when there are not items in the ListView
        /// </summary>
        public string EmptyText { get; set; }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x204E: // WM_NOTIFY | WM_REFLECT
                    Win32ListViewAPI.NMHDR nmhdr = (Win32ListViewAPI.NMHDR)m.GetLParam(typeof(Win32ListViewAPI.NMHDR));
                    if (Win32ListViewAPI.LVN_GETEMPTYMARKUP != nmhdr.code) break;
                    var em = (Win32ListViewAPI.NMLVEMPTYMARKUP)m.GetLParam(typeof(Win32ListViewAPI.NMLVEMPTYMARKUP));
                    em.szMarkup = EmptyText;
                    em.dwFlags = 1; // Center the text
                    Marshal.StructureToPtr(em, m.LParam, true);
                    m.Result = (IntPtr)1;
                    return;
            }
            base.WndProc(ref m);
        }

        public BetterListView()
        {
            if (!this.DesignMode)
            {
                this.DoubleBuffered = true;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Win32ListViewAPI.DontOverlayImage(this);
        }

    }
}
