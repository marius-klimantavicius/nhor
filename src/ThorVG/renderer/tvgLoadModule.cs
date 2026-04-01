// Ported from ThorVG/src/renderer/tvgLoadModule.h

using System;
using System.IO;

namespace ThorVG
{
    /// <summary>
    /// Asset resolver callback. Mirrors C++ AssetResolver struct.
    /// </summary>
    public class AssetResolver
    {
        public Func<Paint, string, object?, bool>? func;
        public object? data;
    }

    /// <summary>
    /// Base load module. Mirrors C++ tvg::LoadModule.
    /// </summary>
    public abstract class LoadModule : IInlistNode<LoadModule>
    {
        // IInlistNode implementation
        public LoadModule? Prev { get; set; }
        public LoadModule? Next { get; set; }

        // Use either hashkey(data) or hashpath(path)
        public ulong hashkey;
        public string? hashpath;

        public FileType type;                           // current loader file type
        public int sharing;                             // reference count (atomic in C++)
        public bool readied;                            // read done already
        public bool cached;                             // cached for sharing

        protected LoadModule(FileType type) { this.type = type; }

        public void Cache(ulong data)
        {
            hashkey = data;
            cached = true;
        }

        public void Cache(string? data)
        {
            hashpath = data;
            cached = true;
        }

        public virtual bool Open(string path) => false;
        public virtual bool Open(byte[] data, uint size, string? rpath, bool copy) => false;
        public virtual bool Resize(Paint paint, float w, float h) => false;
        public virtual void Sync() { }

        public virtual bool Read()
        {
            if (readied) return false;
            readied = true;
            return true;
        }

        public virtual bool Close()
        {
            if (sharing == 0) return true;
            --sharing;
            return false;
        }

        /// <summary>
        /// Read a file into a byte array. Mirrors C++ LoadModule::open(path, size, text).
        /// </summary>
        public static byte[]? ReadFile(string path, bool text = false)
        {
            try
            {
                if (text)
                {
                    var content = File.ReadAllText(path);
                    return System.Text.Encoding.UTF8.GetBytes(content);
                }
                return File.ReadAllBytes(path);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Image loader base class. Mirrors C++ tvg::ImageLoader.
    /// </summary>
    public class ImageLoader : LoadModule
    {
        /// <summary>Desired color space for image decoding (shared across loaders).</summary>
        public static ColorSpace cs = ColorSpace.ABGR8888;

        public float w, h;                              // default image size
        public RenderSurface surface = new RenderSurface();

        public ImageLoader() : base(FileType.Unknown) { }
        public ImageLoader(FileType type) : base(type) { }

        public virtual bool Animatable() => false;
        public virtual Paint? GetPaint() => null;
        public virtual void Set(AssetResolver? resolver) { }

        public virtual unsafe RenderSurface? Bitmap()
        {
            if (surface.data != null && surface.data.Length > 0) return surface;
            return null;
        }
    }

    /// <summary>
    /// Font metrics for text rendering. Mirrors C++ FontMetrics.
    /// </summary>
    public class FontMetrics
    {
        public Point size;              // text width, height
        public float scale;
        public Point align;
        public Point box;
        public Point spacing = new Point(1.0f, 1.0f);
        public float fontSize;
        public uint lines = 1;              // line count
        public TextWrap wrap = TextWrap.None;
        public object? engine;          // engine extension (TtfMetrics in TTF loader)
    }

    /// <summary>
    /// Abstract font loader. Mirrors C++ tvg::FontLoader.
    /// </summary>
    public abstract class FontLoader : LoadModule
    {
        public const float DPI = 96.0f / 72.0f;
        public string? name;

        protected FontLoader(FileType type) : base(type) { }

        public abstract bool Get(FontMetrics fm, string? text, uint len, RenderPath output);
        public bool Get(FontMetrics fm, string? text, RenderPath output) => Get(fm, text, text != null ? (uint)text.Length : 0, output);
        public abstract void Transform(Paint paint, FontMetrics fm, float italicShear);
        public abstract void Release(FontMetrics fm);
        public abstract void Metrics(FontMetrics fm, out TextMetrics output);
        public abstract bool GlyphMetrics(FontMetrics fm, string ch, out ThorVG.GlyphMetrics output);
        public abstract void Copy(FontMetrics input, FontMetrics output);
    }
}
