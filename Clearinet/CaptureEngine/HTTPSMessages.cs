using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Clearinet.CaptureEngine
{
    internal static class HTTPSMessages
    {
        // How many bytes should we sniff from the start of a TLS connection
        // to try to extract the ClientHello or ServerHello? In most cases,
        // I expect 64kb should be plenty.
        private const int TunnelSniffBytes = 64 * 1024;

        internal static bool TryBuildTlsClientHelloSummary(byte[] sniffBuffer, int sniffLength, out string summary, out string sniffedSNI)
        {
            summary = null;
            sniffedSNI = null;
            if (IsClearlyNotTls(sniffBuffer, sniffLength)) return false;
            if (!TryExtractTlsHandshakeMessage(sniffBuffer, sniffLength, 1, out byte[] body)) return false;
            return TryDescribeClientHello(body, out summary, out sniffedSNI);
        }

        internal static bool TryBuildTlsServerHelloSummary(byte[] sniffBuffer, int sniffLength, out string summary)
        {
            summary = null;
            if (IsClearlyNotTls(sniffBuffer, sniffLength)) return false;
            if (!TryExtractTlsHandshakeMessage(sniffBuffer, sniffLength, 2, out byte[] body)) return false;
            return TryDescribeServerHello(body, out summary);
        }

        /// <summary>
        /// Are the bytes clearly not the start of TLS?
        /// </summary>
        private static bool IsClearlyNotTls(byte[] data, int dataLength)
        {
            Debug.Assert(dataLength > 0 && dataLength <= data.Length, "dataLength is out of bounds");
            if (!data.HasData()) return false;

            // 1) TLS record content type: Handshake
            if (data[0] != 0x16) return true;

            int safeLength = Math.Min(dataLength, data.Length);
            // 2) TLS record version major byte
            if (safeLength < 2) return false;
            if (data[1] != 0x03) return true;

            // 3) TLS record version minor byte (SSL3..TLS1.3)
            if (safeLength < 3) return false;
            if (data[2] > 0x04) return true;

            return false;
        }

        private static bool TryExtractTlsHandshakeMessage(byte[] data, int dataLength, byte handshakeType, out byte[] body)
        {
            body = null;
            if (data == null || dataLength < 5) return false;

            int safeLength = Math.Min(dataLength, data.Length);
            byte[] handshakeBuffer = new byte[Math.Min(safeLength, TunnelSniffBytes)];
            int handshakeLength = 0;
            int offset = 0;

            while (offset + 5 <= safeLength)
            {
                byte contentType = data[offset];
                int recordLength = ReadUInt16(data, offset + 3);
                offset += 5;

                if (recordLength < 0 || offset + recordLength > safeLength)
                {
                    break;
                }

                if (contentType == 22)
                {
                    if (handshakeLength + recordLength > handshakeBuffer.Length)
                    {
                        int newLength = Math.Min(TunnelSniffBytes, Math.Max(handshakeLength + recordLength, handshakeBuffer.Length * 2));
                        if (newLength > handshakeBuffer.Length) Array.Resize(ref handshakeBuffer, newLength);
                    }

                    int copied = Math.Min(recordLength, handshakeBuffer.Length - handshakeLength);
                    if (copied > 0)
                    {
                        Buffer.BlockCopy(data, offset, handshakeBuffer, handshakeLength, copied);
                        handshakeLength += copied;
                    }

                    int parseOffset = 0;
                    while (parseOffset + 4 <= handshakeLength)
                    {
                        byte messageType = handshakeBuffer[parseOffset];
                        int messageLength = (handshakeBuffer[parseOffset + 1] << 16)
                                            | (handshakeBuffer[parseOffset + 2] << 8)
                                            | handshakeBuffer[parseOffset + 3];
                        if (parseOffset + 4 + messageLength > handshakeLength) break;

                        if (messageType == handshakeType)
                        {
                            body = new byte[messageLength];
                            Buffer.BlockCopy(handshakeBuffer, parseOffset + 4, body, 0, messageLength);
                            return true;
                        }

                        parseOffset += 4 + messageLength;
                    }

                    if (parseOffset > 0)
                    {
                        int remaining = handshakeLength - parseOffset;
                        if (remaining > 0) Buffer.BlockCopy(handshakeBuffer, parseOffset, handshakeBuffer, 0, remaining);
                        handshakeLength = remaining;
                    }
                }

                offset += recordLength;
            }

            return false;
        }

        private static bool TryDescribeClientHello(byte[] body, out string summary, out string sniffedSNI)
        {
            summary = null;
            sniffedSNI = null;
            if (body == null || body.Length < 34) return false;

            int offset = 0;
            ushort legacyVersion = ReadUInt16(body, offset);
            offset += 2 + 32;
            if (offset > body.Length) return false;

            if (!TryReadVector8(body, ref offset, out byte[] sessionId)) return false;
            if (!TryReadVector16(body, ref offset, out byte[] cipherSuitesRaw)) return false;
            if ((cipherSuitesRaw.Length & 1) != 0) return false;
            if (!TryReadVector8(body, ref offset, out byte[] compressionMethods)) return false;

            var sb = new StringBuilder();
            sb.AppendLine("TLS ClientHello");
            sb.AppendLine($"LegacyVersion: {DescribeTlsVersion(legacyVersion)}");
            sb.AppendLine($"SessionIdLength: {sessionId.Length}");
            sb.AppendLine($"CipherSuites: {cipherSuitesRaw.Length / 2}");
            for (int i = 0; i + 1 < cipherSuitesRaw.Length; i += 2)
            {
                ushort suite = ReadUInt16(cipherSuitesRaw, i);
                sb.AppendLine($"  - {DescribeCipherSuite(suite)}");
            }

            sb.AppendLine($"CompressionMethods: {compressionMethods.Length}");
            for (int i = 0; i < compressionMethods.Length; i++)
            {
                sb.AppendLine($"  - {DescribeCompressionMethod(compressionMethods[i])}");
            }

            if (offset < body.Length)
            {
                if (!TryReadVector16(body, ref offset, out byte[] extensionsRaw)) return false;
                if (!TryDescribeExtensions(extensionsRaw, true, sb, out sniffedSNI)) return false;
            }

            summary = sb.ToString();
            return true;
        }

        private static bool TryDescribeServerHello(byte[] body, out string summary)
        {
            summary = null;
            if (body == null || body.Length < 38) return false;

            int offset = 0;
            ushort legacyVersion = ReadUInt16(body, offset);
            offset += 2 + 32;
            if (offset > body.Length) return false;

            if (!TryReadVector8(body, ref offset, out byte[] sessionIdEcho)) return false;
            if (offset + 3 > body.Length) return false;

            ushort cipherSuite = ReadUInt16(body, offset);
            offset += 2;
            byte compressionMethod = body[offset++];

            var sb = new StringBuilder();
            sb.AppendLine("TLS ServerHello");
            sb.AppendLine($"LegacyVersion: {DescribeTlsVersion(legacyVersion)}");
            sb.AppendLine($"SessionIdEchoLength: {sessionIdEcho.Length}");
            sb.AppendLine($"CipherSuite: {DescribeCipherSuite(cipherSuite)}");
            sb.AppendLine($"CompressionMethod: {DescribeCompressionMethod(compressionMethod)}");

            if (offset < body.Length)
            {
                if (!TryReadVector16(body, ref offset, out byte[] extensionsRaw)) return false;
                if (!TryDescribeExtensions(extensionsRaw, false, sb, out _)) return false;
            }

            summary = sb.ToString();
            return true;
        }

        private static bool TryDescribeExtensions(byte[] extensionsRaw, bool fromClientHello, StringBuilder sb, out string sniffedSNI)
        {
            sniffedSNI = null;
            int offset = 0;
            int extensionCount = 0;
            var extensionsBlock = new StringBuilder();

            while (offset < extensionsRaw.Length)
            {
                if (offset + 4 > extensionsRaw.Length) return false;
                ushort extensionType = ReadUInt16(extensionsRaw, offset);
                ushort extensionLength = ReadUInt16(extensionsRaw, offset + 2);
                offset += 4;
                if (offset + extensionLength > extensionsRaw.Length) return false;

                string detail = DescribeExtension(extensionType, extensionsRaw, offset, extensionLength, fromClientHello);
                extensionsBlock.AppendLine($"  - {DescribeExtensionType(extensionType)}: {detail}");
                if (extensionType == 0) // server_name
                {
                    sniffedSNI = detail;
                }
                extensionCount++;

                offset += extensionLength;
            }

            sb.AppendLine($"Extensions: {extensionCount}");
            sb.Append(extensionsBlock);
            return true;
        }

        private static string DescribeExtension(ushort extensionType, byte[] source, int offset, int length, bool fromClientHello)
        {
            try
            {
                switch (extensionType)
                {
                    case 0: // server_name
                        return DescribeServerNameExtension(source, offset, length);
                    case 10: // supported_groups
                        return DescribeSupportedGroupsExtension(source, offset, length);
                    case 11: // ec_point_formats
                        return DescribePointFormatsExtension(source, offset, length);
                    case 13: // signature_algorithms
                        return DescribeSignatureAlgorithmsExtension(source, offset, length);
                    case 16: // application_layer_protocol_negotiation
                        return DescribeAlpnExtension(source, offset, length);
                    case 5: // status_request
                        return DescribeStatusRequestExtension(source, offset, length);
                    case 28: // record_size_limit
                        return DescribeRecordSizeLimitExtension(source, offset, length);
                    case 34: // delegated_credential
                        return DescribeDelegatedCredentialExtension(source, offset, length);
                    case 43: // supported_versions
                        return DescribeSupportedVersionsExtension(source, offset, length, fromClientHello);
                    case 45: // psk_key_exchange_modes
                        return DescribePskModesExtension(source, offset, length);
                    case 51: // key_share
                        return DescribeKeyShareExtension(source, offset, length, fromClientHello);
                    case 0x1b: // compress_certificate
                        return DescribeCompressCertificateExtension(source, offset, length);
                    case 0xfe0d: // encrypted_client_hello
                        return DescribeEncryptedClientHelloExtension(source, offset, length);
                }
            }
            catch
            {
                return $"{length} bytes";
            }

            return $"{length} bytes";
        }

        private static string DescribeServerNameExtension(byte[] source, int offset, int length)
        {
            if (length < 2) return $"{length} bytes";
            int listLength = ReadUInt16(source, offset);
            int position = offset + 2;
            int end = Math.Min(offset + length, position + listLength);

            var sb = new StringBuilder();
            while (position + 3 <= end)
            {
                byte nameType = source[position++];
                int nameLength = ReadUInt16(source, position);
                position += 2;
                if (position + nameLength > end) break;

                if (sb.Length > 0) sb.Append(", ");
                string host = Encoding.ASCII.GetString(source, position, nameLength);
                sb.Append(nameType == 0 ? host : $"type {nameType}:{host}");
                position += nameLength;
            }

            return sb.Length > 0 ? sb.ToString() : $"{length} bytes";
        }

        private static string DescribeDelegatedCredentialExtension(byte[] source, int offset, int length)
        {
            if (length < 2) return $"{length} bytes";
            int listLength = ReadUInt16(source, offset);
            int position = offset + 2;
            int end = Math.Min(offset + length, position + listLength);

            var sb = new StringBuilder();
            while (position + 1 < end)
            {
                ushort algorithm = ReadUInt16(source, position);
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(DescribeSignatureAlgorithm(algorithm));
                position += 2;
            }

            return $"{length} bytes";
        }

        private static string DescribeStatusRequestExtension(byte[] source, int offset, int length)
        {
            if (length < 5) return $"{length} bytes";

            byte statusType = source[offset];
            int responderIdListLength = ReadUInt16(source, offset + 1);
            int position = offset + 3;
            int end = offset + length;
            if (position + responderIdListLength > end) return $"{length} bytes";

            int responderIdCount = 0;
            int responderIdsEnd = position + responderIdListLength;
            while (position + 2 <= responderIdsEnd)
            {
                int responderIdLength = ReadUInt16(source, position);
                position += 2;
                if (position + responderIdLength > responderIdsEnd) return $"{length} bytes";
                position += responderIdLength;
                responderIdCount++;
            }

            if (position != responderIdsEnd || position + 2 > end) return $"{length} bytes";

            int requestExtensionsLength = ReadUInt16(source, position);
            position += 2;
            if (position + requestExtensionsLength > end) return $"{length} bytes";

            return $"{DescribeStatusType(statusType)}, responder_ids={responderIdCount}, request_extensions={requestExtensionsLength} bytes";
        }

        private static string DescribeCompressCertificateExtension(byte[] source, int offset, int length)
        {
            if (length < 1) return $"{length} bytes";

            int listLength = source[offset];
            int position = offset + 1;
            int end = Math.Min(offset + length, position + listLength);

            var sb = new StringBuilder();
            while (position + 1 < end)
            {
                ushort algorithm = ReadUInt16(source, position);
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(DescribeCertificateCompressionAlgorithm(algorithm));
                position += 2;
            }

            return $"{length} bytes";
        }

        private static string DescribeEncryptedClientHelloExtension(byte[] source, int offset, int length)
        {
            if (length < 1) return $"{length} bytes";

            byte clientHelloType = source[offset];
            switch (clientHelloType)
            {
                case 0:
                    {
                        int position = offset + 1;
                        int end = offset + length;
                        if (position + 7 > end)
                        {
                            return $"outer (truncated, {length.ToString("N0", CultureInfo.InvariantCulture)} bytes)";
                        }

                        ushort kdfId = ReadUInt16(source, position);
                        position += 2;
                        ushort aeadId = ReadUInt16(source, position);
                        position += 2;
                        byte configId = source[position++];

                        ushort encLength = ReadUInt16(source, position);
                        position += 2;
                        if (position + encLength > end)
                        {
                            return $"outer: config_id=0x{configId:x2}, kdf={DescribeHpkeKdfId(kdfId)}, aead={DescribeHpkeAeadId(aeadId)}, enc={encLength.ToString("N0", CultureInfo.InvariantCulture)} bytes (truncated)";
                        }
                        position += encLength;

                        if (position + 2 > end)
                        {
                            return $"outer: config_id=0x{configId:x2}, kdf={DescribeHpkeKdfId(kdfId)}, aead={DescribeHpkeAeadId(aeadId)}, enc={encLength.ToString("N0", CultureInfo.InvariantCulture)} bytes, payload=(missing length)";
                        }

                        ushort payloadLength = ReadUInt16(source, position);
                        position += 2;
                        if (position + payloadLength > end)
                        {
                            return $"outer: config_id=0x{configId:x2}, kdf={DescribeHpkeKdfId(kdfId)}, aead={DescribeHpkeAeadId(aeadId)}, enc={encLength.ToString("N0", CultureInfo.InvariantCulture)} bytes, payload={payloadLength.ToString("N0", CultureInfo.InvariantCulture)} bytes (truncated)";
                        }

                        return $"outer: config_id=0x{configId:x2}, kdf={DescribeHpkeKdfId(kdfId)}, aead={DescribeHpkeAeadId(aeadId)}, enc={encLength.ToString("N0", CultureInfo.InvariantCulture)} bytes, payload={payloadLength.ToString("N0", CultureInfo.InvariantCulture)} bytes";
                    }
                case 1:
                    if (length == 1) return "inner (empty payload)";
                    return $"inner: ({(length - 1).ToString("N0", CultureInfo.InvariantCulture)} payload bytes)";
                default: return $"type=0x{clientHelloType:x2}, {length.ToString("N0", CultureInfo.InvariantCulture)} bytes";
            }
        }

        private static string DescribeHpkeKdfId(ushort value)
        {
            switch (value)
            {
                case 0x0001: return "HKDF-SHA256 (0x0001)";
                case 0x0002: return "HKDF-SHA384 (0x0002)";
                case 0x0003: return "HKDF-SHA512 (0x0003)";
                default: return $"0x{value:x4}";
            }
        }

        private static string DescribeHpkeAeadId(ushort value)
        {
            switch (value)
            {
                case 0x0001: return "AES-128-GCM (0x0001)";
                case 0x0002: return "AES-256-GCM (0x0002)";
                case 0x0003: return "ChaCha20Poly1305 (0x0003)";
                case 0xFFFF: return "ExportOnly (0xFFFF)";
                default: return $"0x{value:x4}";
            }
        }

        private static string DescribeRecordSizeLimitExtension(byte[] source, int offset, int length)
        {
            if (length != 2) return $"{length} bytes";
            ushort limit = ReadUInt16(source, offset);
            return $"{limit.ToString("N0", CultureInfo.InvariantCulture)} bytes (0x{limit:x4})";
        }

        private static string DescribeStatusType(byte value)
        {
            switch (value)
            {
                case 1: return "ocsp (0x1)";
                case 2: return "ocsp_multi (0x2)";
                default: return $"0x{value:X2}";
            }
        }

        private static string DescribeCertificateCompressionAlgorithm(ushort value)
        {
            switch (value)
            {
                case 1: return "zlib (0x1)";
                case 2: return "brotli (0x2)";
                case 3: return "zstd (0x3)";
                default: return $"0x{value:X4}";
            }
        }

        private static string DescribeSupportedGroupsExtension(byte[] source, int offset, int length)
        {
            if (length < 2) return $"{length} bytes";
            int listLength = ReadUInt16(source, offset);
            int position = offset + 2;
            int end = Math.Min(offset + length, position + listLength);

            var sb = new StringBuilder();
            while (position + 1 < end)
            {
                ushort group = ReadUInt16(source, position);
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(DescribeNamedGroup(group));
                position += 2;
            }

            return sb.Length > 0 ? sb.ToString() : $"{length} bytes";
        }

        private static string DescribePointFormatsExtension(byte[] source, int offset, int length)
        {
            if (length < 1) return $"{length} bytes";
            int listLength = source[offset];
            int position = offset + 1;
            int end = Math.Min(offset + length, position + listLength);
            var sb = new StringBuilder();
            while (position < end)
            {
                byte format = source[position++];
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(DescribePointFormat(format));
            }

            return sb.Length > 0 ? sb.ToString() : $"{length} bytes";
        }

        private static string DescribeSignatureAlgorithmsExtension(byte[] source, int offset, int length)
        {
            if (length < 2) return $"{length} bytes";
            int listLength = ReadUInt16(source, offset);
            int position = offset + 2;
            int end = Math.Min(offset + length, position + listLength);
            var sb = new StringBuilder();
            while (position + 1 < end)
            {
                ushort algorithm = ReadUInt16(source, position);
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(DescribeSignatureAlgorithm(algorithm));
                position += 2;
            }

            return sb.Length > 0 ? sb.ToString() : $"{length} bytes";
        }

        private static string DescribeAlpnExtension(byte[] source, int offset, int length)
        {
            if (length < 2) return $"{length} bytes";
            int listLength = ReadUInt16(source, offset);
            int position = offset + 2;
            int end = Math.Min(offset + length, position + listLength);
            var sb = new StringBuilder();
            while (position < end)
            {
                int itemLength = source[position++];
                if (position + itemLength > end) break;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(Encoding.ASCII.GetString(source, position, itemLength));
                position += itemLength;
            }

            return sb.Length > 0 ? sb.ToString() : $"{length} bytes";
        }

        private static string DescribeSupportedVersionsExtension(byte[] source, int offset, int length, bool fromClientHello)
        {
            if (length < 1) return $"{length} bytes";

            if (!fromClientHello)
            {
                if (length < 2) return $"{length} bytes";
                return DescribeTlsVersion(ReadUInt16(source, offset));
            }

            int listLength = source[offset];
            int position = offset + 1;
            int end = Math.Min(offset + length, position + listLength);
            var sb = new StringBuilder();
            while (position + 1 < end)
            {
                ushort version = ReadUInt16(source, position);
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(DescribeTlsVersion(version));
                position += 2;
            }

            return sb.Length > 0 ? sb.ToString() : $"{length} bytes";
        }

        private static string DescribePskModesExtension(byte[] source, int offset, int length)
        {
            if (length < 1) return $"{length} bytes";
            int listLength = source[offset];
            int position = offset + 1;
            int end = Math.Min(offset + length, position + listLength);

            var sb = new StringBuilder();
            while (position < end)
            {
                byte mode = source[position++];
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(mode == 0 ? "psk_ke" : mode == 1 ? "psk_dhe_ke" : $"0x{mode:x2}");
            }

            return sb.Length > 0 ? sb.ToString() : $"{length} bytes";
        }

        private static string DescribeKeyShareExtension(byte[] source, int offset, int length, bool fromClientHello)
        {
            int position = offset;
            int end = offset + length;

            if (fromClientHello)
            {
                if (length < 2) return $"{length} bytes";
                int listLength = ReadUInt16(source, position);
                position += 2;
                end = Math.Min(end, position + listLength);
            }

            var sb = new StringBuilder();
            while (position + 4 <= end)
            {
                ushort group = ReadUInt16(source, position);
                position += 2;
                int keyLength = ReadUInt16(source, position);
                position += 2;
                if (position + keyLength > end) break;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append($"{DescribeNamedGroup(group)} ({keyLength} bytes)");
                position += keyLength;
            }

            return sb.Length > 0 ? sb.ToString() : $"{length} bytes";
        }

        private static string DescribeTlsVersion(ushort value)
        {
            switch (value)
            {
                case 0x0300: return "SSL 3.0 (0x0300)";
                case 0x0301: return "TLS 1.0 (0x0301)";
                case 0x0302: return "TLS 1.1 (0x0302)";
                case 0x0303: return "TLS 1.2 (0x0303)";
                case 0x0304: return "TLS 1.3 (0x0304)";
                default:
                    if ((value & 0xFF) == (value >> 8))
                    {
                        return $"GREASE (0x{value:x4})";
                    }
                    return $"0x{value:x4}";
            }
        }

        private static string DescribeCompressionMethod(byte value)
        {
            switch (value)
            {
                case 0: return "null (0x0)";
                case 1: return "DEFLATE (0x1)";
                default: return $"0x{value:x2}";
            }
        }

        private static string DescribeExtensionType(ushort value)
        {
            switch (value)
            {
                case 0: return "server_name (0x0)";
                case 5: return "status_request (0x5)";
                case 10: return "supported_groups (0xa)";
                case 11: return "ec_point_formats (0xb)";
                case 13: return "signature_algorithms (0xd)";
                case 16: return "application_layer_protocol_negotiation (0x10)";
                case 18: return "signed_certificate_timestamp (0x12)";
                case 21: return "padding (0x15)";
                case 23: return "extended_master_secret (0x17)";
                case 34: return "delegated_credential (0x22)";
                case 35: return "session_ticket (0x23)";
                case 43: return "supported_versions (0x2b)";
                case 45: return "psk_key_exchange_modes (0x2d)";
                case 51: return "key_share (0x33)";
                case 0x1b: return "compress_certificate (0x1b)";
                case 0x001c: return "record_size_limit (0x001c)";
                case 0x44cd: return "application_settings (0x44cd)";
                case 0xfe0d: return "encrypted_client_hello (0xfe0d)";
                case 0x29: return "pre_shared_key (0x29)";
                case 65281: return "renegotiation_info (0xff01)";
                default:
                    if ((value & 0xFF) == (value >> 8))
                    {
                        return $"GREASE (0x{value:x4})";
                    }
                    return $"0x{value:x4}";
            }
        }

        private static string DescribeNamedGroup(ushort value)
        {
            switch (value)
            {
                case 23: return "secp256r1 (0x17)";
                case 24: return "secp384r1 (0x18)";
                case 25: return "secp521r1 (0x19)";
                case 29: return "x25519 (0x1d)";
                case 30: return "x448 (0x1e)";
                case 256: return "ffdhe2048 (0x100)";
                case 257: return "ffdhe3072 (0x101)";
                case 258: return "ffdhe4096 (0x102)";
                case 259: return "ffdhe6144 (0x103)";
                case 260: return "ffdhe8192 (0x104)";
                case 0x11ec: return "x25519mlkem768 (0x11ec)";
                default:
                    if ((value & 0xFF) == (value >> 8))
                    {
                        return $"GREASE (0x{value:x4})";
                    }
                    return $"0x{value:x4}";
            }
        }

        private static string DescribePointFormat(byte value)
        {
            switch (value)
            {
                case 0: return "uncompressed (0x0)";
                case 1: return "ansiX962_compressed_prime (0x1)";
                case 2: return "ansiX962_compressed_char2 (0x2)";
                default: return $"0x{value:x2}";
            }
        }

        private static string DescribeSignatureAlgorithm(ushort value)
        {
            switch (value)
            {
                case 0x0201: return "rsa_pkcs1_sha1";
                case 0x0203: return "ecdsa_sha1";
                case 0x0401: return "rsa_pkcs1_sha256 (0x0401)";
                case 0x0501: return "rsa_pkcs1_sha384 (0x0501)";
                case 0x0601: return "rsa_pkcs1_sha512 (0x0601)";
                case 0x0403: return "ecdsa_secp256r1_sha256 (0x0403)";
                case 0x0503: return "ecdsa_secp384r1_sha384 (0x0503)";
                case 0x0603: return "ecdsa_secp521r1_sha512 (0x0603)";
                case 0x0804: return "rsa_pss_rsae_sha256 (0x0804)";
                case 0x0805: return "rsa_pss_rsae_sha384 (0x0805)";
                case 0x0806: return "rsa_pss_rsae_sha512 (0x0806)";
                case 0x0807: return "ed25519 (0x0807)";
                case 0x0808: return "ed448 (0x0808)";
                case 0x0809: return "rsa_pss_pss_sha256 (0x0809)";
                case 0x080A: return "rsa_pss_pss_sha384 (0x080A)";
                case 0x080B: return "rsa_pss_pss_sha512 (0x080B)";
                case 0x0904: return "mldsa44";
                case 0x0905: return "mldsa65";
                case 0x0906: return "mldsa87";

                default:
                    if ((value & 0xFF) == (value >> 8))
                    {
                        return $"GREASE (0x{value:x4})";
                    }
                    return $"0x{value:x4}";
            }
        }

        private static string DescribeCipherSuite(ushort value)
        {
            switch (value)
            {
                case 0x1301: return "TLS_AES_128_GCM_SHA256 (0x1301)";
                case 0x1302: return "TLS_AES_256_GCM_SHA384 (0x1302)";
                case 0x1303: return "TLS_CHACHA20_POLY1305_SHA256 (0x1303)";
                case 0x1304: return "TLS_AES_128_CCM_SHA256 (0x1304)";
                case 0x1305: return "TLS_AES_128_CCM_8_SHA256 (0x1305)";
                case 0xC013: return "TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA (0xC013)";
                case 0xC014: return "TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA (0xC014)";
                case 0xC02F: return "TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256 (0xC02F)";
                case 0xC030: return "TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384 (0xC030)";
                case 0xC02B: return "TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256 (0xC02B)";
                case 0xC02C: return "TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384 (0xC02C)";
                case 0xCCA8: return "TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256 (0xCCA8)";
                case 0xCCA9: return "TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256 (0xCCA9)";
                case 0x009C: return "TLS_RSA_WITH_AES_128_GCM_SHA256 (0x009C)";
                case 0x009D: return "TLS_RSA_WITH_AES_256_GCM_SHA384 (0x009D)";
                case 0x002F: return "TLS_RSA_WITH_AES_128_CBC_SHA (0x002F)";
                case 0x0035: return "TLS_RSA_WITH_AES_256_CBC_SHA (0x0035)";
                case 0x000A: return "TLS_RSA_WITH_3DES_EDE_CBC_SHA (0x000A)";
                case 0x0005: return "TLS_RSA_WITH_RC4_128_SHA (0x0005)";
                case 0x0004: return "TLS_RSA_WITH_RC4_128_MD5 (0x0004)";
                case 0xC007: return "TLS_ECDHE_ECDSA_WITH_RC4_128_SHA (0xC007)";
                case 0xC009: return "TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA (0xC009)";
                case 0xC00A: return "TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA (0xC00A)";
                case 0xC011: return "TLS_ECDHE_RSA_WITH_RC4_128_SHA (0xC011)";
                case 0xC012: return "TLS_ECDHE_RSA_WITH_3DES_EDE_CBC_SHA (0xC012)";
                case 0xC023: return "TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256 (0xC023)";
                case 0xC024: return "TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA384 (0xC024)";
                case 0xC027: return "TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256 (0xC027)";
                case 0xC028: return "TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384 (0xC028)";

                default:
                    if ((value & 0xFF) == (value >> 8))
                    {
                        return $"GREASE (0x{value:x4})";
                    }
                    return $"0x{value:x4}";
            }
        }

        private static bool TryReadVector8(byte[] source, ref int offset, out byte[] value)
        {
            value = null;
            if (source == null || offset >= source.Length) return false;
            int len = source[offset++];
            if (offset + len > source.Length) return false;
            value = new byte[len];
            if (len > 0) Buffer.BlockCopy(source, offset, value, 0, len);
            offset += len;
            return true;
        }

        private static bool TryReadVector16(byte[] source, ref int offset, out byte[] value)
        {
            value = null;
            if (source == null || offset + 1 >= source.Length) return false;
            int len = ReadUInt16(source, offset);
            offset += 2;
            if (offset + len > source.Length) return false;
            value = new byte[len];
            if (len > 0) Buffer.BlockCopy(source, offset, value, 0, len);
            offset += len;
            return true;
        }

        private static ushort ReadUInt16(byte[] source, int offset)
        {
            return (ushort)((source[offset] << 8) | source[offset + 1]);
        }
    }
}
