using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Sledge.BspEditor.Assets;
using Sledge.Providers.Texture;

namespace Sledge.BspEditor.Environment
{
    /// <summary>
    /// Exposes Working/textures as a normal Sledge texture package. This means
    /// the existing texture browser, texture application tool and texture
    /// preview pipeline can use game PNG/JPG/BMP/GIF/TGA files directly.
    /// </summary>
    public sealed class WorkingTexturePackage : TexturePackage
    {
        private readonly string _workingRoot;
        private readonly Dictionary<string, string> _files;

        public WorkingTexturePackage(string workingRoot)
            : base("Working/textures", "Working")
        {
            _workingRoot = Path.GetFullPath(workingRoot);
            _files = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            Scan();
        }

        private void Scan()
        {
            var textureRoot = Path.Combine(_workingRoot, "textures");
            if (!Directory.Exists(textureRoot)) return;

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(textureRoot, "*.*", SearchOption.AllDirectories); }
            catch { return; }

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file);
                if (!IsTextureExtension(ext)) continue;

                var relative = file.Substring(_workingRoot.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
                if (relative.Length == 0) continue;

                Textures.Add(relative);
                _files[relative] = file;
            }
        }

        public override async Task<IEnumerable<TextureItem>> GetTextures(IEnumerable<string> names)
        {
            var result = new List<TextureItem>();
            foreach (var name in names ?? Enumerable.Empty<string>())
            {
                var item = await GetTexture(name);
                if (item != null) result.Add(item);
            }
            return result;
        }

        public override Task<TextureItem> GetTexture(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Task.FromResult<TextureItem>(null);
            if (!_files.TryGetValue(name, out var file) || !File.Exists(file))
                return Task.FromResult<TextureItem>(null);

            try
            {
                using (var image = TextureImageLoader.Load(file))
                {
                    return Task.FromResult(new TextureItem(name, TextureFlags.None, image.Width, image.Height)
                    {
                        Reference = file
                    });
                }
            }
            catch
            {
                return Task.FromResult<TextureItem>(null);
            }
        }

        public override ITextureStreamSource GetStreamSource()
        {
            return new WorkingTextureStreamSource(_files);
        }

        private static bool IsTextureExtension(string ext)
        {
            switch (ext?.ToLowerInvariant())
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".bmp":
                case ".gif":
                case ".tga":
                    return true;
                default:
                    return false;
            }
        }

        private sealed class WorkingTextureStreamSource : ITextureStreamSource
        {
            private readonly Dictionary<string, string> _files;

            public WorkingTextureStreamSource(Dictionary<string, string> files)
            {
                _files = files;
            }

            public bool HasImage(string item) => item != null && _files.ContainsKey(item);

            public Task<Bitmap> GetImage(string item, int maxWidth, int maxHeight)
            {
                if (item == null || !_files.TryGetValue(item, out var file) || !File.Exists(file))
                    return Task.FromResult<Bitmap>(null);

                try
                {
                    using (var source = TextureImageLoader.Load(file))
                    {
                        var width = source.Width;
                        var height = source.Height;
                        var scale = Math.Min(1.0, Math.Min((double)maxWidth / width, (double)maxHeight / height));
                        var targetWidth = Math.Max(1, (int)Math.Round(width * scale));
                        var targetHeight = Math.Max(1, (int)Math.Round(height * scale));

                        var result = new Bitmap(targetWidth, targetHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        using (var g = Graphics.FromImage(result))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.DrawImage(source, new Rectangle(0, 0, targetWidth, targetHeight));
                        }
                        return Task.FromResult(result);
                    }
                }
                catch
                {
                    return Task.FromResult<Bitmap>(null);
                }
            }

            public void Dispose() { }
        }
    }
}
