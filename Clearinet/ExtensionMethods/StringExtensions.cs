using System;

namespace Clearinet
{
    public static class StringExtensions
    {
        public static bool OICContains(this String sIn, String sMatch)
        {
            return (-1 < sIn.IndexOf(sMatch, StringComparison.OrdinalIgnoreCase));
        }

        public static bool OICEquals(this String sIn, String sMatch)
        {
            return String.Equals(sIn, sMatch, StringComparison.OrdinalIgnoreCase);
        }

        public static bool OICStartsWith(this String sIn, String sMatch)
        {
            return sIn.StartsWith(sMatch, StringComparison.OrdinalIgnoreCase);
        }

        public static bool OICStartsWithAny(this String sIn, params String[] sMatch)
        {
            for (var i = 0; i < sMatch.Length; i++)
            {
                if (sIn.StartsWith(sMatch[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
        public static bool OICEndsWith(this String sIn, String sMatch)
        {
            return sIn.EndsWith(sMatch, StringComparison.OrdinalIgnoreCase);
        }
        public static bool OICEndsWithAny(this String sIn, params String[] sArrMatch)
        {
            for (var i = 0; i < sArrMatch.Length; i++)
            {
                if (sIn.EndsWith(sArrMatch[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        internal static int StrLen(this string s)
        {
            if (String.IsNullOrEmpty(s)) return 0;
            return s.Length;
        }

        public static bool HasText(this string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        public static bool HasLeadingWhitespace(this string value)
        {
            return ((value.Length > 0) && Char.IsWhiteSpace(value[0]));
        }

        public static string RemoveAllWhitespace(this string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            char[] buffer = new char[input.Length];
            int writerIndex = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (!char.IsWhiteSpace(c))
                {
                    buffer[writerIndex++] = c;
                }
            }

            // If no whitespace was removed, return the original string instance
            if (writerIndex == input.Length)
                return input;

            return new string(buffer, 0, writerIndex);
        }

        // Returns string before first delimiter; empty if null.
        public static string TrimAfter(this string s, string delim)
        {
            if (s == null) return string.Empty;
            if (delim == null) return s;
            int idx = s.IndexOf(delim);
            return idx < 0 ? s : s.Substring(0, idx);
        }

        // Returns string before first instance of a delimiter; empty if null.
        public static string TrimAfter(this string s, char delim)
        {
            if (s == null) return string.Empty;
            int idx = s.IndexOf(delim);
            return idx < 0 ? s : s.Substring(0, idx);
        }

        // Returns string after first delimiter; empty if null.
        public static string TrimBefore(this string s, char delim)
        {
            if (s == null) return string.Empty;
            int idx = s.IndexOf(delim);
            return idx < 0 ? s : s.Substring(idx + 1);
        }

        // Returns string after first delimiter; empty if null.
        public static string TrimBefore(this string s, string delim)
        {
            if (s == null) return string.Empty;
            if (delim == null) return s;
            int idx = s.IndexOf(delim);
            return idx < 0 ? s : s.Substring(idx + delim.Length);
        }

        // Returns string from first delimiter onward; empty if null.
        public static string TrimUpTo(this string s, string delim)
        {
            if (s == null) return string.Empty;
            if (delim == null) return s;
            int idx = s.IndexOf(delim);
            return idx < 0 ? s : s.Substring(idx);
        }

        // Returns string after last delimiter; empty if null.
        public static string TrimBeforeLast(this string s, char delim)
        {
            if (s == null) return string.Empty;
            int idx = s.LastIndexOf(delim);
            return idx < 0 ? s : s.Substring(idx + 1);
        }

        // Returns string after last delimiter; empty if null.
        public static string TrimBeforeLast(this string s, string delim)
        {
            if (s == null) return string.Empty;
            if (delim == null) return s;
            int idx = s.LastIndexOf(delim);
            return idx < 0 ? s : s.Substring(idx + delim.Length);
        }
    }
}
