using System;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

namespace Clearinet
{
    public partial class frmFind : Form
    {
        public static void BeginFinding(string sFindText = "")
        {
            using (frmFind f = new frmFind())
            {
                if (sFindText.HasText()) f.txtToFind.Text = sFindText;
                f.ShowDialog(CApp.UI);
            }
        }
        public frmFind()
        {
            InitializeComponent();
            txtToFind.Text = CApp.Prefs.GetStringPref("app.find.lastfind.text.ephemeral", string.Empty);
            Win32UI.SetCueText(txtToFind, "Plaintext to find, or REGEX:");
            cbxFindIn.SelectedIndex = CApp.Prefs.GetInt32Pref("app.find.lastfind.findin", 0);
            cbxWhichComponent.SelectedIndex = CApp.Prefs.GetInt32Pref("app.find.lastfind.whichcomponent", 0);
        }

        private void btnFind_Click(object sender, EventArgs e)
        {
            CApp.Prefs.SetStringPref("app.find.lastfind.text.ephemeral", txtToFind.Text);
            CApp.Prefs.SetInt32Pref("app.find.lastfind.findin", cbxFindIn.SelectedIndex);
            CApp.Prefs.SetInt32Pref("app.find.lastfind.whichcomponent", cbxWhichComponent.SelectedIndex);

            foreach (Exchange exch in CApp.UI.GetAllExchanges())
            {
                // Sloppy hack 
                /*
                cbxWhichComponent.SelectedIndex:
                    0 - Headers & bodies
                    1 - Headers only
                    2 - Bodies only
                */
                StringBuilder sb = new StringBuilder();
                if (cbxFindIn.SelectedIndex == 0)
                {  // Request and Response
                    sb.Append(exch.fullUrl);
                    if (cbxWhichComponent.SelectedIndex != 1) sb.Append(exch.GetRequestBodyAsString()).Append(exch.GetResponseBodyAsString());
                    if (cbxWhichComponent.SelectedIndex != 3) sb.Append(exch.RequestHeaders?.ToString()).Append(exch.ResponseHeaders?.ToString());
                }
                else if (cbxFindIn.SelectedIndex == 1)
                {  // Requests only 
                    sb.Append(exch.fullUrl);
                    if (cbxWhichComponent.SelectedIndex != 1) sb.Append(exch.GetRequestBodyAsString());
                    if (cbxWhichComponent.SelectedIndex != 3) sb.Append(exch.RequestHeaders?.ToString());
                }

                else if (cbxFindIn.SelectedIndex == 2)
                {    // responses only
                    if (cbxWhichComponent.SelectedIndex != 1) sb.Append(exch.GetResponseBodyAsString());
                    if (cbxWhichComponent.SelectedIndex != 3) sb.Append(exch.ResponseHeaders?.ToString());
                }

                else if (cbxFindIn.SelectedIndex == 3)
                {    // urls only
                    sb.Append(exch.fullUrl);
                }

                else { Debug.Assert(false); continue; }

                if (sb.ToString().OICContains(txtToFind.Text))
                {
                    exch.ViewItem.BackColor = System.Drawing.Color.Yellow;
                    exch.ViewItem.Selected = true;
                }
                else
                {
                    exch.ViewItem.BackColor = CApp.UI.lvExchanges.BackColor;
                    exch.ViewItem.Selected = false;
                }
            }
        }

        // Use ProcessDialogKey to handle Escape without the keystroke "leaking" and causing beeps.
        protected override bool ProcessDialogKey(Keys keyData)
        {
            // Indicates the key was consumed and prevents further processing
            if (ModifierKeys == Keys.None && keyData == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }
    }
}
