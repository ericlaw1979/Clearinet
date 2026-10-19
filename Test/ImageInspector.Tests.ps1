#Requires -Version 5.1
param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Extensions\bin\Release'))
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
    throw 'Run with powershell.exe -NoProfile -STA -File .\Test\ImageInspector.Tests.ps1'
}
$directory = (Resolve-Path -LiteralPath $BuildDirectory).Path
$corePath = Join-Path $directory 'Clearinet.exe'
$extensionPath = Join-Path $directory 'CAE-Default.dll'
$null = [Reflection.Assembly]::LoadFrom($corePath)
$null = [Reflection.Assembly]::LoadFrom($extensionPath)
$tests = @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Clearinet;
using Extensions;

public static class ImageInspectorTests
{
    static int assertions;
    static readonly MethodInfo AnalyzeMethod = typeof(ImageInspector).Assembly.GetType("Extensions.ImageMetadata").GetMethod("Analyze", BindingFlags.Static | BindingFlags.NonPublic);
    static string Analyze(byte[] bytes) { return (string)AnalyzeMethod.Invoke(null, new object[] { bytes, null }); }
    static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }
    static void Contains(string text, params string[] values)
    {
        foreach (string value in values) Check(text.Contains(value), "Missing [" + value + "] in:\n" + text);
    }
    static void Accounting(byte[] file, int count)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = AnalyzeMethod.DeclaringType;
        object analyzer = Activator.CreateInstance(type, flags, null, new object[] { file }, null);
        string parser = file[0] == 137 ? "Png" : file[0] == 255 ? "Jpeg" : file[0] == 'G' ? "Gif" : file[0] == 'R' ? "WebP" : "Exif";
        type.GetMethod(parser, flags).Invoke(analyzer, parser == "Exif" ? new object[] { 0, file.Length, true } : null);
        long actual = (long)type.GetMethod("MetadataBytes", flags).Invoke(analyzer, null);
        Check(actual == count, "Expected exactly " + count + " metadata bytes; got " + actual);
        string output = Analyze(file);
        if (count <= 512) Check(!output.Contains("Non-rendering metadata:"), "Totals of 512 bytes or less are hidden");
        else
        {
            string expected = string.Format(CultureInfo.InvariantCulture, "Non-rendering metadata: {0:N0} bytes ({1:0.00}% of file)", count, 100.0 * count / file.Length);
            Contains(output, expected);
        }
    }
    static byte[] Join(params byte[][] parts) { return parts.SelectMany(p => p).ToArray(); }
    static byte[] Text(string value) { return Encoding.UTF8.GetBytes(value); }
    static byte[] Be(int value) { return new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value }; }
    static byte[] Chunk(string type, byte[] payload)
    {
        byte[] content = Join(Text(type), payload);
        uint crc = 0xffffffff;
        foreach (byte b in content)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
        }
        return Join(Be(payload.Length), content, Be(unchecked((int)~crc)));
    }
    static byte[] Png(params byte[][] chunks)
    {
        return Join(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, Chunk("IHDR", Join(Be(2), Be(3), new byte[] { 8, 6, 0, 0, 0 })), Join(chunks), Chunk("IEND", new byte[0]));
    }
    static byte[] Segment(byte marker, byte[] payload)
    {
        return Join(new byte[] { 255, marker, (byte)((payload.Length + 2) >> 8), (byte)(payload.Length + 2) }, payload);
    }
    static byte[] Jpeg(params byte[][] segments)
    {
        return Join(new byte[] { 255, 216 }, Join(segments), new byte[] { 255, 217 });
    }
    static byte[] RiffChunk(string type, byte[] value)
    {
        return Join(Text(type), BitConverter.GetBytes(value.Length), value, value.Length % 2 == 0 ? new byte[0] : new byte[1]);
    }
    static byte[] WebP(params byte[][] chunks)
    {
        byte[] body = Join(Text("WEBP"), Join(chunks));
        return Join(Text("RIFF"), BitConverter.GetBytes(body.Length), body);
    }
    static void Put16(byte[] b, int p, int value, bool little)
    {
        b[p] = (byte)(little ? value : value >> 8); b[p + 1] = (byte)(little ? value >> 8 : value);
    }
    static void Put32(byte[] b, int p, int value, bool little)
    {
        byte[] valueBytes = little ? BitConverter.GetBytes(value) : Be(value);
        Buffer.BlockCopy(valueBytes, 0, b, p, 4);
    }
    static void Field(byte[] b, int p, int tag, int type, int count, int value, bool little)
    {
        Put16(b, p, tag, little); Put16(b, p + 2, type, little); Put32(b, p + 4, count, little);
        if (type == 3 && count == 1) Put16(b, p + 8, value, little); else Put32(b, p + 8, value, little);
    }
    static byte[] Exif(bool little, int orientation)
    {
        var b = new byte[304];
        b[0] = b[1] = (byte)(little ? 'I' : 'M'); Put16(b, 2, 42, little); Put32(b, 4, 8, little);
        Put16(b, 8, 3, little);
        Field(b, 10, 0x8825, 4, 1, 60, little);
        Field(b, 22, 0x10e, 2, 6, 220, little);
        Field(b, 34, 0x112, 3, 1, orientation, little);
        Put32(b, 46, 120, little);
        Put16(b, 60, 4, little);
        Field(b, 62, 1, 2, 2, 0, little); b[70] = (byte)'N';
        Field(b, 74, 2, 5, 3, 160, little);
        Field(b, 86, 3, 2, 2, 0, little); b[94] = (byte)'W';
        Field(b, 98, 4, 5, 3, 184, little);
        Put16(b, 120, 2, little);
        Field(b, 122, 0x201, 4, 1, 300, little);
        Field(b, 134, 0x202, 4, 1, 4, little);
        int[] components = { 47, 30, 0, 122, 15, 0 };
        for (int i = 0; i < components.Length; i++)
        {
            Put32(b, 160 + i * 8, components[i], little); Put32(b, 164 + i * 8, 1, little);
        }
        Buffer.BlockCopy(Text("hello\0"), 0, b, 220, 6);
        b[300] = b[302] = 255; b[301] = 216; b[303] = 217;
        return b;
    }
    static string Xmp(string latitude, string longitude, string comment)
    {
        return "<x:xmpmeta xmlns:x='adobe:ns:meta/' xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#' xmlns:exif='http://ns.adobe.com/exif/1.0/' xmlns:dc='http://purl.org/dc/elements/1.1/'><rdf:RDF><rdf:Description exif:GPSLatitude='" + latitude + "' exif:GPSLongitude='" + longitude + "'><dc:description>" + comment + "</dc:description></rdf:Description></rdf:RDF></x:xmpmeta>";
    }
    static byte[] GifComment(string text)
    {
        byte[] bytes = Text(text);
        using (var stream = new MemoryStream())
        {
            stream.WriteByte(0x21); stream.WriteByte(0xfe);
            for (int p = 0; p < bytes.Length; p += 255)
            {
                int n = Math.Min(255, bytes.Length - p); stream.WriteByte((byte)n); stream.Write(bytes, p, n);
            }
            stream.WriteByte(0); return stream.ToArray();
        }
    }
    static byte[] Gif(int frames, byte[] comment)
    {
        byte[] image = new byte[] { 0x2c, 0, 0, 0, 0, 1, 0, 1, 0, 0, 2, 2, 0x44, 1, 0 };
        byte[] loop = Join(new byte[] { 0x21, 0xff, 11 }, Text("NETSCAPE2.0"), new byte[] { 3, 1, 0, 0, 0 });
        return Join(Text("GIF89a"), new byte[] { 1, 0, 1, 0, 0x80, 0, 0, 0, 0, 0, 255, 255, 255 }, loop,
            comment, Join(Enumerable.Repeat(image, frames).ToArray()), new byte[] { 0x3b });
    }
    static byte[] Saved(ImageFormat format)
    {
        using (var bitmap = new Bitmap(7, 5, PixelFormat.Format24bppRgb))
        using (var stream = new MemoryStream()) { bitmap.Save(stream, format); return stream.ToArray(); }
    }
    static void RunPng()
    {
        byte[] text = Chunk("tEXt", Text("Comment\0hello PNG"));
        byte[] file = Png(text, Chunk("tRNS", new byte[] { 0, 0 }), Chunk("gAMA", Be(45455)), Chunk("iCCP", Text("profile")));
        Contains(Analyze(file), "Dimensions: 2 x 3 pixels", "Color depth: 32 bits/pixel", "hello PNG");
        Accounting(file, text.Length);
        byte[] control = new byte[26];
        byte[] animated = Png(text, Chunk("acTL", Join(Be(2), Be(0))), Chunk("fcTL", control), Chunk("IDAT", new byte[0]), Chunk("fcTL", control), Chunk("fdAT", Be(1)));
        Contains(Analyze(animated), "Frames: 2"); Accounting(animated, text.Length);
        byte[] badFrames = Png(Chunk("acTL", Join(Be(3), Be(0))), Chunk("fcTL", control));
        Contains(Analyze(badFrames), "unavailable (incomplete analysis)", "frame count");
        foreach (int length in new[] { 1023, 1024, 1025, 4000 })
        {
            string output = Analyze(Png(Chunk("tEXt", Text("Comment\0" + new string('A', length)))));
            string preview = output.Substring(output.IndexOf("Comments (first 1 KB):\r\n", StringComparison.Ordinal) + "Comments (first 1 KB):\r\n".Length);
            Check(preview == new string('A', Math.Min(length, 1024)) + (length > 1024 ? "..." : ""), "Exact PNG preview limit " + length);
        }
        byte[] compressed = Chunk("zTXt", Join(Text("Comment\0"), new byte[1], Utilities.ZLibDeflaterCompress(Text(new string('Z', 5000)))));
        string compressedOutput = Analyze(Png(compressed));
        Check(compressedOutput.EndsWith(new string('Z', 1024) + "...", StringComparison.Ordinal), "Compressed PNG preview bounded at 1024 bytes");
        Accounting(Png(compressed), compressed.Length);
        byte[] unicode = Chunk("iTXt", Join(Text("Comment\0"), new byte[] { 0, 0, 0, 0 }, Text(new string('a', 1023) + "\u00e9")));
        string unicodeOutput = Analyze(Png(unicode));
        Check(unicodeOutput.EndsWith(new string('a', 1023) + "...", StringComparison.Ordinal) && !unicodeOutput.Contains("\ufffd"), "UTF-8 boundary has no replacement character");
        string xmp = Xmp("47,30N", "122,15W", "PNG location");
        foreach (bool zipped in new[] { false, true })
        {
            byte[] payload = zipped ? Utilities.ZLibDeflaterCompress(Text(xmp)) : Text(xmp);
            byte[] chunk = Chunk("iTXt", Join(Text("XML:com.adobe.xmp\0"), new byte[] { (byte)(zipped ? 1 : 0), 0, 0, 0 }, payload));
            byte[] image = Png(chunk);
            Contains(Analyze(image), "Geolocation: [47.5, -122.25]", "PNG location"); Accounting(image, chunk.Length);
        }
    }
    static void RunJpeg()
    {
        byte[] comment = Segment(0xfe, Text("JPEG comment"));
        byte[] sof = Segment(0xc2, new byte[] { 8, 0, 3, 0, 2, 3, 1, 0x11, 0, 2, 0x11, 0, 3, 0x11, 0 });
        byte[] profile = Segment(0xe2, Join(Text("ICC_PROFILE\0"), new byte[4]));
        byte[] adobe = Segment(0xee, Join(Text("Adobe"), new byte[7]));
        byte[] file = Jpeg(profile, adobe, comment, sof);
        Contains(Analyze(file), "JPEG: progressive", "JPEG comment", "Dimensions: 2 x 3 pixels", "Color depth: 24 bits/pixel"); Accounting(file, comment.Length);
        Check(!Analyze(Jpeg(Segment(0xc0, sof.Skip(4).ToArray()))).Contains("progressive"), "Baseline JPEG is not progressive");
        byte[] scan = Segment(0xda, new byte[] { 1, 1, 0, 0, 63, 0 });
        byte[] scanned = Jpeg(sof, scan, new byte[] { 1, 2, 255, 0, 3, 255, 0xd0, 4 }, comment, scan, new byte[] { 5, 6 });
        Contains(Analyze(scanned), "JPEG comment", "JPEG: progressive"); Accounting(scanned, comment.Length);
        foreach (bool little in new[] { true, false })
        {
            byte[] exif = Segment(0xe1, Join(Text("Exif\0\0"), Exif(little, 1)));
            file = Jpeg(exif, sof);
            Contains(Analyze(file), "Geolocation: [47.5, -122.25]", "Thumbnail: 4 bytes at file offset 312 (0x138)", "hello"); Accounting(file, exif.Length);
            byte[] oriented = Jpeg(Segment(0xe1, Join(Text("Exif\0\0"), Exif(little, 6))), sof);
            Accounting(oriented, 166);
            byte[] invalidGps = Exif(little, 1); Put32(invalidGps, 164, 0, little);
            Contains(Analyze(Jpeg(Segment(0xe1, Join(Text("Exif\0\0"), invalidGps)))), "incomplete analysis", "Zero denominator");
            byte[] cyclic = Exif(little, 1); Put32(cyclic, 146, 8, little);
            Contains(Analyze(Jpeg(Segment(0xe1, Join(Text("Exif\0\0"), cyclic)))), "Cyclic or excessive");
        }
        byte[] jfif = Join(Text("JFIF\0"), new byte[] { 1, 2, 0, 0, 1, 0, 1, 1, 1, 10, 20, 30 });
        Contains(Analyze(Jpeg(Segment(0xe0, jfif))), "Thumbnail: 3 bytes at file offset 20 (0x14)");
        Contains(Analyze(Jpeg(Segment(0xe0, Join(Text("JFXX\0"), new byte[] { 0x10, 255, 216, 255, 217 })))), "Thumbnail: 4 bytes at file offset 12 (0xC)");
        byte[] thumbnailHeader = Join(Be(1), Be(1), Be(1), Be(4), Be(4), Be(4), new byte[] { 0, 24, 0, 1 });
        byte[] thumbnailResource = Join(Text("8BIM"), new byte[] { 4, 12, 0, 0 }, Be(32), thumbnailHeader, new byte[] { 255, 216, 255, 217 });
        byte[] caption = Join(new byte[] { 0x1c, 2, 120, 0, 4 }, Text("IPTC"));
        byte[] captionResource = Join(Text("8BIM"), new byte[] { 4, 4, 0, 0 }, Be(caption.Length), caption, new byte[1]);
        byte[] photoshop = Segment(0xed, Join(Text("Photoshop 3.0\0"), thumbnailResource, captionResource));
        Contains(Analyze(Jpeg(photoshop)), "Thumbnail: 4 bytes at file offset 60 (0x3C)", "IPTC"); Accounting(Jpeg(photoshop), photoshop.Length);
        byte[] xmp = Segment(0xe1, Join(Text("http://ns.adobe.com/xap/1.0/\0"), Text(Xmp("12,30S", "34,15E", "XMP comment"))));
        Contains(Analyze(Jpeg(xmp)), "Geolocation: [-12.5, 34.25]", "XMP comment"); Accounting(Jpeg(xmp), xmp.Length);
        Contains(Analyze(Jpeg(Segment(0xe1, Join(Text("http://ns.adobe.com/xap/1.0/\0"), Text("<!DOCTYPE x [<!ENTITY t SYSTEM 'file:///not-read'>]><x>&t;</x>"))))), "Invalid or oversized XMP", "incomplete analysis");
    }
    static void RunGifAndWebP()
    {
        byte[] comment = GifComment(new string('G', 1200));
        byte[] gif = Gif(2, comment);
        string output = Analyze(gif);
        Contains(output, "Frames: 2", "Dimensions: 1 x 1 pixels", "Color depth: 1 bits/pixel");
        Check(output.EndsWith(new string('G', 1024) + "...", StringComparison.Ordinal), "GIF subblocks concatenate and truncate exactly"); Accounting(gif, comment.Length);
        Contains(Analyze(Gif(1, GifComment("static"))), "Frames: 1", "static");
        byte[] exif = RiffChunk("EXIF", Exif(true, 1));
        byte[] xmp = RiffChunk("XMP ", Text(Xmp("47,30N", "122,15W", "WebP comment")));
        byte[] webp = WebP(RiffChunk("VP8X", new byte[] { 0x1c, 0, 0, 0, 1, 0, 0, 2, 0, 0 }), RiffChunk("ICCP", Text("icc")), exif, xmp);
        Contains(Analyze(webp), "Dimensions: 2 x 3 pixels", "Color depth: 32 bits/pixel", "Geolocation: [47.5, -122.25]", "WebP comment", "Thumbnail: 4 bytes at file offset 350 (0x15E)"); Accounting(webp, exif.Length + xmp.Length);
        Accounting(WebP(RiffChunk("EXIF", Exif(true, 6))), 166);
        byte[] pngExif = Chunk("eXIf", Exif(false, 1));
        Contains(Analyze(Png(pngExif)), "Geolocation: [47.5, -122.25]", "Thumbnail: 4 bytes at file offset 341 (0x155)"); Accounting(Png(pngExif), pngExif.Length);
    }
    static void RunTiffAndText()
    {
        byte[] savedTiff = Saved(ImageFormat.Tiff);
        Contains(Analyze(savedTiff), "Dimensions: 7 x 5 pixels", "Color depth: 24 bits/pixel"); Accounting(savedTiff, 0);
        foreach (int count in new[] { 511, 512, 513 }) Accounting(Jpeg(Segment(0xfe, Text(new string('T', count - 4)))), count);
        foreach (bool little in new[] { true, false })
        {
            foreach (bool reduced in new[] { true, false })
            {
                var tiff = new byte[304]; tiff[0] = tiff[1] = (byte)(little ? 'I' : 'M');
                Put16(tiff, 2, 42, little); Put32(tiff, 4, 8, little); Put16(tiff, 8, 6, little);
                Field(tiff, 10, 0x100, 4, 1, 7, little); Field(tiff, 22, 0x101, 4, 1, 5, little);
                Field(tiff, 34, 0x102, 3, 3, 100, little); Field(tiff, 46, 0x106, 3, 1, 2, little);
                Field(tiff, 58, 0x10e, 2, 6, 110, little); Field(tiff, 70, 0x111, 4, 1, 200, little);
                Put32(tiff, 82, 120, little);
                Put16(tiff, 100, 8, little); Put16(tiff, 102, 8, little); Put16(tiff, 104, 8, little);
                Buffer.BlockCopy(Text("hello\0"), 0, tiff, 110, 6);
                Put16(tiff, 120, 5, little);
                Field(tiff, 122, 0xfe, 4, 1, reduced ? 1 : 0, little);
                Field(tiff, 134, 0x100, 4, 1, 2, little); Field(tiff, 146, 0x101, 4, 1, 3, little);
                Field(tiff, 158, 0x111, 4, 1, 300, little); Field(tiff, 170, 0x117, 4, 1, 4, little);
                string report = Analyze(tiff);
                Contains(report, "Dimensions: 7 x 5 pixels", "Color depth: 24 bits/pixel", "hello");
                if (reduced) Contains(report, "Thumbnail: 4 bytes at file offset 300 (0x12C)");
                else Check(!report.Contains("Thumbnail:"), "Full-sized TIFF pages are not thumbnails");
                Accounting(tiff, reduced ? 88 : 18);
            }
            byte[] unicodeExif = Exif(little, 1);
            byte[] userComment = Join(Text("UNICODE\0"), (little ? Encoding.Unicode : Encoding.BigEndianUnicode).GetBytes("hi"));
            Field(unicodeExif, 22, 0x9286, 7, userComment.Length, 220, little);
            Buffer.BlockCopy(userComment, 0, unicodeExif, 220, userComment.Length);
            Contains(Analyze(Jpeg(Segment(0xe1, Join(Text("Exif\0\0"), unicodeExif)))), "Comments (first 1 KB):\r\nhi");
        }
        byte[] first = Chunk("tEXt", Text("Comment\0" + new string('A', 800)));
        byte[] second = Chunk("zTXt", Join(Text("Comment\0"), new byte[1], Utilities.ZLibDeflaterCompress(Text(new string('B', 1000)))));
        string combined = Analyze(Png(first, second));
        Check(combined.EndsWith(new string('A', 800) + "\r\n" + new string('B', 224) + "...", StringComparison.Ordinal), "Comment budget is combined across metadata records");
        Accounting(Png(first, second), first.Length + second.Length);
        string orientedXmp = Xmp("47,30N", "122,15W", "orientation comment").Replace("exif:GPSLatitude=", "xmlns:tiff='http://ns.adobe.com/tiff/1.0/' tiff:Orientation='6' exif:GPSLatitude=");
        byte[] xmp = Segment(0xe1, Join(Text("http://ns.adobe.com/xap/1.0/\0"), Text(orientedXmp)));
        Accounting(Jpeg(xmp), 0); Contains(Analyze(Jpeg(xmp)), "orientation comment", "Geolocation: [47.5, -122.25]");
        byte[] decimalXmp = Segment(0xe1, Join(Text("http://ns.adobe.com/xap/1.0/\0"), Text(Xmp("-12.5", "34.25", "decimal"))));
        Contains(Analyze(Jpeg(decimalXmp)), "Geolocation: [-12.5, 34.25]");
    }
    static void RunMalformed()
    {
        foreach (byte[] file in new[] { Png(Chunk("tEXt", Text("Comment\0hi"))), Gif(2, GifComment("hi")), Jpeg(Segment(0xe1, Join(Text("Exif\0\0"), Exif(true, 1)))), WebP(RiffChunk("EXIF", Exif(false, 1))) })
        {
            for (int i = 0; i < file.Length; i++)
            {
                string output = Analyze(file.Take(i).ToArray());
                Check(output.Contains("not supported") || output.Contains("incomplete analysis"), "Truncated prefix must not have a complete total: " + i);
            }
        }
        byte[] hugePng = Png(); Buffer.BlockCopy(Be(int.MaxValue), 0, hugePng, 8, 4);
        Contains(Analyze(hugePng), "incomplete analysis");
        byte[] hugeRiff = WebP(); for (int i = 4; i < 8; i++) hugeRiff[i] = 255;
        Contains(Analyze(hugeRiff), "incomplete analysis");
        var random = new Random(1234);
        foreach (byte[] seed in new[] { Png(), Gif(1, GifComment("hi")), Jpeg(Segment(0xe1, Join(Text("Exif\0\0"), Exif(true, 1)))), WebP(RiffChunk("EXIF", Exif(false, 1))) })
        {
            for (int i = 0; i < 300; i++)
            {
                byte[] mutated = (byte[])seed.Clone();
                int p = random.Next(mutated.Length); mutated[p] ^= (byte)random.Next(1, 256);
                string output = Analyze(mutated);
                Check(output.StartsWith("File size:", StringComparison.Ordinal), "Malformed input always yields a report");
            }
        }
    }
    static TextBox Metadata(ImageView view) { return (TextBox)typeof(ImageView).GetField("txtMetadata", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view); }
    static PictureBox Picture(ImageView view) { return (PictureBox)typeof(ImageView).GetField("pbImage", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view); }
    static HTTPResponseHeaders Headers(string mime) { return new HTTPResponseHeaders(200, "OK", new[] { "Content-Type: " + mime }); }
    static void RunIntegration()
    {
        var inspector = new ImageInspector();
        using (var tab = new TabPage())
        {
            inspector.AddToTab(tab);
            var view = (ImageView)tab.Controls[0];
            byte[] original = Saved(ImageFormat.Png);
            byte[] text = Chunk("tEXt", Text("Comment\0original bytes"));
            byte[] png = Join(original.Take(original.Length - 12).ToArray(), text, original.Skip(original.Length - 12).ToArray());
            inspector.Assign(Headers("image/png"), png, true);
            Contains(Metadata(view).Text, "Dimensions: 7 x 5 pixels", "Color depth: 24 bits/pixel", "original bytes");
            Check(Picture(view).Image != null && Metadata(view).ScrollBars == ScrollBars.Vertical, "Image and scrollable metadata appear together");
            var headers = Headers("IMAGE/PNG"); headers["Content-Encoding"] = "gzip";
            byte[] compressed = Utilities.GzipCompress(png);
            inspector.Assign(headers, compressed, true);
            Contains(Metadata(view).Text, "File size: " + png.Length.ToString("N0", CultureInfo.InvariantCulture) + " bytes", "original bytes");
            Check(Picture(view).Image != null, "Uppercase content type and gzip decode");
            headers = Headers("image/png"); headers["Transfer-Encoding"] = "chunked";
            inspector.Assign(headers, Join(Text(png.Length.ToString("x") + "\r\n"), png, Text("\r\n0\r\n\r\n")), true);
            Contains(Metadata(view).Text, "original bytes"); Check(Picture(view).Image != null, "Chunked PNG renders");
            inspector.Assign(Headers("image/gif"), Gif(2, GifComment("animated")), true);
            Contains(Metadata(view).Text, "Frames: 2", "animated"); Check(Picture(view).Image != null, "GIF frame count integration");
            inspector.Assign(Headers("image/bmp"), Saved(ImageFormat.Bmp), true);
            Contains(Metadata(view).Text, "Dimensions: 7 x 5 pixels", "not supported for this format");
            Check(!Metadata(view).Text.Contains("animated"), "Assign clears previous metadata");
            byte[] realJpeg = Saved(ImageFormat.Jpeg);
            inspector.Assign(Headers("image/jpeg"), Join(realJpeg.Take(2).ToArray(), Segment(0xe1, Join(Text("Exif\0\0"), Exif(false, 1))), realJpeg.Skip(2).ToArray()), true);
            Contains(Metadata(view).Text, "Dimensions: 7 x 5 pixels", "Geolocation: [47.5, -122.25]", "Thumbnail: 4 bytes at file offset 312 (0x138)");
            Check(Picture(view).Image != null, "Real JPEG renders alongside Exif metadata");
            byte[] webp = WebP(RiffChunk("VP8X", new byte[] { 0x0c, 0, 0, 0, 1, 0, 0, 2, 0, 0 }), RiffChunk("EXIF", Exif(true, 1)));
            inspector.Assign(Headers("image/webp"), webp, true);
            Contains(Metadata(view).Text, "File size: " + webp.Length.ToString("N0", CultureInfo.InvariantCulture) + " bytes", "Geolocation: [47.5, -122.25]", "Thumbnail: 4 bytes at file offset 338 (0x152)");
            inspector.Assign(Headers("image/jpeg"), Jpeg(Segment(0xfe, Text("visible despite rendering failure"))), true);
            Contains(Metadata(view).Text, "Unable to render image", "visible despite rendering failure");
            Check(Picture(view).Image == null, "Rendering failure clears previous image");
            inspector.Clear();
            Check(Metadata(view).Text == "" && Picture(view).Image == null, "Clear resets image and metadata");
        }
    }
    public static void Run()
    {
        RunPng(); RunJpeg(); RunGifAndWebP(); RunTiffAndText(); RunMalformed(); RunIntegration();
        Console.WriteLine("Image inspector assertions passed: " + assertions);
    }
}
'@
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.ReferencedAssemblies.AddRange([string[]]@('System.dll', 'System.Core.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll', $corePath, $extensionPath))
Add-Type -TypeDefinition $tests -CompilerParameters $compiler
[ImageInspectorTests]::Run()
