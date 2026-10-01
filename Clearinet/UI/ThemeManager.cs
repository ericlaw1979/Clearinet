using Clearinet;
using System.Drawing;
using System.Windows.Forms;

public static class ThemeManager
{
    public static readonly Color DarkBackground = Color.FromArgb(32, 32, 32);
    public static readonly Color DarkSurface = Color.FromArgb(45, 45, 48);
    public static readonly Color DarkText = Color.FromArgb(240, 240, 240);

    public static void ApplyDarkMode(Control control)
    {
        Win32UI.UseDarkTitleBar(control.Handle);
        control.BackColor = DarkBackground;
        control.ForeColor = DarkText;

        foreach (Control c in control.Controls)
        {
            ApplyDarkModeToControl(c);
        }
    }

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

        // Recursively theme child controls inside panels, groupboxes, etc.
        foreach (Control child in c.Controls)
        {
            ApplyDarkModeToControl(child);
        }
    }
}