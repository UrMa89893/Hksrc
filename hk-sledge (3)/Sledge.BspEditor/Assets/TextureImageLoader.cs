using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Sledge.BspEditor.Assets
{
    internal static class TextureImageLoader
    {
        public static Bitmap Load(string path)
        {
            if (string.Equals(Path.GetExtension(path), ".tga", StringComparison.OrdinalIgnoreCase))
                return LoadTga(path);

            using (var image = Image.FromFile(path))
                return new Bitmap(image);
        }

        private static Bitmap LoadTga(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
            {
                var idLength = reader.ReadByte();
                var colourMapType = reader.ReadByte();
                var imageType = reader.ReadByte();
                if (colourMapType != 0 || (imageType != 2 && imageType != 10))
                    throw new InvalidDataException("Only true-colour TGA textures are supported.");

                reader.ReadUInt16(); // colour map first entry
                reader.ReadUInt16(); // colour map length
                reader.ReadByte();   // colour map entry size
                reader.ReadUInt16(); // x origin
                reader.ReadUInt16(); // y origin
                var width = reader.ReadUInt16();
                var height = reader.ReadUInt16();
                var depth = reader.ReadByte();
                var descriptor = reader.ReadByte();
                if (depth != 24 && depth != 32)
                    throw new InvalidDataException("Only 24-bit and 32-bit TGA textures are supported.");

                if (idLength > 0) reader.ReadBytes(idLength);

                var bytesPerPixel = depth / 8;
                var pixels = new byte[width * height * bytesPerPixel];
                if (imageType == 2)
                {
                    ReadExact(reader, pixels);
                }
                else
                {
                    var written = 0;
                    while (written < pixels.Length)
                    {
                        var header = reader.ReadByte();
                        var count = (header & 0x7f) + 1;
                        if ((header & 0x80) != 0)
                        {
                            var pixel = reader.ReadBytes(bytesPerPixel);
                            for (var i = 0; i < count; i++)
                            {
                                Buffer.BlockCopy(pixel, 0, pixels, written, bytesPerPixel);
                                written += bytesPerPixel;
                            }
                        }
                        else
                        {
                            var size = count * bytesPerPixel;
                            var data = reader.ReadBytes(size);
                            Buffer.BlockCopy(data, 0, pixels, written, size);
                            written += size;
                        }
                    }
                }

                var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                var flipVertical = (descriptor & 0x20) == 0;
                var flipHorizontal = (descriptor & 0x10) != 0;
                for (var y = 0; y < height; y++)
                {
                    var sourceY = flipVertical ? height - 1 - y : y;
                    for (var x = 0; x < width; x++)
                    {
                        var sourceX = flipHorizontal ? width - 1 - x : x;
                        var index = (sourceY * width + sourceX) * bytesPerPixel;
                        var b = pixels[index];
                        var g = pixels[index + 1];
                        var r = pixels[index + 2];
                        var a = bytesPerPixel == 4 ? pixels[index + 3] : (byte)255;
                        bitmap.SetPixel(x, y, Color.FromArgb(a, r, g, b));
                    }
                }
                return bitmap;
            }
        }

        private static void ReadExact(BinaryReader reader, byte[] destination)
        {
            var offset = 0;
            while (offset < destination.Length)
            {
                var chunk = reader.Read(destination, offset, destination.Length - offset);
                if (chunk <= 0) throw new EndOfStreamException();
                offset += chunk;
            }
        }
    }
}
