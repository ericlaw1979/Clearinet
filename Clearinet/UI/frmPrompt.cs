using System;
using System.Windows.Forms;

namespace Clearinet
{
    public partial class frmPrompt : Form
    {
        public enum PromptIcon
        {
            Default,
            Password,
            Numbers
        }
        /// <summary>
        /// LEGACY API: Get a string value from the user.
        /// </summary>
        public static string GetUserString(string sTitle, string sPromptText, string sDefault)
        {
            return GetUserString(sTitle, sPromptText, sDefault, false, PromptIcon.Default);
        }

        /// <summary>
        /// LEGACY API: Get a string value from the user.
        /// </summary>
        public static string GetUserString(string sTitle, string sPrompt, string sDefault, bool bReturnNullOnCancel)
        {
            return GetUserString(CApp.UI, sTitle, sPrompt, sDefault, bReturnNullOnCancel, PromptIcon.Default);
        }

        public static string GetUserString(string sTitle, string sPrompt, string sDefault, bool bReturnNullOnCancel, PromptIcon piIcon)
        {
            return GetUserString(CApp.UI, sTitle, sPrompt, sDefault, bReturnNullOnCancel, piIcon);
        }

        public static string GetUserString(IWin32Window wndOwner, string sTitle, string sPrompt, string sDefault, bool bReturnNullOnCancel, PromptIcon piIcon)
        {
            DialogResult drResult;
            string sResult = sDefault;
            using (frmPrompt frm = new frmPrompt())
            {
                frm.StartPosition = FormStartPosition.CenterScreen;
                frm.Text = sTitle;
                frm.rePrompt.Text = sPrompt;
                frm.txtResponse.Text = sResult;
                // TODO: Set CUE Text

                try
                {
                    switch (piIcon)
                    {
                        case PromptIcon.Password:
                            //                        frm.pbPromptIcon.Image = Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("frmPrompt.password.png"));
                            break;
                        case PromptIcon.Numbers:
                            //                        frm.pbPromptIcon.Image = Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("frmPrompt.numbers.png"));
                            break;
                    }
                }
                catch (Exception eX) { CApp.ReportException(eX, "Oops"); }
                drResult = frm.ShowDialog(wndOwner);

                if (DialogResult.OK == drResult)
                {
                    sResult = frm.txtResponse.Text;
                }
            }

            if ((DialogResult.OK != drResult) && bReturnNullOnCancel) return null;
            return sResult;
        }
        internal frmPrompt()
        {
            InitializeComponent();
        }
    }
}
