using Clearinet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Extensions
{
    public sealed class AuthView : UserControl
    {
        private static readonly HashSet<string> RequestTokenHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "X-Auth-Token",
            "X-Access-Token",
            "X-JWT-Token",
            "Auth-Token",
            "Api-Token"
        };

        private static readonly HashSet<string> ResponseTokenHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "authorization",
            "x-access-token",
            "x-refresh-token"
        };

        private static readonly HashSet<string> TokenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "access_token",
            "id_token",
            "token",
            "jwt",
            "refresh_token",
            "idToken"
        };

        private static readonly Regex JwtValueRegex = new Regex(@"\b[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex JsonTokenRegex = new Regex(
            "(?<q>[\"'])(?<key>access_token|id_token|token|jwt|refresh_token|idToken)\\k<q>\\s*:\\s*(?<vq>[\"'])(?<value>.*?)\\k<vq>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

        internal void AssignRequest(HTTPRequestHeaders headersRequest, byte[] requestBody)
        {
            StringBuilder output = new StringBuilder();
            HashSet<string> shownJwt = new HashSet<string>(StringComparer.Ordinal);

            bool anyFound = false;
            anyFound |= AppendAuthHeaders(output, headersRequest, shownJwt, "Authorization", "Proxy-Authorization");
            anyFound |= AppendNamedTokenHeaders(output, headersRequest, shownJwt, RequestTokenHeaders, "Request token headers");
            anyFound |= AppendCookieTokens(output, headersRequest, shownJwt, false);
            anyFound |= AppendQueryStringTokens(output, headersRequest == null ? null : headersRequest.RequestPath, shownJwt);
            anyFound |= AppendBodyTokens(output, headersRequest, requestBody, shownJwt, "Request body tokens");

            if (!anyFound)
            {
                output.AppendLine("No authentication artifacts found.");
            }

            txtAuth.Text = output.ToString();
            txtAuth.SelectionStart = 0;
            txtAuth.SelectionLength = 0;
        }

        internal void AssignResponse(HTTPResponseHeaders headersResponse, byte[] responseBody)
        {
            StringBuilder output = new StringBuilder();
            HashSet<string> shownJwt = new HashSet<string>(StringComparer.Ordinal);

            bool anyFound = false;
            anyFound |= AppendAuthHeaders(output, headersResponse, shownJwt, "WWW-Authenticate", "Proxy-Authenticate");
            anyFound |= AppendNamedTokenHeaders(output, headersResponse, shownJwt, ResponseTokenHeaders, "Response token headers");
            anyFound |= AppendCookieTokens(output, headersResponse, shownJwt, true);
            anyFound |= AppendBodyTokens(output, headersResponse, responseBody, shownJwt, "Response body tokens");

            if (!anyFound)
            {
                output.AppendLine("No authentication artifacts found.");
            }

            txtAuth.Text = output.ToString();
            txtAuth.SelectionStart = 0;
            txtAuth.SelectionLength = 0;
        }

        private static bool AppendAuthHeaders(
            StringBuilder output,
            IEnumerable<HTTPHeaderItem> headers,
            HashSet<string> shownJwt,
            string serverHeaderName,
            string proxyHeaderName)
        {
            bool found = false;
            if (headers == null) return false;

            foreach (HTTPHeaderItem header in headers)
            {
                if (!header.Name.OICEquals(serverHeaderName) && !header.Name.OICEquals(proxyHeaderName)) continue;

                if (!found)
                {
                    output.AppendLine("Authentication headers:");
                }

                found = true;
                output.AppendLine(header.ToString());

                foreach (string challenge in SplitChallenges(header.Value ?? string.Empty))
                {
                    AppendChallenge(output, challenge, shownJwt);
                }
                output.AppendLine();
            }

            return found;
        }

        private static bool AppendNamedTokenHeaders(
            StringBuilder output,
            IEnumerable<HTTPHeaderItem> headers,
            HashSet<string> shownJwt,
            HashSet<string> headerNames,
            string sectionTitle)
        {
            bool found = false;
            if (headers == null) return false;

            foreach (HTTPHeaderItem header in headers)
            {
                if (!headerNames.Contains(header.Name)) continue;

                if (!found)
                {
                    output.AppendLine(sectionTitle + ":");
                }

                found = true;
                output.AppendLine(header.ToString());

                string value = header.Value ?? string.Empty;
                string tokenFromBearer = TryGetBearerToken(value);
                if (tokenFromBearer != null)
                {
                    AppendJwt(output, shownJwt, tokenFromBearer, $"  {header.Name} bearer");
                }
                else if (LooksLikeJwt(value.Trim()))
                {
                    AppendJwt(output, shownJwt, value.Trim(), $"  {header.Name}");
                }

                output.AppendLine();
            }

            return found;
        }

        private static bool AppendCookieTokens(
            StringBuilder output,
            IEnumerable<HTTPHeaderItem> headers,
            HashSet<string> shownJwt,
            bool responseCookies)
        {
            bool found = false;
            if (headers == null) return false;

            string cookieHeaderName = responseCookies ? "Set-Cookie" : "Cookie";
            string sectionTitle = responseCookies ? "Set-Cookie tokens:" : "Cookie tokens:";

            foreach (HTTPHeaderItem header in headers)
            {
                if (!header.Name.OICEquals(cookieHeaderName)) continue;

                if (responseCookies)
                {
                    if (!TryParseResponseSetCookie(header.Value, out string cookieName, out string cookieValue))
                    {
                        continue;
                    }

                    bool keyLooksToken = TokenKeys.Contains(cookieName) || cookieName.OICContains("token", "jwt");
                    bool valueLooksJwt = LooksLikeJwt(cookieValue);

                    if (!keyLooksToken && !valueLooksJwt) continue;

                    if (!found)
                    {
                        output.AppendLine(sectionTitle);
                    }
                    found = true;

                    output.AppendLine($"{header.Name}: {cookieName}={cookieValue}");
                    if (valueLooksJwt)
                    {
                        AppendJwt(output, shownJwt, cookieValue, $"  cookie {cookieName}");
                    }
                    output.AppendLine();
                }
                else
                {
                    string[] pairs = (header.Value ?? string.Empty).Split(';');
                    for (int i = 0; i < pairs.Length; i++)
                    {
                        string pair = pairs[i].Trim();
                        int equals = pair.IndexOf('=');
                        if (equals < 1) continue;

                        string cookieName = pair.Substring(0, equals).Trim();
                        string cookieValue = pair.Substring(equals + 1).Trim();

                        bool keyLooksToken = TokenKeys.Contains(cookieName) || cookieName.OICContains("token", "jwt");
                        bool valueLooksJwt = LooksLikeJwt(cookieValue);

                        if (!keyLooksToken && !valueLooksJwt) continue;

                        if (!found)
                        {
                            output.AppendLine(sectionTitle);
                        }
                        found = true;

                        output.AppendLine($"{header.Name}: {cookieName}={cookieValue}");
                        if (valueLooksJwt)
                        {
                            AppendJwt(output, shownJwt, cookieValue, $"  cookie {cookieName}");
                        }
                        output.AppendLine();
                    }
                }
            }

            return found;
        }

        private static bool AppendQueryStringTokens(StringBuilder output, string requestPath, HashSet<string> shownJwt)
        {
            if (string.IsNullOrEmpty(requestPath)) return false;

            int queryStart = requestPath.IndexOf('?');
            if (queryStart < 0 || queryStart >= requestPath.Length - 1) return false;

            string query = requestPath.Substring(queryStart + 1);
            int fragment = query.IndexOf('#');
            if (fragment >= 0)
            {
                query = query.Substring(0, fragment);
            }

            bool found = false;
            string[] pairs = query.Split('&');
            for (int i = 0; i < pairs.Length; i++)
            {
                if (pairs[i].Length == 0) continue;
                int equals = pairs[i].IndexOf('=');

                string key = equals < 0 ? pairs[i] : pairs[i].Substring(0, equals);
                string value = equals < 0 ? string.Empty : pairs[i].Substring(equals + 1);

                key = Utilities.UrlDecode(key ?? string.Empty);
                value = Utilities.UrlDecode(value ?? string.Empty);

                bool keyLooksToken = TokenKeys.Contains(key) || key.OICContains("token", "jwt");
                bool valueLooksJwt = LooksLikeJwt(value);

                if (!keyLooksToken && !valueLooksJwt) continue;

                if (!found)
                {
                    output.AppendLine("Querystring tokens:");
                }
                found = true;

                output.AppendLine($"  {key}={value}");
                if (valueLooksJwt)
                {
                    AppendJwt(output, shownJwt, value, $"  query parameter {key}");
                }
                output.AppendLine();
            }

            return found;
        }

        private static bool AppendBodyTokens(
            StringBuilder output,
            HTTPHeaders headers,
            byte[] bodyBytes,
            HashSet<string> shownJwt,
            string sectionTitle)
        {
            if (!bodyBytes.HasData()) return false;

            byte[] decodedBytes;
            try
            {
                decodedBytes = Exchange.DecodeBodyFromHeaders(headers, bodyBytes);
            }
            catch
            {
                decodedBytes = bodyBytes;
            }

            // Skip big or empty bodies.
            if (!decodedBytes.HasData() ||
                (decodedBytes.Length > 4096)) return false;  // TODO: App preference

            string bodyText = Encoding.UTF8.GetString(decodedBytes);
            if (!bodyText.HasText()) return false;

            string contentType = headers == null ? string.Empty : headers["Content-Type"];
            bool looksForm = contentType.OICContains("application/x-www-form-urlencoded");
            bool looksJson = contentType.OICContains("/json") || bodyText.TrimStart().StartsWith("{") || bodyText.TrimStart().StartsWith("[");

            StringBuilder section = new StringBuilder();

            if (looksForm || (!looksJson && bodyText.Contains("=")))
            {
                AppendFormBodyTokens(section, bodyText, shownJwt);
            }

            if (looksJson || bodyText.Contains("\"access_token\"") || bodyText.Contains("'access_token'"))
            {
                AppendJsonBodyTokens(section, bodyText, shownJwt);
            }

            AppendLooseJwtMatches(section, bodyText, shownJwt, "body");

            if (section.Length == 0) return false;

            output.AppendLine(sectionTitle + ":");
            output.Append(section);
            output.AppendLine();
            return true;
        }

        private static void AppendFormBodyTokens(StringBuilder output, string body, HashSet<string> shownJwt)
        {
            string[] pairs = body.Split('&');
            for (int i = 0; i < pairs.Length; i++)
            {
                if (pairs[i].Length == 0) continue;
                int equals = pairs[i].IndexOf('=');

                string key = equals < 0 ? pairs[i] : pairs[i].Substring(0, equals);
                string value = equals < 0 ? string.Empty : pairs[i].Substring(equals + 1);

                key = Utilities.UrlDecode(key ?? string.Empty);
                value = Utilities.UrlDecode(value ?? string.Empty);

                bool keyLooksToken = TokenKeys.Contains(key) || key.OICContains("token", "jwt");
                bool valueLooksJwt = LooksLikeJwt(value);

                if (!keyLooksToken && !valueLooksJwt) continue;

                output.AppendLine($"  form field: {key}={value}");
                if (valueLooksJwt)
                {
                    AppendJwt(output, shownJwt, value, $"  form field {key}");
                }
            }
        }

        private static void AppendJsonBodyTokens(StringBuilder output, string body, HashSet<string> shownJwt)
        {
            MatchCollection matches = JsonTokenRegex.Matches(body);
            for (int i = 0; i < matches.Count; i++)
            {
                string key = matches[i].Groups["key"] == null ? string.Empty : matches[i].Groups["key"].Value;
                string value = matches[i].Groups["value"] == null ? string.Empty : matches[i].Groups["value"].Value;
                if (!key.HasText()) continue;

                output.AppendLine($"  json field: {key}={value}");
                if (LooksLikeJwt(value))
                {
                    AppendJwt(output, shownJwt, value, $"  json field {key}");
                }
            }
        }

        private static void AppendLooseJwtMatches(StringBuilder output, string text, HashSet<string> shownJwt, string source)
        {
            MatchCollection matches = JwtValueRegex.Matches(text);
            for (int i = 0; i < matches.Count; i++)
            {
                string token = matches[i].Value;
                if (LooksLikeJwt(token))
                {
                    AppendJwt(output, shownJwt, token, "  " + source);
                }
            }
        }

        private static void AppendChallenge(StringBuilder output, string challenge, HashSet<string> shownJwt)
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

            if (auth.Scheme.OICEquals("Basic"))
            {
                AppendBasicAuth(output, auth.Parameter);
                return;
            }

            if (auth.Scheme.OICEquals("Digest"))
            {
                AppendDigestAuth(output, auth.Parameter);
                return;
            }

            if (auth.Scheme.OICEquals("Bearer"))
            {
                string bearerToken = auth.Parameter.Trim().Trim('"');
                if (LooksLikeJwt(bearerToken))
                {
                    AppendJwt(output, shownJwt, bearerToken, "  bearer token");
                }
                return;
            }

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

        private static void AppendBasicAuth(StringBuilder output, string parameter)
        {
            string token = parameter == null ? string.Empty : parameter.Trim();
            if (token.Length == 0)
            {
                output.AppendLine("Basic: missing credentials token.");
                return;
            }

            int whitespace = token.IndexOf(' ');
            if (whitespace > -1)
            {
                token = token.Substring(0, whitespace);
            }

            try
            {
                byte[] bytes = Convert.FromBase64String(token);
                string value;
                try
                {
                    value = Encoding.UTF8.GetString(bytes);
                }
                catch
                {
                    value = Encoding.GetEncoding("ISO-8859-1").GetString(bytes);
                }

                int colon = value.IndexOf(':');
                if (colon > -1)
                {
                    output.AppendLine("Basic username: " + value.Substring(0, colon));
                    output.AppendLine("Basic password: " + value.Substring(colon + 1));
                }
                else
                {
                    output.AppendLine("Basic decoded value: " + value);
                }
            }
            catch (FormatException)
            {
                output.AppendLine("Basic credentials are not valid Base64.");
            }
        }

        private static void AppendDigestAuth(StringBuilder output, string parameter)
        {
            var parameters = ParseAuthParameters(parameter);
            if (parameters.Count == 0)
            {
                output.AppendLine("Digest parameters could not be parsed.");
                return;
            }

            output.AppendLine("Digest parameters:");
            foreach (var item in parameters)
            {
                output.AppendLine($"  {item.Key} = {item.Value}");
            }
        }

        private static List<KeyValuePair<string, string>> ParseAuthParameters(string text)
        {
            var list = new List<KeyValuePair<string, string>>();
            if (!text.HasText()) return list;

            int index = 0;
            while (index < text.Length)
            {
                while (index < text.Length && (char.IsWhiteSpace(text[index]) || text[index] == ',')) index++;
                if (index >= text.Length) break;

                int keyStart = index;
                while (index < text.Length && text[index] != '=' && text[index] != ',') index++;
                if (index >= text.Length || text[index] != '=') break;

                string key = text.Substring(keyStart, index - keyStart).Trim();
                index++; // '='

                string value;
                if (index < text.Length && text[index] == '"')
                {
                    index++; // opening quote
                    var sb = new StringBuilder();
                    while (index < text.Length)
                    {
                        char c = text[index++];
                        if (c == '\\' && index < text.Length)
                        {
                            sb.Append(text[index++]);
                            continue;
                        }
                        if (c == '"') break;
                        sb.Append(c);
                    }
                    value = sb.ToString();
                }
                else
                {
                    int valueStart = index;
                    while (index < text.Length && text[index] != ',') index++;
                    value = text.Substring(valueStart, index - valueStart).Trim();
                }

                if (key.HasText())
                {
                    list.Add(new KeyValuePair<string, string>(key, value));
                }
            }

            return list;
        }

        private static void AppendJwt(StringBuilder output, HashSet<string> shownJwt, string token, string source)
        {
            token = token == null ? string.Empty : token.Trim().Trim('"');
            if (!LooksLikeJwt(token)) return;
            if (!shownJwt.Add(token)) return;

            output.AppendLine($"JWT source: {source}");
            output.AppendLine($"JWT: {token}");
            try
            {
                JwtDecodeResult decoded = JwtDecoder.Decode(token);
                output.AppendLine("  Header:");
                output.AppendLine(decoded.HeaderJson);
                output.AppendLine("  Payload:");
                output.AppendLine(decoded.PayloadJson);
                if (decoded.SignatureHex.HasText())
                {
                    output.AppendLine("  Signature (hex):");
                    output.AppendLine(decoded.SignatureHex);
                }
            }
            catch (Exception ex)
            {
                output.AppendLine("  Failed to decode JWT: " + ex.Message);
            }
        }

        private static string TryGetBearerToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string trimmed = value.Trim();

            if (!trimmed.OICStartsWith("Bearer ")) return null;
            string token = trimmed.Substring("Bearer ".Length).Trim().Trim('"');
            return token.Length > 0 ? token : null;
        }

        private static bool TryParseResponseSetCookie(string setCookie, out string cookieName, out string cookieValue)
        {
            cookieName = string.Empty;
            cookieValue = string.Empty;

            if (!setCookie.HasText()) return false;
            int pairEnd = setCookie.IndexOf(';');
            string pair = pairEnd < 0 ? setCookie : setCookie.Substring(0, pairEnd);

            int equals = pair.IndexOf('=');
            if (equals < 1) return false;

            cookieName = pair.Substring(0, equals).Trim();
            cookieValue = pair.Substring(equals + 1).Trim();
            return cookieName.HasText();
        }

        private static bool LooksLikeJwt(string value)
        {
            if (!value.HasText()) return false;

            string candidate = value.Trim().Trim('"');
            string[] parts = candidate.Split('.');
            if (parts.Length != 3) return false;

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) return false;
                for (int j = 0; j < parts[i].Length; j++)
                {
                    char c = parts[i][j];
                    bool valid = (c >= 'A' && c <= 'Z') ||
                                 (c >= 'a' && c <= 'z') ||
                                 (c >= '0' && c <= '9') ||
                                 c == '_' || c == '-';
                    if (!valid) return false;
                }
            }

            return true;
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
