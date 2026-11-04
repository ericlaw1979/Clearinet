using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Clearinet
{
    /// <summary>
    /// Data sources for Exchange list columns beyond headers and flags:
    /// "@timers.Name" shows any ExchangeTimers field, and "@calc.Name" shows a value calculated from the Exchange.
    /// </summary>
    internal static class ExchangeColumnSources
    {
        internal const string TimersPrefix = "@timers.";
        internal const string CalcPrefix = "@calc.";

        internal sealed class Source
        {
            public string Name;
            public string DefaultTitle;
            public string Description;
            public ColumnSortType SortType;
            public Func<Exchange, string> Provider;
            public override string ToString() => Name; // Shown in the Add columns dialog's field list.
        }

        private static readonly Dictionary<string, Source> s_Timers = new Dictionary<string, Source>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Source> s_Calculated = new Dictionary<string, Source>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<Source> s_TimersInOrder = new List<Source>();
        private static readonly List<Source> s_CalculatedInOrder = new List<Source>();

        // Decoding and hashing a body can be expensive, and columns are repopulated on every row update, so results
        // are cached per response body array. Changing the body (e.g. utilDecodeResponse) assigns a new array.
        private sealed class BodyInfo
        {
            public int EncodedLength = -1;   // After removing transfer encoding; -1 if decoding failed.
            public int DecodedLength = -1;   // After also removing content encoding; -1 if decoding failed.
            public string Sha256;            // Of the decoded body; null until requested.
        }
        private static readonly ConditionalWeakTable<byte[], BodyInfo> s_BodyInfo = new ConditionalWeakTable<byte[], BodyInfo>();

        static ExchangeColumnSources()
        {
            foreach (FieldInfo field in typeof(ExchangeTimers).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                FieldInfo f = field;
                if (f.FieldType == typeof(DateTime))
                {
                    AddSource(s_Timers, f.Name, f.Name, "Time of " + f.Name, ColumnSortType.DateTime, x =>
                    {
                        DateTime value = (DateTime)f.GetValue(x.Timers);
                        return value == DateTime.MinValue ? string.Empty : value.ToString("HH:mm:ss.fff");
                    });
                }
                else if (f.FieldType == typeof(int))
                {
                    // As in ExchangeTimers.ToString, zero means not measured.
                    AddSource(s_Timers, f.Name, f.Name + " ms", f.Name + " in milliseconds", ColumnSortType.Numeric, x =>
                    {
                        int value = (int)f.GetValue(x.Timers);
                        return value > 0 ? value.ToString() : string.Empty;
                    });
                }
            }

            AddSource(s_Calculated, "TotalTime", "Total ms", "Milliseconds from the start of the request to the end of the response",
                ColumnSortType.Numeric, x => Elapsed(x.Timers.ClientBeginRequest, x.Timers.ClientDoneResponse));
            AddSource(s_Calculated, "TTFB", "TTFB ms", "Milliseconds the server took to start responding after the request was sent",
                ColumnSortType.Numeric, x => Elapsed(x.Timers.ProxyDoneRequest, x.Timers.ServerBeginResponse));
            AddSource(s_Calculated, "DownloadTime", "Download ms", "Milliseconds from the first to the last byte of the response",
                ColumnSortType.Numeric, x => Elapsed(x.Timers.ServerBeginResponse, x.Timers.ServerDoneResponse));
            AddSource(s_Calculated, "RequestBodySize", "Req. body", "Size of the request body in bytes",
                ColumnSortType.Numeric, x => x.RequestHeaders == null ? string.Empty : (x.RequestBody?.Length ?? 0).ToString("N0"));
            AddSource(s_Calculated, "DecodedResponseSize", "Decoded size", "Size of the response body in bytes after removing compression",
                ColumnSortType.Numeric, x => GetBodyInfo(x) is BodyInfo bi && bi.DecodedLength >= 0 ? bi.DecodedLength.ToString("N0") : string.Empty);
            AddSource(s_Calculated, "CompressionSavings", "Saved %", "Percentage of the response body saved by Content-Encoding compression",
                ColumnSortType.Numeric, CompressionSavings);
            AddSource(s_Calculated, "ResponseHash", "SHA-256", "SHA-256 hash (hex) of the response body after removing compression",
                ColumnSortType.Text, ResponseHash);
            AddSource(s_Calculated, "SecurityHeaders", "Security", "Security-related response headers that are present",
                ColumnSortType.Text, SecurityHeaders);
        }

        private static void AddSource(Dictionary<string, Source> sources, string name, string title, string description,
            ColumnSortType sortType, Func<Exchange, string> provider)
        {
            var source = new Source { Name = name, DefaultTitle = title, Description = description, SortType = sortType, Provider = provider };
            sources[name] = source;
            (sources == s_Timers ? s_TimersInOrder : s_CalculatedInOrder).Add(source);
        }

        /// <summary>The timer fields that can be shown, in declaration order.</summary>
        internal static IReadOnlyList<Source> Timers => s_TimersInOrder;

        /// <summary>The calculated values that can be shown, in the order listed above.</summary>
        internal static IReadOnlyList<Source> Calculated => s_CalculatedInOrder;

        /// <summary>
        /// Finds the source for a bound data field such as "@timers.DNSTime" or "@calc.TTFB" (case-insensitive).
        /// Returns null for other fields, including unknown names after a known prefix.
        /// </summary>
        internal static Source Find(string sBoundDataField)
        {
            if (sBoundDataField == null) return null;
            Source source;
            if (sBoundDataField.StartsWith(TimersPrefix, StringComparison.OrdinalIgnoreCase)
                && s_Timers.TryGetValue(sBoundDataField.Substring(TimersPrefix.Length), out source)) return source;
            if (sBoundDataField.StartsWith(CalcPrefix, StringComparison.OrdinalIgnoreCase)
                && s_Calculated.TryGetValue(sBoundDataField.Substring(CalcPrefix.Length), out source)) return source;
            return null;
        }

        private static string Elapsed(DateTime start, DateTime end)
        {
            if (start == DateTime.MinValue || end == DateTime.MinValue || end < start) return string.Empty;
            return ((long)Math.Round((end - start).TotalMilliseconds)).ToString();
        }

        private static BodyInfo GetBodyInfo(Exchange x)
        {
            if (x.ResponseHeaders == null || x.ResponseBody == null) return null;
            return s_BodyInfo.GetValue(x.ResponseBody, body =>
            {
                var info = new BodyInfo();
                try
                {
                    HTTPResponseHeaders transferOnly = x.ResponseHeaders.Clone();
                    transferOnly.Remove("Content-Encoding");
                    info.EncodedLength = Exchange.DecodeBodyFromHeaders(transferOnly, body).Length;
                    info.DecodedLength = Exchange.DecodeBodyFromHeaders(x.ResponseHeaders, body).Length;
                }
                catch
                {
                    info.EncodedLength = info.DecodedLength = -1;
                }
                return info;
            });
        }

        private static string CompressionSavings(Exchange x)
        {
            if (x.ResponseHeaders == null || !x.ResponseHeaders.Exists("Content-Encoding")) return string.Empty;
            BodyInfo info = GetBodyInfo(x);
            if (info == null || info.EncodedLength < 0 || info.DecodedLength <= 0) return string.Empty;
            double saved = 100.0 * (info.DecodedLength - info.EncodedLength) / info.DecodedLength;
            return Math.Round(saved).ToString("0");
        }

        private static string ResponseHash(Exchange x)
        {
            BodyInfo info = GetBodyInfo(x);
            if (info == null || info.DecodedLength <= 0) return string.Empty;
            if (info.Sha256 == null)
            {
                try
                {
                    byte[] decoded = Exchange.DecodeBodyFromHeaders(x.ResponseHeaders, x.ResponseBody);
                    info.Sha256 = Utilities.GetHash("sha256", decoded).Replace("-", string.Empty).ToLowerInvariant();
                }
                catch
                {
                    info.Sha256 = string.Empty;
                }
            }
            return info.Sha256;
        }

        private static readonly KeyValuePair<string, string>[] s_SecurityHeaders =
        {
            new KeyValuePair<string, string>("Strict-Transport-Security", "HSTS"),
            new KeyValuePair<string, string>("Content-Security-Policy", "CSP"),
            new KeyValuePair<string, string>("Content-Security-Policy-Report-Only", "CSP-Report-Only"),
            new KeyValuePair<string, string>("X-Frame-Options", "XFO"),
            new KeyValuePair<string, string>("Referrer-Policy", "Referrer-Policy"),
            new KeyValuePair<string, string>("Permissions-Policy", "Permissions-Policy"),
            new KeyValuePair<string, string>("Cross-Origin-Opener-Policy", "COOP"),
            new KeyValuePair<string, string>("Cross-Origin-Embedder-Policy", "COEP"),
            new KeyValuePair<string, string>("Cross-Origin-Resource-Policy", "CORP"),
        };

        private static string SecurityHeaders(Exchange x)
        {
            HTTPResponseHeaders headers = x.ResponseHeaders;
            if (headers == null) return string.Empty;
            var present = new List<string>();
            foreach (var header in s_SecurityHeaders)
            {
                if (headers.Exists(header.Key)) present.Add(header.Value);
            }
            // X-Content-Type-Options has only one meaningful value.
            if (headers.ExistsAndContains("X-Content-Type-Options", "nosniff")) present.Add("nosniff");
            return string.Join(", ", present);
        }
    }
}
