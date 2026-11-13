using Clearinet;
using System;
using System.Drawing;
using System.Windows.Forms;

public static class ThemeManager
{
    public static Color GetContrastingTextColor(Color background)
    {
        double Linearize(byte channel)
        {
            double value = channel / 255.0;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        double luminance = 0.2126 * Linearize(background.R) + 0.7152 * Linearize(background.G) + 0.0722 * Linearize(background.B);
        double blackContrast = (luminance + 0.05) / 0.05;
        double whiteContrast = 1.05 / (luminance + 0.05);
        return blackContrast >= whiteContrast ? Color.Black : Color.White;
    }

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
    internal static void SetDarkModeContextMenu(ContextMenuStrip menuStrip)
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
    public static readonly Color DarkContrastingText = Color.FromArgb(240, 240, 240);
    public static readonly Color DarkDisabledEdit = CONFIG.COLOR_DARK_DEFAULT_DISABLEDEDIT;

    public static void SetAppToDarkMode()
    {
        // This doesn't seem to do a whole lot, but it ~does~ at least set the
        // system menu (hit Alt+Space) to dark mode.
        //
        Win32UI.SetPreferredAppMode(Win32UI.PreferredAppMode.ForceDark);
        Win32UI.FlushMenuThemes();

        // Interestingly, Common File Dialogs seem to inherit the system's dark mode
        // setting, regardless of what we do here. Meanwhile, common controls like ListView, TreeView, etc.
        // do not inherit the system's dark mode setting and don't expose any straightforward way to respect it.
    }

    public static void ApplyDarkMode(Form frm)
    {
        // On latest .NET, we could use Application.SetColorMode(SystemColorMode.Dark);
        // It's not clear that any of these calls actually work. sigh.
        Win32UI.SetDWMWA_USE_IMMERSIVE_DARK_MODE(frm.Handle);

        // This one gets the ListView headers to dark mode, but it also shows the "Column Header divider extensions" (vertical lines).
        // Preventing those from painting seems basically impossible.
        Win32UI.AllowDarkModeForWindowWithParentFallback(frm.Handle, true);
        //     Win32UI.AllowDarkModeForWindow(frm.Handle, true);

        // HACKERY. 
        CONFIG.colorDisabledEdit = DarkDisabledEdit; // TODO: Allow user to override.
        CONFIG.colorEditForeColor = CONFIG.colorDisabledEditForeColor = Color.White;
        CONFIG.colorEdit = DarkBackground;
        CONFIG.COLOR_DEFAULT_EXCHANGE = Color.FromArgb(247, 254, 160); // TODO: Allow user to override.

        // Enable dark mode for a specific HWND (e.g., your Form or Control handle)
        SetDarkModeMenu(CApp.UI.MainMenuStrip);
        SetDarkModeContextMenu(CApp.UI.lvExchanges.ContextMenuStrip);

        frm.BackColor = DarkBackground;
        frm.ForeColor = DarkContrastingText;

        foreach (Control c in frm.Controls)
        {
            ApplyDarkModeToControl(c);
        }
    }

    private static void ApplyDarkModeToControl(Control c)
    {
        c.BackColor = (c is Button || c is TextBox) ? DarkSurface : DarkBackground;
        c.ForeColor = DarkContrastingText;

        Win32UI.AllowDarkModeForWindow(c.Handle, true);

        // Custom handling for specific controls like DataGridView.
        if (c is DataGridView dgv)
        {
            dgv.BackgroundColor = DarkBackground;
            dgv.DefaultCellStyle.BackColor = DarkSurface;
            dgv.DefaultCellStyle.ForeColor = DarkContrastingText;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = DarkBackground;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = DarkContrastingText;
            dgv.EnableHeadersVisualStyles = false;
        }

        if (c is LinkLabel linkLabel)
        {
            linkLabel.LinkColor = Color.FromArgb(159, 253, 255);
            linkLabel.ActiveLinkColor = Color.FromArgb(87, 166, 74);
        }

        if (c is TabControl tabControl)
        {
            // HACKERY: The ComCtrl32 library only renders the tab headers in dark mode
            // if the TabControl's Appearance is set to Normal. If it's set to Buttons,
            // the headers are always light. So we'll just show regular tabs in this case.
            if (tabControl.Appearance == TabAppearance.FlatButtons)
            {
                tabControl.Appearance = TabAppearance.Normal;
            }
        }

        if (c is ListView lv)
        {
            lv.BackColor = DarkSurface;
            lv.ForeColor = DarkContrastingText;

            // Win32UI.SetDWMWA_USE_IMMERSIVE_DARK_MODE(lv.Handle);

            Win32UI.SetListViewHeadersToDarkMode(lv);
        }

        // Recursively theme child controls inside panels, groupboxes, etc.
        foreach (Control child in c.Controls)
        {
            ApplyDarkModeToControl(child);
        }
    }


}