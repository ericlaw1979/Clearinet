using Clearinet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http.Headers;
using System.Text;
using System.Windows.Forms;

namespace Extensions
{
    public sealed class AuthView : UserControl
    {
        readonly TextBox txtAuth;
        Font viewFont;

        public AuthView()
        {
            txtAuth = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                BackColor = CONFIG.colorDisabledEdit,
                BorderStyle = BorderStyle.FixedSingle,
            };
            Controls.Add(txtAuth);
            SetFontSize(CONFIG.flFontSize);
        }

        internal void Assign(IEnumerable<HTTPHeaderItem> headers, string sServerHeaderName, string sProxyHeaderName)
        {
            var output = new StringBuilder();
            bool bServerHeaderFound = false;
            bool bProxyHeaderFound = false;

            if (headers != null)
            {
                foreach (HTTPHeaderItem header in headers)
                {
                    if (!header.Name.OICEquals(sServerHeaderName)) continue;
                    output.AppendLine(header.ToString());
                    bServerHeaderFound = true;
                    foreach (string challenge in SplitChallenges(header.Value ?? string.Empty))
                    {
                        AppendChallenge(output, challenge);
                    }
                    output.AppendLine();
                }
                foreach (HTTPHeaderItem header in headers)
                {
                    if (!header.Name.OICEquals(sProxyHeaderName)) continue;
                    output.AppendLine(header.ToString());
                    bProxyHeaderFound = true;
                    foreach (string challenge in SplitChallenges(header.Value ?? string.Empty))
                    {
                        AppendChallenge(output, challenge);
                    }
                    output.AppendLine();
                }
            }
            if (!bServerHeaderFound && !bProxyHeaderFound)
            {
                output.AppendLine($"No '{sServerHeaderName}' or '{sProxyHeaderName}' headers found.");
            }
            txtAuth.Text = output.ToString();
            txtAuth.SelectionStart = 0;
            txtAuth.SelectionLength = 0;
        }

        private static void AppendChallenge(StringBuilder output, string challenge)
        {
            if (!AuthenticationHeaderValue.TryParse(challenge, out AuthenticationHeaderValue auth))
            {
                output.AppendLine("Invalid authentication header value.");
                return;
            }

            output.AppendLine($"Scheme: {auth.Scheme}");
            if (string.IsNullOrEmpty(auth.Parameter))
            {
                output.AppendLine("No credentials or challenge data.");
                return;
            }

            output.AppendLine($"Credentials / parameters: {auth.Parameter}");
            if (!auth.Scheme.OICEquals("NTLM") && !auth.Scheme.OICEquals("Negotiate")) return;

            try
            {
                byte[] bytes = Convert.FromBase64String(auth.Parameter);
                output.AppendLine("Parsed authentication token:");
                AuthTokenDecoder.Append(output, bytes);
                output.AppendLine();
                output.AppendLine($"Decoded token ({bytes.Length} bytes):");
                output.Append(Utilities.ByteArrayToHexView(bytes, 0, 16, bytes.Length, true));
            }
            catch (FormatException)
            {
                output.AppendLine("Invalid Base64 token; unable to decode authentication data.");
            }
        }

        private static IEnumerable<string> SplitChallenges(string value)
        {
            int start = 0;
            bool quoted = false;
            for (int i = 0; i < value.Length; i++)
            {
                if (quoted && value[i] == '\\')
                {
                    i++;
                    continue;
                }
                if (value[i] == '"') quoted = !quoted;
                if (quoted || value[i] != ',') continue;

                // A comma before another auth-param belongs to this challenge, not a new scheme.
                int next = i + 1;
                while (next < value.Length && char.IsWhiteSpace(value[next])) next++;
                while (next < value.Length && !char.IsWhiteSpace(value[next]) &&
                    value[next] != '=' && value[next] != ',') next++;
                while (next < value.Length && char.IsWhiteSpace(value[next])) next++;
                if (next < value.Length && value[next] == '=') continue;

                yield return value.Substring(start, i - start).Trim();
                start = i + 1;
            }
            yield return value.Substring(start).Trim();
        }

        public void Clear() => txtAuth.Clear();

        internal void SetFontSize(float size)
        {
            Font oldFont = viewFont;
            viewFont = new Font("Lucida Console", size);
            txtAuth.Font = viewFont;
            oldFont?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) viewFont?.Dispose();
        }
    }
}
