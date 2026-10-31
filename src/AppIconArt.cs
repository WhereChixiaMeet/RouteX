using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace NetProbe
{
    public static class AppIconArt
    {
        static Bitmap Scale(Image image, int size)
        {
            var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap)) { g.Clear(Color.Transparent); g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(image, new Rectangle(0, 0, size, size)); }
            return bitmap;
        }
        static Bitmap PngFrame(Stream stream, int size)
        {
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
            {
                if (reader.ReadUInt16() != 0 || reader.ReadUInt16() != 1) return null;
                int count = reader.ReadUInt16(); if (count < 1 || count > 256) return null;
                long bestOffset = 0; int bestLength = 0, score = int.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    int width = reader.ReadByte(); if (width == 0) width = 256;
                    reader.ReadBytes(7); uint length = reader.ReadUInt32(), offset = reader.ReadUInt32();
                    if (length < 8 || length > 16 * 1024 * 1024 || offset > stream.Length || length > stream.Length - offset) continue;
                    long next = stream.Position; stream.Position = offset;
                    bool png = reader.ReadUInt64() == 0x0A1A0A0D474E5089UL; stream.Position = next;
                    int distance = Math.Abs(width - size) + (width < size ? 256 : 0);
                    if (png && distance < score) { score = distance; bestOffset = offset; bestLength = (int)length; }
                }
                if (bestLength == 0) return null;
                stream.Position = bestOffset;
                using (var png = new MemoryStream(reader.ReadBytes(bestLength))) using (var image = Image.FromStream(png)) return Scale(image, size);
            }
        }
        public static Bitmap Load(string custom, Icon fallback, int size)
        {
            if (!string.IsNullOrWhiteSpace(custom) && File.Exists(custom))
            {
                string ext = Path.GetExtension(custom).ToLowerInvariant();
                if (ext == ".ico") { using (var stream = File.OpenRead(custom)) { var png = PngFrame(stream, size); if (png != null) return png; } }
                else if (ext != ".exe" && ext != ".dll") using (var image = Image.FromFile(custom)) return Scale(image, size);
                using (var bitmap = fallback.ToBitmap()) return Scale(bitmap, size);
            }
            using (var stream = typeof(AppIconArt).Assembly.GetManifestResourceStream("NetProbe.AppIcon.png"))
                if (stream != null) using (var image = Image.FromStream(stream)) return Scale(image, size);
            using (var bitmap = SystemIcons.Application.ToBitmap()) return Scale(bitmap, size);
        }
    }
}
