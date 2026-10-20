using Clearinet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace Extensions
{
    public sealed class CookieView : UserControl
    {
        readonly TextBox txtCookies;
        readonly TextBox txtFind;
        readonly Label searchStatus;
        Font viewFont;

        public CookieView()
        {
            txtCookies = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                HideSelection = false,
                BackColor = CONFIG.colorDisabledEdit,
                AccessibleName = "Cookies",
                BorderStyle = BorderStyle.FixedSingle
            };
            txtFind = new TextBox { Dock = DockStyle.Fill, AccessibleName = "Find cookies", BorderStyle = BorderStyle.FixedSingle };
            searchStatus = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6) };
            var next = new Button { Text = "Next", AutoSize = true, Anchor = AnchorStyles.Left };
            var searchPanel = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 4, RowCount = 1 };
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchPanel.Controls.Add(new Label { Text = "Find:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            searchPanel.Controls.Add(txtFind, 1, 0);
            searchPanel.Controls.Add(next, 2, 0);
            searchPanel.Controls.Add(searchStatus, 3, 0);
            Controls.Add(txtCookies);
            Controls.Add(searchPanel);
            txtFind.TextChanged += (sender, e) => Find(false, true);
            next.Click += (sender, e) => Find(false, false);
            txtFind.KeyDown += SearchKeyDown;
            txtCookies.KeyDown += SearchKeyDown;
            SetFontSize(CONFIG.flFontSize);
        }

        internal void Assign(IEnumerable<HTTPHeaderItem> headers, bool response)
        {
            txtCookies.Text = FormatCookies(headers, response, DateTimeOffset.UtcNow);
            txtCookies.Select(0, 0);
            Find(false, true);
        }

        internal static string FormatCookies(IEnumerable<HTTPHeaderItem> headers, bool response, DateTimeOffset now)
        {
            string headerName = response ? "Set-Cookie" : "Cookie";
            var output = new StringBuilder();
            bool found = false;
            long totalCharacters = 0;
            if (headers != null)
            {
                var headerList = new List<HTTPHeaderItem>(headers);
                if (response)
                {
                    // Historical captures must be evaluated when sent, not when inspected.
                    foreach (HTTPHeaderItem header in headerList)
                    {
                        if (header.Name.OICEquals("Date") && DateTimeOffset.TryParse(header.Value, CultureInfo.InvariantCulture,
                            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                            out DateTimeOffset sent))
                        {
                            now = sent;
                            break;
                        }
                    }
                }
                foreach (HTTPHeaderItem header in headerList)
                {
                    if (!header.Name.OICEquals(headerName) && !header.Name.OICEquals(headerName + "2")) continue;
                    found = true;
                    output.AppendLine(header.Name + ":");
                    string value = header.Value ?? string.Empty;
                    totalCharacters += value.Length;
                    if (string.IsNullOrWhiteSpace(value)) output.AppendLine("    Empty cookie header.");
                    foreach (string cookie in Split(value, !response))
                    {
                        if (response) AppendResponseCookie(output, cookie, now);
                        else AppendPair(output, cookie, "    ", cookie.StartsWith("$", StringComparison.Ordinal), true);
                    }
                    if (HasUnclosedQuote(value)) output.AppendLine("    Invalid cookie header: unterminated quoted value.");
                    output.AppendLine();
                }
            }
            if (!found) output.AppendLine($"No '{headerName}' or '{headerName}2' headers found.");
            if (!response) output.Insert(0, $"Total cookie data: {totalCharacters} characters\r\n\r\n");
            return output.ToString();
        }

        private static void AppendPair(StringBuilder output, string pair, string indent, bool attribute, bool showLength = false)
        {
            int equals = pair.IndexOf('=');
            if (equals < 1 && !attribute)
                output.AppendLine(indent + "Invalid cookie (expected name=value): " + pair);
            else
                output.AppendLine(indent + pair + (showLength && !attribute ? $" ({pair.Length} characters)" : string.Empty));
        }

        private static void AppendResponseCookie(StringBuilder output, string cookie, DateTimeOffset now)
        {
            bool first = true;
            bool? maxAgeDeletes = null;
            DateTimeOffset? expires = null;
            foreach (string part in Split(cookie, true, false))
            {
                if (first)
                {
                    AppendPair(output, part, "    Cookie: ", false);
                    first = false;
                    continue;
                }
                output.AppendLine("        " + part);
                int equals = part.IndexOf('=');
                if (equals < 0) continue;
                string name = part.Substring(0, equals).Trim();
                string value = part.Substring(equals + 1).Trim().Trim('"');
                if (name.OICEquals("Max-Age"))
                {
                    if (TryMaxAge(value, out bool deletes)) maxAgeDeletes = deletes;
                    else output.AppendLine("        Invalid Max-Age; ignored for expiration.");
                }
                else if (name.OICEquals("Expires"))
                {
                    if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out DateTimeOffset date)) expires = date;
                    else output.AppendLine("        Invalid Expires date; ignored for expiration.");
                }
            }
            // A valid Max-Age overrides Expires, even when the latter is in the past.
            if (maxAgeDeletes ?? (expires.HasValue && expires.Value <= now))
                output.AppendLine("        Operation: Delete (cookie sent expired)");
            output.AppendLine();
        }

        private static bool TryMaxAge(string value, out bool deletes)
        {
            deletes = false;
            int start = value.StartsWith("-", StringComparison.Ordinal) ? 1 : 0;
            if (start == value.Length) return false;
            bool zero = true;
            for (int i = start; i < value.Length; i++)
            {
                if (value[i] < '0' || value[i] > '9') return false;
                if (value[i] != '0') zero = false;
            }
            deletes = start == 1 || zero;
            return true;
        }

        private static IEnumerable<string> Split(string value, bool semicolons, bool commas = true)
        {
            int start = 0;
            bool quoted = false;
            for (int i = 0; i < value.Length; i++)
            {
                if (quoted && value[i] == '\\') { i++; continue; }
                if (value[i] == '"') quoted = !quoted;
                if (quoted) continue;
                // An Expires date contains a comma, but a new cookie starts with a name=value pair.
                bool boundary = (semicolons && value[i] == ';') ||
                    (commas && value[i] == ',' && StartsCookie(value, i + 1));
                if (!boundary) continue;
                string part = value.Substring(start, i - start).Trim();
                if (part.Length > 0) yield return part;
                start = i + 1;
            }
            string last = value.Substring(start).Trim();
            if (last.Length > 0) yield return last;
        }

        private static bool StartsCookie(string value, int start)
        {
            while (start < value.Length && char.IsWhiteSpace(value[start])) start++;
            int nameStart = start;
            while (start < value.Length && !char.IsWhiteSpace(value[start]) &&
                value[start] != '=' && value[start] != ';' && value[start] != ',') start++;
            if (start == nameStart) return false;
            while (start < value.Length && char.IsWhiteSpace(value[start])) start++;
            return start < value.Length && value[start] == '=';
        }

        private static bool HasUnclosedQuote(string value)
        {
            bool quoted = false;
            for (int i = 0; i < value.Length; i++)
            {
                if (quoted && value[i] == '\\') { i++; continue; }
                if (value[i] == '"') quoted = !quoted;
            }
            return quoted;
        }

        private void SearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F3 || (sender == txtFind && e.KeyCode == Keys.Enter))
            {
                Find(e.Shift, false);
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.F)
            {
                txtFind.Focus();
                txtFind.SelectAll();
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (sender == txtFind && e.KeyCode == Keys.Escape)
            {
                txtFind.Clear();
                e.Handled = e.SuppressKeyPress = true;
            }
        }

        private void Find(bool backwards, bool fromStart)
        {
            string text = txtCookies.Text;
            string query = txtFind.Text;
            searchStatus.Text = string.Empty;
            if (query.Length == 0)
            {
                txtCookies.Select(0, 0);
                return;
            }
            int index;
            if (backwards)
            {
                int start = txtCookies.SelectionStart - 1;
                index = start >= 0 ? text.LastIndexOf(query, start, StringComparison.OrdinalIgnoreCase) : -1;
                if (index < 0) index = text.LastIndexOf(query, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                int start = fromStart ? 0 : txtCookies.SelectionStart + txtCookies.SelectionLength;
                index = text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);
                if (index < 0 && start > 0) index = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            }
            if (index < 0)
            {
                txtCookies.SelectionLength = 0;
                searchStatus.Text = "Not found";
                return;
            }
            txtCookies.Select(index, query.Length);
            txtCookies.ScrollToCaret();
        }

        public void Clear()
        {
            txtCookies.Clear();
            searchStatus.Text = string.Empty;
        }

        internal void SetFontSize(float size)
        {
            Font oldFont = viewFont;
            viewFont = new Font("Lucida Console", size);
            txtCookies.Font = viewFont;
            oldFont?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) viewFont?.Dispose();
        }
    }
}
