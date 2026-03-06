// Ported from ThorVG/src/renderer/tvgSaver.cpp

namespace ThorVG
{
    /// <summary>
    /// Saves paint data or animations to files.
    /// Mirrors C++ tvg::Saver.
    /// </summary>
    public class Saver
    {
        private SaveModule? _saveModule;
        private Paint? _bg;

        private Saver() { }

        ~Saver()
        {
            if (_bg != null) _bg.Unref();
        }

        public static Saver Gen() => new Saver();

        /// <summary>
        /// Find save module by file type. Mirrors C++ _find(FileType).
        /// </summary>
        private static SaveModule? FindByType(FileType type)
        {
            switch (type)
            {
                case FileType.Gif:
                    return new GifSaver();
                default:
                    break;
            }
            return null;
        }

        /// <summary>
        /// Find save module by filename extension. Mirrors C++ _find(filename).
        /// </summary>
        private static SaveModule? FindByFilename(string? filename)
        {
            if (filename == null) return null;
            var ext = TvgStr.Fileext(filename);
            if (!string.IsNullOrEmpty(ext) && ext.ToLowerInvariant() == "gif")
                return FindByType(FileType.Gif);
            return null;
        }

        public Result Background(Paint? paint)
        {
            if (paint == null) return Result.InvalidArguments;

            if (_bg != null) _bg.Unref();
            paint.Ref();
            _bg = paint;

            return Result.Success;
        }

        public Result Save(Paint? paint, string? filename, uint quality = 100)
        {
            if (paint == null) return Result.InvalidArguments;

            // Already on saving another resource.
            if (_saveModule != null)
            {
                Paint.Rel(paint);
                return Result.InsufficientCondition;
            }

            var saveModule = FindByFilename(filename);
            if (saveModule != null)
            {
                if (saveModule.Save(paint, _bg, filename!, quality))
                {
                    _saveModule = saveModule;
                    return Result.Success;
                }
                else
                {
                    Paint.Rel(paint);
                    return Result.Unknown;
                }
            }
            Paint.Rel(paint);
            return Result.NonSupport;
        }

        public Result Save(Animation? animation, string? filename, uint quality = 100, uint fps = 0)
        {
            if (animation == null) return Result.InvalidArguments;

            if (TvgMath.Zero(animation.TotalFrame()))
            {
                return Result.InsufficientCondition;
            }

            // Already on saving another resource.
            if (_saveModule != null)
            {
                return Result.InsufficientCondition;
            }

            var saveModule = FindByFilename(filename);
            if (saveModule != null)
            {
                if (saveModule.Save(animation, _bg, filename!, quality, fps))
                {
                    _saveModule = saveModule;
                    return Result.Success;
                }
                else
                {
                    return Result.Unknown;
                }
            }
            return Result.NonSupport;
        }

        public Result Sync()
        {
            if (_saveModule == null) return Result.InsufficientCondition;
            _saveModule.Close();
            _saveModule = null;

            return Result.Success;
        }
    }
}
