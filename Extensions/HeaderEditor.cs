using Clearinet;
using System.Drawing;
using System.Windows.Forms;
using static Extensions.HeaderView;

namespace Extensions
{
    internal partial class HeaderEditor : Form
    {
        internal HeaderEditor(HeaderViewMode mode)
        {
            InitializeComponent();
            InitializeHeaderTemplates();
            txtValue.Focus();
            txtName.AutoCompleteMode = AutoCompleteMode.Append;
            txtName.AutoCompleteSource = AutoCompleteSource.CustomSource;
            // Issue: We don't actually need to do this unless we're in Edit mode.
            if (mode == HeaderViewMode.Request)
            {
                txtName.AutoCompleteCustomSource = new AutoCompleteStringCollection();
                var sPopularRequestHeaders = CApp.Prefs.GetStringPref("lists.common_request_headers", DEFAULTS.sPopularRequestHeaders);
                txtName.AutoCompleteCustomSource.AddRange(sPopularRequestHeaders.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
            }
            else
            {
                txtName.AutoCompleteCustomSource = new AutoCompleteStringCollection();
                var sPopularResponseHeaders = CApp.Prefs.GetStringPref("lists.common_response_headers", DEFAULTS.sPopularResponseHeaders);
                txtName.AutoCompleteCustomSource.AddRange(sPopularResponseHeaders.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
            }
            // TODO: Set icon to something reasonable
            if (CONFIG.isDarkMode) ThemeManager.ApplyDarkMode(this);
        }

        public bool IsReadonly
        {
            get { return txtName.ReadOnly; }
            set
            {
                txtName.ReadOnly = value;
                txtValue.ReadOnly = value;
                txtName.BackColor = value ? CONFIG.colorDisabledEdit : SystemColors.Window;
                txtValue.BackColor = value ? CONFIG.colorDisabledEdit : SystemColors.Window;
                btnTemplates.Enabled = !value;

                // We adjust this dynamically on edit.
                btnSave.Enabled = false;
            }
        }

        private void lnkHeaderLookup_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Utilities.LaunchHyperlink(CApp.Prefs.GetStringPref("inspector.headers.searchurl",
                 "https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/$W$").Replace("$W$", txtName.Text));
        }

        private void txtName_TextChanged(object sender, System.EventArgs e)
        {
            if (txtName.Text.HasText())
            {
                if (!txtName.ReadOnly) btnSave.Enabled = true;
                lnkHeaderLookup.Enabled = true;
            }
            else
            {
                btnSave.Enabled = false;
                lnkHeaderLookup.Enabled = false;
            }
        }

        private void txtValue_TextChanged(object sender, System.EventArgs e)
        {
            if (!txtName.ReadOnly) btnSave.Enabled = true;
        }

        private void btnSave_Click(object sender, System.EventArgs e)
        {
            txtName.Text = txtName.Text.RemoveAllWhitespace();
        }

        private void btnTemplates_Click(object sender, System.EventArgs e)
        {
            Point pvOrigin = PointToScreen(btnTemplates.Location);
            pvOrigin.Y += btnTemplates.Height;
            mnuTemplates.Show(pvOrigin, ToolStripDropDownDirection.BelowRight);
        }

        private void InitializeHeaderTemplates()
        {
            AddTemplateCategory("&Caching",
                new[] { "If-Modified-Since", "Wed, 21 Oct 2015 07:28:00 GMT" },
                new[] { "If-None-Match", "*" },
                new[] { "Expires", "Wed, 21 Oct 2015 07:28:00 GMT" },
                new[] { "Cache-Control", "no-cache" },
                new[] { "Cache-Control", "max-age=86400" });

            AddTemplateCategory("&Cookies",
                new[] { "Cookie", "sessionId=abc123; preferences=compact" },
                new[] { "Set-Cookie", "sessionId=abc123; Path=/; HttpOnly; Secure; SameSite=Strict" },
                new[] { "Set-Cookie", "sessionId=abc123; Max-Age=3600; Path=/" },
                new[] { "Set-Cookie", "preferences=compact; Path=/; SameSite=Lax" },
                new[] { "Set-Cookie", "sessionId=abc123; Domain=example.com; Path=/" });

            AddTemplateCategory("&Auth",
                new[] { "Authorization", "Bearer <access-token>" },
                new[] { "Authorization", "Basic <base64-credentials>" },
                new[] { "WWW-Authenticate", "Bearer realm=\\\"api\\\"" },
                new[] { "Proxy-Authorization", "Basic <base64-credentials>" },
                new[] { "Authentication-Info", "nextnonce=\\\"<nonce>\\\"" });

            AddTemplateCategory("&Security",
                new[] { "Content-Security-Policy", "default-src 'none'; script-src 'self'; connect-src 'self'; img-src 'self'; style-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'self'; manifest-src 'self'; media-src 'self'; object-src 'none'; worker-src 'self'" },
                new[] { "X-Content-Type-Options", "nosniff" },
                new[] { "Strict-Transport-Security", "max-age=31536000; includeSubDomains" },
                new[] { "X-Frame-Options", "DENY" },
                new[] { "Referrer-Policy", "strict-origin-when-cross-origin" });

            AddTemplateCategory("&Misc",
                new[] { "Content-Type", "application/json; charset=utf-8" },
                new[] { "Accept", "application/json" },
                new[] { "User-Agent", "ExampleClient/1.0" },
                new[] { "X-Request-ID", "<request-id>" },
                new[] { "Location", "https://example.com/resource" });
        }

        private void AddTemplateCategory(string categoryName, params string[][] templates)
        {
            var category = new ToolStripMenuItem(categoryName);
            foreach (var template in templates)
            {
                var valuePreview = template[1].Length > 24 ? template[1].Substring(0, 21) + "..." : template[1];
                var item = new ToolStripMenuItem(template[0] + " (" + valuePreview + ")")
                {
                    Tag = template
                };
                item.Click += tsmiTemplate_Click;
                category.DropDownItems.Add(item);
            }
            mnuTemplates.Items.Add(category);
        }

        private void tsmiTemplate_Click(object sender, System.EventArgs e)
        {
            var template = (string[])((ToolStripMenuItem)sender).Tag;
            txtName.Text = template[0];
            txtValue.Text = template[1];
        }
    }
}
