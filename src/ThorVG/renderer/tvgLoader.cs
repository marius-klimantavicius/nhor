// Ported from ThorVG/src/renderer/tvgLoader.h and tvgLoader.cpp

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
    /// Base loader operations parameter. Mirrors C++ LoaderOps.
    /// </summary>
    public class LoaderOps
    {
        public Type caller;
        public LoaderOps(Type caller) { this.caller = caller; }
    }

    /// <summary>
    /// Picture-specific loader operations. Mirrors C++ PictureOps.
    /// </summary>
    public class PictureOps : LoaderOps
    {
        public AssetResolver? resolver;
        public string? rpath;
        public bool accessible;

        public PictureOps(AssetResolver? resolver, string? rpath, bool accessible)
            : base(Type.Picture)
        {
            this.resolver = resolver;
            this.rpath = rpath;
            this.accessible = accessible;
        }
    }

    /// <summary>
    /// Base loader. Mirrors C++ tvg::Loader.
    /// </summary>
    public abstract class Loader : IInlistNode<Loader>
    {
        // IInlistNode implementation
        public Loader? Prev { get; set; }
        public Loader? Next { get; set; }

        // Use either hashkey(data) or hashpath(path)
        public ulong hashkey;
        public string? hashpath;

        public FileType type;                           // current loader file type
        public int sharing;                             // reference count (atomic in C++)
        public bool readied;                            // read done already
        public bool cached;                             // cached for sharing

        protected Loader(FileType type) { this.type = type; }

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

        public virtual bool Open(string path, LoaderOps? ops = null) => false;
        public virtual bool Open(byte[] data, uint size, LoaderOps? ops, bool copy) => false;
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
        /// Read a file into a byte array. Mirrors C++ Loader::open(path, size, text).
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
    public class ImageLoader : Loader
    {
        /// <summary>Desired color space for image decoding (shared across loaders).</summary>
        public static ColorSpace cs = ColorSpace.ABGR8888;

        public float w, h;                              // default image size
        public RenderSurface surface = new RenderSurface();

        public ImageLoader() : base(FileType.Unknown) { }
        public ImageLoader(FileType type) : base(type) { }

        public virtual bool Animatable() => false;
        public virtual Paint? GetPaint() => null;

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
        public object? engine;          // engine extension (TtfMetrics in SFNT loader)
    }

    /// <summary>
    /// Abstract font loader. Mirrors C++ tvg::FontLoader.
    /// </summary>
    public abstract class FontLoader : Loader
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

    /// <summary>
    /// Loader manager. Mirrors C++ tvg::LoaderMgr.
    /// Responsible for creating the appropriate loader based on file type,
    /// managing caching, and font loader access.
    /// </summary>
    public static class LoaderMgr
    {
        private static readonly Key _key = new Key();
        private static readonly Inlist<Loader> _activeLoaders = new Inlist<Loader>();

        private static ulong HashKey(byte[] data)
        {
            // Mirrors C++ HASH_KEY which uses reinterpret_cast<uintptr_t>(data)
            // In C# we use the hash code of the array reference as a stable identity
            return (ulong)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(data);
        }

        public static bool Init() => true;

        public static bool Term()
        {
            // clean up the remained font loaders which are globally used.
            var loader = _activeLoaders.Head;
            while (loader != null)
            {
                var next = loader.Next;
                if (loader.type != FileType.Sfnt)
                {
                    loader = next;
                    continue;
                }
                var ret = loader.Close();
                _activeLoaders.Remove(loader);
                // In C# we let GC handle cleanup
                loader = next;
            }
            return true;
        }

        /// <summary>
        /// Create a loader for the given file type. Mirrors C++ _find().
        /// </summary>
        public static Loader? Find(FileType type)
        {
            switch (type)
            {
                case FileType.Png: return new PngLoader();
                case FileType.Jpg: return new JpgLoader();
                case FileType.Webp: return null; // WebP loader removed for now
                case FileType.Raw: return new RawLoader();
                case FileType.Lot: return new LottieLoader();
                case FileType.Sfnt: return new TtfLoader();
                case FileType.Svg: return new SvgLoaderInstance();
                default: return null;
            }
        }

        /// <summary>
        /// Determine file type from extension. Mirrors C++ _findByPath().
        /// </summary>
        private static Loader? FindByPath(string filename)
        {
            var ext = TvgStr.Fileext(filename);
            if (string.IsNullOrEmpty(ext)) return null;

            ext = ext.ToLowerInvariant();

            if (ext == "svg") return Find(FileType.Svg);
            if (ext == "lot" || ext == "json") return Find(FileType.Lot);
            if (ext == "png") return Find(FileType.Png);
            if (ext == "jpg") return Find(FileType.Jpg);
            if (ext == "webp") return Find(FileType.Webp);
            if (ext == "ttf" || ext == "ttc") return Find(FileType.Sfnt);
            if (ext == "otf" || ext == "otc") return Find(FileType.Sfnt);
            return null;
        }

        /// <summary>
        /// Determine file type from MIME type string. Mirrors C++ _convert().
        /// </summary>
        private static FileType Convert(string? mimeType)
        {
            if (string.IsNullOrEmpty(mimeType)) return FileType.Unknown;

            if (mimeType == "svg" || mimeType == "svg+xml") return FileType.Svg;
            if (mimeType == "ttf" || mimeType == "otf") return FileType.Sfnt;
            if (mimeType == "lot" || mimeType == "lottie+json") return FileType.Lot;
            if (mimeType == "raw") return FileType.Raw;
            if (mimeType == "png") return FileType.Png;
            if (mimeType == "jpg" || mimeType == "jpeg") return FileType.Jpg;
            if (mimeType == "webp") return FileType.Webp;

            return FileType.Unknown;
        }

        /// <summary>
        /// Find by MIME type. Mirrors C++ _findByType().
        /// </summary>
        private static Loader? FindByType(string? mimeType)
        {
            return Find(Convert(mimeType));
        }

        /// <summary>
        /// Find from cache by filename. Mirrors C++ _findFromCache(filename).
        /// </summary>
        private static Loader? FindFromCache(string filename)
        {
            using var lk = new ScopedLock(_key);
            var loader = _activeLoaders.Head;
            while (loader != null)
            {
                if (loader.cached && loader.hashpath != null && loader.hashpath == filename)
                {
                    ++loader.sharing;
                    return loader;
                }
                loader = loader.Next;
            }
            return null;
        }

        /// <summary>
        /// Find from cache by data pointer and MIME type. Mirrors C++ _findFromCache(data, size, mimeType).
        /// </summary>
        private static Loader? FindFromCache(byte[] data, uint size, string? mimeType)
        {
            var type = Convert(mimeType);
            if (type == FileType.Unknown) return null;

            var key = HashKey(data);

            using var lk = new ScopedLock(_key);
            var loader = _activeLoaders.Head;
            while (loader != null)
            {
                if (loader.type == type && loader.hashkey == key)
                {
                    ++loader.sharing;
                    return loader;
                }
                loader = loader.Next;
            }
            return null;
        }

        /// <summary>
        /// Retrieve (release) a loader. Mirrors C++ LoaderMgr::retrieve(loader).
        /// </summary>
        public static bool Retrieve(Loader? loader)
        {
            if (loader == null) return false;

            if (loader.Close())
            {
                if (loader.cached)
                {
                    _activeLoaders.Remove(loader);
                }
                // In C# we let GC handle deletion
            }
            return true;
        }

        /// <summary>
        /// Load from a file path. Mirrors C++ LoaderMgr::loader(filename, invalid).
        /// </summary>
        public static Loader? Loader(string filename, out bool invalid)
        {
            invalid = false;

            // TODO: make lottie sharable.
            var allowCache = true;
            var ext = TvgStr.Fileext(filename);
            if (!string.IsNullOrEmpty(ext))
            {
                var extLower = ext.ToLowerInvariant();
                if (extLower == "json" || extLower == "lot") allowCache = false;
            }

            if (allowCache)
            {
                var cached = FindFromCache(filename);
                if (cached != null) return cached;
            }

            var loader = FindByPath(filename);
            if (loader != null)
            {
                if (loader.Open(filename))
                {
                    if (allowCache)
                    {
                        loader.Cache(TvgStr.Duplicate(filename));
                        using (var lk = new ScopedLock(_key))
                        {
                            _activeLoaders.Back(loader);
                        }
                    }
                    return loader;
                }
                // loader not usable, let GC collect
            }

            // Unknown MimeType. Try with the candidates in the order
            for (int i = 0; i < (int)FileType.Raw; i++)
            {
                loader = Find((FileType)i);
                if (loader != null)
                {
                    if (loader.Open(filename))
                    {
                        if (allowCache)
                        {
                            loader.Cache(TvgStr.Duplicate(filename));
                            using (var lk = new ScopedLock(_key))
                            {
                                _activeLoaders.Back(loader);
                            }
                        }
                        return loader;
                    }
                    // loader not usable
                }
            }

            invalid = true;
            return null;
        }

        /// <summary>
        /// Retrieve by filename. Mirrors C++ LoaderMgr::retrieve(filename).
        /// </summary>
        public static bool Retrieve(string filename)
        {
            return Retrieve(FindFromCache(filename));
        }

        /// <summary>
        /// Load from memory buffer with optional MIME type.
        /// Mirrors C++ LoaderMgr::loader(data, size, mimeType, rpath, copy).
        /// </summary>
        public static Loader? Loader(byte[] data, uint size, string? mimeType, LoaderOps? ops, bool copy)
        {
            // Note that users could use the same data pointer with different content.
            // Thus caching is only valid for shareable.
            var allowCache = !copy;

            // TODO: make lottie shareable.
            if (allowCache)
            {
                var type = Convert(mimeType);
                if (type == FileType.Lot) allowCache = false;
            }

            if (allowCache)
            {
                var cached = FindFromCache(data, size, mimeType);
                if (cached != null) return cached;
            }

            // Try with the given MimeType
            if (!string.IsNullOrEmpty(mimeType))
            {
                var loader = FindByType(mimeType);
                if (loader != null)
                {
                    if (loader.Open(data, size, ops, copy))
                    {
                        if (allowCache)
                        {
                            loader.Cache(HashKey(data));
                            using var lk = new ScopedLock(_key);
                            _activeLoaders.Back(loader);
                        }
                        return loader;
                    }
                    // loader failed
                }
            }

            // Unknown MimeType. Try with the candidates in the order
            for (int i = 0; i < (int)FileType.Raw; i++)
            {
                var loader = Find((FileType)i);
                if (loader != null)
                {
                    if (loader.Open(data, size, ops, copy))
                    {
                        if (allowCache)
                        {
                            loader.Cache(HashKey(data));
                            using var lk = new ScopedLock(_key);
                            _activeLoaders.Back(loader);
                        }
                        return loader;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Load raw pixel data. Mirrors C++ LoaderMgr::loader(data, w, h, cs, copy).
        /// </summary>
        public static Loader? Loader(uint[] data, uint w, uint h, ColorSpace cs, bool copy)
        {
            // Note that users could use the same data pointer with the different content.
            // Thus caching is only valid for shareable.
            if (!copy)
            {
                // TODO: should we check premultiplied??
                // Use the array reference hash as cache key
                var key = (ulong)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(data);
                using (var lk = new ScopedLock(_key))
                {
                    var existing = _activeLoaders.Head;
                    while (existing != null)
                    {
                        if (existing.type == FileType.Raw && existing.hashkey == key)
                        {
                            ++existing.sharing;
                            return existing;
                        }
                        existing = existing.Next;
                    }
                }
            }

            // function is dedicated for raw images only
            var loader = new RawLoader();
            if (loader.Open(data, w, h, cs, copy))
            {
                if (!copy)
                {
                    loader.Cache((ulong)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(data));
                    using var lk = new ScopedLock(_key);
                    _activeLoaders.Back(loader);
                }
                return loader;
            }
            return null;
        }

        /// <summary>
        /// Load font from memory. Loader is cached regardless of copy value.
        /// Mirrors C++ LoaderMgr::loader(name, data, size, mimeType, copy).
        /// </summary>
        public static Loader? Loader(string name, byte[] data, uint size, string? mimeType, bool copy)
        {
            // TODO: add check for mimetype?
            var existing = Font(name);
            if (existing != null) return existing;

            // function is dedicated for sfnt loader (the only supported font loader)
            var loader = new TtfLoader();
            if (loader.Open(data, size, null, copy))
            {
                loader.name = TvgStr.Duplicate(name);
                loader.cached = true; // force it
                using var lk = new ScopedLock(_key);
                _activeLoaders.Back(loader);
                return loader;
            }
            return null;
        }

        /// <summary>
        /// Find a cached font loader by name. Mirrors C++ LoaderMgr::font(name).
        /// </summary>
        public static Loader? Font(string? name)
        {
            using var lk = new ScopedLock(_key);
            var loader = _activeLoaders.Head;
            while (loader != null)
            {
                if (loader.type != FileType.Sfnt)
                {
                    loader = loader.Next;
                    continue;
                }
                if (loader.cached && TvgStr.Equal(name, ((FontLoader)loader).name))
                {
                    ++loader.sharing;
                    return loader;
                }
                loader = loader.Next;
            }
            return null;
        }

        /// <summary>
        /// Find any cached font loader. Mirrors C++ LoaderMgr::anyfont().
        /// </summary>
        public static Loader? AnyFont()
        {
            using var lk = new ScopedLock(_key);
            var loader = _activeLoaders.Head;
            while (loader != null)
            {
                if (loader.cached && loader.type == FileType.Sfnt)
                {
                    ++loader.sharing;
                    return loader;
                }
                loader = loader.Next;
            }
            return null;
        }
    }
}
