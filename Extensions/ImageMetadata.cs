using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Extensions
{
    internal sealed class ImageMetadata
    {
        private readonly byte[] data;
        private readonly List<string> details = new List<string>();
        private readonly List<string> comments = new List<string>();
        private readonly List<Tuple<int, int>> metadata = new List<Tuple<int, int>>();
        private int commentBytes;
        private bool commentsTruncated;
        private bool supported;
        private bool complete = true;
        private long width, height;
        private int depth;
        private string depthDescription;

        private ImageMetadata(byte[] bytes) { data = bytes; }

        internal static string Analyze(byte[] bytes, System.Drawing.Image rendered = null)
        {
            var result = new ImageMetadata(bytes);
            try
            {
                if (result.Match(0, "\x89PNG\r\n\x1a\n")) result.Png();
                else if (result.Match(0, "GIF87a") || result.Match(0, "GIF89a")) result.Gif();
                else if (bytes.Length >= 2 && bytes[0] == 0xff && bytes[1] == 0xd8) result.Jpeg();
                else if ((bytes.Length >= 12 && result.Match(0, "\0\0\0\x0cJXL \x0d\x0a\x87\x0a"))
                     || (bytes.Length >= 2 && bytes[0] == 0xff && bytes[1] == 0x0a))
                {
                    result.JpegXL();
                }
                else if (result.Match(0, "RIFF") && result.Match(8, "WEBP")) result.WebP();
                else if (result.Match(0, "II\x2a\0") || result.Match(0, "MM\0\x2a"))
                {
                    result.supported = true;
                    result.Exif(0, bytes.Length, true);
                }
            }
            catch (InvalidDataException e)
            {
                result.complete = false;
                result.details.Add("Metadata analysis incomplete: " + e.Message);
            }
            if (result.width == 0 && rendered != null)
            {
                result.width = rendered.Width;
                result.height = rendered.Height;
            }
            if (result.depth == 0 && rendered != null)
            {
                result.depth = System.Drawing.Image.GetPixelFormatSize(rendered.PixelFormat);
                result.depthDescription = " (decoded pixels)";
            }
            var text = new StringBuilder();
            text.AppendFormat(CultureInfo.InvariantCulture, "File size: {0:N0} bytes\r\n", bytes.Length);
            if (result.width > 0 && result.height > 0)
                text.AppendFormat(CultureInfo.InvariantCulture, "Dimensions: {0} x {1} pixels\r\n", result.width, result.height);
            if (result.depth > 0)
                text.AppendFormat(CultureInfo.InvariantCulture, "Color depth: {0} bits/pixel{1}\r\n", result.depth, result.depthDescription);
            if (result.supported && result.complete)
            {
                long count = result.MetadataBytes();
                if (count > 512)
                {
                    text.AppendFormat(CultureInfo.InvariantCulture, "Non-rendering metadata: {0:N0} bytes ({1:0.00}% of file)\r\n",
                        count, bytes.Length == 0 ? 0 : 100.0 * count / bytes.Length);
                }
            }
            else text.AppendLine("Non-rendering metadata: " + (result.supported ? "unavailable (incomplete analysis)" : "not supported for this format"));
            foreach (string detail in result.details) text.Append(detail).Append("\r\n");
            if (result.comments.Count != 0 || result.commentsTruncated)
            {
                text.Append("Comments (first 1 KB):\r\n").Append(string.Join("\r\n", result.comments));
                if (result.commentsTruncated) text.Append("...");
                text.Append("\r\n");
            }
            return text.ToString().TrimEnd('\r', '\n');
        }

        private void Require(int offset, long length, int end)
        {
            if (offset < 0 || length < 0 || offset > end || length > end - offset || end > data.Length)
                throw new InvalidDataException("Truncated or invalid image metadata length/offset.");
        }

        private bool Match(int offset, string value)
        {
            if (offset < 0 || offset > data.Length - value.Length) return false;
            for (int i = 0; i < value.Length; i++) if (data[offset + i] != value[i]) return false;
            return true;
        }

        private uint U32(int offset, bool little, int end)
        {
            Require(offset, 4, end);
            if (little) return (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24);
            return (uint)(data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]);
        }

        private int U16(int offset, bool little, int end)
        {
            Require(offset, 2, end);
            return little ? data[offset] | data[offset + 1] << 8 : data[offset] << 8 | data[offset + 1];
        }

        private void Mark(int offset, int length)
        {
            Require(offset, length, data.Length);
            if (length > 0) metadata.Add(Tuple.Create(offset, offset + length));
        }

        private long MetadataBytes()
        {
            long total = 0;
            int end = 0;
            foreach (var range in metadata.OrderBy(r => r.Item1))
            {
                total += Math.Max(0, range.Item2 - Math.Max(end, range.Item1));
                end = Math.Max(end, range.Item2);
            }
            return total;
        }

        private void Thumbnail(int offset, int length)
        {
            Require(offset, length, data.Length);
            details.Add(string.Format(CultureInfo.InvariantCulture, "Thumbnail: {0:N0} bytes at file offset {1} (0x{1:X})", length, offset));
        }

        private void Comment(byte[] bytes, int offset, int length, Encoding encoding)
        {
            int take = Math.Min(length, 1024 - commentBytes);
            if (take > 0)
            {
                char[] chars = new char[encoding.GetMaxCharCount(take)];
                int used, written;
                bool converted;
                encoding.GetDecoder().Convert(bytes, offset, take, chars, 0, chars.Length, take == length, out used, out written, out converted);
                string value = new string(chars, 0, written).TrimEnd('\0').Replace("\0", " ");
                if (value.Length > 0) comments.Add(value);
                commentBytes += take;
            }
            if (take < length) commentsTruncated = true;
        }

        private void Comment(int offset, int length, Encoding encoding) { Comment(data, offset, length, encoding); }
        private void Comment(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            Comment(bytes, 0, bytes.Length, Encoding.UTF8);
        }

        private void Png()
        {
            supported = true;
            int p = 8, frames = 0, declaredFrames = 0;
            bool header = false;
            while (p < data.Length)
            {
                Require(p, 12, data.Length);
                uint size = U32(p, false, data.Length);
                Require(p + 8, (long)size + 4, data.Length);
                int n = (int)size, start = p + 8, end = start + n;
                string type = Encoding.ASCII.GetString(data, p + 4, 4);
                if (type == "IHDR")
                {
                    if (n != 13 || header) throw new InvalidDataException("Invalid PNG header.");
                    header = true;
                    width = U32(start, false, end); height = U32(start + 4, false, end);
                    int channels;
                    switch (data[start + 9])
                    {
                        case 0: case 3: channels = 1; break;
                        case 2: channels = 3; break;
                        case 4: channels = 2; break;
                        case 6: channels = 4; break;
                        default: throw new InvalidDataException("Invalid PNG color type.");
                    }
                    depth = data[start + 8] * channels;
                    if (data[start + 9] == 3) depthDescription = " (palette indices)";
                }
                else if (type == "acTL")
                {
                    if (n != 8) throw new InvalidDataException("Invalid APNG animation control.");
                    uint count = U32(start, false, end);
                    if (count == 0 || count > int.MaxValue) throw new InvalidDataException("Invalid APNG frame count.");
                    declaredFrames = (int)count;
                }
                else if (type == "fcTL")
                {
                    if (n != 26) throw new InvalidDataException("Invalid APNG frame control.");
                    frames++;
                }
                else if (type == "eXIf")
                {
                    if (!Exif(start, n, false)) Mark(p, n + 12);
                }
                else if (type == "tEXt" || type == "zTXt" || type == "iTXt")
                {
                    if (!PngText(type, start, end)) Mark(p, n + 12);
                }
                else if (type == "tIME" || type == "pHYs" || type == "hIST" || type == "sPLT") Mark(p, n + 12);
                if (type == "IEND")
                {
                    if (!header || n != 0) throw new InvalidDataException("Invalid PNG end/header.");
                    if (declaredFrames != 0 || frames != 0)
                    {
                        if (frames != declaredFrames) throw new InvalidDataException("APNG frame count does not match its frame controls.");
                        details.Add("Frames: " + frames.ToString(CultureInfo.InvariantCulture));
                    }
                    return;
                }
                p = end + 4;
            }
            throw new InvalidDataException("Missing PNG end chunk.");
        }

        private int Zero(int p, int end)
        {
            int zero = Array.IndexOf(data, (byte)0, p, end - p);
            if (zero < 0) throw new InvalidDataException("Unterminated metadata text field.");
            return zero;
        }

        private bool PngText(string type, int start, int end)
        {
            int separator = Zero(start, end);
            bool xmp = Encoding.ASCII.GetString(data, start, separator - start) == "XML:com.adobe.xmp";
            int p = separator + 1;
            Encoding encoding = Encoding.GetEncoding(28591);
            bool compressed = type == "zTXt";
            if (type == "iTXt")
            {
                Require(p, 2, end);
                if (data[p] > 1 || data[p + 1] != 0) throw new InvalidDataException("Invalid PNG text compression.");
                compressed = data[p] == 1; p += 2;
                p = Zero(p, end) + 1;
                p = Zero(p, end) + 1;
                encoding = Encoding.UTF8;
            }
            else if (compressed)
            {
                Require(p, 1, end);
                if (data[p++] != 0) throw new InvalidDataException("Unknown PNG text compression method.");
            }
            if (!compressed)
            {
                if (xmp) return Xmp(p, end - p);
                Comment(p, end - p, encoding);
            }
            else
            {
                Require(p, 6, end);
                if ((data[p] & 15) != 8 || (data[p] >> 4) > 7 || ((data[p] << 8 | data[p + 1]) % 31) != 0 || (data[p + 1] & 32) != 0)
                    throw new InvalidDataException("Invalid PNG text zlib header.");
                using (var source = new MemoryStream(data, p + 2, end - p - 6))
                using (var inflater = new DeflateStream(source, CompressionMode.Decompress))
                {
                    byte[] preview = new byte[xmp ? 1024 * 1024 + 1 : 1025 - commentBytes];
                    int read = 0, count;
                    while (read < preview.Length && (count = inflater.Read(preview, read, preview.Length - read)) != 0) read += count;
                    if (xmp)
                    {
                        if (read == preview.Length) throw new InvalidDataException("Oversized compressed XMP packet.");
                        return Xmp(preview, 0, read);
                    }
                    Comment(preview, 0, read, encoding);
                }
            }
            return false;
        }

        private int GifBlocks(int p, bool comment)
        {
            byte[] preview = comment ? new byte[1025 - commentBytes] : null;
            int copied = 0;
            while (true)
            {
                Require(p, 1, data.Length);
                int n = data[p++];
                if (n == 0)
                {
                    if (comment) Comment(preview, 0, copied, Encoding.GetEncoding(28591));
                    return p;
                }
                Require(p, n, data.Length);
                if (comment)
                {
                    int take = Math.Min(n, preview.Length - copied);
                    Buffer.BlockCopy(data, p, preview, copied, take);
                    copied += take;
                }
                p += n;
            }
        }

        private void Gif()
        {
            supported = true;
            Require(0, 13, data.Length);
            width = U16(6, true, data.Length); height = U16(8, true, data.Length);
            depth = (data[10] & 128) != 0 ? (data[10] & 7) + 1 : 0;
            depthDescription = " (palette indices)";
            int p = 13, frames = 0;
            if ((data[10] & 128) != 0) p += 3 * (1 << depth);
            while (true)
            {
                Require(p, 1, data.Length);
                int start = p, kind = data[p++];
                if (kind == 0x3b)
                {
                    details.Add("Frames: " + frames.ToString(CultureInfo.InvariantCulture));
                    return;
                }
                if (kind == 0x2c)
                {
                    Require(p, 9, data.Length);
                    int flags = data[p + 8]; p += 9;
                    if ((flags & 128) != 0)
                    {
                        int bits = (flags & 7) + 1;
                        depth = Math.Max(depth, bits);
                        p += 3 * (1 << bits);
                    }
                    Require(p, 1, data.Length); p++;
                    p = GifBlocks(p, false); frames++;
                }
                else if (kind == 0x21)
                {
                    Require(p, 1, data.Length);
                    int label = data[p++];
                    bool rendering = label == 0xf9 || label == 0x01;
                    if (label == 0xff)
                    {
                        Require(p, 12, data.Length);
                        rendering = Match(p + 1, "NETSCAPE2.0") || Match(p + 1, "ANIMEXTS1.0") || Match(p + 1, "ICCRGBG1012");
                    }
                    p = GifBlocks(p, label == 0xfe);
                    if (!rendering) Mark(start, p - start);
                }
                else throw new InvalidDataException("Unknown GIF block.");
            }
        }


        private void Jpeg()
        {
            supported = true;
            int p = 2;
            bool entropy = false;
            while (p < data.Length)
            {
                if (entropy)
                {
                    while (p < data.Length && data[p] != 0xff) p++;
                    if (p == data.Length) break;
                }
                int start = p;
                if (data[p++] != 0xff) throw new InvalidDataException("Invalid JPEG marker.");
                while (p < data.Length && data[p] == 0xff) p++;
                Require(p, 1, data.Length);
                int marker = data[p++];
                if (entropy && (marker == 0 || marker >= 0xd0 && marker <= 0xd7)) continue;
                if (marker == 0xd9) return;
                if (marker == 0x01 || marker >= 0xd0 && marker <= 0xd8) continue;
                int length = U16(p, false, data.Length);
                if (length < 2) throw new InvalidDataException("Invalid JPEG segment length.");
                Require(p, length, data.Length);
                int payload = p + 2, n = length - 2, end = p + length;
                bool optional = marker == 0xfe || marker >= 0xe0 && marker <= 0xef;
                if (marker == 0xe2 && Match(payload, "ICC_PROFILE\0") || marker == 0xee && Match(payload, "Adobe")) optional = false;
                if (marker == 0xe1 && n >= 6 && Match(payload, "Exif\0\0"))
                {
                    if (Exif(payload + 6, n - 6, false)) optional = false;
                }
                else if (marker == 0xe1 && n >= 29 && Match(payload, "http://ns.adobe.com/xap/1.0/\0"))
                {
                    if (Xmp(payload + 29, n - 29)) optional = false;
                }
                else if (marker == 0xfe) Comment(payload, n, Encoding.GetEncoding(28591));
                else if (marker == 0xed && n >= 14 && Match(payload, "Photoshop 3.0\0")) Photoshop(payload + 14, end);
                else if (marker == 0xe0 && n >= 14 && Match(payload, "JFIF\0"))
                {
                    int thumbLength = 3 * data[payload + 12] * data[payload + 13];
                    Require(payload + 14, thumbLength, end);
                    if (thumbLength != 0) Thumbnail(payload + 14, thumbLength);
                }
                else if (marker == 0xe0 && n >= 6 && Match(payload, "JFXX\0"))
                {
                    if (data[payload + 5] == 0x10) Thumbnail(payload + 6, n - 6);
                    else if (data[payload + 5] == 0x11 || data[payload + 5] == 0x13)
                    {
                        Require(payload + 6, 2, end);
                        int thumbLength = data[payload + 6] * data[payload + 7] * (data[payload + 5] == 0x13 ? 3 : 1);
                        int thumbStart = payload + 8;
                        if (data[payload + 5] == 0x11) thumbLength += 768;
                        Require(thumbStart, thumbLength, end);
                        Thumbnail(thumbStart, thumbLength);
                    }
                }
                if (optional) Mark(start, end - start);
                if (marker >= 0xc0 && marker <= 0xcf && marker != 0xc4 && marker != 0xc8 && marker != 0xcc)
                {
                    Require(payload, 6, end);
                    height = U16(payload + 1, false, end); width = U16(payload + 3, false, end);
                    depth = data[payload] * data[payload + 5];
                    if (marker == 0xc2 || marker == 0xc6 || marker == 0xca || marker == 0xce) details.Add("JPEG: progressive");
                }
                p = end;
                if (marker == 0xda) entropy = true;
            }
            throw new InvalidDataException("Missing JPEG end marker.");
        }

        private void Photoshop(int p, int end)
        {
            while (p < end)
            {
                Require(p, 7, end);
                if (!Match(p, "8BIM")) throw new InvalidDataException("Invalid Photoshop resource signature.");
                int id = U16(p + 4, false, end);
                int nameLength = data[p + 6] + 1;
                p += 6 + nameLength + (nameLength & 1);
                uint length = U32(p, false, end); p += 4;
                Require(p, (long)length + (length & 1), end);
                int resourceEnd = p + (int)length;
                if (id == 0x409 || id == 0x40c)
                {
                    Require(p, 28, resourceEnd);
                    uint compressedSize = U32(p + 20, false, resourceEnd);
                    Require(p + 28, compressedSize, resourceEnd);
                    Thumbnail(p + 28, (int)compressedSize);
                }
                else if (id == 0x404) Iptc(p, resourceEnd);
                p = resourceEnd + ((int)length & 1);
            }
        }

        private void Iptc(int p, int end)
        {
            Encoding encoding = Encoding.GetEncoding(28591);
            while (p < end)
            {
                Require(p, 5, end);
                if (data[p++] != 0x1c) throw new InvalidDataException("Invalid IPTC record marker.");
                int record = data[p++], dataset = data[p++];
                int length = U16(p, false, end); p += 2;
                if ((length & 0x8000) != 0)
                {
                    int sizeBytes = length & 0x7fff;
                    if (sizeBytes < 1 || sizeBytes > 4) throw new InvalidDataException("Invalid IPTC extended length.");
                    Require(p, sizeBytes, end);
                    long size = 0;
                    for (int i = 0; i < sizeBytes; i++) size = (size << 8) | data[p++];
                    if (size > int.MaxValue) throw new InvalidDataException("Oversized IPTC dataset.");
                    length = (int)size;
                }
                Require(p, length, end);
                if (record == 1 && dataset == 90 && length == 3 && Match(p, "\x1b%G")) encoding = Encoding.UTF8;
                if (record == 2 && (dataset == 120 || dataset == 105)) Comment(p, length, encoding);
                p += length;
            }
        }

        private void JpegXL()
        {
            supported = true;

            // Raw codestream (no container boxes)
            if (data.Length >= 2 && data[0] == 0xff && data[1] == 0x0a)
            {
                details.Add("JPEG XL: raw codestream");
                return;
            }

            // ISO BMFF-style container signature
            if (!Match(0, "\0\0\0\x0cJXL \x0d\x0a\x87\x0a"))
                throw new InvalidDataException("Invalid JPEG XL signature.");

            int p = 12;
            while (p < data.Length)
            {
                Require(p, 8, data.Length);

                long boxLength = U32(p, false, data.Length);
                int header = 8;

                if (boxLength == 1)
                {
                    Require(p + 8, 8, data.Length);
                    ulong extended = ((ulong)U32(p + 8, false, data.Length) << 32) | U32(p + 12, false, data.Length);
                    if (extended > int.MaxValue) throw new InvalidDataException("Oversized JPEG XL box.");
                    boxLength = (long)extended;
                    header = 16;
                }
                else if (boxLength == 0)
                {
                    boxLength = data.Length - p;
                }

                if (boxLength < header) throw new InvalidDataException("Invalid JPEG XL box length.");
                Require(p, boxLength, data.Length);

                int start = p + header;
                int n = (int)boxLength - header;

                if (Match(p + 4, "Exif"))
                {
                    bool orientation = false;
                    bool parsed = false;

                    if (n >= 6 && Match(start, "Exif\0\0"))
                    {
                        orientation = Exif(start + 6, n - 6, false);
                        parsed = true;
                    }
                    else
                    {
                        int exifStart = start;
                        int exifLength = n;

                        // Some Exif payloads start with a 4-byte TIFF offset.
                        if (n >= 8)
                        {
                            uint tiffOffset = U32(start, false, start + n);
                            if (tiffOffset <= (uint)(n - 4))
                            {
                                int candidate = start + 4 + (int)tiffOffset;
                                if (candidate <= start + n - 4 && (Match(candidate, "II") || Match(candidate, "MM")))
                                {
                                    exifStart = candidate;
                                    exifLength = start + n - candidate;
                                }
                            }
                        }

                        if (exifLength >= 8 && (Match(exifStart, "II") || Match(exifStart, "MM")))
                        {
                            orientation = Exif(exifStart, exifLength, false);
                            parsed = true;
                        }
                    }

                    if (!parsed || !orientation) Mark(p, (int)boxLength);
                }
                else if (Match(p + 4, "xml "))
                {
                    if (!Xmp(start, n)) Mark(p, (int)boxLength);
                }

                p += (int)boxLength;
            }
        }
        private void WebP()
        {
            supported = true;
            uint size = U32(4, true, data.Length);
            Require(8, size, data.Length);
            int end = (int)(size + 8), p = 12, frames = 0;
            while (p < end)
            {
                Require(p, 8, end);
                uint length = U32(p + 4, true, end);
                Require(p + 8, (long)length + (length & 1), end);
                int n = (int)length, start = p + 8;
                if (Match(p, "EXIF"))
                {
                    int skip = n >= 6 && Match(start, "Exif\0\0") ? 6 : 0;
                    if (!Exif(start + skip, n - skip, false)) Mark(p, 8 + n + (n & 1));
                }
                else if (Match(p, "XMP "))
                {
                    if (!Xmp(start, n)) Mark(p, 8 + n + (n & 1));
                }
                else if (Match(p, "VP8X"))
                {
                    Require(start, 10, start + n);
                    width = 1 + (data[start + 4] | data[start + 5] << 8 | data[start + 6] << 16);
                    height = 1 + (data[start + 7] | data[start + 8] << 8 | data[start + 9] << 16);
                    depth = (data[start] & 16) != 0 ? 32 : 24;
                }
                else if (Match(p, "VP8 ") && width == 0)
                {
                    Require(start, 10, start + n);
                    width = U16(start + 6, true, start + n) & 0x3fff;
                    height = U16(start + 8, true, start + n) & 0x3fff;
                    depth = 24;
                }
                else if (Match(p, "VP8L") && width == 0)
                {
                    Require(start, 5, start + n);
                    uint bits = U32(start + 1, true, start + n);
                    width = 1 + (bits & 0x3fff); height = 1 + ((bits >> 14) & 0x3fff);
                    depth = (bits & (1 << 28)) != 0 ? 32 : 24;
                }
                else if (Match(p, "ANMF")) frames++;
                p = start + n + (n & 1);
            }
            if (frames != 0) details.Add("Frames: " + frames.ToString(CultureInfo.InvariantCulture));
        }

        private sealed class TiffField
        {
            internal int Tag, Type, Count, Offset, Length;
        }

        private bool Exif(int start, int length, bool standalone)
        {
            int end = start + length;
            Require(start, 8, end);
            bool little = Match(start, "II");
            if ((!little && !Match(start, "MM")) || U16(start + 2, little, end) != 42)
                throw new InvalidDataException("Invalid TIFF/Exif header.");
            bool orientation = false;
            var visited = new HashSet<int>();
            var pending = new Queue<Tuple<uint, int>>();
            pending.Enqueue(Tuple.Create(U32(start + 4, little, end), 0));
            while (pending.Count != 0)
            {
                var directory = pending.Dequeue();
                if (directory.Item1 == 0) continue;
                if (directory.Item1 > (uint)length) throw new InvalidDataException("Invalid Exif directory offset.");
                int p = start + (int)directory.Item1, kind = directory.Item2;
                if (!visited.Add(p) || visited.Count > 128) throw new InvalidDataException("Cyclic or excessive Exif directories.");
                int count = U16(p, little, end);
                Require(p + 2, count * 12L + 4, end);
                if (standalone && kind == 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        int entry = p + 2 + i * 12;
                        if (U16(entry, little, end) == 0xfe && U16(entry + 2, little, end) == 4 &&
                            U32(entry + 4, little, end) == 1 && (U32(entry + 8, little, end) & 1) != 0) kind = 1;
                    }
                }
                var fields = new Dictionary<int, TiffField>();
                for (int i = 0; i < count; i++)
                {
                    int entry = p + 2 + i * 12;
                    int tag = U16(entry, little, end), type = U16(entry + 2, little, end);
                    uint items = U32(entry + 4, little, end);
                    int unit;
                    switch (type)
                    {
                        case 1: case 2: case 6: case 7: unit = 1; break;
                        case 3: case 8: unit = 2; break;
                        case 4: case 9: case 11: case 13: unit = 4; break;
                        case 5: case 10: case 12: unit = 8; break;
                        default: throw new InvalidDataException("Unknown TIFF field type.");
                    }
                    long bytes = (long)items * unit;
                    uint relative = bytes > 4 ? U32(entry + 8, little, end) : (uint)(entry + 8 - start);
                    if (relative > length || bytes > int.MaxValue) throw new InvalidDataException("Invalid Exif value offset/length.");
                    int value = start + (int)relative;
                    Require(value, bytes, end);
                    var field = new TiffField { Tag = tag, Type = type, Count = (int)items, Offset = value, Length = (int)bytes };
                    if (fields.ContainsKey(tag)) throw new InvalidDataException("Duplicate Exif tag.");
                    fields.Add(tag, field);
                    bool optional = kind != 0 || tag == 0x10e || tag == 0x10f || tag == 0x110 || tag == 0x131 || tag == 0x132 || tag == 0x13b || tag == 0x8298 || tag == 0x8769 || tag == 0x8825 || tag == 0x2bc || tag >= 0x9c9b && tag <= 0x9c9f;
                    if (tag == 0x2bc && Xmp(value, (int)bytes)) { orientation = true; optional = false; }
                    if (tag == 0x112)
                    {
                        uint rotation = Number(field, little, end);
                        if (rotation != 1) orientation = true;
                        optional = false;
                    }
                    if (tag == 0xa001 || tag == 0x8773) optional = false;
                    if (optional)
                    {
                        Mark(entry, 12);
                        if (bytes > 4) Mark(value, (int)bytes);
                    }
                    if (tag == 0x8769 || tag == 0x8825 || tag == 0xa005)
                        pending.Enqueue(Tuple.Create(Number(field, little, end), tag == 0x8825 ? 2 : 3));
                    if (tag == 0x10e || tag == 0x9c9c)
                        Comment(value, (int)bytes, tag == 0x9c9c ? Encoding.Unicode : Encoding.GetEncoding(28591));
                    if (tag == 0x9286 && bytes >= 8)
                    {
                        Encoding encoding = Match(value, "UNICODE\0") ? (little ? Encoding.Unicode : Encoding.BigEndianUnicode) : Encoding.GetEncoding(28591);
                        Comment(value + 8, (int)bytes - 8, encoding);
                    }
                }
                if (kind != 0) { Mark(p, 2); Mark(p + 2 + count * 12, 4); }
                TiffField f;
                if (standalone && kind == 0 && width == 0)
                {
                    if (fields.TryGetValue(0x100, out f)) width = Number(f, little, end);
                    if (fields.TryGetValue(0x101, out f)) height = Number(f, little, end);
                    if (fields.TryGetValue(0x102, out f))
                    {
                        depth = 0;
                        if (f.Type != 3) throw new InvalidDataException("Invalid TIFF bits per sample.");
                        for (int i = 0; i < f.Count; i++) depth += U16(f.Offset + i * 2, little, end);
                    }
                }
                if (kind == 2) Gps(fields, little, end);
                TiffField offset, size;
                if (kind == 1 && fields.TryGetValue(0x201, out offset) && fields.TryGetValue(0x202, out size))
                {
                    uint relative = Number(offset, little, end), bytes = Number(size, little, end);
                    if (relative > length) throw new InvalidDataException("Invalid thumbnail offset.");
                    Require(start + (int)relative, bytes, end);
                    Thumbnail(start + (int)relative, (int)bytes);
                    Mark(start + (int)relative, (int)bytes);
                }
                if (kind == 1 && fields.TryGetValue(0x111, out offset) && fields.TryGetValue(0x117, out size))
                {
                    if (offset.Count != size.Count) throw new InvalidDataException("Invalid thumbnail strips.");
                    long total = 0;
                    for (int i = 0; i < offset.Count; i++)
                    {
                        uint relative = Number(offset, little, end, i), bytes = Number(size, little, end, i);
                        if (relative > length) throw new InvalidDataException("Invalid thumbnail strip offset.");
                        Require(start + (int)relative, bytes, end);
                        Thumbnail(start + (int)relative, (int)bytes);
                        total += bytes;
                        Mark(start + (int)relative, (int)bytes);
                    }
                    if (offset.Count > 1) details.Add(string.Format(CultureInfo.InvariantCulture, "Thumbnail total: {0:N0} bytes in {1} strips (offsets listed above)", total, offset.Count));
                }
                uint next = U32(p + 2 + count * 12, little, end);
                if (next != 0) pending.Enqueue(Tuple.Create(next, standalone ? 0 : 1));
            }
            return orientation;
        }

        private uint Number(TiffField field, bool little, int end, int index = 0)
        {
            if (index < 0 || index >= field.Count) throw new InvalidDataException("Missing Exif numeric value.");
            if (field.Type == 3) return (uint)U16(field.Offset + index * 2, little, end);
            if (field.Type == 4 || field.Type == 13) return U32(field.Offset + index * 4, little, end);
            if (field.Type == 1) return data[field.Offset + index];
            throw new InvalidDataException("Invalid Exif numeric type.");
        }

        private double Coordinate(TiffField field, bool little, int end)
        {
            if (field.Type != 5 || field.Count != 3) throw new InvalidDataException("Invalid Exif GPS coordinate.");
            double value = 0;
            for (int i = 0; i < 3; i++)
            {
                uint denominator = U32(field.Offset + i * 8 + 4, little, end);
                if (denominator == 0) throw new InvalidDataException("Zero denominator in Exif GPS coordinate.");
                double component = (double)U32(field.Offset + i * 8, little, end) / denominator;
                if (i > 0 && component >= 60) throw new InvalidDataException("Invalid GPS minutes/seconds.");
                value += component / (i == 0 ? 1 : i == 1 ? 60 : 3600);
            }
            return value;
        }

        private void Gps(Dictionary<int, TiffField> fields, bool little, int end)
        {
            TiffField lat, lon, latRef, lonRef;
            if (!fields.TryGetValue(2, out lat) || !fields.TryGetValue(4, out lon) || !fields.TryGetValue(1, out latRef) || !fields.TryGetValue(3, out lonRef))
            {
                details.Add("Geolocation: incomplete GPS tags (coordinates unavailable)");
                return;
            }
            if (latRef.Length == 0 || lonRef.Length == 0 || (data[latRef.Offset] != 'N' && data[latRef.Offset] != 'S') || (data[lonRef.Offset] != 'E' && data[lonRef.Offset] != 'W'))
                throw new InvalidDataException("Invalid Exif GPS hemisphere.");
            double latitude = Coordinate(lat, little, end), longitude = Coordinate(lon, little, end);
            Location(data[latRef.Offset] == 'S' ? -latitude : latitude, data[lonRef.Offset] == 'W' ? -longitude : longitude);
        }

        private void Location(double latitude, double longitude)
        {
            // Test Images: https://www.geoimgr.com/sample-photos.html
            if (double.IsNaN(latitude) || double.IsNaN(longitude) || double.IsInfinity(latitude) || double.IsInfinity(longitude) || Math.Abs(latitude) > 90 || Math.Abs(longitude) > 180)
                throw new InvalidDataException("GPS coordinate is out of range.");
            details.Add(string.Format(CultureInfo.InvariantCulture, "Geolocation: [{0:0.######}, {1:0.######}]", latitude, longitude));
        }

        private bool Xmp(int offset, int length) { return Xmp(data, offset, length); }

        private bool Xmp(byte[] bytes, int offset, int length)
        {
            try
            {
                using (var stream = new MemoryStream(bytes, offset, length))
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 }))
                {
                    XDocument document = XDocument.Load(reader);
                    string latitude = null, longitude = null;
                    bool orientation = false;
                    foreach (XElement element in document.Descendants())
                    {
                        if (element.Name.LocalName == "description" || element.Name.LocalName == "UserComment") Comment(element.Value);
                        if (element.Name.LocalName == "GPSLatitude") latitude = element.Value;
                        if (element.Name.LocalName == "GPSLongitude") longitude = element.Value;
                        if (element.Name.LocalName == "Orientation" && element.Value != "1") orientation = true;
                        foreach (XAttribute attribute in element.Attributes())
                        {
                            if (attribute.Name.LocalName == "GPSLatitude") latitude = attribute.Value;
                            if (attribute.Name.LocalName == "GPSLongitude") longitude = attribute.Value;
                            if (attribute.Name.LocalName == "Orientation" && attribute.Value != "1") orientation = true;
                            if (attribute.Name.LocalName == "description" || attribute.Name.LocalName == "UserComment") Comment(attribute.Value);
                        }
                    }
                    if (latitude != null && longitude != null) Location(XmpCoordinate(latitude, true), XmpCoordinate(longitude, false));
                    else if (latitude != null || longitude != null) details.Add("Geolocation: incomplete XMP coordinates");
                    return orientation;
                }
            }
            catch (XmlException e) { throw new InvalidDataException("Invalid or oversized XMP: " + e.Message, e); }
        }

        private static double XmpCoordinate(string value, bool latitude)
        {
            value = value.Trim();
            int sign = 1;
            if (value.Length > 0 && char.IsLetter(value[value.Length - 1]))
            {
                char hemisphere = char.ToUpperInvariant(value[value.Length - 1]);
                if (latitude ? hemisphere != 'N' && hemisphere != 'S' : hemisphere != 'E' && hemisphere != 'W')
                    throw new InvalidDataException("Invalid XMP GPS hemisphere.");
                sign = hemisphere == 'S' || hemisphere == 'W' ? -1 : 1;
                value = value.Substring(0, value.Length - 1);
            }
            string[] parts = value.Split(',');
            if (parts.Length > 3) throw new InvalidDataException("Invalid XMP GPS coordinate.");
            double result = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                double component;
                if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out component) || i > 0 && (component < 0 || component >= 60))
                    throw new InvalidDataException("Invalid XMP GPS coordinate.");
                result += component / (i == 0 ? 1 : i == 1 ? 60 : 3600);
            }
            return sign * result;
        }
    }
}
