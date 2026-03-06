// Ported from ThorVG/src/common/tvgCommon.h

using System.Diagnostics;

namespace ThorVG
{
    /// <summary>Known file types handled by ThorVG.</summary>
    public enum FileType
    {
        Png = 0,
        Jpg,
        Webp,
        Svg,
        Lot,
        Ttf,
        Raw,
        Gif,
        Unknown
    }

    /// <summary>
    /// Global state and logging helpers ported from tvgCommon.h.
    /// </summary>
    public static class TvgCommon
    {
        /// <summary>
        /// Reference count for engine initialisation.
        /// Mirrors the C++ <c>tvg::engineInit</c> global.
        /// </summary>
        public static int engineInit;

        // ----- Logging (mirrors TVGERR / TVGLOG macros) --------------------
        // In C++ these are compiled-out when THORVG_LOG_ENABLED is not set.
        // Here we always compile them, but guard with a static flag so that
        // callers can enable/disable at run-time.

        /// <summary>Set to <c>true</c> to enable diagnostic logging.</summary>
        public static bool LogEnabled;

        [Conditional("THORVG_LOG")]
        public static void TVGERR(string tag, string fmt, params object[] args)
        {
            if (!LogEnabled) return;
            System.Console.Error.Write($"[E] {tag}: ");
            System.Console.Error.WriteLine(string.Format(fmt, args));
        }

        [Conditional("THORVG_LOG")]
        public static void TVGLOG(string tag, string fmt, params object[] args)
        {
            if (!LogEnabled) return;
            System.Console.Out.Write($"[L] {tag}: ");
            System.Console.Out.WriteLine(string.Format(fmt, args));
        }

        /// <summary>
        /// Cast helper for downcasting Paint pointers.
        /// Mirrors C++ <c>template&lt;typename T&gt; static inline T* to(const Paint* p)</c>.
        /// </summary>
        public static T? To<T>(Paint? p) where T : Paint
        {
            return p as T;
        }
    }
}
