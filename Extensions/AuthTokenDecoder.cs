using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Extensions
{
    internal static class AuthTokenDecoder
    {
        private const string NtlmOid = "1.3.6.1.4.1.311.2.2.10";
        private const string KerberosOid = "1.2.840.113554.1.2.2";
        private const string SpnegoOid = "1.3.6.1.5.5.2";
        private static readonly Encoding Unicode = new UnicodeEncoding(false, false, true);
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        internal static void Append(StringBuilder output, byte[] token)
        {
            try
            {
                DecodeToken(output, token, "", 0);
            }
            catch (FormatException ex)
            {
                output.AppendLine("Malformed authentication token: " + ex.Message);
            }
        }

        private static void DecodeToken(StringBuilder output, byte[] token, string indent, int depth)
        {
            if (depth > 16) throw Invalid("Token nesting limit exceeded.");
            if (token.Length >= 8 && Encoding.ASCII.GetString(token, 0, 8) == "NTLMSSP\0")
            {
                AppendNtlm(output, token, indent);
                return;
            }
            if (token.Length == 0) throw Invalid("The token is empty.");
            if (token.Length >= 3 && token[1] == 0 &&
                ((token[0] == 1 && token[2] == 0x6e) || (token[0] == 2 && token[2] == 0x6f) || (token[0] == 3 && token[2] == 0x7e)))
            {
                AppendKerberos(output, token, indent, depth + 1);
                return;
            }
            int offset = 0;
            Node root = ReadNode(token, ref offset, token.Length);
            if (offset != token.Length) throw Invalid("Trailing data after the ASN.1 token.");
            if (root.Tag == 0x60)
            {
                int position = root.Start;
                Node mechanism = ReadNode(token, ref position, root.End);
                if (mechanism.Tag != 6) throw Invalid("GSS-API token is missing its mechanism OID.");
                string oid = Oid(token, mechanism);
                output.AppendLine(indent + "GSS-API mechanism: " + Mechanism(oid));
                byte[] inner = Slice(token, position, root.End - position);
                if (IsKerberos(oid))
                {
                    AppendKerberos(output, inner, indent + "  ", depth + 1);
                }
                else if (oid == SpnegoOid || oid == NtlmOid)
                {
                    DecodeToken(output, inner, indent + "  ", depth + 1);
                }
                else
                {
                    output.AppendLine(indent + "Mechanism data: " + Hex(inner));
                }
                return;
            }
            if (root.Tag == 0xa0 || root.Tag == 0xa1)
            {
                AppendSpnego(output, token, root, indent, depth + 1);
                return;
            }
            if (root.Tag == 0x6e || root.Tag == 0x6f || root.Tag == 0x7e || root.Tag == 0x61)
                output.AppendLine(indent + "Mechanism: Kerberos");
            else
                output.AppendLine(indent + "ASN.1 token (mechanism not identified):");
            int budget = 4096;
            AppendAsn(output, token, root, indent, "", "", 0, ref budget);
        }

        private static void AppendNtlm(StringBuilder output, byte[] data, string indent)
        {
            uint type = U32(data, 8);
            output.AppendLine(indent + "Mechanism: NTLM");
            output.AppendLine(indent + "Signature: NTLMSSP\\0");
            output.AppendLine(indent + "Message type: " + type + " (" +
                (type == 1 ? "NEGOTIATE" : type == 2 ? "CHALLENGE" : type == 3 ? "AUTHENTICATE" : "Unknown") + ")");
            if (type < 1 || type > 3)
            {
                output.AppendLine(indent + "Message data: " + Hex(Slice(data, 12, data.Length - 12)));
                return;
            }
            int minimum = type == 1 ? 16 : type == 2 ? 32 : 64;
            Require(data, 0, minimum);
            uint flags = U32(data, type == 1 ? 12 : type == 2 ? 20 : 60);
            AppendFlags(output, indent + "Negotiate flags", flags);
            bool unicode = (flags & 1) != 0;
            var buffers = new List<SecurityBuffer>();
            if (type == 1)
            {
                if (data.Length >= 32)
                {
                    buffers.Add(AppendBuffer(output, data, 16, "Domain (OEM)", indent, false));
                    buffers.Add(AppendBuffer(output, data, 24, "Workstation (OEM)", indent, false));
                    minimum = 32;
                }
                else if ((flags & 0x02003000) != 0)
                    throw Invalid("NTLM negotiate message is missing supplied name buffers or version.");
            }
            else if (type == 2)
            {
                SecurityBuffer target = AppendBuffer(output, data, 12, "Target name", indent, unicode);
                buffers.Add(target);
                int headerEnd = target.Length == 0 ? data.Length : target.Offset;
                output.AppendLine(indent + "Server challenge: " + Hex(Slice(data, 24, 8)));
                if (headerEnd >= 40)
                {
                    output.AppendLine(indent + "Reserved/context: " + Hex(Slice(data, 32, 8)));
                    minimum = 40;
                }
                if (headerEnd >= 48)
                {
                    SecurityBuffer info = AppendBuffer(output, data, 40, "Target information", indent, null);
                    buffers.Add(info);
                    if (info.Length != 0) AppendAvPairs(output, Slice(data, info.Offset, info.Length), indent + "  ");
                    minimum = 48;
                }
                else if ((flags & 0x00800000) != 0)
                    throw Invalid("NTLM challenge is missing its target information buffer.");
            }
            else
            {
                SecurityBuffer lm = AppendBuffer(output, data, 12, "LM challenge response", indent, null);
                SecurityBuffer nt = AppendBuffer(output, data, 20, "NT challenge response", indent, null);
                buffers.Add(lm);
                buffers.Add(nt);
                buffers.Add(AppendBuffer(output, data, 28, "Domain", indent, unicode));
                buffers.Add(AppendBuffer(output, data, 36, "Username", indent, unicode));
                buffers.Add(AppendBuffer(output, data, 44, "Workstation", indent, unicode));
                buffers.Add(AppendBuffer(output, data, 52, "Encrypted random session key", indent, null));
                AppendResponses(output, data, lm, nt, indent + "  ", flags);
            }
            if ((flags & 0x02000000) != 0)
            {
                int versionOffset = type == 1 ? 32 : type == 2 ? 48 : 64;
                Require(data, versionOffset, 8);
                output.AppendLine(indent + "Version: " + data[versionOffset] + "." + data[versionOffset + 1] +
                    "." + U16(data, versionOffset + 2) + "; NTLM revision " + data[versionOffset + 7]);
                output.AppendLine(indent + "Version reserved: " + Hex(Slice(data, versionOffset + 4, 3)));
                minimum = versionOffset + 8;
            }
            int payload = data.Length;
            foreach (SecurityBuffer buffer in buffers)
            {
                if (buffer.Length == 0) continue;
                if (buffer.Offset < minimum) throw Invalid("NTLM payload overlaps its message header.");
                payload = Math.Min(payload, buffer.Offset);
            }
            if (payload > minimum)
            {
                // The MIC has no standalone presence flag; NTLMv2 AV flags can confirm it.
                bool micPresent = type == 3 && HasMicFlag(data, buffers[1]);
                if (micPresent)
                {
                    if (payload - minimum < 16) throw Invalid("NTLMv2 flags require a MIC, but it is truncated.");
                    output.AppendLine(indent + "MIC: " + Hex(Slice(data, minimum, 16)));
                    minimum += 16;
                }
                else if (type == 3 && payload - minimum >= 16)
                {
                    output.AppendLine(indent + "Possible MIC (presence not confirmed by NTLMv2 AV flags): " + Hex(Slice(data, minimum, 16)));
                    minimum += 16;
                }
                if (payload > minimum)
                    output.AppendLine(indent + "Header padding/extension: " + Hex(Slice(data, minimum, payload - minimum)));
            }
            else if (type == 3 && HasMicFlag(data, buffers[1]))
                throw Invalid("NTLMv2 flags require a MIC, but it is missing.");
            output.AppendLine(indent + "NTLM OEM names use the local OEM code page; their original code page is not carried in the token.");
        }

        private sealed class SecurityBuffer
        {
            internal int Offset;
            internal int Length;
        }

        private static SecurityBuffer AppendBuffer(StringBuilder output, byte[] data, int position, string name, string indent, bool? unicode)
        {
            int length = U16(data, position);
            int maximum = U16(data, position + 2);
            uint offset = U32(data, position + 4);
            output.AppendLine(indent + name + ": length=" + length + ", maximum=" + maximum + ", offset=" + offset);
            if (maximum < length) throw Invalid(name + " maximum length is smaller than its length.");
            if (length != 0 && (offset > int.MaxValue || offset > data.Length || length > data.Length - (int)offset))
                throw Invalid(name + " points outside the token.");
            var buffer = new SecurityBuffer { Offset = length == 0 ? 0 : (int)offset, Length = length };
            byte[] value = Slice(data, buffer.Offset, length);
            output.AppendLine(indent + "  " + (unicode.HasValue ? Text(value, unicode.Value ? Unicode : Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage)) : Hex(value)));
            return buffer;
        }

        private static void AppendResponses(StringBuilder output, byte[] data, SecurityBuffer lm, SecurityBuffer nt, string indent, uint flags)
        {
            if (nt.Length >= 20 && U32(data, nt.Offset + 16) == 0x00000101)
            {
                byte[] blob = Slice(data, nt.Offset + 16, nt.Length - 16);
                Require(blob, 0, 32);
                output.AppendLine(indent + "Response format: NTLMv2");
                output.AppendLine(indent + "NT proof string: " + Hex(Slice(data, nt.Offset, 16)));
                output.AppendLine(indent + "Response version: " + blob[0] + "; highest response version: " + blob[1]);
                output.AppendLine(indent + "Reserved: " + Hex(Slice(blob, 2, 6)));
                AppendFileTime(output, indent + "Timestamp", blob, 8);
                output.AppendLine(indent + "Client challenge: " + Hex(Slice(blob, 16, 8)));
                output.AppendLine(indent + "Reserved: " + Hex(Slice(blob, 24, 4)));
                int end = AppendAvPairs(output, Slice(blob, 28, blob.Length - 28), indent);
                if (28 + end < blob.Length)
                    output.AppendLine(indent + "NTLMv2 trailing reserved: " + Hex(Slice(blob, 28 + end, blob.Length - 28 - end)));
                if (lm.Length == 24)
                {
                    output.AppendLine(indent + "LMv2 proof/response (may be zeroed): " + Hex(Slice(data, lm.Offset, 16)));
                    output.AppendLine(indent + "LMv2 client challenge (may be zeroed): " + Hex(Slice(data, lm.Offset + 16, 8)));
                }
            }
            else if (nt.Length == 24)
            {
                output.AppendLine(indent + "Response format: NTLMv1" + ((flags & 0x00080000) != 0 ? " with extended session security (NTLM2 session)" : ""));
                if ((flags & 0x00080000) != 0 && lm.Length == 24)
                    output.AppendLine(indent + "Client challenge: " + Hex(Slice(data, lm.Offset, 8)));
            }
            else
                output.AppendLine(indent + "Response format: empty or unrecognized (raw fields shown above).");
        }

        private static bool HasMicFlag(byte[] data, SecurityBuffer nt)
        {
            if (nt.Length < 48 || U32(data, nt.Offset + 16) != 0x00000101) return false;
            int end = nt.Offset + nt.Length;
            int position = nt.Offset + 44;
            while (position <= end - 4)
            {
                int id = U16(data, position);
                int length = U16(data, position + 2);
                position += 4;
                if (length > end - position || id == 0) return false;
                if (id == 6 && length == 4 && (U32(data, position) & 2) != 0) return true;
                position += length;
            }
            return false;
        }

        private static int AppendAvPairs(StringBuilder output, byte[] data, string indent)
        {
            string[] names = { "End of list", "NetBIOS computer name", "NetBIOS domain name", "DNS computer name", "DNS domain name", "DNS tree name", "AV flags", "Timestamp", "Single host data", "Target SPN", "Channel bindings hash" };
            int position = 0;
            while (position < data.Length)
            {
                int id = U16(data, position);
                int length = U16(data, position + 2);
                position += 4;
                byte[] value = Slice(data, position, length);
                position += length;
                string label = indent + "AV " + id + " (" + (id < names.Length ? names[id] : "Unknown") + ")";
                if (id == 0)
                {
                    if (length != 0) throw Invalid("AV end-of-list has a nonzero length.");
                    output.AppendLine(label);
                    if (position < data.Length) output.AppendLine(indent + "AV trailing data: " + Hex(Slice(data, position, data.Length - position)));
                    return position;
                }
                if ((id >= 1 && id <= 5) || id == 9) output.AppendLine(label + ": " + Text(value, Unicode));
                else if (id == 6)
                {
                    if (length != 4) throw Invalid("AV flags must contain four bytes.");
                    uint flags = U32(value, 0);
                    output.AppendLine(label + ": 0x" + flags.ToString("X8") + " (" +
                        ((flags & 1) != 0 ? "constrained authentication; " : "") +
                        ((flags & 2) != 0 ? "MIC present; " : "") +
                        ((flags & 4) != 0 ? "untrusted SPN; " : "") + "unknown bits=0x" + (flags & ~7u).ToString("X8") + ")");
                }
                else if (id == 7)
                {
                    if (length != 8) throw Invalid("AV timestamp must contain eight bytes.");
                    AppendFileTime(output, label, value, 0);
                }
                else if (id == 8 && length == 48)
                {
                    output.AppendLine(label + ": size=" + U32(value, 0) + ", reserved=0x" + U32(value, 4).ToString("X8"));
                    output.AppendLine(indent + "  Custom data: " + Hex(Slice(value, 8, 8)));
                    output.AppendLine(indent + "  Machine ID: " + Hex(Slice(value, 16, 32)));
                }
                else output.AppendLine(label + ": " + Hex(value));
            }
            throw Invalid("Target information is missing its AV end-of-list marker.");
        }

        private static void AppendFlags(StringBuilder output, string label, uint flags)
        {
            string[] names = {
                "UNICODE", "OEM", "REQUEST_TARGET", "RESERVED_3", "SIGN", "SEAL", "DATAGRAM", "LM_KEY",
                "RESERVED_8", "NTLM", "RESERVED_10", "ANONYMOUS", "OEM_DOMAIN_SUPPLIED", "OEM_WORKSTATION_SUPPLIED", "RESERVED_14", "ALWAYS_SIGN",
                "TARGET_TYPE_DOMAIN", "TARGET_TYPE_SERVER", "RESERVED_18", "EXTENDED_SESSIONSECURITY", "IDENTIFY", "RESERVED_21", "REQUEST_NON_NT_SESSION_KEY", "TARGET_INFO",
                "RESERVED_24", "VERSION", "RESERVED_26", "RESERVED_27", "RESERVED_28", "128", "KEY_EXCH", "56"
            };
            var set = new List<string>();
            for (int bit = 0; bit < 32; bit++) if ((flags & (1u << bit)) != 0) set.Add(names[bit]);
            output.AppendLine(label + ": 0x" + flags.ToString("X8") + " (" + string.Join(", ", set) + ")");
        }

        private static void AppendFileTime(StringBuilder output, string label, byte[] data, int offset)
        {
            ulong value = U32(data, offset) | ((ulong)U32(data, offset + 4) << 32);
            string text = value <= (ulong)DateTime.MaxValue.ToFileTimeUtc() ? DateTime.FromFileTimeUtc((long)value).ToString("O", CultureInfo.InvariantCulture) : "out of range";
            output.AppendLine(label + ": " + value + " (" + text + ")");
        }

        private static void AppendSpnego(StringBuilder output, byte[] data, Node root, string indent, int depth)
        {
            bool initial = root.Tag == 0xa0;
            output.AppendLine(indent + "SPNEGO: " + (initial ? "NegTokenInit" : "NegTokenResp"));
            List<Node> sequence = Children(data, root);
            if (sequence.Count != 1 || sequence[0].Tag != 0x30) throw Invalid("SPNEGO token must contain a sequence.");
            foreach (Node field in Children(data, sequence[0]))
            {
                List<Node> values = Children(data, field);
                if (values.Count != 1) throw Invalid("SPNEGO field must contain one value.");
                Node value = values[0];
                if (initial && field.Tag == 0xa0)
                {
                    if (value.Tag != 0x30) throw Invalid("SPNEGO mechanism list is not a sequence.");
                    output.AppendLine(indent + "Offered mechanisms (not proof of selection):");
                    foreach (Node mech in Children(data, value))
                    {
                        if (mech.Tag != 6) throw Invalid("SPNEGO mechanism is not an OID.");
                        output.AppendLine(indent + "  " + Mechanism(Oid(data, mech)));
                    }
                }
                else if (!initial && field.Tag == 0xa1)
                {
                    if (value.Tag != 6) throw Invalid("SPNEGO selected mechanism is not an OID.");
                    output.AppendLine(indent + "Selected mechanism: " + Mechanism(Oid(data, value)));
                }
                else if (!initial && field.Tag == 0xa0)
                {
                    if (value.Tag != 10) throw Invalid("SPNEGO negotiation state is not an ENUMERATED value.");
                    long state = Integer(data, value);
                    string[] states = { "accept-completed", "accept-incomplete", "reject", "request-mic" };
                    output.AppendLine(indent + "Negotiation state: " + state + " (" + (state >= 0 && state < states.Length ? states[(int)state] : "Unknown") + ")");
                }
                else if (field.Tag == 0xa2)
                {
                    if (value.Tag != 4) throw Invalid("SPNEGO mechanism token is not an OCTET STRING.");
                    output.AppendLine(indent + (initial ? "Optimistic mechanism token:" : "Response mechanism token:"));
                    if (value.Length == 0) output.AppendLine(indent + "  (empty)");
                    else DecodeToken(output, Slice(data, value.Start, value.Length), indent + "  ", depth + 1);
                }
                else
                {
                    string label = initial && field.Tag == 0xa1 ? "Request flags" :
                        field.Tag == 0xa3 && value.Tag == 0x30 ? "Negotiation hints" :
                        field.Tag == 0xa3 || field.Tag == 0xa4 ? "Mechanism list MIC" : "Unknown SPNEGO field";
                    int budget = 4096;
                    string schema = label == "Negotiation hints" ? "NegHints" : label == "Request flags" ? "RequestFlags" : "";
                    AppendAsn(output, data, value, indent + "  ", schema, label, 0, ref budget);
                }
            }
        }

        private static void AppendKerberos(StringBuilder output, byte[] token, string indent, int depth)
        {
            Require(token, 0, 2);
            int id = token[0] << 8 | token[1];
            string name = id == 0x0100 ? "AP-REQ" : id == 0x0200 ? "AP-REP" : id == 0x0300 ? "KRB-ERROR" :
                id == 0x0404 ? "RFC 4121 MIC" : id == 0x0504 ? "RFC 4121 Wrap" :
                id == 0x0101 ? "RFC 1964 MIC" : id == 0x0201 ? "RFC 1964 Wrap" : "Unknown";
            output.AppendLine(indent + "Kerberos token ID: 0x" + id.ToString("X4") + " (" + name + ")");
            if (id == 0x0100 || id == 0x0200 || id == 0x0300)
                DecodeToken(output, Slice(token, 2, token.Length - 2), indent, depth + 1);
            else output.AppendLine(indent + "Mechanism data: " + Hex(Slice(token, 2, token.Length - 2)));
        }

        private sealed class Node
        {
            internal int Tag;
            internal int Start;
            internal int Length;
            internal int End => Start + Length;
        }

        private static Node ReadNode(byte[] data, ref int offset, int end)
        {
            if (offset >= end) throw Invalid("Truncated ASN.1 tag.");
            int tag = data[offset++];
            if ((tag & 31) == 31) throw Invalid("High-tag-number ASN.1 encoding is not supported.");
            if (offset >= end) throw Invalid("Truncated ASN.1 length.");
            int length = data[offset++];
            if (length >= 128)
            {
                int count = length & 127;
                if (count == 0) throw Invalid("Indefinite ASN.1 lengths are not valid DER.");
                if (count > 4 || count > end - offset) throw Invalid("Invalid ASN.1 length.");
                long result = 0;
                for (int i = 0; i < count; i++) result = (result << 8) | data[offset++];
                if (result > int.MaxValue) throw Invalid("ASN.1 length is too large.");
                length = (int)result;
            }
            if (length > end - offset) throw Invalid("ASN.1 value extends beyond its container.");
            var node = new Node { Tag = tag, Start = offset, Length = length };
            offset += length;
            return node;
        }

        private static List<Node> Children(byte[] data, Node parent)
        {
            if ((parent.Tag & 32) == 0) throw Invalid("Expected constructed ASN.1 value.");
            var children = new List<Node>();
            int offset = parent.Start;
            while (offset < parent.End)
            {
                if (children.Count == 4096) throw Invalid("ASN.1 field count limit exceeded.");
                children.Add(ReadNode(data, ref offset, parent.End));
            }
            return children;
        }

        private static void AppendAsn(StringBuilder output, byte[] data, Node node, string indent, string schema, string label, int depth, ref int budget)
        {
            if (depth > 32 || --budget < 0) throw Invalid("ASN.1 complexity limit exceeded.");
            string application = Application(node.Tag);
            if (application != null)
            {
                schema = application;
                label = application;
                if (application == "AP-REQ")
                    output.AppendLine(indent + "Client username/domain are normally in the encrypted ticket/authenticator and cannot be recovered without decryption keys.");
            }
            string prefix = indent + (label.Length == 0 ? "ASN.1 tag 0x" + node.Tag.ToString("X2") : label);
            if ((node.Tag & 32) != 0)
            {
                List<Node> children = Children(data, node);
                if ((node.Tag & 0xe0) == 0xa0 && children.Count == 1 && (children[0].Tag & 32) == 0)
                {
                    AppendAsn(output, data, children[0], indent, schema, label, depth + 1, ref budget);
                    return;
                }
                output.AppendLine(prefix + ":");
                if (schema == "PrincipalName" && node.Tag == 0x30)
                {
                    var parts = new List<string>();
                    foreach (Node field in children)
                        if (field.Tag == 0xa1)
                            foreach (Node names in Children(data, field))
                                foreach (Node part in Children(data, names)) parts.Add(AsnString(data, part));
                    if (parts.Count != 0) output.AppendLine(indent + "  Principal: " + string.Join("/", parts));
                }
                foreach (Node child in children)
                {
                    string childSchema = schema;
                    string childLabel = "";
                    if ((child.Tag & 0xc0) == 0x80)
                        Field(schema, child.Tag & 31, out childLabel, out childSchema);
                    AppendAsn(output, data, child, indent + "  ", childSchema, childLabel, depth + 1, ref budget);
                }
                return;
            }
            string value;
            if (node.Tag == 6) value = Mechanism(Oid(data, node));
            else if (node.Tag == 2 || node.Tag == 10)
            {
                long number = Integer(data, node);
                value = number.ToString(CultureInfo.InvariantCulture);
                if (schema == "EncryptionType") value += " (" + EncryptionType(number) + ")";
            }
            else if (node.Tag == 3)
            {
                if (node.Length == 0 || data[node.Start] > 7 || (node.Length == 1 && data[node.Start] != 0))
                    throw Invalid("Invalid ASN.1 BIT STRING.");
                value = "unused bits=" + data[node.Start] + ", bits=" + Hex(Slice(data, node.Start + 1, node.Length - 1));
                value += BitFlags(data, node, schema);
            }
            else if (node.Tag == 12 || node.Tag == 19 || node.Tag == 20 || node.Tag == 22 || node.Tag == 23 || node.Tag == 24 || node.Tag == 27 || node.Tag == 30)
                value = AsnString(data, node);
            else
            {
                value = Hex(Slice(data, node.Start, node.Length));
                if (schema == "Cipher") value += " (encrypted; decryption keys required)";
            }
            output.AppendLine(prefix + ": " + value);
        }

        private static string BitFlags(byte[] data, Node node, string schema)
        {
            string[] names;
            switch (schema)
            {
                case "RequestFlags": names = new[] { "delegation", "mutual", "replay", "sequence", "anonymous", "confidentiality", "integrity" }; break;
                case "ApOptions": names = new[] { "reserved", "use-session-key", "mutual-required" }; break;
                case "TicketFlags": names = new[] { "reserved", "forwardable", "forwarded", "proxiable", "proxy", "may-postdate", "postdated", "invalid", "renewable", "initial", "pre-authenticated", "hardware-authenticated", "transited-policy-checked", "ok-as-delegate", "anonymous", "enc-pa-rep" }; break;
                case "KdcOptions": names = new[] { "reserved", "forwardable", "forwarded", "proxiable", "proxy", "allow-postdate", "postdated", "unused", "renewable", "unused", "unused", "opt-hardware-auth", "unused", "unused", "canonicalize", "cname-in-addl-tkt", "unused", "unused", "unused", "unused", "unused", "unused", "unused", "unused", "unused", "unused", "disable-transited-check", "renewable-ok", "enc-tkt-in-skey", "unused", "renew", "validate" }; break;
                default: return "";
            }
            var flags = new List<string>();
            int bits = (node.Length - 1) * 8 - data[node.Start];
            for (int bit = 0; bit < bits; bit++)
                if ((data[node.Start + 1 + bit / 8] & (0x80 >> (bit % 8))) != 0)
                    flags.Add(bit < names.Length ? names[bit] : "unknown-bit-" + bit);
            return " (" + string.Join(", ", flags) + ")";
        }

        private static string Application(int tag)
        {
            switch (tag)
            {
                case 0x61: return "Ticket";
                case 0x62: return "Authenticator";
                case 0x63: return "EncTicketPart";
                case 0x6a: return "AS-REQ";
                case 0x6b: return "AS-REP";
                case 0x6c: return "TGS-REQ";
                case 0x6d: return "TGS-REP";
                case 0x6e: return "AP-REQ";
                case 0x6f: return "AP-REP";
                case 0x74: return "KRB-SAFE";
                case 0x75: return "KRB-PRIV";
                case 0x76: return "KRB-CRED";
                case 0x7e: return "KRB-ERROR";
                default: return null;
            }
        }

        private static void Field(string schema, int field, out string label, out string childSchema)
        {
            string[] fields;
            switch (schema)
            {
                case "Ticket": fields = new[] { "Ticket version", "Realm", "Service principal|PrincipalName", "Encrypted ticket|EncryptedData" }; break;
                case "PrincipalName": fields = new[] { "Name type", "Name components" }; break;
                case "EncryptedData": fields = new[] { "Encryption type|EncryptionType", "Key version", "Ciphertext|Cipher" }; break;
                case "AP-REQ": fields = new[] { "Protocol version", "Message type", "AP options|ApOptions", "Ticket|Ticket", "Encrypted authenticator|EncryptedData" }; break;
                case "AP-REP": fields = new[] { "Protocol version", "Message type", "Encrypted reply|EncryptedData" }; break;
                case "KRB-ERROR": fields = new[] { "Protocol version", "Message type", "Client time", "Client microseconds", "Server time", "Server microseconds", "Error code", "Client realm", "Client principal|PrincipalName", "Service realm", "Service principal|PrincipalName", "Error text", "Error data" }; break;
                case "AS-REQ":
                case "TGS-REQ": fields = new[] { "[0]", "Protocol version", "Message type", "Pre-authentication data|PA-DATA", "Request body|KDC-REQ-BODY" }; break;
                case "AS-REP":
                case "TGS-REP": fields = new[] { "Protocol version", "Message type", "Pre-authentication data|PA-DATA", "Client realm", "Client principal|PrincipalName", "Ticket|Ticket", "Encrypted reply|EncryptedData" }; break;
                case "KDC-REQ-BODY": fields = new[] { "KDC options|KdcOptions", "Client principal|PrincipalName", "Realm", "Service principal|PrincipalName", "From", "Till", "Renew till", "Nonce", "Encryption types|EncryptionType", "Addresses|HostAddress", "Encrypted authorization data|EncryptedData", "Additional tickets|Ticket" }; break;
                case "PA-DATA": fields = new[] { "[0]", "Pre-authentication type", "Pre-authentication value" }; break;
                case "HostAddress": fields = new[] { "Address type", "Address" }; break;
                case "Authenticator": fields = new[] { "Authenticator version", "Client realm", "Client principal|PrincipalName", "Checksum|Checksum", "Client microseconds", "Client time", "Subkey|EncryptionKey", "Sequence number", "Authorization data|AuthorizationData" }; break;
                case "EncTicketPart": fields = new[] { "Ticket flags|TicketFlags", "Key|EncryptionKey", "Client realm", "Client principal|PrincipalName", "Transited|TransitedEncoding", "Authentication time", "Start time", "End time", "Renew till", "Client addresses|HostAddress", "Authorization data|AuthorizationData" }; break;
                case "EncryptionKey": fields = new[] { "Key type|EncryptionType", "Key value" }; break;
                case "Checksum": fields = new[] { "Checksum type", "Checksum value" }; break;
                case "AuthorizationData": fields = new[] { "Authorization data type", "Authorization data value" }; break;
                case "TransitedEncoding": fields = new[] { "Transited type", "Contents" }; break;
                case "KRB-CRED": fields = new[] { "Protocol version", "Message type", "Tickets|Ticket", "Encrypted part|EncryptedData" }; break;
                case "KRB-PRIV": fields = new[] { "Protocol version", "Message type", "[2]", "Encrypted part|EncryptedData" }; break;
                case "KRB-SAFE": fields = new[] { "Protocol version", "Message type", "Safe body|KRB-SAFE-BODY", "Checksum|Checksum" }; break;
                case "KRB-SAFE-BODY": fields = new[] { "User data", "Timestamp", "Microseconds", "Sequence number", "Sender address|HostAddress", "Recipient address|HostAddress" }; break;
                case "NegHints": fields = new[] { "Hint name", "Hint address" }; break;
                default: fields = new string[0]; break;
            }
            string entry = field < fields.Length ? fields[field] : "Context [" + field + "]";
            string[] parts = entry.Split('|');
            label = parts[0];
            childSchema = parts.Length > 1 ? parts[1] : "";
        }

        private static string EncryptionType(long value)
        {
            switch (value)
            {
                case 0: return "none";
                case 1: return "des-cbc-crc";
                case 3: return "des-cbc-md5";
                case 16: return "des3-cbc-sha1";
                case 17: return "aes128-cts-hmac-sha1-96";
                case 18: return "aes256-cts-hmac-sha1-96";
                case 19: return "aes128-cts-hmac-sha256-128";
                case 20: return "aes256-cts-hmac-sha384-192";
                case 23: return "rc4-hmac";
                case 24: return "rc4-hmac-exp";
                default: return "Unknown";
            }
        }

        private static string Mechanism(string oid)
        {
            return (oid == NtlmOid ? "NTLM" : IsKerberos(oid) ? "Kerberos" : oid == SpnegoOid ? "SPNEGO (Negotiate)" :
                oid == "1.3.6.1.4.1.311.2.2.30" ? "NEGOEX" : "Unknown mechanism") + " (" + oid + ")";
        }

        private static bool IsKerberos(string oid) => oid == KerberosOid || oid == "1.2.840.48018.1.2.2" || oid == "1.3.6.1.5.2.5";

        private static string Oid(byte[] data, Node node)
        {
            var parts = new List<string>();
            ulong value = 0;
            bool pending = false;
            for (int i = node.Start; i < node.End; i++)
            {
                if (value > (ulong.MaxValue >> 7)) throw Invalid("OID component overflow.");
                value = (value << 7) | (uint)(data[i] & 127);
                pending = (data[i] & 128) != 0;
                if (pending) continue;
                if (parts.Count == 0)
                {
                    parts.Add(value < 40 ? "0" : value < 80 ? "1" : "2");
                    parts.Add((value < 40 ? value : value < 80 ? value - 40 : value - 80).ToString(CultureInfo.InvariantCulture));
                }
                else parts.Add(value.ToString(CultureInfo.InvariantCulture));
                value = 0;
            }
            if (pending || parts.Count == 0) throw Invalid("Truncated or empty OID.");
            return string.Join(".", parts);
        }

        private static long Integer(byte[] data, Node node)
        {
            if (node.Length == 0 || node.Length > 8) throw Invalid("ASN.1 integer is empty or exceeds 64 bits.");
            long value = (data[node.Start] & 128) != 0 ? -1 : 0;
            for (int i = node.Start; i < node.End; i++) value = (value << 8) | data[i];
            return value;
        }

        private static string AsnString(byte[] data, Node node)
        {
            Encoding encoding = node.Tag == 30 ? new UnicodeEncoding(true, false, true) : Utf8;
            return Text(Slice(data, node.Start, node.Length), encoding);
        }

        private static string Text(byte[] value, Encoding encoding)
        {
            string decoded;
            try { decoded = encoding.GetString(value); }
            catch (DecoderFallbackException) { throw Invalid("Invalid " + encoding.WebName + " text."); }
            var result = new StringBuilder();
            foreach (char c in decoded)
                if (char.IsControl(c)) result.Append("\\u" + ((int)c).ToString("X4"));
                else result.Append(c);
            return result.ToString();
        }

        private static ushort U16(byte[] data, int offset)
        {
            Require(data, offset, 2);
            return (ushort)(data[offset] | data[offset + 1] << 8);
        }

        private static uint U32(byte[] data, int offset)
        {
            Require(data, offset, 4);
            return (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24);
        }

        private static void Require(byte[] data, int offset, int length)
        {
            if (offset < 0 || length < 0 || offset > data.Length || length > data.Length - offset)
                throw Invalid("Truncated field or invalid buffer offset/length.");
        }

        private static byte[] Slice(byte[] data, int offset, int length)
        {
            Require(data, offset, length);
            var result = new byte[length];
            Buffer.BlockCopy(data, offset, result, 0, length);
            return result;
        }

        private static string Hex(byte[] data) => data.Length == 0 ? "(empty)" : BitConverter.ToString(data).Replace("-", " ");
        private static FormatException Invalid(string message) => new FormatException(message);
    }
}
