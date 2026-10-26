using System;
using System.Text;
using System.Windows.Forms;

namespace Clearinet
{
    public partial class frmPrompt : Form
    {
        private bool showPasswordChecksum;

        public enum PromptKind
        {
            Default,
            Password,
            Numbers
        }
        public class PromptOptions
        {
            public string Title { get; set; }
            public string PromptText { get; set; }
            /// <summary>
            /// Faded grey text to show when the input is empty and unfocused
            /// </summary>
            public string CueText { get; set; }
            public string DefaultValue { get; set; }
            public bool ReturnNullOnCancel { get; set; }
            public uint MaxLength { get; set; } = 0;
            public PromptKind Kind { get; set; } = PromptKind.Default;
            public IWin32Window OwnerWindow { get; set; } = CApp.UI;
        }
        /// <summary>
        /// LEGACY API: Get a string value from the user.
        /// </summary>
        public static string GetUserString(string sTitle, string sPromptText, string sDefault, bool bReturnNullOnCancel = false,
                            PromptKind pk = PromptKind.Default)
        {
            return GetUserString(new PromptOptions()
            {
                Title = sTitle,
                PromptText = sPromptText,
                DefaultValue = sDefault,
                ReturnNullOnCancel = bReturnNullOnCancel,
                Kind = pk
            });

        }

        public static string GetUserString(PromptOptions options)
        {
            DialogResult drResult;
            string sResult = options.DefaultValue ?? string.Empty;
            using (frmPrompt frm = new frmPrompt())
            {
                frm.StartPosition = FormStartPosition.CenterScreen;
                frm.Text = options.Title ?? "Clearinet Asks...";
                frm.rePrompt.Text = options.PromptText ?? "Developer forgot what to ask";
                if (options.DefaultValue.HasText()) frm.txtResponse.Text = options.DefaultValue;
                if (options.CueText.HasText()) Win32UI.SetCueText(frm.txtResponse, options.CueText);
                if (options.MaxLength > 0) frm.txtResponse.MaxLength = (int)options.MaxLength;
                frm.showPasswordChecksum = options.Kind == PromptKind.Password;
                frm.lblChecksum.Visible = frm.showPasswordChecksum;
                frm.UpdatePasswordChecksum();
                try
                {
                    switch (options.Kind)
                    {
                        case PromptKind.Password:
                            frm.txtResponse.UseSystemPasswordChar = !(CApp.Prefs.GetBoolPref("app.ui.do_not_mask_passwords", false));
                            //  frm.pbPromptIcon.Image = Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("frmPrompt.password.png"));
                            break;
                        case PromptKind.Numbers:
                            //  frm.pbPromptIcon.Image = Image.FromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("frmPrompt.numbers.png"));
                            break;
                    }
                }
                catch (Exception eX) { CApp.ReportException(eX, "Oops"); }
                drResult = frm.ShowDialog(options.OwnerWindow);

                if (DialogResult.OK == drResult)
                {
                    sResult = frm.txtResponse.Text;
                }
            }

            if ((DialogResult.OK != drResult) && options.ReturnNullOnCancel) return null;
            return sResult;
        }
        internal frmPrompt()
        {
            InitializeComponent();
        }

        private void txtResponse_TextChanged(object sender, EventArgs e)
        {
            UpdatePasswordChecksum();
        }

        private void UpdatePasswordChecksum()
        {
            if (!showPasswordChecksum) return;

            string text = txtResponse.Text;
            int sum1 = 0;
            int sum2 = 0;
            foreach (byte value in Encoding.UTF8.GetBytes(text))
            {
                sum1 = (sum1 + value) % 255;
                sum2 = (sum2 + sum1) % 255;
            }
            lblChecksum.Text = $"({text.Length}:{(sum2 << 8) | sum1:x4})";
        }
    }
}
