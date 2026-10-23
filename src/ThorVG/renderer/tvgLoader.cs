// Ported from ThorVG/src/renderer/tvgLoader.h and tvgLoader.cpp

using System;
using System.IO;
using System.Threading;

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

    /// <summary>How a loader retains supplied data. Transfer gives the loader exclusive ownership.</summary>
    public enum Ownership : byte { Borrow, Copy, Transfer }

    /// <summary>
    /// Base loader operations parameter. Mirrors C++ LoaderOps.
    /// </summary>
    public class LoaderOps
    {
        public Type caller;
        public Ownership owner;
        public LoaderOps(Type caller, Ownership owner = Ownership.Borrow) { this.caller = caller; this.owner = owner; }
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
            : this(Ownership.Borrow, resolver, rpath, accessible) { }

        public PictureOps(Ownership owner, AssetResolver? resolver, string? rpath, bool accessible)
            : base(Type.Picture, owner)
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

        // Use either hashdata(data) or hashpath(path). hashkey is retained for compatibility.
        public ulong hashkey;
        public object? hashdata;
        public string? hashpath;

        public FileType type;                           // current loader file type
        public int sharing;                             // reference count (atomic in C++)
        public Ownership owner;
        public bool readied;                            // read done already
        public bool cached;                             // cached for sharing

        protected Loader(FileType type) { this.type = type; }

        // Interlocked has no UInt16 overload on the supported .NET8 runtime.
        // Keep the compatible field storage, but atomically retain the native
        // uint16_t wrap on every internal increment/decrement.
        internal int IncrementSharing() => ChangeSharing(1);
        internal int DecrementSharing() => ChangeSharing(-1);

        private int ChangeSharing(int delta)
        {
            int previous, next;
            do
            {
                previous = Volatile.Read(ref sharing);
                next = (previous + delta) & 0xffff;
            }
            while (Interlocked.CompareExchange(ref sharing, next, previous) != previous);
            return next;
        }

        public bool AllowCache()
        {
            return type != FileType.Lot && type != FileType.Gif && type != FileType.Media;
        }

        public bool Cache(ulong data)
        {
            if (!AllowCache()) return false;
            hashkey = data;
            cached = true;
            return true;
        }

        public bool Cache(byte[] data)
        {
            if (!AllowCache()) return false;
            hashdata = data;
            cached = true;
            return true;
        }

        public bool Cache(uint[] data)
        {
            if (!AllowCache()) return false;
            hashdata = data;
            cached = true;
            return true;
        }

        public bool Cache(string data)
        {
            if (!AllowCache()) return false;
            hashpath = TvgStr.Duplicate(data);
            cached = true;
            return true;
        }

        public virtual bool Open(string path, LoaderOps? ops = null) => false;
        public virtual bool Open(byte[] data, uint size, LoaderOps? ops, bool copy) => false;
        // Native base entry points are unsupported. Concrete encoded loaders
        // keep their Boolean compatibility API and provide result adapters.
        public virtual Result OpenResult(string path, LoaderOps? ops = null)
        {
            return Result.NonSupport;
        }

        public virtual Result OpenResult(byte[] data, uint size, LoaderOps? ops)
        {
            return Result.NonSupport;
        }
        public virtual bool Resize(Paint paint, float w, float h) => false;
        public virtual bool Sync() => false;

        internal virtual void ReleaseResources()
        {
            hashdata = null;
            hashpath = null;
        }

        public virtual bool Read()
        {
            if (readied) return false;
            readied = true;
            return true;
        }

        public virtual bool Close()
        {
            if (Volatile.Read(ref sharing) == 0) return true;
            DecrementSharing();
            return false;
        }

        /// <summary>
        /// Read a file into a byte array. Mirrors C++ Loader::open(path, size, text).
        /// </summary>
        public static byte[]? ReadFile(string path, bool text = false)
        {
            try
            {
                var bytes = TvgUtf8.ReadAllBytes(path);
                if (bytes.Length == 0) return null;
                // fopen text mode reads bytes, without UTF-8 decoding or BOM
                // removal. Only the Windows CRT translates CRLF and Ctrl-Z.
                if (text && OperatingSystem.IsWindows())
                {
                    int output = 0;
                    for (int input = 0; input < bytes.Length; ++input)
                    {
                        byte value = bytes[input];
                        if (value == 0x1a) break;
                        if (value == 13 && input + 1 < bytes.Length && bytes[input + 1] == 10) continue;
                        bytes[output++] = value;
                    }
                    if (output != bytes.Length) return bytes.AsSpan(0, output).ToArray();
                }
                return bytes;
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
        private static int desiredColorSpace = (int)ColorSpace.ARGB8888;
        public static ColorSpace cs
        {
            get => (ColorSpace)Volatile.Read(ref desiredColorSpace);
            set => Interlocked.Exchange(ref desiredColorSpace, (int)value);
        }

        public float w, h;                              // default image size
        public RenderSurface surface = new RenderSurface();

        public ImageLoader() : base(FileType.Unknown) { }
        public bool playable;
        public ImageLoader(FileType type, bool playable = false) : base(type) { this.playable = playable; }

        public virtual bool Animatable() => playable;
        public virtual Paint? GetPaint() => null;
        public virtual AccessorEntity? Access(uint id) => null;
        public virtual void Access(Func<Paint, object?, bool> callback, object? data) { }

        public virtual unsafe RenderSurface? Bitmap()
        {
            if (surface.data != null && surface.data.Length > 0) return surface;
            return null;
        }

        internal override void ReleaseResources()
        {
            surface.Dispose();
            surface.data = null;
            base.ReleaseResources();
        }
    }

    /// <summary>Raster image loader base; the inherited surface is retained for C# source compatibility.</summary>
    public class BitmapLoader : ImageLoader
    {
        public BitmapLoader(FileType type, bool playable = false) : base(type, playable) { }
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
        public bool hinting;           // local TrueType enhancement, selected per text
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
        public bool Get(FontMetrics fm, string? text, RenderPath output) => Get(fm, text, text != null ? (uint)TvgUtf8.ByteCount(text) : 0, output);
        public abstract void Transform(Paint paint, FontMetrics fm, float italicShear);
        public abstract void Release(FontMetrics fm);
        public abstract void Metrics(FontMetrics fm, out TextMetrics output);
        public abstract bool GlyphMetrics(FontMetrics fm, string ch, out ThorVG.GlyphMetrics output);
        public virtual bool GlyphMetrics(FontMetrics fm, string ch, out ThorVG.GlyphMetrics output, out int next)
        {
            next = 0;
            if (!GlyphMetrics(fm, ch, out output)) return false;
            next = ch.Length > 1 && char.IsSurrogatePair(ch, 0) ? 2 : 1;
            return true;
        }
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
                _activeLoaders.Remove(loader);
                loader.ReleaseResources();
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
                case FileType.Webp: return null; // Approved decoder exclusion.
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
        private static FileType ConvertExtension(string filename)
        {
            var ext = TvgStr.Fileext(filename);
            return ext switch
            {
                "svg" => FileType.Svg,
                "lot" or "json" => FileType.Lot,
                "ttf" or "ttc" or "otf" or "otc" => FileType.Sfnt,
                "png" => FileType.Png,
                "jpg" => FileType.Jpg,
                "webp" => FileType.Webp,
                "mp4" => FileType.Media,
                _ => FileType.Unknown
            };
        }

        /// <summary>
        /// Determine file type from MIME type string. Mirrors C++ _convert().
        /// </summary>
        private static FileType Convert(string? mimeType)
        {
            if (string.IsNullOrEmpty(mimeType)) return FileType.Unknown;
            mimeType = TvgStr.Duplicate(mimeType)!;

            if (mimeType == "svg" || mimeType == "svg+xml") return FileType.Svg;
            if (mimeType == "ttf" || mimeType == "otf") return FileType.Sfnt;
            if (mimeType == "lot" || mimeType == "lottie+json") return FileType.Lot;
            if (mimeType == "raw") return FileType.Raw;
            if (mimeType == "png") return FileType.Png;
            if (mimeType == "jpg" || mimeType == "jpeg") return FileType.Jpg;
            if (mimeType == "webp") return FileType.Webp;
            if (mimeType == "mp4") return FileType.Media;

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
                if (loader.cached && loader.hashpath != null && TvgStr.Equal(loader.hashpath, filename))
                {
                    loader.IncrementSharing();
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

            using var lk = new ScopedLock(_key);
            var loader = _activeLoaders.Head;
            while (loader != null)
            {
                if (loader.type == type && loader.cached && ReferenceEquals(loader.hashdata, data))
                {
                    loader.IncrementSharing();
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
            // Closing can wait for a worker that resolves fonts/images through
            // this cache. Native also closes outside the cache mutex; holding
            // it here prevents that worker from ever finishing.
            if (loader.Close())
            {
                if (loader.cached)
                {
                    using var lk = new ScopedLock(_key);
                    _activeLoaders.Remove(loader);
                }
                loader.ReleaseResources();
            }
            return true;
        }

        /// <summary>
        /// Load from a file path. Mirrors C++ LoaderMgr::loader(filename, invalid).
        /// </summary>
        public static Loader? Loader(string filename, out bool invalid, LoaderOps? ops = null)
        {
            var loader = Loader(filename, ops, out Result result);
            invalid = result == Result.InvalidArguments;
            return loader;
        }

        public static Loader? Loader(string filename, LoaderOps? ops, out Result result)
        {
            result = Result.InvalidArguments;
            if (filename == null) return null;
            filename = TvgStr.Duplicate(filename)!;
            var cached = FindFromCache(filename);
            if (cached != null) { result = Result.Success; return cached; }

            var type = ConvertExtension(filename);
            var loader = Find(type);
            if (type != FileType.Unknown) result = Result.NonSupport;
            if (loader != null)
            {
                result = loader.OpenResult(filename, ops);
                if (result == Result.Success)
                {
                    if (loader.Cache(filename))
                    {
                        using var lk = new ScopedLock(_key);
                        _activeLoaders.Back(loader);
                    }
                    return loader;
                }
                loader.ReleaseResources();
            }
            if (result == Result.NonSupport) return null;

            for (int i = 0; i < (int)FileType.Unknown; ++i)
            {
                loader = Find((FileType)i);
                if (loader == null) continue;
                if (loader.OpenResult(filename, ops) != Result.Success)
                {
                    loader.ReleaseResources();
                    continue;
                }
                if (loader.Cache(filename))
                {
                    using var lk = new ScopedLock(_key);
                    _activeLoaders.Back(loader);
                }
                result = Result.Success;
                return loader;
            }
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
            ops ??= new PictureOps(null, null, false);
            ops.owner = copy ? Ownership.Copy : Ownership.Borrow;
            return Loader(data, size, mimeType, ops, out _);
        }

        public static Loader? Loader(byte[] data, uint size, string? mimeType, LoaderOps? ops, out Result result)
        {
            result = Result.InvalidArguments;
            if (data == null || size == 0 || size > data.Length) return null;
            ops ??= new PictureOps(null, null, false);
            bool borrow = ops.owner == Ownership.Borrow;
            if (borrow)
            {
                var cached = FindFromCache(data, size, mimeType);
                if (cached != null) { result = Result.Success; return cached; }
            }

            if (mimeType != null)
            {
                var type = Convert(mimeType);
                if (type != FileType.Unknown) result = Result.NonSupport;
                var loader = Find(type);
                if (loader != null)
                {
                    result = loader.OpenResult(data, size, ops);
                    if (result == Result.Success)
                    {
                        if (borrow && loader.Cache(data))
                        {
                            using var lk = new ScopedLock(_key);
                            _activeLoaders.Back(loader);
                        }
                        return loader;
                    }
                    loader.ReleaseResources();
                }
            }
            if (result == Result.NonSupport) return null;

            for (int i = 0; i < (int)FileType.Unknown; ++i)
            {
                var loader = Find((FileType)i);
                if (loader == null) continue;
                if (loader.OpenResult(data, size, ops) != Result.Success)
                {
                    loader.ReleaseResources();
                    continue;
                }
                if (borrow && loader.Cache(data))
                {
                    using var lk = new ScopedLock(_key);
                    _activeLoaders.Back(loader);
                }
                result = Result.Success;
                return loader;
            }
            return null;
        }

        /// <summary>
        /// Load raw pixel data. Mirrors C++ LoaderMgr::loader(data, w, h, cs, copy).
        /// </summary>
        public static Loader? Loader(uint[] data, uint w, uint h, ColorSpace cs, bool copy)
            => Loader(data, w, h, cs, copy ? Ownership.Copy : Ownership.Borrow);

        public static Loader? Loader(uint[] data, uint w, uint h, ColorSpace cs, Ownership owner)
        {
            if (data == null || w == 0 || h == 0 || (ulong)w * h > (ulong)data.LongLength) return null;
            if (owner == Ownership.Borrow)
            {
                using var lk = new ScopedLock(_key);
                var existing = _activeLoaders.Head;
                while (existing != null)
                {
                    if (existing.type == FileType.Raw && existing.cached && ReferenceEquals(existing.hashdata, data))
                    {
                        existing.IncrementSharing();
                        return existing;
                    }
                    existing = existing.Next;
                }
            }
            var loader = new RawLoader();
            if (!loader.Open(data, w, h, cs, owner))
            {
                loader.ReleaseResources();
                return null;
            }
            if (owner == Ownership.Borrow && loader.Cache(data))
            {
                using var lk = new ScopedLock(_key);
                _activeLoaders.Back(loader);
            }
            return loader;
        }

        /// <summary>
        /// Load font from memory. Loader is cached regardless of copy value.
        /// Mirrors C++ LoaderMgr::loader(name, data, size, mimeType, copy).
        /// </summary>
        public static Loader? Loader(string name, byte[] data, uint size, string? mimeType, bool copy)
            => Loader(name, data, size, mimeType, new LoaderOps(Type.Text, copy ? Ownership.Copy : Ownership.Borrow));

        public static Loader? Loader(string name, byte[] data, uint size, string? mimeType, LoaderOps ops)
        {
            var existing = Font(name);
            // Native reloads a borrowed font: its caller can release or alter
            // the data. Reuse is permitted only when the cache owns the bytes.
            if (existing != null && existing.owner != Ownership.Borrow) return existing;

            // function is dedicated for sfnt loader (the only supported font loader)
            var loader = new TtfLoader();
            if (loader.OpenResult(data, size, ops) == Result.Success)
            {
                loader.name = TvgStr.Duplicate(name);
                loader.cached = true; // force it
                using var lk = new ScopedLock(_key);
                _activeLoaders.Back(loader);
                return loader;
            }
            loader.ReleaseResources();
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
                    loader.IncrementSharing();
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
                    loader.IncrementSharing();
                    return loader;
                }
                loader = loader.Next;
            }
            return null;
        }
    }
}
