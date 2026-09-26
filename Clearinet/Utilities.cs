using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace Clearinet
{
    internal static class Utilities
    {
        static internal void LaunchHyperlink(string sURL)
        {
            try
            {
                Process.Start(sURL);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to launch the requested hyperlink:\n\n{ex.Message}", "Clearinet", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
