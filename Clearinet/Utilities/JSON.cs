/// <remarks>
/// Originally based on http://techblog.procurios.nl/k/618/news/view/14605/14863/How-do-I-write-my-own-parser-for-JSON.html
/// Licensed under the http://www.opensource.org/licenses/mit-license.php by Patrick van Bergen.
/// Modified based on real-world experience.
/// Modernized for C# 7.0 performance & idiom maintenance.
///</remarks>
using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Clearinet.WebFormats
{
    /// <summary>
    /// This class encodes and decodes JSON text http://www.json.org/
    /// JSON uses Arrays and Objects, similar to DotNet datatypes ArrayList and Hashtable.
    /// All numbers are parsed to doubles.
    /// </summary>
    public class JSON
    {
        /// <summary>
        /// Wrapper around JSON Parse output; needed because JScript.NET does not support OUT Parameters
        /// </summary>
        public class JSONParseResult
        {
            public object JSONObject { get; set; }
            public JSONParseErrors JSONErrors { get; set; }
        }

        public class JSONParseErrors
        {
            /// <summary>
            /// Index of the last error in the stream
            /// </summary>
            public int iErrorIndex { get; set; }

            /// <summary>
            /// Warnings found while parsing JSON
            /// </summary>
            public string sWarningText { get; set; }
        }

        internal enum JSONTokens : byte
        {
            NONE = 0,
            CURLY_OPEN = 1,
            CURLY_CLOSE = 2,
            SQUARED_OPEN = 3,
            SQUARED_CLOSE = 4,
            COLON = 5,
            COMMA = 6,
            STRING = 7,
            NUMBER = 8,
            TRUE = 9,
            FALSE = 10,
            NULL = 11,
            /// <summary>
            /// This token is a JavaScript Identifier that was not properly quoted as it should have been.
            /// </summary>
            IMPLIED_IDENTIFIER_NAME = 12
        }

        private const int BUILDER_DEFAULT_CAPACITY = 2048;

        public static object JsonDecode(string sJSON)
        {
            var oResult = new JSONParseResult();
            oResult.JSONObject = JsonDecode(sJSON, out var oErrors);
            oResult.JSONErrors = oErrors;
            return oResult;
        }

        /// <summary>
        /// Parses the JSON string into an object
        /// </summary>
        /// <param name="sJSON">A JSON string.</param>
        /// <param name="oErrors">Error and warning metadata output.</param>
        /// <returns>An ArrayList, a Hashtable, a double, a string, null, true, or false</returns>
        public static object JsonDecode(string sJSON, out JSONParseErrors oErrors)
        {
            oErrors = new JSONParseErrors { iErrorIndex = -1, sWarningText = string.Empty };

            if (string.IsNullOrEmpty(sJSON))
            {
                return null;
            }

            int index = 0;
            bool success = true;
            return ParseValue(sJSON, ref index, ref success, ref oErrors);
        }

        private static Hashtable ParseObject(string json, ref int index, ref JSONParseErrors oErrors)
        {
            var htThisObject = new Hashtable();

            NextToken(json, ref index);

            while (true)
            {
                var jtToken = LookAhead(json, index);
                if (jtToken == JSONTokens.NONE)
                {
                    return null;
                }

                if (jtToken == JSONTokens.COMMA)
                {
                    NextToken(json, ref index);
                }
                else if (jtToken == JSONTokens.CURLY_CLOSE)
                {
                    NextToken(json, ref index);
                    return htThisObject;
                }
                else
                {
                    string sName;

                    if (jtToken == JSONTokens.IMPLIED_IDENTIFIER_NAME)
                    {
                        sName = ParseUnquotedIdentifier(json, ref index, ref oErrors);
                        if (sName == null)
                        {
                            if (oErrors.iErrorIndex < 0) oErrors.iErrorIndex = index;
                            return null;
                        }
                    }
                    else
                    {
                        Debug.Assert(jtToken == JSONTokens.STRING, "Unexpected Token type; expecting String/Identifier");
                        sName = ParseString(json, ref index);
                        if (sName == null)
                        {
                            if (oErrors.iErrorIndex < 0) oErrors.iErrorIndex = index;
                            return null;
                        }
                    }

                    jtToken = NextToken(json, ref index);
                    if (jtToken != JSONTokens.COLON)
                    {
                        if (oErrors.iErrorIndex < 0) oErrors.iErrorIndex = index;
                        return null;
                    }

                    bool success = true;
                    object value = ParseValue(json, ref index, ref success, ref oErrors);
                    if (!success)
                    {
                        Debug.Assert(false);
                        oErrors.iErrorIndex = index;
                        return null;
                    }

                    htThisObject[sName] = value;
                }
            }
        }

        private static ArrayList ParseArray(string json, ref int index, ref JSONParseErrors oErrors)
        {
            var alThisArray = new ArrayList();

            NextToken(json, ref index);

            while (true)
            {
                JSONTokens jtToken = LookAhead(json, index);
                if (jtToken == JSONTokens.NONE)
                {
                    if (oErrors.iErrorIndex < 0) oErrors.iErrorIndex = index;
                    return null;
                }

                if (jtToken == JSONTokens.COMMA)
                {
                    NextToken(json, ref index);
                }
                else if (jtToken == JSONTokens.SQUARED_CLOSE)
                {
                    NextToken(json, ref index);
                    break;
                }
                else
                {
                    bool success = true;
                    object value = ParseValue(json, ref index, ref success, ref oErrors);
                    if (!success)
                    {
                        if (oErrors.iErrorIndex < 0) oErrors.iErrorIndex = index;
                        return null;
                    }

                    alThisArray.Add(value);
                }
            }

            return alThisArray;
        }

        private static object ParseValue(string json, ref int index, ref bool success, ref JSONParseErrors oErrors)
        {
            switch (LookAhead(json, index))
            {
                case JSONTokens.IMPLIED_IDENTIFIER_NAME:
                    return ParseUnquotedIdentifier(json, ref index, ref oErrors);

                case JSONTokens.STRING:
                    return ParseString(json, ref index);

                case JSONTokens.NUMBER:
                    return ParseNumber(json, ref index);

                case JSONTokens.CURLY_OPEN:
                    return ParseObject(json, ref index, ref oErrors);

                case JSONTokens.SQUARED_OPEN:
                    return ParseArray(json, ref index, ref oErrors);

                case JSONTokens.TRUE:
                    NextToken(json, ref index);
                    return true;

                case JSONTokens.FALSE:
                    NextToken(json, ref index);
                    return false;

                case JSONTokens.NULL:
                    NextToken(json, ref index);
                    return null;

                case JSONTokens.NONE:
                    break;
            }

            success = false;
            return null;
        }

        private static string ParseUnquotedIdentifier(string json, ref int index, ref JSONParseErrors oErrors)
        {
            EatWhitespace(json, ref index);

            int ixStart = index;
            var s = new StringBuilder(BUILDER_DEFAULT_CAPACITY);

            bool complete = false;
            while (!complete)
            {
                if (index == json.Length)
                {
                    break;
                }

                char c = json[index];
                if (!IsValidIdentifierChar(c))
                {
                    if (s.Length < 1) return null;
                    complete = true;
                    break;
                }

                s.Append(c);
                ++index;
            }

            if (!complete)
            {
                return null;
            }

            string identifier = s.ToString();
            oErrors.sWarningText = $"{oErrors.sWarningText}Illegal/Unquoted identifier '{identifier}' at position {ixStart}.\n";

            return identifier;
        }

        private static string ParseString(string json, ref int index)
        {
            var s = new StringBuilder(BUILDER_DEFAULT_CAPACITY);

            EatWhitespace(json, ref index);

            // Eat opening quote
            index++;

            bool complete = false;
            while (!complete)
            {
                if (index == json.Length)
                {
                    break;
                }

                char c = json[index++];
                if (c == '"')
                {
                    complete = true;
                    break;
                }

                if (c == '\\')
                {
                    if (index == json.Length)
                    {
                        break;
                    }

                    c = json[index++];
                    switch (c)
                    {
                        case '"': s.Append('"'); break;
                        case '\\': s.Append('\\'); break;
                        case '/': s.Append('/'); break;
                        case 'b': s.Append('\b'); break;
                        case 'f': s.Append('\f'); break;
                        case 'n': s.Append('\n'); break;
                        case 'r': s.Append('\r'); break;
                        case 't': s.Append('\t'); break;
                        case 'u':
                            int remainingLength = json.Length - index;
                            if (remainingLength >= 4)
                            {
                                if (ushort.TryParse(json.Substring(index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort codeUnit))
                                {
                                    s.Append((char)codeUnit);
                                }
                                else
                                {
                                    s.Append('\uFFFD');
                                }
                                index += 4;
                            }
                            else
                            {
                                complete = false;
                                goto LoopEnd;
                            }
                            break;
                    }
                }
                else
                {
                    s.Append(c);
                }
            }

        LoopEnd:
            return complete ? s.ToString() : null;
        }

        private static double ParseNumber(string json, ref int index)
        {
            EatWhitespace(json, ref index);

            int lastIndex = GetLastIndexOfNumber(json, index);
            int charLength = (lastIndex - index) + 1;

            string sNumber = json.Substring(index, charLength);
            index = lastIndex + 1;

            return double.Parse(sNumber, CultureInfo.InvariantCulture);
        }

        private static int GetLastIndexOfNumber(string json, int index)
        {
            int lastIndex;
            for (lastIndex = index; lastIndex < json.Length; lastIndex++)
            {
                char c = json[lastIndex];
                if (!IsNumberChar(c))
                {
                    break;
                }
            }
            return lastIndex - 1;
        }

        private static bool IsNumberChar(char c)
        {
            return (c >= '0' && c <= '9') || c == '+' || c == '-' || c == '.' || c == 'e' || c == 'E';
        }

        private static void EatWhitespace(string json, ref int index)
        {
            for (; index < json.Length; index++)
            {
                char c = json[index];
                if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
                {
                    break;
                }
            }
        }

        private static JSONTokens LookAhead(string json, int index)
        {
            int saveIndex = index;
            return NextToken(json, ref saveIndex);
        }

        private static JSONTokens NextToken(string json, ref int index)
        {
            EatWhitespace(json, ref index);

            if (index == json.Length)
            {
                return JSONTokens.NONE;
            }

            char c = json[index];
            ++index;

            switch (c)
            {
                case '{': return JSONTokens.CURLY_OPEN;
                case '}': return JSONTokens.CURLY_CLOSE;
                case '[': return JSONTokens.SQUARED_OPEN;
                case ']': return JSONTokens.SQUARED_CLOSE;
                case ',': return JSONTokens.COMMA;
                case '"': return JSONTokens.STRING;
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                case '-': return JSONTokens.NUMBER;
                case ':': return JSONTokens.COLON;
            }

            --index;
            int remainingLength = json.Length - index;

            if (remainingLength >= 5 && string.Compare(json, index, "false", 0, 5, StringComparison.Ordinal) == 0)
            {
                index += 5;
                return JSONTokens.FALSE;
            }

            if (remainingLength >= 4)
            {
                if (string.Compare(json, index, "true", 0, 4, StringComparison.Ordinal) == 0)
                {
                    index += 4;
                    return JSONTokens.TRUE;
                }

                if (string.Compare(json, index, "null", 0, 4, StringComparison.Ordinal) == 0)
                {
                    index += 4;
                    return JSONTokens.NULL;
                }
            }

            if (IsValidIdentifierStart(json[index]))
            {
                return JSONTokens.IMPLIED_IDENTIFIER_NAME;
            }

            return JSONTokens.NONE;
        }

        private static bool IsValidIdentifierStart(char c) =>
            c == '_' || c == '$' || c == '\'' || char.IsLetter(c);

        private static bool IsValidIdentifierChar(char c) =>
            c == '-' || c == '_' || c == '$' || c == '\'' || char.IsLetterOrDigit(c);

        public static string JsonEncode(object json)
        {
            var builder = new StringBuilder(BUILDER_DEFAULT_CAPACITY);
            bool success = SerializeValue(json, builder);
            return success ? builder.ToString() : null;
        }

        private static bool SerializeObject(IDictionary anObject, StringBuilder builder)
        {
            builder.Append('{');

            IDictionaryEnumerator e = anObject.GetEnumerator();
            bool first = true;
            while (e.MoveNext())
            {
                string key = e.Key.ToString();
                object value = e.Value;

                if (!first)
                {
                    builder.Append(", ");
                }

                SerializeString(key, builder);
                builder.Append(':');
                if (!SerializeValue(value, builder))
                {
                    return false;
                }

                first = false;
            }

            builder.Append('}');
            return true;
        }

        private static bool SerializeArray(IList anArray, StringBuilder builder)
        {
            builder.Append('[');

            bool first = true;
            for (int i = 0; i < anArray.Count; i++)
            {
                object value = anArray[i];

                if (!first)
                {
                    builder.Append(", ");
                }

                if (!SerializeValue(value, builder))
                {
                    return false;
                }

                first = false;
            }

            builder.Append(']');
            return true;
        }

        private static bool SerializeValue(object value, StringBuilder builder)
        {
            switch (value)
            {
                case null:
                    builder.Append("null");
                    break;
                case string str:
                    SerializeString(str, builder);
                    break;
                case Hashtable ht:
                    SerializeObject(ht, builder);
                    break;
                case ArrayList al:
                    SerializeArray(al, builder);
                    break;
                case bool b when b:
                    builder.Append("true");
                    break;
                case bool b when !b:
                    builder.Append("false");
                    break;
                case object num when IsNumeric(num):
                    SerializeNumber(Convert.ToDouble(num, CultureInfo.InvariantCulture), builder);
                    break;
                default:
                    return false;
            }

            return true;
        }

        private static void SerializeString(string aString, StringBuilder builder)
        {
            builder.Append('"');

            for (int i = 0; i < aString.Length; i++)
            {
                char c = aString[i];
                switch (c)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append(@"\\"); break;
                    case '\b': builder.Append(@"\b"); break;
                    case '\f': builder.Append(@"\f"); break;
                    case '\n': builder.Append(@"\n"); break;
                    case '\r': builder.Append(@"\r"); break;
                    case '\t': builder.Append(@"\t"); break;
                    default:
                        int codepoint = c;
                        if (codepoint >= 32 && codepoint <= 126)
                        {
                            builder.Append(c);
                        }
                        else
                        {
                            builder.Append($"\\u{codepoint:x4}");
                        }
                        break;
                }
            }

            builder.Append('"');
        }

        private static void SerializeNumber(double number, StringBuilder builder) =>
            builder.Append(Convert.ToString(number, CultureInfo.InvariantCulture));

        private static bool IsNumeric(object o)
        {
            switch (o)
            {
                case sbyte _:
                case byte _:
                case short _:
                case ushort _:
                case int _:
                case uint _:
                case long _:
                case ulong _:
                case float _:
                case double _:
                case decimal _:
                    return true;
                default:
                    return false;
            }
        }
    }
}