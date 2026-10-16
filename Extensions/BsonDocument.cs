using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Extensions
{
    internal sealed class BsonDocument
    {
        readonly byte[] bytes;
        readonly Encoding utf8 = new UTF8Encoding(false, true);
        int position;

        private BsonDocument(byte[] bytes) { this.bytes = bytes; }

        internal static TreeNode Parse(byte[] bytes)
        {
            var reader = new BsonDocument(bytes);
            TreeNode root = reader.ReadDocument("$", false, bytes.Length, 0);
            if (reader.position != bytes.Length) throw new InvalidDataException("Unexpected bytes after the BSON document.");
            return root;
        }

        private void Require(int count, int end)
        {
            if (count < 0 || position > end - count)
                throw new InvalidDataException("Truncated BSON value at byte " + position + ".");
        }

        private byte ReadByte(int end)
        {
            Require(1, end);
            return bytes[position++];
        }

        private int ReadInt32(int end)
        {
            Require(4, end);
            int value = bytes[position] | bytes[position + 1] << 8 | bytes[position + 2] << 16 | bytes[position + 3] << 24;
            position += 4;
            return value;
        }

        private long ReadInt64(int end)
        {
            uint low = unchecked((uint)ReadInt32(end));
            uint high = unchecked((uint)ReadInt32(end));
            return unchecked((long)((ulong)high << 32 | low));
        }

        private string ReadCString(int end)
        {
            int start = position;
            while (ReadByte(end) != 0) { }
            return utf8.GetString(bytes, start, position - start - 1);
        }

        private string ReadString(int end)
        {
            int length = ReadInt32(end);
            if (length < 1) throw new InvalidDataException("Invalid BSON string length.");
            Require(length, end);
            if (bytes[position + length - 1] != 0) throw new InvalidDataException("BSON string is missing its terminator.");
            string value = utf8.GetString(bytes, position, length - 1);
            position += length;
            return value;
        }

        private string ReadHex(int length, int end)
        {
            Require(length, end);
            string value = BitConverter.ToString(bytes, position, length).Replace("-", "").ToLowerInvariant();
            position += length;
            return value;
        }

        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
        private static string Quoted(string value) => "\"" + DataView.DisplayText(value).Replace("\"", "\\\"") + "\"";

        private TreeNode ReadDocument(string name, bool array, int limit, int depth)
        {
            if (depth >= 128) throw new InvalidDataException("BSON nesting exceeds the supported depth of 128.");
            int start = position;
            int length = ReadInt32(limit);
            if (length < 5 || length > limit - start) throw new InvalidDataException("Invalid BSON document length.");
            int end = start + length;
            var node = new TreeNode(name + (array ? ": []" : ": {}"));
            int index = 0;
            while (position < end - 1)
            {
                byte type = ReadByte(end - 1);
                string key = ReadCString(end - 1);
                if (array && key != Number(index)) throw new InvalidDataException("BSON array keys must be consecutive indices starting at zero.");
                string childName = array ? "[" + Number(index) + "]" : DataView.DisplayText(key);
                node.Nodes.Add(ReadValue(type, childName, end - 1, depth + 1));
                index++;
            }
            if (ReadByte(end) != 0) throw new InvalidDataException("BSON document is missing its terminator.");
            return node;
        }

        private TreeNode ReadValue(byte type, string name, int end, int depth)
        {
            string value;
            switch (type)
            {
                case 0x01:
                    long bits = ReadInt64(end);
                    value = "Double(" + BitConverter.Int64BitsToDouble(bits).ToString("R", CultureInfo.InvariantCulture) + ")";
                    break;
                case 0x02:
                    value = Quoted(ReadString(end));
                    break;
                case 0x03: return ReadDocument(name, false, end, depth);
                case 0x04: return ReadDocument(name, true, end, depth);
                case 0x05:
                    int length = ReadInt32(end);
                    byte subtype = ReadByte(end);
                    Require(length, end);
                    if (subtype == 2)
                    {
                        if (length < 4 || ReadInt32(end) != length - 4) throw new InvalidDataException("Invalid legacy BSON binary length.");
                        length -= 4;
                    }
                    value = "Binary(subtype=0x" + subtype.ToString("x2", CultureInfo.InvariantCulture) + ", hex=" + ReadHex(length, end) + ")";
                    break;
                case 0x06: value = "Undefined"; break;
                case 0x07: value = "ObjectId(" + ReadHex(12, end) + ")"; break;
                case 0x08:
                    byte boolean = ReadByte(end);
                    if (boolean > 1) throw new InvalidDataException("Invalid BSON boolean value.");
                    value = boolean == 1 ? "true" : "false";
                    break;
                case 0x09:
                    value = "DateTime(" + Number(ReadInt64(end)) + " ms since Unix epoch)";
                    break;
                case 0x0a: value = "null"; break;
                case 0x0b:
                    value = "Regex(" + Quoted(ReadCString(end)) + ", " + Quoted(ReadCString(end)) + ")";
                    break;
                case 0x0c:
                    value = "DBPointer(" + Quoted(ReadString(end)) + ", ObjectId(" + ReadHex(12, end) + "))";
                    break;
                case 0x0d: value = "JavaScript(" + Quoted(ReadString(end)) + ")"; break;
                case 0x0e: value = "Symbol(" + Quoted(ReadString(end)) + ")"; break;
                case 0x0f:
                    int start = position;
                    int size = ReadInt32(end);
                    if (size < 14 || size > end - start) throw new InvalidDataException("Invalid BSON code-with-scope length.");
                    int scopeEnd = start + size;
                    var code = new TreeNode(name + ": JavaScript(" + Quoted(ReadString(scopeEnd)) + ")");
                    code.Nodes.Add(ReadDocument("Scope", false, scopeEnd, depth));
                    if (position != scopeEnd) throw new InvalidDataException("Invalid BSON code-with-scope boundary.");
                    return code;
                case 0x10: value = "Int32(" + Number(ReadInt32(end)) + ")"; break;
                case 0x11:
                    uint increment = unchecked((uint)ReadInt32(end));
                    uint seconds = unchecked((uint)ReadInt32(end));
                    value = "Timestamp(seconds=" + Number(seconds) + ", increment=" + Number(increment) + ")";
                    break;
                case 0x12: value = "Int64(" + Number(ReadInt64(end)) + ")"; break;
                // Preserve all 128 bits, including special values and noncanonical encodings.
                case 0x13: value = "Decimal128(little-endian hex=" + ReadHex(16, end) + ")"; break;
                case 0x7f: value = "MaxKey"; break;
                case 0xff: value = "MinKey"; break;
                default: throw new InvalidDataException("Unknown BSON element type 0x" + type.ToString("x2", CultureInfo.InvariantCulture) + ".");
            }
            return new TreeNode(name + ": " + value);
        }
    }
}
