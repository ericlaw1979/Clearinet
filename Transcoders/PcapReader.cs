using System;
using System.Collections.Generic;
using System.IO;

namespace TestExtensions
{
    // This reader deliberately has no Clearinet dependencies, so capture validation can be tested independently.
    internal sealed class PcapReader
    {
        internal const int MaxBlock = 16 * 1024 * 1024;
        internal const long MaxCapture = 256L * 1024 * 1024;
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly Stream input;
        private readonly Action<string> warn;
        private readonly Action<int, string> progress;
        private long consumed;
        private int packets;

        private sealed class Interface
        {
            internal uint Link, Snap;
            internal decimal Units = 1000000;
            internal long Offset;
        }

        internal PcapReader(Stream input, Action<string> warn, Action<int, string> progress)
        {
            this.input = input;
            this.warn = warn;
            this.progress = progress;
        }

        internal void Read(Action<byte[], uint, DateTime, int, bool> packet)
        {
            progress(0, "Reading packet capture.");
            byte[] magic = ReadBytes(4, false);
            if (U32(magic, 0, false) == 0x0a0d0d0a) ReadNg(magic, packet);
            else ReadClassic(magic, packet);
            progress(65, "Reassembling TCP streams.");
        }

        private byte[] ReadBytes(int length, bool allowEof)
        {
            if (length < 0 || length > MaxBlock || consumed + length > MaxCapture)
                throw new InvalidDataException("Capture size or block resource limit exceeded.");
            byte[] result = new byte[length];
            int offset = 0;
            while (offset < length)
            {
                progress(Percent(), "Reading packet capture.");
                int read = input.Read(result, offset, Math.Min(length - offset, 65536));
                if (read == 0)
                {
                    if (allowEof && offset == 0) return null;
                    throw new InvalidDataException("Truncated capture structure.");
                }
                consumed += read;
                offset += read;
            }
            return result;
        }

        private int Percent()
        {
            return input.CanSeek && input.Length > 0 ? (int)(consumed * 65 / input.Length) : 0;
        }

        private void Emit(Action<byte[], uint, DateTime, int, bool> packet, byte[] data, uint link,
            DateTime time, int scope, bool truncated)
        {
            if (++packets > 1000000) throw new InvalidDataException("Capture packet count limit exceeded.");
            if (truncated) warn("Snap-length truncated packet");
            packet(data, link, time, scope, truncated);
        }

        private void ReadClassic(byte[] magic, Action<byte[], uint, DateTime, int, bool> packet)
        {
            uint m = U32(magic, 0, true);
            bool little = m == 0xa1b2c3d4 || m == 0xa1b23c4d;
            bool nano = m == 0xa1b23c4d || m == 0x4d3cb2a1;
            if (!little && m != 0xd4c3b2a1 && m != 0x4d3cb2a1)
                throw new InvalidDataException("Not a classic PCAP or PCAPNG capture.");
            byte[] header = ReadBytes(20, false);
            if (U16(header, 0, little) != 2 || U16(header, 2, little) != 4)
                throw new InvalidDataException("Unsupported PCAP version (expected 2.4).");
            uint snap = U32(header, 12, little), link = U32(header, 16, little) & 0xffff;
            if (snap == 0 || snap > MaxBlock) throw new InvalidDataException("Invalid PCAP snap length.");
            byte[] record;
            while ((record = ReadBytes(16, true)) != null)
            {
                uint seconds = U32(record, 0, little), fraction = U32(record, 4, little);
                uint captured = U32(record, 8, little), original = U32(record, 12, little);
                if (captured > snap || captured > original || captured > MaxBlock || fraction >= (nano ? 1000000000u : 1000000u))
                    throw new InvalidDataException("Invalid PCAP packet length or timestamp.");
                DateTime time = Epoch.AddTicks((long)seconds * TimeSpan.TicksPerSecond + (nano ? fraction / 100 : fraction * 10L));
                Emit(packet, ReadBytes((int)captured, false), link, time, 0, captured < original);
            }
        }

        private void ReadNg(byte[] first, Action<byte[], uint, DateTime, int, bool> packet)
        {
            bool little = true, haveSection = false;
            int nextScope = 0;
            List<Interface> interfaces = new List<Interface>();
            List<int> scopes = new List<int>();
            byte[] typeBytes = first;
            while (typeBytes != null)
            {
                byte[] lengthBytes = ReadBytes(4, false);
                bool section = U32(typeBytes, 0, true) == 0x0a0d0d0a;
                byte[] bom = section ? ReadBytes(4, false) : null;
                if (section)
                {
                    uint order = U32(bom, 0, true);
                    if (order != 0x1a2b3c4d && order != 0x4d3c2b1a)
                        throw new InvalidDataException("Invalid PCAPNG byte-order magic.");
                    little = order == 0x1a2b3c4d;
                }
                else if (!haveSection) throw new InvalidDataException("PCAPNG block outside a section.");
                uint length = U32(lengthBytes, 0, little);
                if (length < (section ? 28u : 12u) || length > MaxBlock || (length & 3) != 0)
                    throw new InvalidDataException("Invalid PCAPNG block length.");
                byte[] block = new byte[(int)length];
                Buffer.BlockCopy(typeBytes, 0, block, 0, 4);
                Buffer.BlockCopy(lengthBytes, 0, block, 4, 4);
                if (section) Buffer.BlockCopy(bom, 0, block, 8, 4);
                byte[] tail = ReadBytes((int)length - (section ? 12 : 8), false);
                Buffer.BlockCopy(tail, 0, block, section ? 12 : 8, tail.Length);
                if (U32(block, block.Length - 4, little) != length)
                    throw new InvalidDataException("PCAPNG trailing block length mismatch.");
                uint type = U32(typeBytes, 0, little);
                if (section)
                {
                    if (U16(block, 12, little) != 1 || U16(block, 14, little) != 0)
                        throw new InvalidDataException("Unsupported PCAPNG section version.");
                    interfaces.Clear();
                    scopes.Clear();
                    haveSection = true;
                    Options(block, 24, little, null);
                }
                else if (type == 1)
                {
                    Require(block, 20);
                    if (interfaces.Count >= 4096) throw new InvalidDataException("PCAPNG interface limit exceeded.");
                    Interface iface = new Interface { Link = U16(block, 8, little), Snap = U32(block, 12, little) };
                    if (iface.Snap > MaxBlock) throw new InvalidDataException("PCAPNG snap length limit exceeded.");
                    Options(block, 16, little, delegate(ushort code, byte[] value)
                    {
                        if (code == 9)
                        {
                            if (value.Length != 1) throw new InvalidDataException("Invalid PCAPNG timestamp resolution.");
                            int exponent = value[0] & 127;
                            bool binary = (value[0] & 128) != 0;
                            if (exponent > (binary ? 63 : 18)) throw new InvalidDataException("Unsupported PCAPNG timestamp resolution.");
                            iface.Units = 1;
                            for (int i = 0; i < exponent; i++) iface.Units *= binary ? 2 : 10;
                        }
                        if (code == 14)
                        {
                            if (value.Length != 8) throw new InvalidDataException("Invalid PCAPNG timestamp offset.");
                            iface.Offset = unchecked((long)U64(value, 0, little));
                        }
                    });
                    interfaces.Add(iface);
                    scopes.Add(nextScope++);
                }
                else if (type == 6 || type == 2 || type == 3)
                {
                    Require(block, type == 3 ? 16 : 32);
                    uint id = type == 3 ? 0 : type == 2 ? U16(block, 8, little) : U32(block, 8, little);
                    if (id >= interfaces.Count) throw new InvalidDataException("PCAPNG packet references an unknown interface.");
                    Interface iface = interfaces[(int)id];
                    uint original = U32(block, type == 3 ? 8 : 24, little);
                    uint captured = type == 3 ? Math.Min(original, iface.Snap == 0 ? original : iface.Snap) : U32(block, 20, little);
                    int start = type == 3 ? 12 : 28;
                    long padded = ((long)captured + 3) & ~3L;
                    if (captured > original || captured > MaxBlock || (iface.Snap != 0 && captured > iface.Snap) || start + padded > block.Length - 4)
                        throw new InvalidDataException("Invalid PCAPNG packet length.");
                    DateTime time = DateTime.MinValue;
                    if (type == 3)
                    {
                        if (start + padded != block.Length - 4) throw new InvalidDataException("Invalid PCAPNG simple packet padding.");
                        warn("PCAPNG simple packet has no timestamp");
                    }
                    else
                    {
                        ulong timestamp = ((ulong)U32(block, 12, little) << 32) | U32(block, 16, little);
                        decimal ticks = decimal.Truncate((decimal)timestamp * TimeSpan.TicksPerSecond / iface.Units) + (decimal)iface.Offset * TimeSpan.TicksPerSecond;
                        if (ticks < -Epoch.Ticks || ticks > DateTime.MaxValue.Ticks - Epoch.Ticks)
                            throw new InvalidDataException("PCAPNG timestamp is outside the DateTime range.");
                        time = Epoch.AddTicks((long)ticks);
                        Options(block, start + (int)padded, little, null);
                    }
                    byte[] data = new byte[(int)captured];
                    Buffer.BlockCopy(block, start, data, 0, data.Length);
                    Emit(packet, data, iface.Link, time, scopes[(int)id], captured < original);
                }
                // Unknown, statistics, name-resolution, and decryption-secret blocks are not needed for plaintext HTTP.
                typeBytes = ReadBytes(4, true);
            }
        }

        private static void Options(byte[] block, int offset, bool little, Action<ushort, byte[]> option)
        {
            int end = block.Length - 4;
            while (offset < end)
            {
                if (end - offset < 4) throw new InvalidDataException("Truncated PCAPNG option.");
                ushort code = U16(block, offset, little), length = U16(block, offset + 2, little);
                offset += 4;
                if (code == 0)
                {
                    if (length != 0) throw new InvalidDataException("Invalid PCAPNG end-of-options.");
                    for (int i = offset; i < end; i++) if (block[i] != 0) throw new InvalidDataException("Data after PCAPNG end-of-options.");
                    return;
                }
                int padded = (length + 3) & ~3;
                if (padded > end - offset) throw new InvalidDataException("PCAPNG option exceeds block bounds.");
                if (option != null)
                {
                    byte[] value = new byte[length];
                    Buffer.BlockCopy(block, offset, value, 0, length);
                    option(code, value);
                }
                offset += padded;
            }
        }

        private static void Require(byte[] data, int minimum)
        {
            if (data.Length < minimum) throw new InvalidDataException("PCAPNG block is too short.");
        }

        internal static ushort U16(byte[] b, int p, bool little)
        {
            return (ushort)(little ? b[p] | b[p + 1] << 8 : b[p] << 8 | b[p + 1]);
        }

        internal static uint U32(byte[] b, int p, bool little)
        {
            return little ? (uint)(b[p] | b[p + 1] << 8 | b[p + 2] << 16 | b[p + 3] << 24) :
                (uint)(b[p] << 24 | b[p + 1] << 16 | b[p + 2] << 8 | b[p + 3]);
        }

        private static ulong U64(byte[] b, int p, bool little)
        {
            return little ? U32(b, p, true) | ((ulong)U32(b, p + 4, true) << 32) :
                ((ulong)U32(b, p, false) << 32) | U32(b, p + 4, false);
        }
    }
}
