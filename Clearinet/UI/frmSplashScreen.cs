using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Clearinet
{
    public partial class frmSplashScreen : Form
    {
        private static frmSplashScreen _Instance;

        internal frmSplashScreen()
        {
            InitializeComponent();
            // TODO: Need to do something for DarkMode.
            Region = Region.FromHrgn(PlatformAPI.CreateRoundRectRgn(0, 0, Width, Height, 20, 20));

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
        internal static void CreateAndShow()
        {
            // TODO: Should this be on its own thread to avoid
            // needing the main form to periodically pump the message loop?
            if (_Instance == null)
            {
                _Instance = new frmSplashScreen();
                if (!CONFIG.isQuietMode)
                {
                    _Instance.Show();
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
            }
        }

        internal static void CloseSplashScreen()
        {
            if (null == _Instance) return;
            _Instance.Close();
            _Instance = null;
        }

        internal static void SetStatusText(string sStatus)
        {
            if (null == _Instance) { Debug.Assert(false); return; }
            _Instance.lblStatus.Text = sStatus;
            Application.DoEvents();

        }
    }
}
