using Clearinet;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class ThemeManager
{
    public static readonly Color DarkBackground = Color.FromArgb(32, 32, 32);
    public static readonly Color DarkSurface = Color.FromArgb(45, 45, 48);
    public static readonly Color DarkText = Color.FromArgb(240, 240, 240);
    public static readonly Color DarkDisabledEdit = Color.FromArgb(39, 72, 97);

    public static void ApplyDarkMode(Form frm)
    {
        Win32UI.UseDarkTitleBar(frm.Handle);
        CONFIG.colorDisabledEdit = DarkDisabledEdit; // TODO: Allow user to override.

        frm.BackColor = DarkBackground;
        frm.ForeColor = DarkText;

        foreach (Control c in frm.Controls)
        {
            ApplyDarkModeToControl(c);
        }
    }

    [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private const int LVM_GETHEADER = 0x101F;
    public static bool IsSetWindowThemeSupported =>
        Environment.OSVersion.Platform == PlatformID.Win32NT &&
        Environment.OSVersion.Version >= new Version(10, 0, 17763);

    private static void ApplyDarkModeToControl(Control c)
    {
        c.BackColor = (c is Button || c is TextBox) ? DarkSurface : DarkBackground;
        c.ForeColor = DarkText;

        // Custom handling for specific controls like DataGridView
        if (c is DataGridView dgv)
        {
            dgv.BackgroundColor = DarkBackground;
            dgv.DefaultCellStyle.BackColor = DarkSurface;
            dgv.DefaultCellStyle.ForeColor = DarkText;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = DarkBackground;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = DarkText;
            dgv.EnableHeadersVisualStyles = false;
        }

        if (c is ListView lv)
        {
            lv.BackColor = DarkSurface;
            lv.ForeColor = DarkText;
            // set column headers to dark, somehow? TODO: Doesn't seem to work here.
            // if (IsSetWindowThemeSupported) SetWindowTheme(lv.Handle, "Explorer", null);
            IntPtr headerHandle = SendMessage(lv.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);

            if (headerHandle != IntPtr.Zero)
            {
                // TODO
                // 3. Apply the dark theme directly to the header
                SetWindowTheme(headerHandle, "Explorer", null); // Or "ItemView" depending on OS build
            }

        }

        // Recursively theme child controls inside panels, groupboxes, etc.
        foreach (Control child in c.Controls)
        {
            ApplyDarkModeToControl(child);
        }
    }
}