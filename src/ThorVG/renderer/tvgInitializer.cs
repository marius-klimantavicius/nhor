// Ported from ThorVG/src/renderer/tvgInitializer.cpp

namespace ThorVG
{
    /// <summary>
    /// ThorVG library initializer. Mirrors C++ tvg::Initializer.
    /// </summary>
    public static class Initializer
    {
        public const string VersionString = "0.15.7";

        private static ushort _version;

        private static bool BuildVersionInfo(out uint major, out uint minor, out uint micro)
        {
            major = minor = micro = 0;
            var parts = VersionString.Split('.');
            if (parts.Length != 3) return false;
            if (!uint.TryParse(parts[0], out major)) return false;
            if (!uint.TryParse(parts[1], out minor)) return false;
            if (!uint.TryParse(parts[2], out micro)) return false;

            var sum = $"{major}{minor:D2}{micro:D2}";
            if (!ushort.TryParse(sum, out _version)) return false;

            return true;
        }

        public static Result Init(uint threads = 0)
        {
            if (TvgCommon.engineInit++ > 0) return Result.Success;

            if (!BuildVersionInfo(out _, out _, out _)) return Result.Unknown;

            if (!LoaderMgr.Init()) return Result.Unknown;

            TaskScheduler.Init(threads);

            return Result.Success;
        }

        public static Result Term()
        {
            if (TvgCommon.engineInit == 0) return Result.InsufficientCondition;

            if (--TvgCommon.engineInit > 0) return Result.Success;

            // SW renderer term (mirrors C++ #ifdef THORVG_SW_RASTER_SUPPORT)
            SwRenderer.Term();

            // GL renderer term
            GlRenderer.Term();

            TaskScheduler.Term();

            if (!LoaderMgr.Term()) return Result.Unknown;

            return Result.Success;
        }

        public static string? Version(out uint major, out uint minor, out uint micro)
        {
            if (!BuildVersionInfo(out major, out minor, out micro)) return null;
            return VersionString;
        }

        public static string Version()
        {
            return VersionString;
        }

        /// <summary>
        /// Returns the numeric version code. Mirrors C++ THORVG_VERSION_NUMBER().
        /// </summary>
        public static ushort VersionNumber() => _version;
    }
}
