// Ported from ThorVG/src/loaders/lottie/thorvg_lottie.h and tvgLottieAnimation.cpp

namespace ThorVG
{
    /// <summary>
    /// The LottieAnimation class enables control of advanced Lottie features.
    /// Extends Animation with Lottie-specific interfaces for markers, slots,
    /// tweening, and expression variables.
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
        public string? Marker(uint idx)
        {
            var loader = GetPicture().loader;
            if (loader == null) return null;

            var lottieLoader = loader as LottieLoader;
            if (lottieLoader == null) return null;

            return lottieLoader.GetMarker(idx);
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
        /// Assigns a variable value to a layer expression.
        /// </summary>
        public Result Assign(string layer, uint ix, string var_, float val)
        {
            if (layer == null || var_ == null) return Result.InvalidArguments;

            var loader = GetPicture().loader as LottieLoader;
            if (loader == null) return Result.InsufficientCondition;
            if (loader.Assign(layer, ix, var_, val))
            {
                GetPicture().pImpl.Mark(RenderUpdateFlag.All);
                return Result.Success;
            }
            return Result.NonSupport;
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
    }
}
