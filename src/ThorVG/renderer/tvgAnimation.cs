// Ported from ThorVG/src/renderer/tvgAnimation.h and tvgAnimation.cpp

namespace ThorVG
{
    /// <summary>
    /// Controls animation playback for picture content.
    /// Mirrors C++ tvg::Animation.
    /// </summary>
    public class Animation
    {
        private readonly Picture _picture;

        protected Animation()
        {
            _picture = Picture.Gen();
            _picture.Ref();
        }

        ~Animation()
        {
            _picture.Unref();
        }

        public static Animation Gen() => new Animation();

        public Picture GetPicture() => _picture;

        public Result Frame(float no)
        {
            var loader = _picture.loader;

            if (loader == null) return Result.InsufficientCondition;
            if (!loader.Animatable()) return Result.NonSupport;

            if (((AnimLoader)loader).Frame(no))
            {
                _picture.pImpl.Mark(RenderUpdateFlag.All);
                return Result.Success;
            }
            return Result.InsufficientCondition;
        }

        public float CurFrame()
        {
            var loader = _picture.loader;

            if (loader == null) return 0;
            if (!loader.Animatable()) return 0;

            return ((AnimLoader)loader).CurFrame();
        }

        public float TotalFrame()
        {
            var loader = _picture.loader;

            if (loader == null) return 0;
            if (!loader.Animatable()) return 0;

            return ((AnimLoader)loader).TotalFrame();
        }

        public float Duration()
        {
            var loader = _picture.loader;

            if (loader == null) return 0;
            if (!loader.Animatable()) return 0;

            return ((AnimLoader)loader).Duration();
        }

        public Result Segment(float begin, float end)
        {
            var loader = _picture.loader;
            if (loader == null) return Result.InsufficientCondition;
            if (!loader.Animatable()) return Result.NonSupport;

            return ((AnimLoader)loader).Segment(begin, end);
        }

        public Result Segment(out float begin, out float end)
        {
            begin = 0;
            end = 0;

            var loader = _picture.loader;
            if (loader == null) return Result.InsufficientCondition;
            if (!loader.Animatable()) return Result.NonSupport;

            ((AnimLoader)loader).Segment(out begin, out end);

            return Result.Success;
        }
    }
}
