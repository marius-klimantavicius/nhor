// Ported from ThorVG/src/loaders/lottie/tvgLottieProperty.h
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ThorVG
{
    // Frame types -- no unmanaged constraint since we need them for PathSet, ColorStop, TextDocument too
    public class LottieScalarFrame<T>
    {
        public T value = default !; // keyframe value
        public float no; // frame number
        public LottieInterpolator? interpolator;
        public bool hold; // do not interpolate
        public LottieScalarFrame<T> Clone() => (LottieScalarFrame<T>)MemberwiseClone();
    }

    public class LottieVectorFrame<T>
    {
        public T value = default !; // keyframe value
        public float no; // frame number
        public LottieInterpolator? interpolator;
        public T outTangent = default !;
        public T inTangent = default !;
        public float length;
        public bool hasTangent;
        public bool hold;
        public LottieVectorFrame<T> Clone() => (LottieVectorFrame<T>)MemberwiseClone();
    }

    // Struct monomorphization for frame number access — replaces Func<TFrame, float>
    public interface IFrameNo<TFrame>
    {
        static abstract float Get(TFrame f);
    }

    public struct ScalarFrameNo<T> : IFrameNo<LottieScalarFrame<T>>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Get(LottieScalarFrame<T> f) => f.no;
    }

    public struct VectorFrameNo<T> : IFrameNo<LottieVectorFrame<T>>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Get(LottieVectorFrame<T> f) => f.no;
    }

    // Interpolation helpers for scalar frames
    public static class ScalarFrameHelper
    {
        public static float InterpolateFloat(LottieScalarFrame<float> frame, LottieScalarFrame<float> next, float frameNo)
        {
            var t = (frameNo - frame.no) / (next.no - frame.no);
            if (frame.interpolator != null)
                t = frame.interpolator.Progress(t);
            if (frame.hold)
                return (t < 1.0f) ? frame.value : next.value;
            return TvgMath.Lerp(frame.value, next.value, t);
        }

        public static sbyte InterpolateSByte(LottieScalarFrame<sbyte> frame, LottieScalarFrame<sbyte> next, float frameNo)
        {
            var t = (frameNo - frame.no) / (next.no - frame.no);
            if (frame.interpolator != null)
                t = frame.interpolator.Progress(t);
            if (frame.hold)
                return (t < 1.0f) ? frame.value : next.value;
            return unchecked((sbyte)TvgMath.Lerp((int)frame.value, (int)next.value, t));
        }

        public static byte InterpolateByte(LottieScalarFrame<byte> frame, LottieScalarFrame<byte> next, float frameNo)
        {
            var t = (frameNo - frame.no) / (next.no - frame.no);
            if (frame.interpolator != null)
                t = frame.interpolator.Progress(t);
            if (frame.hold)
                return (t < 1.0f) ? frame.value : next.value;
            return TvgMath.Lerp(frame.value, next.value, t);
        }

        public static Point InterpolatePoint(LottieScalarFrame<Point> frame, LottieScalarFrame<Point> next, float frameNo)
        {
            var t = (frameNo - frame.no) / (next.no - frame.no);
            if (frame.interpolator != null)
                t = frame.interpolator.Progress(t);
            if (frame.hold)
                return (t < 1.0f) ? frame.value : next.value;
            return TvgMath.Lerp(frame.value, next.value, t);
        }

        public static RGB32 InterpolateRGB32(LottieScalarFrame<RGB32> frame, LottieScalarFrame<RGB32> next, float frameNo)
        {
            var t = (frameNo - frame.no) / (next.no - frame.no);
            if (frame.interpolator != null)
                t = frame.interpolator.Progress(t);
            if (frame.hold)
                return (t < 1.0f) ? frame.value : next.value;
            return RGB32.Lerp(frame.value, next.value, t);
        }

        public static Point InterpolateVectorPoint(LottieVectorFrame<Point> frame, LottieVectorFrame<Point> next, float frameNo)
            => InterpolateVectorPoint(frame, next, frameNo, false);

        internal static Point InterpolateVectorPoint(LottieVectorFrame<Point> frame, LottieVectorFrame<Point> next, float frameNo, bool renderVector)
        {
            var t = (frameNo - frame.no) / (next.no - frame.no);
            if (frame.interpolator != null)
                t = frame.interpolator.Progress(t);
            if (frame.hold)
                return (t < 1.0f) ? frame.value : next.value;
            if (frame.hasTangent)
            {
                var bz = new Bezier(frame.value, TvgMath.PointAdd(frame.value, frame.outTangent), TvgMath.PointAdd(next.value, frame.inTangent), next.value);
                var at = bz.AtApprox(t * frame.length, frame.length);
                return renderVector ? bz.AtLottieVector(at) : bz.At(at);
            }

            return TvgMath.Lerp(frame.value, next.value, t);
        }

        public static float VectorPointAngle(LottieVectorFrame<Point> frame, LottieVectorFrame<Point> next, float frameNo)
        {
            if (!frame.hasTangent)
            {
                var dp = TvgMath.PointSub(next.value, frame.value);
                return TvgMath.Rad2Deg(TvgMath.Atan2(dp.y, dp.x));
            }

            var t = (frameNo - frame.no) / (next.no - frame.no);
            if (frame.interpolator != null)
                t = frame.interpolator.Progress(t);
            var bz = new Bezier(frame.value, TvgMath.PointAdd(frame.value, frame.outTangent), TvgMath.PointAdd(next.value, frame.inTangent), next.value);
            t = bz.AtApprox(t * frame.length, frame.length);
            if (t >= 1.0f)
                return bz.AngleAtConstant(0.99f);
            if (t <= 0.0f)
                return bz.AngleAtConstant(0.01f);
            return bz.Angle(t);
        }

        public static void PrepareVectorPoint(LottieVectorFrame<Point> frame, LottieVectorFrame<Point> next)
        {
            var bz = new Bezier(frame.value, TvgMath.PointAdd(frame.value, frame.outTangent), TvgMath.PointAdd(next.value, frame.inTangent), next.value);
            frame.length = bz.LengthApprox();
        }
    }

    // Expression support type
    public class LottieExpression
    {
        public string? code;
        public LottieComposition? comp;
        public LottieLayer? layer;
        public LottieObject? obj;
        public LottieProperty? property;
        public bool disabled;
        public LottieExpression()
        {
        }

        public LottieExpression(LottieExpression rhs)
        {
            code = rhs.code;
            comp = rhs.comp;
            layer = rhs.layer;
            obj = rhs.obj;
            property = rhs.property;
            disabled = rhs.disabled;
        }
    }

    // Property base
    public abstract class LottieProperty
    {
        public enum PropertyType : byte
        {
            Invalid = 0,
            Integer,
            Float,
            Scalar,
            Vector,
            PathSet,
            Color,
            Opacity,
            ColorStop,
            TextDoc,
            Image,
            Scalar3
        }

        public enum Loop : byte
        {
            None = 0,
            InCycle = 1,
            InPingPong,
            InOffset,
            InContinue,
            OutCycle,
            OutPingPong,
            OutOffset,
            OutContinue
        }

        public LottieExpression? exp;
        public PropertyType type;
        public byte ix;
        public ulong sid;
        protected LottieProperty(PropertyType type = PropertyType.Invalid)
        {
            this.type = type;
        }

        public abstract uint FrameCnt();
        public abstract uint Nearest(float frameNo);
        public abstract float FrameNo(int key);
        public abstract float DoLoop(float frameNo, uint key, Loop mode, float inout);
        // Native deletes slot properties polymorphically. Keep that release
        // path for every managed property, including retained frame storage.
        public virtual void Release()
        {
            exp = null;
        }

        public bool Copy(LottieProperty rhs, bool shallow)
        {
            type = rhs.type;
            ix = rhs.ix;
            sid = rhs.sid;
            if (rhs.exp == null)
                return false;
            if (shallow)
            {
                exp = rhs.exp;
                rhs.exp = null;
            }
            else
            {
                exp = new LottieExpression(rhs.exp);
            }

            exp.property = this;
            return true;
        }
    }

    // Binary search and helpers
    public static class LottiePropertyHelper
    {
        public static uint BSearch<TFrame, TAccessor>(List<TFrame> frames, float frameNo)
            where TAccessor : IFrameNo<TFrame>
        {
            int low = 0;
            int high = frames.Count - 1;
            while (low <= high)
            {
                var mid = low + (high - low) / 2;
                if (frameNo < TAccessor.Get(frames[mid]))
                    high = mid - 1;
                else
                    low = mid + 1;
            }

            if (high < low)
                low = high;
            if (low < 0)
                low = 0;
            return (uint)low;
        }

        public static uint NearestFrame<TFrame, TAccessor>(List<TFrame>? frames, float frameNo)
            where TAccessor : IFrameNo<TFrame>
        {
            if (frames != null && frames.Count > 0)
            {
                var key = BSearch<TFrame, TAccessor>(frames, frameNo);
                if (key == (uint)(frames.Count - 1))
                    return key;
                return (MathF.Abs(TAccessor.Get(frames[(int)key]) - frameNo) < MathF.Abs(TAccessor.Get(frames[(int)key + 1]) - frameNo)) ? key : (key + 1);
            }

            return 0;
        }

        public static float GetFrameNo<TFrame, TAccessor>(List<TFrame>? frames, int key)
            where TAccessor : IFrameNo<TFrame>
        {
            if (frames == null || frames.Count == 0)
                return 0.0f;
            if (key < 0)
                key = 0;
            if (key >= frames.Count)
                key = frames.Count - 1;
            return TAccessor.Get(frames[key]);
        }

        public static float DoLoop<TFrame, TAccessor>(List<TFrame>? frames, float frameNo, uint key, LottieProperty.Loop mode, float inout)
            where TAccessor : IFrameNo<TFrame>
        {
            if (frames == null || frames.Count == 0)
                return frameNo;
            if (mode == LottieProperty.Loop.None)
                return frameNo;
            frameNo -= TAccessor.Get(frames[0]);
            float range;
            switch (mode)
            {
                case LottieProperty.Loop.InCycle:
                    range = inout - TAccessor.Get(frames[0]);
                    return Fmod(frameNo, range) + TAccessor.Get(frames[(int)key]);
                case LottieProperty.Loop.InPingPong:
                    range = inout - TAccessor.Get(frames[(int)key]);
                    var forward = ((int)(frameNo / range) % 2) == 0;
                    var f = Fmod(frameNo, range);
                    return (forward ? f : (range - f)) + TAccessor.Get(frames[(int)key]);
                case LottieProperty.Loop.OutCycle:
                    range = TAccessor.Get(frames[frames.Count - 1 - (int)key]) - TAccessor.Get(frames[0]);
                    return Fmod(frameNo, range) + TAccessor.Get(frames[0]);
                case LottieProperty.Loop.OutPingPong:
                    range = TAccessor.Get(frames[frames.Count - 1 - (int)key]) - TAccessor.Get(frames[0]);
                    var forward2 = ((int)(frameNo / range) % 2) == 0;
                    var f2 = Fmod(frameNo, range);
                    return (forward2 ? f2 : (range - f2)) + TAccessor.Get(frames[0]);
            }

            return frameNo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Fmod(float a, float b)
        {
            return a % b;
        }
    }

    // Mirrors LottieOverride::apply: save by moving the original frames/expression,
    // then deep-copy the override. Every slot uses the same ownership rules.
    public static class LottieOverride
    {
        public static bool Apply(LottieProperty target, LottieProperty prop, ref LottieProperty? backup, bool release)
        {
            if (target.sid != prop.sid)
                return false;
            switch (target)
            {
                case LottieFloat value when prop is LottieFloat source:
                    if (release)
                        value.Release();
                    else
                    {
                        var saved = new LottieFloat();
                        saved.CopyFrom(value);
                        backup = saved;
                    }

                    value.CopyFrom(source, false);
                    return true;
                case LottieInteger value when prop is LottieInteger source:
                    if (release)
                        value.Release();
                    else
                    {
                        var saved = new LottieInteger();
                        saved.CopyFrom(value);
                        backup = saved;
                    }

                    value.CopyFrom(source, false);
                    return true;
                case LottieScalar value when prop is LottieScalar source:
                    if (release)
                        value.Release();
                    else
                    {
                        var saved = new LottieScalar();
                        saved.CopyFrom(value);
                        backup = saved;
                    }

                    value.CopyFrom(source, false);
                    return true;
                case LottieScalar3 value when prop is LottieScalar3 source:
                    if (release)
                        value.Release();
                    else
                    {
                        var saved = new LottieScalar3();
                        saved.CopyFrom(value);
                        backup = saved;
                    }

                    value.CopyFrom(source, false);
                    return true;
                case LottieVector value when prop is LottieVector source:
                    if (release)
                        value.Release();
                    else
                    {
                        var saved = new LottieVector();
                        saved.CopyFrom(value);
                        backup = saved;
                    }

                    value.CopyFrom(source, false);
                    return true;
                case LottieColor value when prop is LottieColor source:
                    if (release)
                        value.Release();
                    else
                    {
                        var saved = new LottieColor();
                        saved.CopyFrom(value);
                        backup = saved;
                    }

                    value.CopyFrom(source, false);
                    return true;
                case LottieOpacity value when prop is LottieOpacity source:
                    if (release)
                        value.Release();
                    else
                    {
                        var saved = new LottieOpacity();
                        saved.CopyFrom(value);
                        backup = saved;
                    }

                    value.CopyFrom(source, false);
                    return true;
                case LottiePathSet value when prop is LottiePathSet source:
                    if (release)
                        value.Release();
                    else
                    {
                        var saved = new LottiePathSet();
                        saved.CopyFrom(value);
                        backup = saved;
                    }

                    value.CopyFrom(source, false);
                    return true;
                case LottieColorStop value when prop is LottieColorStop source:
                    if (release)
                        value.Release();
                    else
                    {
                        var saved = new LottieColorStop();
                        saved.CopyFrom(value);
                        backup = saved;
                    }

                    value.CopyFrom(source, false);
                    return true;
                case LottieTextDoc value when prop is LottieTextDoc source:
                    if (release)
                        value.Release();
                    else
                    {
                        var saved = new LottieTextDoc();
                        saved.CopyFrom(value);
                        backup = saved;
                    }

                    value.CopyFrom(source, false);
                    return true;
                case LottieAsset value when prop is LottieAsset source:
                    if (release)
                        value.Release();
                    else
                    {
                        var saved = new LottieAsset();
                        saved.CopyFrom(value);
                        backup = saved;
                    }

                    value.CopyFrom(source, false);
                    return true;
                default:
                    return false;
            }
        }
    }

    // Concrete property: LottieFloat
    public class LottieFloat : LottieProperty
    {
        public List<LottieScalarFrame<float>>? frames;
        public float value;
        private LottieScalarFrame<float>? _nextFrame;
        public LottieFloat(float v = 0.0f) : base(PropertyType.Float)
        {
            value = v;
        }

        public override void Release()
        {
            _nextFrame = null;
            frames = null;
            exp = null;
        }

        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieScalarFrame<float>, ScalarFrameNo<float>>(frames, frameNo);
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieScalarFrame<float>, ScalarFrameNo<float>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) => LottiePropertyHelper.DoLoop<LottieScalarFrame<float>, ScalarFrameNo<float>>(frames, frameNo, key, mode, inout);
        public LottieScalarFrame<float> NewFrame()
        {
            frames ??= new List<LottieScalarFrame<float>>();
            var frame = _nextFrame ?? new LottieScalarFrame<float>();
            _nextFrame = null;
            frames.Add(frame);
            return frame;
        }

        public LottieScalarFrame<float> NextFrame()
        {
            return _nextFrame ??= new LottieScalarFrame<float>();
        }

        public float Evaluate(float frameNo, LottieExpressions? exps = null)
        {
            // Expression override (mirrors C++ operator())
            if (exps != null && exp != null)
            {
                if (exps.ResultFloat(this, frameNo, out var outVal, exp))
                    return outVal;
            }

            if (frames == null || frames.Count == 0)
                return value;
            if (frames.Count == 1 || frameNo <= frames[0].no)
                return frames[0].value;
            if (frameNo >= frames[^1].no)
                return frames[^1].value;
            var key = (int)LottiePropertyHelper.BSearch<LottieScalarFrame<float>, ScalarFrameNo<float>>(frames, frameNo);
            if (TvgMath.Equal(frames[key].no, frameNo))
                return frames[key].value;
            return ScalarFrameHelper.InterpolateFloat(frames[key], frames[key + 1], frameNo);
        }

        public float Evaluate(float frameNo, LottieTween tween, LottieExpressions? exps)
        {
            if (!tween.active || frames == null || frames.Count <= 1)
                return Evaluate(frameNo, exps);
            if (!tween.Inited(this, frameNo))
                tween.Capture(this, frameNo, Evaluate(frameNo, exps));
            return tween.Run(this, frameNo, Evaluate(tween.to, exps));
        }

        public void CopyFrom(LottieFloat rhs, bool shallow = true)
        {
            _nextFrame = null;
            if (Copy(rhs, shallow))
                return;
            if (rhs.frames != null)
            {
                if (shallow)
                {
                    frames = rhs.frames;
                    rhs.frames = null;
                }
                else
                {
                    frames = rhs.frames.ConvertAll(frame => frame.Clone());
                }
            }
            else
            {
                frames = null;
                value = rhs.value;
            }
        }

        public void Prepare()
        {
        }
    }

    // Concrete property: LottieInteger
    public class LottieInteger : LottieProperty
    {
        public List<LottieScalarFrame<sbyte>>? frames;
        public sbyte value;
        private LottieScalarFrame<sbyte>? _nextFrame;
        public LottieInteger(sbyte v = 0) : base(PropertyType.Integer)
        {
            value = v;
        }

        public override void Release()
        {
            _nextFrame = null;
            frames = null;
            exp = null;
        }

        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieScalarFrame<sbyte>, ScalarFrameNo<sbyte>>(frames, frameNo);
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieScalarFrame<sbyte>, ScalarFrameNo<sbyte>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) => LottiePropertyHelper.DoLoop<LottieScalarFrame<sbyte>, ScalarFrameNo<sbyte>>(frames, frameNo, key, mode, inout);
        public LottieScalarFrame<sbyte> NewFrame()
        {
            frames ??= new List<LottieScalarFrame<sbyte>>();
            var frame = _nextFrame ?? new LottieScalarFrame<sbyte>();
            _nextFrame = null;
            frames.Add(frame);
            return frame;
        }

        public LottieScalarFrame<sbyte> NextFrame()
        {
            return _nextFrame ??= new LottieScalarFrame<sbyte>();
        }

        public sbyte Evaluate(float frameNo, LottieExpressions? exps = null)
        {
            // Expression override (mirrors C++ operator())
            if (exps != null && exp != null)
            {
                if (exps.ResultInteger(this, frameNo, out var outVal, exp))
                    return (sbyte)outVal;
            }

            if (frames == null || frames.Count == 0)
                return value;
            if (frames.Count == 1 || frameNo <= frames[0].no)
                return frames[0].value;
            if (frameNo >= frames[^1].no)
                return frames[^1].value;
            var key = (int)LottiePropertyHelper.BSearch<LottieScalarFrame<sbyte>, ScalarFrameNo<sbyte>>(frames, frameNo);
            if (TvgMath.Equal(frames[key].no, frameNo))
                return frames[key].value;
            return ScalarFrameHelper.InterpolateSByte(frames[key], frames[key + 1], frameNo);
        }

        public sbyte Evaluate(float frameNo, LottieTween tween, LottieExpressions? exps)
        {
            if (!tween.active || frames == null || frames.Count <= 1)
                return Evaluate(frameNo, exps);
            if (!tween.Inited(this, frameNo))
                tween.Capture(this, frameNo, Evaluate(frameNo, exps));
            return tween.Run(this, frameNo, Evaluate(tween.to, exps));
        }

        public void CopyFrom(LottieInteger rhs, bool shallow = true)
        {
            _nextFrame = null;
            if (Copy(rhs, shallow))
                return;
            if (rhs.frames != null)
            {
                if (shallow)
                {
                    frames = rhs.frames;
                    rhs.frames = null;
                }
                else
                {
                    frames = rhs.frames.ConvertAll(frame => frame.Clone());
                }
            }
            else
            {
                frames = null;
                value = rhs.value;
            }
        }

        public void Prepare()
        {
        }
    }

    // Concrete property: LottieScalar
    public class LottieScalar : LottieProperty
    {
        public List<LottieScalarFrame<Point>>? frames;
        public Point value;
        private LottieScalarFrame<Point>? _nextFrame;
        public LottieScalar(Point v = default) : base(PropertyType.Scalar)
        {
            value = v;
        }

        public override void Release()
        {
            _nextFrame = null;
            frames = null;
            exp = null;
        }

        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieScalarFrame<Point>, ScalarFrameNo<Point>>(frames, frameNo);
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieScalarFrame<Point>, ScalarFrameNo<Point>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) => LottiePropertyHelper.DoLoop<LottieScalarFrame<Point>, ScalarFrameNo<Point>>(frames, frameNo, key, mode, inout);
        public LottieScalarFrame<Point> NewFrame()
        {
            frames ??= new List<LottieScalarFrame<Point>>();
            var frame = _nextFrame ?? new LottieScalarFrame<Point>();
            _nextFrame = null;
            frames.Add(frame);
            return frame;
        }

        public LottieScalarFrame<Point> NextFrame()
        {
            return _nextFrame ??= new LottieScalarFrame<Point>();
        }

        public Point Evaluate(float frameNo, LottieExpressions? exps = null)
        {
            // Expression override (mirrors C++ operator())
            if (exps != null && exp != null)
            {
                if (exps.ResultScalar(this, frameNo, out var outVal, exp))
                    return outVal;
            }

            if (frames == null || frames.Count == 0)
                return value;
            if (frames.Count == 1 || frameNo <= frames[0].no)
                return frames[0].value;
            if (frameNo >= frames[^1].no)
                return frames[^1].value;
            var key = (int)LottiePropertyHelper.BSearch<LottieScalarFrame<Point>, ScalarFrameNo<Point>>(frames, frameNo);
            if (TvgMath.Equal(frames[key].no, frameNo))
                return frames[key].value;
            return ScalarFrameHelper.InterpolatePoint(frames[key], frames[key + 1], frameNo);
        }

        public Point Evaluate(float frameNo, LottieTween tween, LottieExpressions? exps)
        {
            if (!tween.active || frames == null || frames.Count <= 1)
                return Evaluate(frameNo, exps);
            if (!tween.Inited(this, frameNo))
                tween.Capture(this, frameNo, Evaluate(frameNo, exps));
            return tween.Run(this, frameNo, Evaluate(tween.to, exps));
        }

        public void CopyFrom(LottieScalar rhs, bool shallow = true)
        {
            _nextFrame = null;
            if (Copy(rhs, shallow))
                return;
            if (rhs.frames != null)
            {
                if (shallow)
                {
                    frames = rhs.frames;
                    rhs.frames = null;
                }
                else
                {
                    frames = rhs.frames.ConvertAll(frame => frame.Clone());
                }
            }
            else
            {
                frames = null;
                value = rhs.value;
            }
        }

        public void Prepare()
        {
        }
    }

    // Concrete property: LottieVector
    public class LottieVector : LottieProperty
    {
        public List<LottieVectorFrame<Point>>? frames;
        public Point value;
        private LottieVectorFrame<Point>? _nextFrame;
        public LottieVector(Point v = default) : base(PropertyType.Vector)
        {
            value = v;
        }

        public override void Release()
        {
            _nextFrame = null;
            frames = null;
            exp = null;
        }

        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieVectorFrame<Point>, VectorFrameNo<Point>>(frames, frameNo);
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieVectorFrame<Point>, VectorFrameNo<Point>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) => LottiePropertyHelper.DoLoop<LottieVectorFrame<Point>, VectorFrameNo<Point>>(frames, frameNo, key, mode, inout);
        public LottieVectorFrame<Point> NewFrame()
        {
            frames ??= new List<LottieVectorFrame<Point>>();
            var frame = _nextFrame ?? new LottieVectorFrame<Point>();
            _nextFrame = null;
            frames.Add(frame);
            return frame;
        }

        public LottieVectorFrame<Point> NextFrame()
        {
            return _nextFrame ??= new LottieVectorFrame<Point>();
        }

        public Point Evaluate(float frameNo, LottieExpressions? exps = null) => Evaluate(frameNo, exps, false);

        private Point Evaluate(float frameNo, LottieExpressions? exps, bool renderVector)
        {
            // Expression override (mirrors C++ operator())
            if (exps != null && exp != null)
            {
                if (exps.ResultVector(this, frameNo, out var outVal, exp))
                    return outVal;
            }

            if (frames == null || frames.Count == 0)
                return value;
            if (frames.Count == 1 || frameNo <= frames[0].no)
                return frames[0].value;
            if (frameNo >= frames[^1].no)
                return frames[^1].value;
            var key = (int)LottiePropertyHelper.BSearch<LottieVectorFrame<Point>, VectorFrameNo<Point>>(frames, frameNo);
            if (TvgMath.Equal(frames[key].no, frameNo))
                return frames[key].value;
            return ScalarFrameHelper.InterpolateVectorPoint(frames[key], frames[key + 1], frameNo, renderVector);
        }

        public Point Evaluate(float frameNo, LottieTween tween, LottieExpressions? exps) => Evaluate(frameNo, tween, exps, false);

        internal Point EvaluateForRendering(float frameNo, LottieTween tween, LottieExpressions? exps) => Evaluate(frameNo, tween, exps, true);

        private Point Evaluate(float frameNo, LottieTween tween, LottieExpressions? exps, bool renderVector)
        {
            if (!tween.active || frames == null || frames.Count <= 1)
                return Evaluate(frameNo, exps, renderVector);
            if (!tween.Inited(this, frameNo))
                tween.Capture(this, frameNo, Evaluate(frameNo, exps, renderVector));
            return tween.Run(this, frameNo, Evaluate(tween.to, exps, renderVector));
        }

        public float GetAngle(float frameNo)
        {
            if (frames == null || frames.Count <= 1)
                return 0;
            if (frameNo <= frames[0].no)
                return ScalarFrameHelper.VectorPointAngle(frames[0], frames[1], frames[0].no);
            if (frameNo >= frames[^1].no)
                return ScalarFrameHelper.VectorPointAngle(frames[^2], frames[^1], frames[^1].no);
            var key = (int)LottiePropertyHelper.BSearch<LottieVectorFrame<Point>, VectorFrameNo<Point>>(frames, frameNo);
            return ScalarFrameHelper.VectorPointAngle(frames[key], frames[key + 1], frameNo);
        }

        public float GetAngle(float frameNo, LottieTween tween)
        {
            if (!tween.active || frames == null || frames.Count <= 1)
                return GetAngle(frameNo);
            // Native orientation and position share this property's tween key
            // and numeric union; orientation updates its first float word.
            if (!tween.Inited(this, frameNo))
                tween.Capture(this, frameNo, GetAngle(frameNo));
            return tween.Run(this, frameNo, GetAngle(tween.to));
        }

        public void CopyFrom(LottieVector rhs, bool shallow = true)
        {
            _nextFrame = null;
            if (Copy(rhs, shallow))
                return;
            if (rhs.frames != null)
            {
                if (shallow)
                {
                    frames = rhs.frames;
                    rhs.frames = null;
                }
                else
                {
                    frames = rhs.frames.ConvertAll(frame => frame.Clone());
                }
            }
            else
            {
                frames = null;
                value = rhs.value;
            }
        }

        public void Prepare()
        {
            if (frames == null || frames.Count < 2)
                return;
            for (int i = 0; i < frames.Count - 1; ++i)
            {
                ScalarFrameHelper.PrepareVectorPoint(frames[i], frames[i + 1]);
            }
        }
    }

    public class LottieScalar3 : LottieProperty
    {
        public List<LottieScalarFrame<Point3>>? frames;
        public Point3 value;
        private LottieScalarFrame<Point3>? _nextFrame;
        public LottieScalar3(Point3 v = default) : base(PropertyType.Scalar3)
        {
            value = v;
        }

        public override void Release()
        {
            _nextFrame = null;
            frames = null;
            exp = null;
        }

        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieScalarFrame<Point3>, ScalarFrameNo<Point3>>(frames, frameNo);
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieScalarFrame<Point3>, ScalarFrameNo<Point3>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) => LottiePropertyHelper.DoLoop<LottieScalarFrame<Point3>, ScalarFrameNo<Point3>>(frames, frameNo, key, mode, inout);
        public LottieScalarFrame<Point3> NewFrame()
        {
            frames ??= new List<LottieScalarFrame<Point3>>();
            var frame = _nextFrame ?? new LottieScalarFrame<Point3>();
            _nextFrame = null;
            frames.Add(frame);
            return frame;
        }

        public LottieScalarFrame<Point3> NextFrame()
        {
            return _nextFrame ??= new LottieScalarFrame<Point3>();
        }

        public Point3 Evaluate(float frameNo, LottieExpressions? exps = null)
        {
            if (frames == null || frames.Count == 0)
                return value;
            if (frames.Count == 1 || frameNo <= frames[0].no)
                return frames[0].value;
            if (frameNo >= frames[^1].no)
                return frames[^1].value;
            var key = (int)LottiePropertyHelper.BSearch<LottieScalarFrame<Point3>, ScalarFrameNo<Point3>>(frames, frameNo);
            var frame = frames[key];
            var next = frames[key + 1];
            if (TvgMath.Equal(frame.no, frameNo))
                return frame.value;
            var t = (frameNo - frame.no) / (next.no - frame.no);
            if (frame.interpolator != null)
                t = frame.interpolator.Progress(t);
            if (frame.hold)
                return (t < 1.0f) ? frame.value : next.value;
            var a = frame.value;
            var b = next.value;
            return TvgMath.Lerp(a, b, t);
        }

        public Point3 Evaluate(float frameNo, LottieTween tween, LottieExpressions? exps)
        {
            if (!tween.active || frames == null || frames.Count <= 1)
                return Evaluate(frameNo, exps);
            if (!tween.Inited(this, frameNo))
                tween.Capture(this, frameNo, Evaluate(frameNo, exps));
            return tween.Run(this, frameNo, Evaluate(tween.to, exps));
        }

        public void CopyFrom(LottieScalar3 rhs, bool shallow = true)
        {
            _nextFrame = null;
            if (Copy(rhs, shallow))
                return;
            if (rhs.frames != null)
            {
                if (shallow)
                {
                    frames = rhs.frames;
                    rhs.frames = null;
                }
                else
                {
                    frames = rhs.frames.ConvertAll(frame => frame.Clone());
                }
            }
            else
            {
                frames = null;
                value = rhs.value;
            }
        }

        public void Prepare()
        {
        }
    }

    // Concrete property: LottieColor
    public class LottieColor : LottieProperty
    {
        public List<LottieScalarFrame<RGB32>>? frames;
        public RGB32 value;
        private LottieScalarFrame<RGB32>? _nextFrame;
        public LottieColor(RGB32 v = default) : base(PropertyType.Color)
        {
            value = v;
        }

        public override void Release()
        {
            _nextFrame = null;
            frames = null;
            exp = null;
        }

        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieScalarFrame<RGB32>, ScalarFrameNo<RGB32>>(frames, frameNo);
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieScalarFrame<RGB32>, ScalarFrameNo<RGB32>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) => LottiePropertyHelper.DoLoop<LottieScalarFrame<RGB32>, ScalarFrameNo<RGB32>>(frames, frameNo, key, mode, inout);
        public LottieScalarFrame<RGB32> NewFrame()
        {
            frames ??= new List<LottieScalarFrame<RGB32>>();
            var frame = _nextFrame ?? new LottieScalarFrame<RGB32>();
            _nextFrame = null;
            frames.Add(frame);
            return frame;
        }

        public LottieScalarFrame<RGB32> NextFrame()
        {
            return _nextFrame ??= new LottieScalarFrame<RGB32>();
        }

        public RGB32 Evaluate(float frameNo, LottieExpressions? exps = null)
        {
            // Expression override (mirrors C++ operator())
            if (exps != null && exp != null)
            {
                if (exps.ResultColor(this, frameNo, out var outVal, exp))
                    return outVal;
            }

            if (frames == null || frames.Count == 0)
                return value;
            if (frames.Count == 1 || frameNo <= frames[0].no)
                return frames[0].value;
            if (frameNo >= frames[^1].no)
                return frames[^1].value;
            var key = (int)LottiePropertyHelper.BSearch<LottieScalarFrame<RGB32>, ScalarFrameNo<RGB32>>(frames, frameNo);
            if (TvgMath.Equal(frames[key].no, frameNo))
                return frames[key].value;
            return ScalarFrameHelper.InterpolateRGB32(frames[key], frames[key + 1], frameNo);
        }

        public RGB32 Evaluate(float frameNo, LottieTween tween, LottieExpressions? exps)
        {
            if (!tween.active || frames == null || frames.Count <= 1)
                return Evaluate(frameNo, exps);
            if (!tween.Inited(this, frameNo))
                tween.Capture(this, frameNo, Evaluate(frameNo, exps));
            return tween.Run(this, frameNo, Evaluate(tween.to, exps));
        }

        public void CopyFrom(LottieColor rhs, bool shallow = true)
        {
            _nextFrame = null;
            if (Copy(rhs, shallow))
                return;
            if (rhs.frames != null)
            {
                if (shallow)
                {
                    frames = rhs.frames;
                    rhs.frames = null;
                }
                else
                {
                    frames = rhs.frames.ConvertAll(frame => frame.Clone());
                }
            }
            else
            {
                frames = null;
                value = rhs.value;
            }
        }

        public void Prepare()
        {
        }
    }

    // Concrete property: LottieOpacity
    public class LottieOpacity : LottieProperty
    {
        public List<LottieScalarFrame<byte>>? frames;
        public byte value;
        private LottieScalarFrame<byte>? _nextFrame;
        public LottieOpacity(byte v = 255) : base(PropertyType.Opacity)
        {
            value = v;
        }

        public override void Release()
        {
            _nextFrame = null;
            frames = null;
            exp = null;
        }

        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieScalarFrame<byte>, ScalarFrameNo<byte>>(frames, frameNo);
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieScalarFrame<byte>, ScalarFrameNo<byte>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) => LottiePropertyHelper.DoLoop<LottieScalarFrame<byte>, ScalarFrameNo<byte>>(frames, frameNo, key, mode, inout);
        public LottieScalarFrame<byte> NewFrame()
        {
            frames ??= new List<LottieScalarFrame<byte>>();
            var frame = _nextFrame ?? new LottieScalarFrame<byte>();
            _nextFrame = null;
            frames.Add(frame);
            return frame;
        }

        public LottieScalarFrame<byte> NextFrame()
        {
            return _nextFrame ??= new LottieScalarFrame<byte>();
        }

        public byte Evaluate(float frameNo, LottieExpressions? exps = null)
        {
            // Expression override (mirrors C++ operator())
            if (exps != null && exp != null)
            {
                if (exps.ResultOpacity(this, frameNo, out var outVal, exp))
                    return outVal;
            }

            if (frames == null || frames.Count == 0)
                return value;
            if (frames.Count == 1 || frameNo <= frames[0].no)
                return frames[0].value;
            if (frameNo >= frames[^1].no)
                return frames[^1].value;
            var key = (int)LottiePropertyHelper.BSearch<LottieScalarFrame<byte>, ScalarFrameNo<byte>>(frames, frameNo);
            if (TvgMath.Equal(frames[key].no, frameNo))
                return frames[key].value;
            return ScalarFrameHelper.InterpolateByte(frames[key], frames[key + 1], frameNo);
        }

        public byte Evaluate(float frameNo, LottieTween tween, LottieExpressions? exps)
        {
            if (!tween.active || frames == null || frames.Count <= 1)
                return Evaluate(frameNo, exps);
            if (!tween.Inited(this, frameNo))
                tween.Capture(this, frameNo, Evaluate(frameNo, exps));
            return tween.Run(this, frameNo, Evaluate(tween.to, exps));
        }

        public void CopyFrom(LottieOpacity rhs, bool shallow = true)
        {
            _nextFrame = null;
            if (Copy(rhs, shallow))
                return;
            if (rhs.frames != null)
            {
                if (shallow)
                {
                    frames = rhs.frames;
                    rhs.frames = null;
                }
                else
                {
                    frames = rhs.frames.ConvertAll(frame => frame.Clone());
                }
            }
            else
            {
                frames = null;
                value = rhs.value;
            }
        }

        public void Prepare()
        {
        }
    }

    // LottiePathSet
    public class LottiePathSet : LottieProperty
    {
        // Non-owning wrappers stay separate from the owning RenderPath scratch ring.
        // A depth slot keeps a nested modifier callback from replacing its caller's view.
        [ThreadStatic]
        private static List<RenderPath>? s_borrowedPaths;
        [ThreadStatic]
        private static int s_borrowedDepth;
        public List<LottieScalarFrame<PathSet>>? frames;
        public PathSet value;
        private LottieScalarFrame<PathSet>? _nextFrame;
        public LottiePathSet() : base(PropertyType.PathSet)
        {
        }

        public override void Release()
        {
            _nextFrame = null;
            exp = null;
            frames = null;
            value = default;
        }

        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieScalarFrame<PathSet>, ScalarFrameNo<PathSet>>(frames, frameNo);
        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieScalarFrame<PathSet>, ScalarFrameNo<PathSet>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) => LottiePropertyHelper.DoLoop<LottieScalarFrame<PathSet>, ScalarFrameNo<PathSet>>(frames, frameNo, key, mode, inout);
        public LottieScalarFrame<PathSet> NewFrame()
        {
            frames ??= new List<LottieScalarFrame<PathSet>>();
            var frame = _nextFrame ?? new LottieScalarFrame<PathSet>();
            _nextFrame = null;
            frames.Add(frame);
            return frame;
        }

        public LottieScalarFrame<PathSet> NextFrame()
        {
            return _nextFrame ??= new LottieScalarFrame<PathSet>();
        }

        public void CopyFrom(LottiePathSet rhs, bool shallow = true)
        {
            _nextFrame = null;
            if (Copy(rhs, shallow))
                return;
            if (rhs.frames != null)
            {
                if (shallow)
                {
                    frames = rhs.frames;
                    rhs.frames = null;
                }
                else
                    frames = rhs.frames.ConvertAll(frame =>
                    {
                        var clone = frame.Clone();
                        clone.value = ClonePath(frame.value);
                        return clone;
                    });
            }
            else
            {
                frames = null;
                value = shallow ? rhs.value : ClonePath(rhs.value);
                if (shallow)
                    rhs.value = default;
            }
        }

        private static PathSet ClonePath(PathSet path) => new PathSet
        {
            ptsCnt = path.ptsCnt,
            cmdsCnt = path.cmdsCnt,
            pts = path.ptsCnt == 0 ? null ! : path.pts.AsSpan(0, path.ptsCnt).ToArray(),
            cmds = path.cmdsCnt == 0 ? null ! : path.cmds.AsSpan(0, path.cmdsCnt).ToArray()
        };
        public unsafe bool DefaultPath(float frameNo, RenderPath @out, Matrix* transform)
        {
            PathSet path;
            int frameIdx;
            float t;
            if (Dispatch(frameNo, out path, out frameIdx, out t))
            {
                CopyPathSet(path, @out, transform);
                return true;
            }

            var fv = frames![frameIdx].value;
            var nv = frames[frameIdx + 1].value;
            for (int i = 0; i < fv.ptsCnt; ++i)
            {
                var pt = TvgMath.Lerp(fv.pts[i], nv.pts[i], t);
                if (transform != null)
                    TvgMath.TransformInPlace(ref pt, *transform);
                @out.pts.Push(pt);
            }

            CopyPathSetCmds(fv, @out);
            return true;
        }

        public unsafe bool ModifiedPath(float frameNo, RenderPath @out, Matrix* transform, LottieModifier modifier)
        {
            PathSet path;
            int frameIdx;
            float t;
            if (Dispatch(frameNo, out path, out frameIdx, out t))
            {
                fixed (PathCommand* commands = path.cmds)
                fixed (Point* points = path.pts)
                    ModifyBorrowed(commands, path.cmdsCnt, points, path.ptsCnt, @out, transform, modifier);
                return true;
            }

            var fv = frames![frameIdx].value;
            var nv = frames[frameIdx + 1].value;
            var interpolated = ArrayPool<Point>.Shared.Rent(fv.ptsCnt);
            try
            {
                for (int i = 0; i < fv.ptsCnt; ++i)
                {
                    var pt = TvgMath.Lerp(fv.pts[i], nv.pts[i], t);
                    if (transform != null)
                        TvgMath.TransformInPlace(ref pt, *transform);
                    interpolated[i] = pt;
                }

                fixed (PathCommand* commands = fv.cmds)
                fixed (Point* points = interpolated)
                    ModifyBorrowed(commands, fv.cmdsCnt, points, fv.ptsCnt, @out, null, modifier);
            }
            finally
            {
                ArrayPool<Point>.Shared.Return(interpolated);
            }

            return true;
        }

        private static unsafe void ModifyBorrowed(PathCommand* commands, uint commandCount, Point* points, uint pointCount, RenderPath output, Matrix* transform, LottieModifier modifier)
        {
            var pool = s_borrowedPaths ??= new List<RenderPath>();
            if (pool.Count == s_borrowedDepth)
                pool.Add(new RenderPath());
            var input = pool[s_borrowedDepth++];
            input.cmds = new Array<PathCommand>
            {
                data = commands,
                count = commandCount
            };
            input.pts = new Array<Point>
            {
                data = points,
                count = pointCount
            };
            try
            {
                modifier.Path(input, output, transform);
            }
            finally
            {
                // These arrays belong to pinned PathSet storage or to the interpolation pool.
                input.cmds = default;
                input.pts = default;
                --s_borrowedDepth;
            }
        }

        public unsafe bool Evaluate(float frameNo, RenderPath @out, Matrix* transform, LottieExpressions? exps, LottieModifier? modifier = null)
        {
            if (exps != null && exp != null)
            {
                if (exps.ResultPathSet(this, frameNo, @out, transform, modifier, exp))
                    return true;
            }

            if (modifier != null)
                return ModifiedPath(frameNo, @out, transform, modifier);
            return DefaultPath(frameNo, @out, transform);
        }

        public unsafe bool Evaluate(float frameNo, RenderPath @out, Matrix* transform, LottieTween tween, LottieExpressions? exps, LottieModifier? modifier = null)
        {
            if (!tween.active || frames == null || frames.Count <= 1)
                return Evaluate(frameNo, @out, transform, exps, modifier);
            if (!tween.Inited(this, frameNo))
            {
                var from = RenderPath.Scratch();
                Evaluate(frameNo, from, transform, exps);
                tween.Capture(this, frameNo, from);
            }

            var to = RenderPath.Scratch();
            Evaluate(tween.to, to, transform, exps);
            tween.Run(this, frameNo, to, @out, modifier);
            return true;
        }

        private bool Dispatch(float frameNo, out PathSet path, out int frameIdx, out float t)
        {
            path = default;
            frameIdx = -1;
            t = 0;
            if (frames == null || frames.Count == 0)
            {
                path = value;
                return true;
            }

            if (frames.Count == 1 || frameNo <= frames[0].no)
            {
                path = frames[0].value;
                return true;
            }

            if (frameNo >= frames[^1].no)
            {
                path = frames[^1].value;
                return true;
            }

            frameIdx = (int)LottiePropertyHelper.BSearch<LottieScalarFrame<PathSet>, ScalarFrameNo<PathSet>>(frames, frameNo);
            var frame = frames[frameIdx];
            if (TvgMath.Equal(frame.no, frameNo))
            {
                path = frame.value;
                return true;
            }

            if (frame.value.ptsCnt != frames[frameIdx + 1].value.ptsCnt)
            {
                path = frame.value;
                return true;
            }

            t = (frameNo - frame.no) / (frames[frameIdx + 1].no - frame.no);
            if (frame.interpolator != null)
                t = frame.interpolator.Progress(t);
            if (frame.hold)
            {
                path = (t < 1.0f) ? frame.value : frames[frameIdx + 1].value;
                return true;
            }

            return false;
        }

        private static unsafe void CopyPathSet(PathSet pathset, RenderPath @out, Matrix* transform)
        {
            if (pathset.cmds != null)
                for (int i = 0; i < pathset.cmdsCnt; ++i)
                    @out.cmds.Push(pathset.cmds[i]);
            if (pathset.pts != null)
            {
                if (transform != null)
                    for (int i = 0; i < pathset.ptsCnt; ++i)
                    {
                        var pt = pathset.pts[i];
                        TvgMath.TransformInPlace(ref pt, *transform);
                        @out.pts.Push(pt);
                    }
                else
                    for (int i = 0; i < pathset.ptsCnt; ++i)
                        @out.pts.Push(pathset.pts[i]);
            }
        }

        private static void CopyPathSetCmds(PathSet pathset, RenderPath @out)
        {
            if (pathset.cmds != null)
                for (int i = 0; i < pathset.cmdsCnt; ++i)
                    @out.cmds.Push(pathset.cmds[i]);
        }
    }

    // LottieColorStop
    public class LottieColorStop : LottieProperty, IDisposable
    {
        public List<LottieScalarFrame<ColorStop>>? frames;
        public ColorStop value = new();
        public ushort count;
        public bool populated;
        public LottieColorStop() : base(PropertyType.ColorStop)
        {
        }

        public LottieColorStop(LottieColorStop rhs) : base(PropertyType.ColorStop)
        {
            CopyFrom(rhs, false);
        }

        ~LottieColorStop() => Release();
        public void Dispose()
        {
            Release();
            GC.SuppressFinalize(this);
        }

        public override void Release()
        {
            _nextFrame?.value.input?.Dispose();
            _nextFrame = null;
            exp = null;
            value.input?.Dispose();
            if (frames != null)
                foreach (var frame in frames)
                    frame.value.input?.Dispose();
            value = new ColorStop();
            frames = null;
        }

        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieScalarFrame<ColorStop>, ScalarFrameNo<ColorStop>>(frames, frameNo);
        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieScalarFrame<ColorStop>, ScalarFrameNo<ColorStop>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) => LottiePropertyHelper.DoLoop<LottieScalarFrame<ColorStop>, ScalarFrameNo<ColorStop>>(frames, frameNo, key, mode, inout);
        private LottieScalarFrame<ColorStop>? _nextFrame;
        public LottieScalarFrame<ColorStop> NewFrame()
        {
            frames ??= new List<LottieScalarFrame<ColorStop>>();
            var frame = _nextFrame ?? new LottieScalarFrame<ColorStop>
            {
                value = new ColorStop()
            };
            _nextFrame = null;
            frames.Add(frame);
            return frame;
        }

        public LottieScalarFrame<ColorStop> NextFrame()
        {
            return _nextFrame ??= new LottieScalarFrame<ColorStop>
            {
                value = new ColorStop()
            };
        }

        public Result Evaluate(float frameNo, Fill fill, LottieExpressions? exps = null)
        {
            if (exps != null && exp != null)
            {
                if (exps.ResultColorStop(this, frameNo, fill, exp))
                    return Result.Success;
            }

            if (frames == null || frames.Count == 0)
                return fill.SetColorStops(value.data, count);
            if (frames.Count == 1 || frameNo <= frames[0].no)
                return fill.SetColorStops(frames[0].value.data, count);
            if (frameNo >= frames[^1].no)
                return fill.SetColorStops(frames[^1].value.data, count);
            var key = (int)LottiePropertyHelper.BSearch<LottieScalarFrame<ColorStop>, ScalarFrameNo<ColorStop>>(frames, frameNo);
            if (TvgMath.Equal(frames[key].no, frameNo))
                return fill.SetColorStops(frames[key].value.data, count);
            var t = (frameNo - frames[key].no) / (frames[key + 1].no - frames[key].no);
            var interp = frames[key].interpolator;
            if (interp != null)
                t = interp.Progress(t);
            // Native applies the held value first, then writes its interpolated
            // stops below. Preserve that order, including its fall-through.
            if (frames[key].hold)
                fill.SetColorStops((t < 1.0f) ? frames[key].value.data : frames[key + 1].value.data, count);
            var s = frames[key].value.data;
            var e = frames[key + 1].value.data;
            if (s == null || e == null)
                return fill.SetColorStops(s, count);
            var result = new Fill.ColorStop[count];
            for (int i = 0; i < count && i < s.Length && i < e.Length; ++i)
                result[i] = new Fill.ColorStop(TvgMath.Lerp(s[i].offset, e[i].offset, t), TvgMath.Lerp(s[i].r, e[i].r, t), TvgMath.Lerp(s[i].g, e[i].g, t), TvgMath.Lerp(s[i].b, e[i].b, t), TvgMath.Lerp(s[i].a, e[i].a, t));
            return fill.SetColorStops(result, count);
        }

        public Result Evaluate(float frameNo, Fill fill, LottieTween tween, LottieExpressions? exps)
        {
            if (!tween.active || frames == null || frames.Count <= 1)
                return Evaluate(frameNo, fill, exps);
            if (!tween.Inited(this, frameNo))
            {
                Evaluate(frameNo, fill, exps);
                tween.Capture(this, frameNo, fill);
            }

            Evaluate(tween.to, fill, exps);
            tween.Run(this, frameNo, fill);
            return Result.Success;
        }

        public void CopyFrom(LottieColorStop rhs, bool shallow = true)
        {
            _nextFrame?.value.input?.Dispose();
            _nextFrame = null;
            if (Copy(rhs, shallow))
                return;
            if (rhs.frames != null)
            {
                if (shallow)
                {
                    frames = rhs.frames;
                    rhs.frames = null;
                }
                else
                {
                    frames = new List<LottieScalarFrame<ColorStop>>();
                    foreach (var f in rhs.frames)
                    {
                        var newFrame = new LottieScalarFrame<ColorStop>
                        {
                            no = f.no,
                            interpolator = f.interpolator,
                            hold = f.hold,
                            value = new ColorStop()
                        };
                        newFrame.value.Copy(f.value, rhs.count);
                        frames.Add(newFrame);
                    }
                }
            }
            else
            {
                frames = null;
                if (shallow)
                {
                    value = rhs.value;
                    rhs.value = new ColorStop();
                }
                else
                {
                    value = new ColorStop();
                    value.Copy(rhs.value, rhs.count);
                }
            }

            populated = rhs.populated;
            count = rhs.count;
        }

        public void Prepare()
        {
        }
    }

    // LottieTextDoc
    public class LottieTextDoc : LottieProperty
    {
        public List<LottieScalarFrame<TextDocument>>? frames;
        public TextDocument value = new();
        public LottieTextDoc() : base(PropertyType.TextDoc)
        {
        }

        public LottieTextDoc(LottieTextDoc rhs) : base(PropertyType.TextDoc)
        {
            CopyFrom(rhs, false);
        }

        public override void Release()
        {
            _nextFrame = null;
            exp = null;
            value.text = value.name = null;
            frames = null;
        }

        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieScalarFrame<TextDocument>, ScalarFrameNo<TextDocument>>(frames, frameNo);
        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieScalarFrame<TextDocument>, ScalarFrameNo<TextDocument>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) => LottiePropertyHelper.DoLoop<LottieScalarFrame<TextDocument>, ScalarFrameNo<TextDocument>>(frames, frameNo, key, mode, inout);
        private LottieScalarFrame<TextDocument>? _nextFrame;
        public LottieScalarFrame<TextDocument> NewFrame()
        {
            frames ??= new List<LottieScalarFrame<TextDocument>>();
            var frame = _nextFrame ?? new LottieScalarFrame<TextDocument>
            {
                value = new TextDocument()
            };
            _nextFrame = null;
            frames.Add(frame);
            return frame;
        }

        public LottieScalarFrame<TextDocument> NextFrame()
        {
            return _nextFrame ??= new LottieScalarFrame<TextDocument>
            {
                value = new TextDocument()
            };
        }

        public TextDocument Evaluate(float frameNo)
        {
            if (frames == null || frames.Count == 0)
                return value;
            if (frames.Count == 1 || frameNo <= frames[0].no)
                return frames[0].value;
            if (frameNo >= frames[^1].no)
                return frames[^1].value;
            var key = (int)LottiePropertyHelper.BSearch<LottieScalarFrame<TextDocument>, ScalarFrameNo<TextDocument>>(frames, frameNo);
            return frames[key].value;
        }

        public TextDocument Evaluate(float frameNo, LottieExpressions? exps)
        {
            var @out = Evaluate(frameNo);
            if (exps != null && exp != null)
                exps.ResultTextDoc(frameNo, @out, exp);
            return @out;
        }

        public void CopyFrom(LottieTextDoc rhs, bool shallow = true)
        {
            _nextFrame = null;
            if (Copy(rhs, shallow))
                return;
            if (rhs.frames != null)
            {
                if (shallow)
                {
                    frames = rhs.frames;
                    rhs.frames = null;
                }
                else
                {
                    frames = new List<LottieScalarFrame<TextDocument>>();
                    foreach (var f in rhs.frames)
                    {
                        var newFrame = f.Clone();
                        newFrame.value = f.value.Clone();
                        frames.Add(newFrame);
                    }
                }
            }
            else
            {
                frames = null;
                if (shallow)
                {
                    value = rhs.value.Clone();
                    rhs.value.text = rhs.value.name = null;
                }
                else
                    value.Copy(rhs.value);
            }
        }

        public void Prepare()
        {
        }
    }

    // Target LottieAsset: encoded bytes or a path, independent from loaded paints.
    public class LottieAsset : LottieProperty
    {
        public string? data;
        public string? path { get => data; set => data = value; }

        public byte[]? b64Data;
        public string? mimeType;
        public uint size;
        public bool external;
        public float width;
        public float height;
        public LottieAsset() : base(PropertyType.Image)
        {
        }

        public LottieAsset(LottieAsset rhs) : this()
        {
            CopyFrom(rhs, false);
        }

        public override void Release()
        {
            data = null;
            b64Data = null;
            mimeType = null;
            size = 0;
        }

        public override uint FrameCnt() => 0;
        public override uint Nearest(float frameNo) => 0;
        public override float FrameNo(int key) => 0;
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) => frameNo;
        public void CopyFrom(LottieAsset rhs, bool shallow = true)
        {
            if (Copy(rhs, shallow))
                return;
            Release();
            data = rhs.data;
            mimeType = rhs.mimeType;
            b64Data = shallow ? rhs.b64Data : rhs.b64Data?.AsSpan(0, (int)rhs.size).ToArray();
            if (shallow)
            {
                rhs.data = rhs.mimeType = null;
                rhs.b64Data = null;
            }

            size = rhs.size;
            external = rhs.external;
            width = rhs.width;
            height = rhs.height;
        }
    }

    // Existing managed API compatibility. Slot copies copy source data, never a
    // decoded Picture; LottieImage owns and invalidates its cached picture.
    public class LottieBitmap : LottieAsset
    {
        public Picture? picture;
        public LottieBitmap()
        {
        }

        public LottieBitmap(LottieBitmap rhs)
        {
            CopyFrom(rhs, false);
        }

        ~LottieBitmap()
        {
            Release();
        }

        public override void Release()
        {
            if (picture != null)
            {
                picture.Unref();
                picture = null;
            }

            base.Release();
        }
    }
}
