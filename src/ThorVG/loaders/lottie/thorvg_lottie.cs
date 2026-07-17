// Ported from ThorVG/src/loaders/lottie/thorvg_lottie.h and tvgLottieAnimation.cpp

using System;

namespace ThorVG
{
    /// <summary>
    /// The LottieAnimation class enables control of advanced Lottie features.
    /// Extends Animation with Lottie-specific interfaces for markers, slots,
    /// tweening, and audio synchronization.
    /// </summary>
    public class LottieAnimation : Animation
    {
        private LottieAnimation() { }

        /// <summary>
        /// Creates a new LottieAnimation object.
        /// </summary>
        public static new LottieAnimation Gen() => new LottieAnimation();

        /// <summary>
        /// Specifies a segment by marker name.
        /// </summary>
        public Result Segment(string? marker)
        {
            var loader = GetPicture().loader;
            if (loader == null) return Result.InsufficientCondition;

            var lottieLoader = loader as LottieLoader;
            if (lottieLoader == null) return Result.InsufficientCondition;

            if (marker == null)
            {
                lottieLoader.Segment(0.0f, float.MaxValue);
                return Result.Success;
            }

            if (!lottieLoader.Segment(marker, out float begin, out float end))
                return Result.InvalidArguments;

            return Segment(begin, end);
        }

        /// <summary>
        /// Interpolates between two frames over a specified duration.
        /// </summary>
        public Result Tween(float from, float to, float progress)
        {
            var loader = GetPicture().loader;
            if (loader == null) return Result.InsufficientCondition;

            var lottieLoader = loader as LottieLoader;
            if (lottieLoader == null) return Result.InsufficientCondition;

            if (!lottieLoader.Tween(from, to, progress))
                return Result.InsufficientCondition;

            GetPicture().pImpl.Mark(RenderUpdateFlag.All);
            return Result.Success;
        }

        /// <summary>Sets the target frame for dynamic tweening.</summary>
        public Result TweenTo(float to)
        {
            var loader = GetPicture().loader as LottieLoader;
            if (loader == null || !loader.TweenTo(to)) return Result.InsufficientCondition;
            return Result.Success;
        }

        /// <summary>Updates the progress of a dynamic tween started by <see cref="TweenTo"/>.</summary>
        public Result Tween(float progress)
        {
            var loader = GetPicture().loader as LottieLoader;
            if (loader == null || !loader.Tween(progress)) return Result.InsufficientCondition;
            GetPicture().pImpl.Mark(RenderUpdateFlag.All);
            return Result.Success;
        }

        /// <summary>
        /// Gets the marker count of the animation.
        /// </summary>
        public uint MarkersCnt()
        {
            var loader = GetPicture().loader;
            if (loader == null) return 0;

            var lottieLoader = loader as LottieLoader;
            if (lottieLoader == null) return 0;

            return lottieLoader.MarkersCnt();
        }

        /// <summary>
        /// Gets the marker name by a given index.
        /// </summary>
        [Obsolete("Use Marker(uint, out float, out float) instead.")]
        public string? Marker(uint idx)
        {
            return Marker(idx, out _, out _);
        }

        /// <summary>
        /// Gets the marker name, start frame and end frame by a given index.
        /// </summary>
        public string? Marker(uint idx, out float begin, out float end)
        {
            begin = 0;
            end = 0;

            var loader = GetPicture().loader;
            if (loader == null) return null;

            var lottieLoader = loader as LottieLoader;
            if (lottieLoader == null) return null;

            return lottieLoader.GetMarker(idx, out begin, out end);
        }

        /// <summary>
        /// Generates a slot override from JSON. Returns slot ID (0 on failure).
        /// </summary>
        public uint Gen(string? slotJson)
        {
            var loader = GetPicture().loader as LottieLoader;
            if (loader == null) return 0;
            return loader.GenSlot(slotJson);
        }

        /// <summary>
        /// Applies a slot override by ID. Use 0 to reset to default.
        /// </summary>
        public Result Apply(uint id)
        {
            var loader = GetPicture().loader as LottieLoader;
            if (loader == null) return Result.InsufficientCondition;
            var result = loader.ApplySlot(id);
            if (result == Result.Success)
                GetPicture().pImpl.Mark(RenderUpdateFlag.All);
            return result;
        }

        /// <summary>
        /// Deletes a slot override by ID.
        /// </summary>
        public Result Del(uint id)
        {
            var loader = GetPicture().loader as LottieLoader;
            if (loader == null) return Result.InsufficientCondition;
            var result = loader.DelSlot(id);
            if (result == Result.Success)
                GetPicture().pImpl.Mark(RenderUpdateFlag.All);
            return result;
        }

        /// <summary>
        /// Sets the quality level for Lottie effects (0-100).
        /// </summary>
        public Result Quality(byte value)
        {
            if (value > 100) return Result.InvalidArguments;

            var loader = GetPicture().loader;
            if (loader == null) return Result.InsufficientCondition;

            var lottieLoader = loader as LottieLoader;
            if (lottieLoader == null) return Result.InsufficientCondition;

            if (!lottieLoader.SetQuality(value)) return Result.InsufficientCondition;
            return Result.Success;
        }

        /// <summary>Sets the callback used to synchronize Lottie audio layers.</summary>
        public Result Resolver(Action<LottieAudioResolver, object?>? func, object? data = null)
        {
            var loader = GetPicture().loader as LottieLoader;
            if (loader == null) return Result.InsufficientCondition;
            loader.Resolver(func, data);
            return Result.Success;
        }
    }

    /// <summary>Describes the current playback state of a Lottie audio layer.</summary>
    public struct LottieAudioResolver
    {
        public object? src;
        public string? mimeType;
        public uint size;
        public float offset;
        public float volume;
        public bool active;
        public bool embedded;
    }
}
