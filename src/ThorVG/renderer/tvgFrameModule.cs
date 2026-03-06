// Ported from ThorVG/src/renderer/tvgFrameModule.h

namespace ThorVG
{
    /// <summary>
    /// Base for animation-capable loaders. Mirrors C++ tvg::FrameModule.
    /// </summary>
    public abstract class FrameModule : ImageLoader
    {
        public float segmentBegin;
        public float segmentEnd;            // Initialize the value with the total frame number

        protected FrameModule(FileType type) : base(type) { }

        public abstract bool Frame(float no);           // set the current frame number
        public abstract float TotalFrame();             // return the total frame count
        public abstract float CurFrame();               // return the current frame number
        public abstract float Duration();               // return the animation duration in seconds
        public abstract Result Segment(float begin, float end);

        public void Segment(out float begin, out float end)
        {
            begin = segmentBegin;
            end = segmentEnd;
        }

        public override bool Animatable() => true;
    }
}
