using Clearinet;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Extensions
{
    public partial class frmMezerTools : Form
    {
        private bool isPickingColor;
        private bool isUpdatingNumber;
        private readonly ErrorProvider colorInputErrors;

        public frmMezerTools()
        {
            InitializeComponent();
            if (components == null) components = new Container();
            colorInputErrors = new ErrorProvider(components) { ContainerControl = this, BlinkStyle = ErrorBlinkStyle.NeverBlink };
            txtColorHex.TextChanged += txtColorHex_TextChanged;
            txtHex.TextChanged += NumberTextChanged;
            txtDecimal.TextChanged += NumberTextChanged;
            UpdateClipboardAnalysis();
            Win32UI.SetCueText(txtPattern, "Enter a pattern to repeat...");
            Win32UI.SetCueText(txtColorHex, "#RRGGBB");
            SetPickedColor(Color.Azure);
            //Utilities.LaunchHyperlink("https://mezer.app");
        }
        public static string GenerateRepeatedStringMemory(string srepeat, int length)
        {
            if (length <= 0 || string.IsNullOrEmpty(srepeat))
                return string.Empty;

            char[] buffer = new char[length];
            Span<char> span = buffer.AsSpan();
            ReadOnlySpan<char> source = srepeat.AsSpan();

            int patternLength = source.Length;

            for (int i = 0; i < length; i += patternLength)
            {
                int copyLength = Math.Min(patternLength, length - i);
                source.Slice(0, copyLength).CopyTo(span.Slice(i, copyLength));
            }

            return new string(buffer);
        }

        private void lblCurrentClipboard_MouseMove(object sender, MouseEventArgs e)
        {
            UpdateClipboardAnalysis();
        }

        private void UpdateClipboardAnalysis()
        {
            // TODO: Show all data types. If it contains files, offer to copy the filenames?
            lblCurrentClipboard.Text = Clipboard.ContainsText() ?
                $"The clipboard currently contains {Clipboard.GetText().Length:N0} characters." :
                "The clipboard does not currently contain text.";
        }

        private void btnCopyText_Click(object sender, EventArgs e)
        {
            PlatformAPI.WriteToClipboard(GenerateRepeatedStringMemory(txtPattern.Text, (int)udStringLength.Value));
            UpdateClipboardAnalysis();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((keyData == Keys.Escape))
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void txtPattern_KeyPress(object sender, KeyPressEventArgs e)
        {

        }

        private void txtPattern_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnCopyText.PerformClick();
            }
        }

        private void pbColorPick_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            isPickingColor = true;
            pbColorPick.Cursor = Cursors.Cross;
            pbColorPick.Capture = true;
            UpdatePickedColor();
        }

        private void pbColorPick_MouseMove(object sender, MouseEventArgs e)
        {
            if (isPickingColor) UpdatePickedColor();
        }

        private void pbColorPick_MouseUp(object sender, MouseEventArgs e)
        {
            if (!isPickingColor || e.Button != MouseButtons.Left) return;

            UpdatePickedColor();
            isPickingColor = false;
            pbColorPick.Capture = false;
        }

        private void pbColorPick_MouseCaptureChanged(object sender, EventArgs e)
        {
            if (!pbColorPick.Capture)
            {
                isPickingColor = false;
                pbColorPick.Cursor = Cursors.Hand;
            }
        }

        private void UpdatePickedColor()
        {
            Color color = ReadScreenColor(out string processInfo);
            SetPickedColor(color);
            lblProcessInfo.Text = processInfo;
        }

        private void txtColorHex_TextChanged(object sender, EventArgs e)
        {
            string hex = txtColorHex.Text.Trim();
            if (hex.StartsWith("#", StringComparison.Ordinal)) hex = hex.Substring(1);
            if ((hex.Length != 3 && hex.Length != 6) ||
                !int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int rgb))
            {
                colorInputErrors.SetError(txtColorHex, "Enter 3 or 6 hexadecimal digits (0-9, A-F), optionally preceded by #.");
                return;
            }

            if (hex.Length == 3)
            {
                rgb = ((rgb >> 8) & 15) * 0x110000 + ((rgb >> 4) & 15) * 0x1100 + (rgb & 15) * 0x11;
            }
            SetPickedColor(Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255), false);
        }

        private void SetPickedColor(Color color, bool updateHexText = true)
        {
            if (updateHexText) txtColorHex.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            colorInputErrors.SetError(txtColorHex, string.Empty);
            txtColorRGB.Text = $"R:{color.R},G:{color.G},B:{color.B}";
            txtColorHex.BackColor = txtColorRGB.BackColor = color;
            txtColorHex.ForeColor = txtColorRGB.ForeColor = GetContrastingTextColor(color);
        }

        private static Color GetContrastingTextColor(Color background)
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



        private static Color ReadScreenColor(out string processInfo)
        {
            // Keep cursor coordinates and screen capture in physical pixels, even on mixed-DPI monitors.
            IntPtr previousContext = PlatformAPI.SetThreadDpiAwarenessContext(new IntPtr(-4)); // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
            if (previousContext == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                if (!PlatformAPI.GetPhysicalCursorPos(out Point screenPosition)) throw new Win32Exception(Marshal.GetLastWin32Error());
                processInfo = PlatformAPI.GetProcessInfoAtPhysicalPoint(screenPosition);

                using (Bitmap pixel = new Bitmap(1, 1))
                using (Graphics graphics = Graphics.FromImage(pixel))
                {
                    graphics.CopyFromScreen(screenPosition, Point.Empty, new Size(1, 1));
                    return pixel.GetPixel(0, 0);
                }
            }
            finally
            {
                if (PlatformAPI.SetThreadDpiAwarenessContext(previousContext) == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        private void btnColorPicker_Click(object sender, EventArgs e)
        {
            using (ColorDialog dlgColor = new ColorDialog())
            {
                if (dlgColor.ShowDialog() == DialogResult.OK)
                {
                    SetPickedColor(dlgColor.Color);
                }
            }
        }

        private void NumberTextChanged(object sender, EventArgs e)
        {
            if (isUpdatingNumber) return;

            TextBox source = (TextBox)sender;
            bool isHex = source == txtHex;
            TextBox target = isHex ? txtDecimal : txtHex;
            isUpdatingNumber = true;
            try
            {
                string trimmed = source.Text.Trim();
                if (source.Text != trimmed)
                {
                    int selectionStart = Math.Max(0, source.SelectionStart - (source.Text.Length - source.Text.TrimStart().Length));
                    int selectionLength = source.SelectionLength;
                    source.Text = trimmed;
                    source.SelectionStart = Math.Min(selectionStart, trimmed.Length);
                    source.SelectionLength = Math.Min(selectionLength, trimmed.Length - source.SelectionStart);
                }

                NumberStyles style = isHex ? NumberStyles.AllowHexSpecifier : NumberStyles.None;
                if (!ulong.TryParse(trimmed, style, CultureInfo.InvariantCulture, out ulong value))
                {
                    colorInputErrors.SetError(source, isHex
                        ? "Enter 1 to 16 hexadecimal digits (0-9, A-F), without a prefix."
                        : "Enter a whole number from 0 to 18446744073709551615, without separators.");
                    return;
                }

                colorInputErrors.SetError(source, string.Empty);
                target.Text = value.ToString(isHex ? "D" : "X", CultureInfo.InvariantCulture);
                colorInputErrors.SetError(target, string.Empty);
            }
            finally
            {
                isUpdatingNumber = false;
            }
        }

        private void btnUnicode_Click(object sender, EventArgs e)
        {
            frmPrompt.PromptOptions po = new frmPrompt.PromptOptions
            {
                Title = "Unicode Lookup",
                PromptText = "Enter a single Unicode character:",
                CueText = "⛄",
                MaxLength = 1,
                ReturnNullOnCancel = true,
                OwnerWindow = this
            };
            string sChar = frmPrompt.GetUserString(po);
            if (!sChar.HasText()) return;
            char c = sChar[0];
            txtHex.Text = $"{(int)c:X4}";
        }
    }
    public class MezerTools : IAppExtension
    {
        public void OnBeforeUnload()
        {
            //throw new NotImplementedException();
        }

        public void OnLoad()
        {
            CApp.UI.AddExtensionToolsMenuItem(new ToolStripMenuItem("Me&zerTools...", null, (s, e) => actShowMezerTools()));
        }

        private void actShowMezerTools()
        {
            new frmMezerTools().Show();
        }
    }

}
