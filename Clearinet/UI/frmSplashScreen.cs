using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Clearinet
{
    public partial class frmSplashScreen : Form
    {
        private static frmSplashScreen _Instance;

        internal frmSplashScreen()
        {
            InitializeComponent();
        }
        
        internal static void CreateAndShow()
        {
            // TODO: Should this be on its own thread to avoid
            // needing the main form to periodically pump the message loop?
            if (_Instance == null)
            {
                _Instance = new frmSplashScreen();
                if (!CONFIG.isQuietMode) _Instance.Show();
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
