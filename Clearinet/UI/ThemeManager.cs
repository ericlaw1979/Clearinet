using Clearinet;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class ThemeManager
{
    public class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder => Color.FromArgb(64, 64, 64);
        public override Color MenuItemBorder => Color.FromArgb(80, 80, 80);
        public override Color MenuItemSelected => Color.FromArgb(70, 70, 70);
        public override Color MenuStripGradientBegin => Color.FromArgb(45, 45, 48);
        public override Color MenuStripGradientEnd => Color.FromArgb(45, 45, 48);

        public override Color ToolStripGradientBegin => Color.FromArgb(45, 45, 48);
        public override Color ToolStripGradientMiddle => Color.FromArgb(45, 45, 48);
        public override Color ToolStripGradientEnd => Color.FromArgb(45, 45, 48);

        public override Color ButtonSelectedGradientBegin => Color.FromArgb(62, 62, 64);
        public override Color ButtonSelectedGradientMiddle => Color.FromArgb(62, 62, 64);
        public override Color ButtonSelectedGradientEnd => Color.FromArgb(62, 62, 64);

        public override Color ImageMarginGradientBegin => Color.FromArgb(45, 45, 48);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(45, 45, 48);
        public override Color ImageMarginGradientEnd => Color.FromArgb(45, 45, 48);
        public override Color ToolStripDropDownBackground => Color.FromArgb(45, 45, 48);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(70, 70, 70);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(70, 70, 70);

    }

    private static void SetDarkModeMenu(MenuStrip menuStrip)
    {
        if (menuStrip == null) return;
        menuStrip.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
        SetDarkModeItems(menuStrip.Items);
        menuStrip.ItemAdded += (sender, e) =>
        {
            ToolStripItem addedItem = e.Item;
            if (addedItem is ToolStripMenuItem menuItem)
            {
                // Example: Apply styling or hook up event handlers recursively
                menuItem.BackColor = Color.FromArgb(45, 45, 48);
            }
        };
    }
    private static void SetDarkModeContextMenu(ContextMenuStrip menuStrip)
    {
        if (menuStrip == null) return;
        menuStrip.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
        //SetDarkModeItems(menuStrip.Items);
        menuStrip.Opening += (sender, e) =>
        {
            SetDarkModeItems(menuStrip.Items);
        };
    }

    private static void SetDarkModeItems(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            item.ForeColor = Color.White;
            item.BackColor = Color.FromArgb(45, 45, 48);

            // If it's a dropdown menu (like File, Edit), recursively color its children
            if (item is ToolStripMenuItem menuItem)
            {
                SetDarkModeItems(menuItem.DropDownItems);
            }
        }
    }

    public static readonly Color DarkBackground = Color.FromArgb(32, 32, 32);
    public static readonly Color DarkSurface = Color.FromArgb(45, 45, 48);
    public static readonly Color DarkText = Color.FromArgb(240, 240, 240);
    public static readonly Color DarkDisabledEdit = Color.FromArgb(39, 72, 97);

    public static void ApplyDarkMode(Form frm)
    {
        Win32UI.UseDarkTitleBar(frm.Handle);
        CONFIG.colorDisabledEdit = DarkDisabledEdit; // TODO: Allow user to override.

        SetDarkModeMenu(CApp.UI.MainMenuStrip);
        SetDarkModeContextMenu(CApp.UI.lvExchanges.ContextMenuStrip);

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

        if (c is LinkLabel linkLabel)
        {
            linkLabel.LinkColor = Color.FromArgb(159, 253, 255);
            linkLabel.ActiveLinkColor = Color.FromArgb(87, 166, 74);
        }

        if (c is ListView lv)
        {
            lv.BackColor = DarkSurface;
            lv.ForeColor = DarkText;
            // //todo: try to set column headers to dark, somehow? TODO: Doesn't seem to work here.
            // if (IsSetWindowThemeSupported) SetWindowTheme(lv.Handle, "Explorer", null);
            IntPtr headerHandle = SendMessage(lv.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);

            if (headerHandle != IntPtr.Zero)
            {
                // TODO
                // 3. Apply the dark theme directly to the header
                //  SetWindowTheme(headerHandle, "Explorer", null); // Or "ItemView" depending on OS build

                SetWindowTheme(headerHandle, "DarkMode_DarkTheme", "");

            }

        }

        // Recursively theme child controls inside panels, groupboxes, etc.
        foreach (Control child in c.Controls)
        {
            ApplyDarkModeToControl(child);
        }
    }
}