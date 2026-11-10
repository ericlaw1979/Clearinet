using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace Clearinet
{
    public partial class frmSplashScreen : Form
    {
        private static frmSplashScreen _Instance;

        internal frmSplashScreen()
        {
            InitializeComponent();
            // TODO: Need to do something for DarkMode, as the white background is a /lot/.
            Region = Region.FromHrgn(PlatformAPI.CreateRoundRectRgn(0, 0, Width, Height, 25, 25));

        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }

        /// <summary>
        /// Create the Splash Screen on its own message loop so that
        /// activity on the UI thread of the main form doesn't prevent
        /// the splash screen from updating and repainting.
        /// </summary>
        internal static void CreateAndShow()
        {
            if (_Instance != null) return;

            // Create a new thread dedicated to running the second form
            Thread threadSplashUI = new Thread(() =>
            {
                // Create the form instance on this specific thread
                _Instance = new frmSplashScreen();
                if (!CONFIG.isQuietMode)
                {
                    //_Instance.Show();
                    Label lblVer = new Label()
                    {
                        Parent = _Instance.pbSplash,
                        AutoSize = true,
                        BackColor = System.Drawing.Color.White,
                        ForeColor = System.Drawing.Color.DarkSlateGray,
                        Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point),
                        Text = $"v{Application.ProductVersion}",
                    };
                    _Instance.Text = "Starting Clearinet...";
                    _Instance.Controls.Add(lblVer);
                    lblVer.Location = new System.Drawing.Point(_Instance.Width - lblVer.Width - 5, 5);
                    lblVer.BringToFront();
                }

                // Start a completely independent message loop for this form
                Application.Run(_Instance);
            });

            // CRITICAL: WinForms UI threads must be STA
            threadSplashUI.SetApartmentState(ApartmentState.STA);
            // Start the thread
            threadSplashUI.Start();
        }

        internal static void CloseSplashScreen()
        {
            if (null == _Instance) return;
            if (_Instance.InvokeRequired)
            {
                _Instance.Invoke(new Action(CloseSplashScreen));
                return;
            }
            _Instance.Close();
            _Instance = null;
        }

        internal static void SetStatusText(string sStatus)
        {
            if (null == _Instance) { Debug.Assert(false); return; }
            if (_Instance.InvokeRequired)
            {
                _Instance.Invoke(new Action<string>(SetStatusText), sStatus);
                return;
            }

            _Instance.lblStatus.Text = sStatus;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;
        private void pbSplash_MouseDown(object sender, MouseEventArgs e)
        {
            // Dragging anywhere on the SplashScreen picturebox will move the form.
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
            }
        }
    }
}
