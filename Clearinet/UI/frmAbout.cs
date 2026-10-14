using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Windows.Forms;

namespace Clearinet
{
    public partial class frmAbout : Form
    {
        private static Keys[] kc = new Keys[]
        {
            Keys.Up, Keys.Up, Keys.Down, Keys.Down,
            Keys.Left, Keys.Right, Keys.Left, Keys.Right,
            Keys.B, Keys.A
        };
        int kcix = 0;

        public frmAbout()
        {
            InitializeComponent();
            PlatformAPI.FreeUpMemory();
            txtAbout.Text = $"{GetAppEnvironmentInfo()}\r\n\r\nCopyright ©2026 Clearinet Contributors";
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ActiveControl = null; // Prevent focus on the textbox.
        }

        // Close on ESC or Spacebar
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((keyData == Keys.Escape) || (keyData == Keys.Space))
            {
                Close();
                return true;
            }
            if (keyData == kc[kcix])
            {
                kcix++;
                if (kcix >= kc.Length)
                {
                    txtAbout.Text = Encoding.UTF8.GetString(Convert.FromBase64String("8J+OifCfjonwn46J8J+OiQ0KVGhhbmsgeW91IGZvciB1c2luZyBDbGVhcmluZXQhDQrwn46J8J+OifCfjonwn46J"));
                }
            }
            else
            {
                kcix = 0; // Reset if the sequence is broken
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public static string GetAppEnvironmentInfo()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Version appVersion = assembly.GetName().Version;
            string targetFramework = "Unknown";

            var targetAttr = assembly.GetCustomAttribute<TargetFrameworkAttribute>();
            if (targetAttr != null)
            {
                targetFramework = targetAttr.FrameworkDisplayName ?? targetAttr.FrameworkName;
            }

            DateTime updatedDate = File.GetLastWriteTime(assembly.Location);
            DateTime builtDate = assembly.GetCustomAttribute<AssemblyBuiltDateAttribute>()?.BuiltDate ?? new DateTime(1885, 9, 2);

            string bitness = Environment.Is64BitProcess ? "64-bit" : "32-bit";
            string architecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "x86";

            double privateBytesMB = 0; // Total Exclusive Memory allocated
            double workingSetMB = 0;   // Total Physical RAM (includes shared DLLs)

            using (Process currentProcess = Process.GetCurrentProcess())
            {
                // Private Bytes: Total memory reserved exclusively for this process
                privateBytesMB = currentProcess.PrivateMemorySize64 / (1024.0 * 1024.0);
                // Working Set: Total physical RAM currently paged in
                workingSetMB = currentProcess.WorkingSet64 / (1024.0 * 1024.0);
            }
            string runtimeFramework = RuntimeInformation.FrameworkDescription;
            Version osVersion = Environment.OSVersion.Version;
            string osName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "WinNT" : Environment.OSVersion.Platform.ToString();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{CApp.VersionString} for {targetFramework}");
            sb.AppendLine($"Built:\t{builtDate:yyyy-MMM-d}");
            sb.AppendLine($"Updated:\t{updatedDate:yyyy-MMM-d}");
            sb.AppendLine(string.Empty);
            sb.AppendLine($"CPU: {bitness} {architecture}");
            sb.AppendLine($"WorkingSet: {workingSetMB:F1}mb; Private bytes: {privateBytesMB:F1}mb");
            sb.AppendLine($"{runtimeFramework} {osName} {osVersion}");
            if (PlatformAPI.RunningElevated()) sb.AppendLine("Running ELEVATED");

            return sb.ToString();
        }

        private void lnkWebsite_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Utilities.LaunchHyperlink("https://clearinet.app/");
        }
    }
}
