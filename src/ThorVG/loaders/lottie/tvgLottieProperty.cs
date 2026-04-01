// Ported from ThorVG/src/loaders/lottie/tvgLottieProperty.h

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ThorVG
{
    // Frame types -- no unmanaged constraint since we need them for PathSet, ColorStop, TextDocument too
    public class LottieScalarFrame<T>
    {
        public T value = default!;    // keyframe value
        public float no;              // frame number
        public LottieInterpolator? interpolator;
        public bool hold;             // do not interpolate
    }

    public class LottieVectorFrame<T>
    {
        public T value = default!;    // keyframe value
        public float no;              // frame number
        public LottieInterpolator? interpolator;
        public T outTangent = default!;
        public T inTangent = default!;
        public float length;
        public bool hasTangent;
        public bool hold;
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
            if (frame.interpolator != null) t = frame.interpolator.Progress(t);
            if (frame.hold) return (t < 1.0f) ? frame.value : next.value;
            return TvgMath.Lerp(frame.value, next.value, t);
        }

        public static sbyte InterpolateSByte(LottieScalarFrame<sbyte> frame, LottieScalarFrame<sbyte> next, float frameNo)
        {
            var t = (frameNo - frame.no) / (next.no - frame.no);
            if (frame.interpolator != null) t = frame.interpolator.Progress(t);
            if (frame.hold) return (t < 1.0f) ? frame.value : next.value;
            return (sbyte)TvgMath.Clamp((int)(frame.value + (next.value - frame.value) * t), sbyte.MinValue, sbyte.MaxValue);
        }

        public static byte InterpolateByte(LottieScalarFrame<byte> frame, LottieScalarFrame<byte> next, float frameNo)
        {
            var t = (frameNo - frame.no) / (next.no - frame.no);
            if (frame.interpolator != null) t = frame.interpolator.Progress(t);
            if (frame.hold) return (t < 1.0f) ? frame.value : next.value;
            return (byte)TvgMath.Clamp((int)(frame.value + (next.value - frame.value) * t), 0, 255);
        }

        public static Point InterpolatePoint(LottieScalarFrame<Point> frame, LottieScalarFrame<Point> next, float frameNo)
        {
            var t = (frameNo - frame.no) / (next.no - frame.no);
            if (frame.interpolator != null) t = frame.interpolator.Progress(t);
            if (frame.hold) return (t < 1.0f) ? frame.value : next.value;
            return new Point(
                frame.value.x + (next.value.x - frame.value.x) * t,
                frame.value.y + (next.value.y - frame.value.y) * t);
        }

        public static RGB32 InterpolateRGB32(LottieScalarFrame<RGB32> frame, LottieScalarFrame<RGB32> next, float frameNo)
        {
            var t = (frameNo - frame.no) / (next.no - frame.no);
            if (frame.interpolator != null) t = frame.interpolator.Progress(t);
            if (frame.hold) return (t < 1.0f) ? frame.value : next.value;
            return RGB32.Lerp(frame.value, next.value, t);
        }

        public static Point InterpolateVectorPoint(LottieVectorFrame<Point> frame, LottieVectorFrame<Point> next, float frameNo)
        {
            var t = (frameNo - frame.no) / (next.no - frame.no);
            if (frame.interpolator != null) t = frame.interpolator.Progress(t);
            if (frame.hold) return (t < 1.0f) ? frame.value : next.value;

            if (frame.hasTangent)
            {
                var bz = new Bezier(
                    frame.value,
                    TvgMath.PointAdd(frame.value, frame.outTangent),
                    TvgMath.PointAdd(next.value, frame.inTangent),
                    next.value);
                return bz.At(bz.AtApprox(t * frame.length, frame.length));
            }
            return new Point(
                frame.value.x + (next.value.x - frame.value.x) * t,
                frame.value.y + (next.value.y - frame.value.y) * t);
        }

        public static float VectorPointAngle(LottieVectorFrame<Point> frame, LottieVectorFrame<Point> next, float frameNo)
        {
            if (!frame.hasTangent)
            {
                var dp = TvgMath.PointSub(next.value, frame.value);
                return TvgMath.Rad2Deg(TvgMath.Atan2(dp.y, dp.x));
            }

            var t = (frameNo - frame.no) / (next.no - frame.no);
            if (frame.interpolator != null) t = frame.interpolator.Progress(t);
            var bz = new Bezier(
                frame.value,
                TvgMath.PointAdd(frame.value, frame.outTangent),
                TvgMath.PointAdd(next.value, frame.inTangent),
                next.value);
            t = bz.AtApprox(t * frame.length, frame.length);
            return bz.Angle(t >= 1.0f ? 0.99f : (t <= 0.0f ? 0.01f : t));
        }

        public static void PrepareVectorPoint(LottieVectorFrame<Point> frame, LottieVectorFrame<Point> next)
        {
            var bz = new Bezier(
                frame.value,
                TvgMath.PointAdd(frame.value, frame.outTangent),
                TvgMath.PointAdd(next.value, frame.inTangent),
                next.value);
            frame.length = bz.LengthApprox();
        }
    }

    // Expression support type
    public class LottieExpression
    {
        public class Writable
        {
            public string? var_;
            public float val;
        }

        public string? code;
        public LottieComposition? comp;
        public LottieLayer? layer;
        public LottieObject? obj;
        public LottieProperty? property;
        public List<Writable> writables = new();
        public bool disabled;

        public LottieExpression() { }

        public LottieExpression(LottieExpression rhs)
        {
            code = rhs.code;
            comp = rhs.comp;
            layer = rhs.layer;
            obj = rhs.obj;
            property = rhs.property;
            disabled = rhs.disabled;
        }

        public bool Assign(string var_, float val)
        {
            foreach (var w in writables)
            {
                if (TvgStr.Equal(var_, w.var_))
                {
                    w.val = val;
                    return true;
                }
            }
            writables.Add(new Writable { var_ = var_, val = val });
            return true;
        }
    }

    // Property base
    public abstract class LottieProperty
    {
        public enum PropertyType : byte
        {
            Invalid = 0, Integer, Float, Scalar, Vector, PathSet, Color, Opacity, ColorStop, TextDoc, Image
        }

        public enum Loop : byte
        {
            None = 0, InCycle = 1, InPingPong, InOffset, InContinue, OutCycle, OutPingPong, OutOffset, OutContinue
        }

        public LottieExpression? exp;
        public PropertyType type;
        public byte ix;
        public ulong sid;

        protected LottieProperty(PropertyType type = PropertyType.Invalid) { this.type = type; }

        public abstract uint FrameCnt();
        public abstract uint Nearest(float frameNo);
        public abstract float FrameNo(int key);
        public abstract float DoLoop(float frameNo, uint key, Loop mode, float inout);

        public bool Copy(LottieProperty rhs, bool shallow)
        {
            type = rhs.type;
            ix = rhs.ix;
            sid = rhs.sid;
            if (rhs.exp == null) return false;
            if (shallow) { exp = rhs.exp; rhs.exp = null; }
            else { exp = new LottieExpression(rhs.exp); }
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
                if (frameNo < TAccessor.Get(frames[mid])) high = mid - 1;
                else low = mid + 1;
            }
            if (high < low) low = high;
            if (low < 0) low = 0;
            return (uint)low;
        }

        public static uint NearestFrame<TFrame, TAccessor>(List<TFrame>? frames, float frameNo)
            where TAccessor : IFrameNo<TFrame>
        {
            if (frames != null && frames.Count > 0)
            {
                var key = BSearch<TFrame, TAccessor>(frames, frameNo);
                if (key == (uint)(frames.Count - 1)) return key;
                return (MathF.Abs(TAccessor.Get(frames[(int)key]) - frameNo) < MathF.Abs(TAccessor.Get(frames[(int)key + 1]) - frameNo)) ? key : (key + 1);
            }
            return 0;
        }

        public static float GetFrameNo<TFrame, TAccessor>(List<TFrame>? frames, int key)
            where TAccessor : IFrameNo<TFrame>
        {
            if (frames == null || frames.Count == 0) return 0.0f;
            if (key < 0) key = 0;
            if (key >= frames.Count) key = frames.Count - 1;
            return TAccessor.Get(frames[key]);
        }

        public static float DoLoop<TFrame, TAccessor>(List<TFrame>? frames, float frameNo, uint key, LottieProperty.Loop mode, float inout)
            where TAccessor : IFrameNo<TFrame>
        {
            if (frames == null || frames.Count == 0) return frameNo;
            if (mode == LottieProperty.Loop.None) return frameNo;
            frameNo -= TAccessor.Get(frames[0]);
            float range;
            switch (mode)
            {
                case LottieProperty.Loop.InCycle:
                    range = inout - TAccessor.Get(frames[0]);
                    if (range <= 0) return frameNo;
                    return Fmod(frameNo, range) + TAccessor.Get(frames[(int)key]);
                case LottieProperty.Loop.InPingPong:
                    range = inout - TAccessor.Get(frames[(int)key]);
                    if (range <= 0) return frameNo;
                    var forward = ((int)(frameNo / range) % 2) == 0;
                    var f = Fmod(frameNo, range);
                    return (forward ? f : (range - f)) + TAccessor.Get(frames[(int)key]);
                case LottieProperty.Loop.OutCycle:
                    range = TAccessor.Get(frames[frames.Count - 1 - (int)key]) - TAccessor.Get(frames[0]);
                    if (range <= 0) return frameNo;
                    return Fmod(frameNo, range) + TAccessor.Get(frames[0]);
                case LottieProperty.Loop.OutPingPong:
                    range = TAccessor.Get(frames[frames.Count - 1 - (int)key]) - TAccessor.Get(frames[0]);
                    if (range <= 0) return frameNo;
                    var forward2 = ((int)(frameNo / range) % 2) == 0;
                    var f2 = Fmod(frameNo, range);
                    return (forward2 ? f2 : (range - f2)) + TAccessor.Get(frames[0]);
            }
            return frameNo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Fmod(float a, float b)
        {
            if (b == 0) return 0;
            return a - b * MathF.Truncate(a / b);
        }
    }

    // Concrete property: LottieFloat
    public class LottieFloat : LottieProperty
    {
        public List<LottieScalarFrame<float>>? frames;
        public float value;
        private bool _nextReady;

        public LottieFloat(float v = 0.0f) : base(PropertyType.Float) { value = v; }

        public void Release() { frames = null; exp = null; }
        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieScalarFrame<float>, ScalarFrameNo<float>>(frames, frameNo);
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieScalarFrame<float>, ScalarFrameNo<float>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) => LottiePropertyHelper.DoLoop<LottieScalarFrame<float>, ScalarFrameNo<float>>(frames, frameNo, key, mode, inout);

        public LottieScalarFrame<float> NewFrame()
        {
            frames ??= new List<LottieScalarFrame<float>>();
            if (_nextReady) { _nextReady = false; return frames[^1]; }
            var f = new LottieScalarFrame<float>();
            frames.Add(f);
            return f;
        }

        public LottieScalarFrame<float> NextFrame()
        {
            frames ??= new List<LottieScalarFrame<float>>();
            var f = new LottieScalarFrame<float>();
            frames.Add(f);
            _nextReady = true;
            return f;
        }

        public float Evaluate(float frameNo, LottieExpressions? exps = null)
        {
            // Expression override (mirrors C++ operator())
            if (exps != null && exp != null)
            {
                if (exps.ResultFloat(this, frameNo, out var outVal, exp)) return outVal;
            }
            if (frames == null || frames.Count == 0) return value;
            if (frames.Count == 1 || frameNo <= frames[0].no) return frames[0].value;
            if (frameNo >= frames[^1].no) return frames[^1].value;
            var key = (int)LottiePropertyHelper.BSearch<LottieScalarFrame<float>, ScalarFrameNo<float>>(frames, frameNo);
            if (TvgMath.Equal(frames[key].no, frameNo)) return frames[key].value;
            return ScalarFrameHelper.InterpolateFloat(frames[key], frames[key + 1], frameNo);
        }

        public float Evaluate(float frameNo, Tween tween, LottieExpressions? exps)
        {
            if (!tween.active || frames == null || frames.Count <= 1) return Evaluate(frameNo, exps);
            return TvgMath.Lerp(Evaluate(frameNo, exps), Evaluate(tween.frameNo, exps), tween.progress);
        }

        public void CopyFrom(LottieFloat rhs, bool shallow = true)
        {
            if (Copy(rhs, shallow)) return;
            if (rhs.frames != null) { if (shallow) { frames = rhs.frames; rhs.frames = null; } else { frames = new List<LottieScalarFrame<float>>(rhs.frames); } }
            else { frames = null; value = rhs.value; }
        }

        public void Prepare() { }
    }

    // Concrete property: LottieInteger
    public class LottieInteger : LottieProperty
    {
        public List<LottieScalarFrame<sbyte>>? frames;
        public sbyte value;
        private bool _nextReady;

        public LottieInteger(sbyte v = 0) : base(PropertyType.Integer) { value = v; }

        public void Release() { frames = null; exp = null; }
        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieScalarFrame<sbyte>, ScalarFrameNo<sbyte>>(frames, frameNo);
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieScalarFrame<sbyte>, ScalarFrameNo<sbyte>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) => LottiePropertyHelper.DoLoop<LottieScalarFrame<sbyte>, ScalarFrameNo<sbyte>>(frames, frameNo, key, mode, inout);

        public LottieScalarFrame<sbyte> NewFrame()
        {
            frames ??= new List<LottieScalarFrame<sbyte>>();
            if (_nextReady) { _nextReady = false; return frames[^1]; }
            var f = new LottieScalarFrame<sbyte>();
            frames.Add(f);
            return f;
        }

        public LottieScalarFrame<sbyte> NextFrame()
        {
            frames ??= new List<LottieScalarFrame<sbyte>>();
            var f = new LottieScalarFrame<sbyte>();
            frames.Add(f);
            _nextReady = true;
            return f;
        }

        public sbyte Evaluate(float frameNo, LottieExpressions? exps = null)
        {
            // Expression override (mirrors C++ operator())
            if (exps != null && exp != null)
            {
                if (exps.ResultInteger(this, frameNo, out var outVal, exp)) return (sbyte)outVal;
            }
            if (frames == null || frames.Count == 0) return value;
            if (frames.Count == 1 || frameNo <= frames[0].no) return frames[0].value;
            if (frameNo >= frames[^1].no) return frames[^1].value;
            var key = (int)LottiePropertyHelper.BSearch<LottieScalarFrame<sbyte>, ScalarFrameNo<sbyte>>(frames, frameNo);
            if (TvgMath.Equal(frames[key].no, frameNo)) return frames[key].value;
            return ScalarFrameHelper.InterpolateSByte(frames[key], frames[key + 1], frameNo);
        }

        public sbyte Evaluate(float frameNo, Tween tween, LottieExpressions? exps)
        {
            if (!tween.active || frames == null || frames.Count <= 1) return Evaluate(frameNo, exps);
            var a = Evaluate(frameNo, exps);
            var b = Evaluate(tween.frameNo, exps);
            return (sbyte)TvgMath.Clamp((int)(a + (b - a) * tween.progress), sbyte.MinValue, sbyte.MaxValue);
        }

        public void CopyFrom(LottieInteger rhs, bool shallow = true)
        {
            if (Copy(rhs, shallow)) return;
            if (rhs.frames != null) { if (shallow) { frames = rhs.frames; rhs.frames = null; } else { frames = new List<LottieScalarFrame<sbyte>>(rhs.frames); } }
            else { frames = null; value = rhs.value; }
        }

        public void Prepare() { }
    }

    // Concrete property: LottieScalar
    public class LottieScalar : LottieProperty
    {
        public List<LottieScalarFrame<Point>>? frames;
        public Point value;
        private bool _nextReady;

        public LottieScalar(Point v = default) : base(PropertyType.Scalar) { value = v; }

        public void Release() { frames = null; exp = null; }
        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieScalarFrame<Point>, ScalarFrameNo<Point>>(frames, frameNo);
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieScalarFrame<Point>, ScalarFrameNo<Point>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) => LottiePropertyHelper.DoLoop<LottieScalarFrame<Point>, ScalarFrameNo<Point>>(frames, frameNo, key, mode, inout);

        public LottieScalarFrame<Point> NewFrame()
        {
            frames ??= new List<LottieScalarFrame<Point>>();
            if (_nextReady) { _nextReady = false; return frames[^1]; }
            var f = new LottieScalarFrame<Point>();
            frames.Add(f);
            return f;
        }

        public LottieScalarFrame<Point> NextFrame()
        {
            frames ??= new List<LottieScalarFrame<Point>>();
            var f = new LottieScalarFrame<Point>();
            frames.Add(f);
            _nextReady = true;
            return f;
        }

        public Point Evaluate(float frameNo, LottieExpressions? exps = null)
        {
            // Expression override (mirrors C++ operator())
            if (exps != null && exp != null)
            {
                if (exps.ResultScalar(this, frameNo, out var outVal, exp)) return outVal;
            }
            if (frames == null || frames.Count == 0) return value;
            if (frames.Count == 1 || frameNo <= frames[0].no) return frames[0].value;
            if (frameNo >= frames[^1].no) return frames[^1].value;
            var key = (int)LottiePropertyHelper.BSearch<LottieScalarFrame<Point>, ScalarFrameNo<Point>>(frames, frameNo);
            if (TvgMath.Equal(frames[key].no, frameNo)) return frames[key].value;
            return ScalarFrameHelper.InterpolatePoint(frames[key], frames[key + 1], frameNo);
        }

        public Point Evaluate(float frameNo, Tween tween, LottieExpressions? exps)
        {
            if (!tween.active || frames == null || frames.Count <= 1) return Evaluate(frameNo, exps);
            var a = Evaluate(frameNo, exps);
            var b = Evaluate(tween.frameNo, exps);
            return new Point(a.x + (b.x - a.x) * tween.progress, a.y + (b.y - a.y) * tween.progress);
        }

        public void CopyFrom(LottieScalar rhs, bool shallow = true)
        {
            if (Copy(rhs, shallow)) return;
            if (rhs.frames != null) { if (shallow) { frames = rhs.frames; rhs.frames = null; } else { frames = new List<LottieScalarFrame<Point>>(rhs.frames); } }
            else { frames = null; value = rhs.value; }
        }

        public void Prepare() { }
    }

    // Concrete property: LottieVector
    public class LottieVector : LottieProperty
    {
        public List<LottieVectorFrame<Point>>? frames;
        public Point value;
        private bool _nextReady;

        public LottieVector(Point v = default) : base(PropertyType.Vector) { value = v; }

        public void Release() { frames = null; exp = null; }
        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieVectorFrame<Point>, VectorFrameNo<Point>>(frames, frameNo);
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieVectorFrame<Point>, VectorFrameNo<Point>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) => LottiePropertyHelper.DoLoop<LottieVectorFrame<Point>, VectorFrameNo<Point>>(frames, frameNo, key, mode, inout);

        public LottieVectorFrame<Point> NewFrame()
        {
            frames ??= new List<LottieVectorFrame<Point>>();
            if (_nextReady) { _nextReady = false; return frames[^1]; }
            var f = new LottieVectorFrame<Point>();
            frames.Add(f);
            return f;
        }

        public LottieVectorFrame<Point> NextFrame()
        {
            frames ??= new List<LottieVectorFrame<Point>>();
            var f = new LottieVectorFrame<Point>();
            frames.Add(f);
            _nextReady = true;
            return f;
        }

        public Point Evaluate(float frameNo, LottieExpressions? exps = null)
        {
            // Expression override (mirrors C++ operator())
            if (exps != null && exp != null)
            {
                if (exps.ResultVector(this, frameNo, out var outVal, exp)) return outVal;
            }
            if (frames == null || frames.Count == 0) return value;
            if (frames.Count == 1 || frameNo <= frames[0].no) return frames[0].value;
            if (frameNo >= frames[^1].no) return frames[^1].value;
            var key = (int)LottiePropertyHelper.BSearch<LottieVectorFrame<Point>, VectorFrameNo<Point>>(frames, frameNo);
            if (TvgMath.Equal(frames[key].no, frameNo)) return frames[key].value;
            return ScalarFrameHelper.InterpolateVectorPoint(frames[key], frames[key + 1], frameNo);
        }

        public Point Evaluate(float frameNo, Tween tween, LottieExpressions? exps)
        {
            if (!tween.active || frames == null || frames.Count <= 1) return Evaluate(frameNo, exps);
            var a = Evaluate(frameNo, exps);
            var b = Evaluate(tween.frameNo, exps);
            return new Point(a.x + (b.x - a.x) * tween.progress, a.y + (b.y - a.y) * tween.progress);
        }

        public float GetAngle(float frameNo)
        {
            if (frames == null || frames.Count <= 1) return 0;
            if (frameNo <= frames[0].no) return ScalarFrameHelper.VectorPointAngle(frames[0], frames[1], frames[0].no);
            if (frameNo >= frames[^1].no) return ScalarFrameHelper.VectorPointAngle(frames[^2], frames[^1], frames[^1].no);
            var key = (int)LottiePropertyHelper.BSearch<LottieVectorFrame<Point>, VectorFrameNo<Point>>(frames, frameNo);
            return ScalarFrameHelper.VectorPointAngle(frames[key], frames[key + 1], frameNo);
        }

        public float GetAngle(float frameNo, Tween tween)
        {
            if (!tween.active || frames == null || frames.Count <= 1) return GetAngle(frameNo);
            return TvgMath.Lerp(GetAngle(frameNo), GetAngle(tween.frameNo), tween.progress);
        }

        public void CopyFrom(LottieVector rhs, bool shallow = true)
        {
            if (Copy(rhs, shallow)) return;
            if (rhs.frames != null) { if (shallow) { frames = rhs.frames; rhs.frames = null; } else { frames = new List<LottieVectorFrame<Point>>(rhs.frames); } }
            else { frames = null; value = rhs.value; }
        }

        public void Prepare()
        {
            if (frames == null || frames.Count < 2) return;
            for (int i = 0; i < frames.Count - 1; ++i)
            {
                if (frames[i].hasTangent)
                    ScalarFrameHelper.PrepareVectorPoint(frames[i], frames[i + 1]);
            }
        }
    }

    // Concrete property: LottieColor
    public class LottieColor : LottieProperty
    {
        public List<LottieScalarFrame<RGB32>>? frames;
        public RGB32 value;
        private bool _nextReady;

        public LottieColor(RGB32 v = default) : base(PropertyType.Color) { value = v; }

        public void Release() { frames = null; exp = null; }
        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieScalarFrame<RGB32>, ScalarFrameNo<RGB32>>(frames, frameNo);
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieScalarFrame<RGB32>, ScalarFrameNo<RGB32>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) => LottiePropertyHelper.DoLoop<LottieScalarFrame<RGB32>, ScalarFrameNo<RGB32>>(frames, frameNo, key, mode, inout);

        public LottieScalarFrame<RGB32> NewFrame()
        {
            frames ??= new List<LottieScalarFrame<RGB32>>();
            if (_nextReady) { _nextReady = false; return frames[^1]; }
            var f = new LottieScalarFrame<RGB32>();
            frames.Add(f);
            return f;
        }

        public LottieScalarFrame<RGB32> NextFrame()
        {
            frames ??= new List<LottieScalarFrame<RGB32>>();
            var f = new LottieScalarFrame<RGB32>();
            frames.Add(f);
            _nextReady = true;
            return f;
        }

        public RGB32 Evaluate(float frameNo, LottieExpressions? exps = null)
        {
            // Expression override (mirrors C++ operator())
            if (exps != null && exp != null)
            {
                if (exps.ResultColor(this, frameNo, out var outVal, exp)) return outVal;
            }
            if (frames == null || frames.Count == 0) return value;
            if (frames.Count == 1 || frameNo <= frames[0].no) return frames[0].value;
            if (frameNo >= frames[^1].no) return frames[^1].value;
            var key = (int)LottiePropertyHelper.BSearch<LottieScalarFrame<RGB32>, ScalarFrameNo<RGB32>>(frames, frameNo);
            if (TvgMath.Equal(frames[key].no, frameNo)) return frames[key].value;
            return ScalarFrameHelper.InterpolateRGB32(frames[key], frames[key + 1], frameNo);
        }

        public RGB32 Evaluate(float frameNo, Tween tween, LottieExpressions? exps)
        {
            if (!tween.active || frames == null || frames.Count <= 1) return Evaluate(frameNo, exps);
            return RGB32.Lerp(Evaluate(frameNo, exps), Evaluate(tween.frameNo, exps), tween.progress);
        }

        public void CopyFrom(LottieColor rhs, bool shallow = true)
        {
            if (Copy(rhs, shallow)) return;
            if (rhs.frames != null) { if (shallow) { frames = rhs.frames; rhs.frames = null; } else { frames = new List<LottieScalarFrame<RGB32>>(rhs.frames); } }
            else { frames = null; value = rhs.value; }
        }

        public void Prepare() { }
    }

    // Concrete property: LottieOpacity
    public class LottieOpacity : LottieProperty
    {
        public List<LottieScalarFrame<byte>>? frames;
        public byte value;
        private bool _nextReady;

        public LottieOpacity(byte v = 255) : base(PropertyType.Opacity) { value = v; }

        public void Release() { frames = null; exp = null; }
        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieScalarFrame<byte>, ScalarFrameNo<byte>>(frames, frameNo);
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieScalarFrame<byte>, ScalarFrameNo<byte>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) => LottiePropertyHelper.DoLoop<LottieScalarFrame<byte>, ScalarFrameNo<byte>>(frames, frameNo, key, mode, inout);

        public LottieScalarFrame<byte> NewFrame()
        {
            frames ??= new List<LottieScalarFrame<byte>>();
            if (_nextReady) { _nextReady = false; return frames[^1]; }
            var f = new LottieScalarFrame<byte>();
            frames.Add(f);
            return f;
        }

        public LottieScalarFrame<byte> NextFrame()
        {
            frames ??= new List<LottieScalarFrame<byte>>();
            var f = new LottieScalarFrame<byte>();
            frames.Add(f);
            _nextReady = true;
            return f;
        }

        public byte Evaluate(float frameNo, LottieExpressions? exps = null)
        {
            // Expression override (mirrors C++ operator())
            if (exps != null && exp != null)
            {
                if (exps.ResultOpacity(this, frameNo, out var outVal, exp)) return outVal;
            }
            if (frames == null || frames.Count == 0) return value;
            if (frames.Count == 1 || frameNo <= frames[0].no) return frames[0].value;
            if (frameNo >= frames[^1].no) return frames[^1].value;
            var key = (int)LottiePropertyHelper.BSearch<LottieScalarFrame<byte>, ScalarFrameNo<byte>>(frames, frameNo);
            if (TvgMath.Equal(frames[key].no, frameNo)) return frames[key].value;
            return ScalarFrameHelper.InterpolateByte(frames[key], frames[key + 1], frameNo);
        }

        public byte Evaluate(float frameNo, Tween tween, LottieExpressions? exps)
        {
            if (!tween.active || frames == null || frames.Count <= 1) return Evaluate(frameNo, exps);
            var a = Evaluate(frameNo, exps);
            var b = Evaluate(tween.frameNo, exps);
            return (byte)TvgMath.Clamp((int)(a + (b - a) * tween.progress), 0, 255);
        }

        public void CopyFrom(LottieOpacity rhs, bool shallow = true)
        {
            if (Copy(rhs, shallow)) return;
            if (rhs.frames != null) { if (shallow) { frames = rhs.frames; rhs.frames = null; } else { frames = new List<LottieScalarFrame<byte>>(rhs.frames); } }
            else { frames = null; value = rhs.value; }
        }

        public void Prepare() { }
    }

    // LottiePathSet
    public class LottiePathSet : LottieProperty
    {
        public List<LottieScalarFrame<PathSet>>? frames;
        public PathSet value;
        private bool _nextReady;

        public LottiePathSet() : base(PropertyType.PathSet) { }

        public void Release() { exp = null; frames = null; }

        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieScalarFrame<PathSet>, ScalarFrameNo<PathSet>>(frames, frameNo);
        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieScalarFrame<PathSet>, ScalarFrameNo<PathSet>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) =>
            LottiePropertyHelper.DoLoop<LottieScalarFrame<PathSet>, ScalarFrameNo<PathSet>>(frames, frameNo, key, mode, inout);

        public LottieScalarFrame<PathSet> NewFrame()
        {
            frames ??= new List<LottieScalarFrame<PathSet>>();
            if (_nextReady) { _nextReady = false; return frames[^1]; }
            var f = new LottieScalarFrame<PathSet>();
            frames.Add(f);
            return f;
        }

        public LottieScalarFrame<PathSet> NextFrame()
        {
            frames ??= new List<LottieScalarFrame<PathSet>>();
            var f = new LottieScalarFrame<PathSet>();
            frames.Add(f);
            _nextReady = true;
            return f;
        }

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
                var pt = new Point(fv.pts[i].x + (nv.pts[i].x - fv.pts[i].x) * t, fv.pts[i].y + (nv.pts[i].y - fv.pts[i].y) * t);
                if (transform != null) TvgMath.TransformInPlace(ref pt, *transform);
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
                modifier.Path(path.cmds, path.cmdsCnt, path.pts, path.ptsCnt, transform, @out);
                return true;
            }

            var fv = frames![frameIdx].value;
            var nv = frames[frameIdx + 1].value;
            var interpPts = new Point[fv.ptsCnt];
            for (int i = 0; i < fv.ptsCnt; ++i)
            {
                interpPts[i] = new Point(fv.pts[i].x + (nv.pts[i].x - fv.pts[i].x) * t, fv.pts[i].y + (nv.pts[i].y - fv.pts[i].y) * t);
                if (transform != null) TvgMath.TransformInPlace(ref interpPts[i], *transform);
            }
            modifier.Path(fv.cmds, fv.cmdsCnt, interpPts, fv.ptsCnt, null, @out);
            return true;
        }

        public unsafe bool Evaluate(float frameNo, RenderPath @out, Matrix* transform, LottieExpressions? exps, LottieModifier? modifier = null)
        {
            if (exps != null && exp != null)
            {
                if (exps.ResultPathSet(this, frameNo, @out, transform, modifier, exp)) return true;
            }
            if (modifier != null) return ModifiedPath(frameNo, @out, transform, modifier);
            return DefaultPath(frameNo, @out, transform);
        }

        public unsafe bool Evaluate(float frameNo, RenderPath @out, Matrix* transform, Tween tween, LottieExpressions? exps, LottieModifier? modifier = null)
        {
            if (!tween.active || frames == null || frames.Count <= 1) return Evaluate(frameNo, @out, transform, exps, modifier);
            return Tweening(frameNo, @out, transform, modifier, tween, exps);
        }

        private unsafe bool Tweening(float frameNo, RenderPath @out, Matrix* transform, LottieModifier? modifier, Tween tween, LottieExpressions? exps)
        {
            var to = new RenderPath();
            var pivot = @out.pts.count;
            if (!Evaluate(frameNo, @out, transform, exps)) return false;
            if (!Evaluate(tween.frameNo, to, transform, exps)) return false;

            var fromCount = @out.pts.count - pivot;

            for (uint i = 0; i < Math.Min(to.pts.count, fromCount); ++i)
            {
                var fromPt = @out.pts[pivot + i];
                var toPt = to.pts[i];
                @out.pts[pivot + i] = new Point(
                    fromPt.x + (toPt.x - fromPt.x) * tween.progress,
                    fromPt.y + (toPt.y - fromPt.y) * tween.progress);
            }

            if (modifier == null) return true;

            // Apply modifiers
            to.Clear();
            modifier.Path(to.cmds.ToArray(), (int)to.cmds.count, to.pts.ToArray(), (int)to.pts.count, transform, @out);
            return true;
        }

        private bool Dispatch(float frameNo, out PathSet path, out int frameIdx, out float t)
        {
            path = default;
            frameIdx = -1;
            t = 0;
            if (frames == null || frames.Count == 0) { path = value; return true; }
            if (frames.Count == 1 || frameNo <= frames[0].no) { path = frames[0].value; return true; }
            if (frameNo >= frames[^1].no) { path = frames[^1].value; return true; }
            frameIdx = (int)LottiePropertyHelper.BSearch<LottieScalarFrame<PathSet>, ScalarFrameNo<PathSet>>(frames, frameNo);
            var frame = frames[frameIdx];
            if (TvgMath.Equal(frame.no, frameNo)) { path = frame.value; return true; }
            if (frame.value.ptsCnt != frames[frameIdx + 1].value.ptsCnt) { path = frame.value; return true; }
            t = (frameNo - frame.no) / (frames[frameIdx + 1].no - frame.no);
            if (frame.interpolator != null) t = frame.interpolator.Progress(t);
            if (frame.hold) { path = (t < 1.0f) ? frame.value : frames[frameIdx + 1].value; return true; }
            return false;
        }

        private static unsafe void CopyPathSet(PathSet pathset, RenderPath @out, Matrix* transform)
        {
            if (pathset.cmds != null) for (int i = 0; i < pathset.cmdsCnt; ++i) @out.cmds.Push(pathset.cmds[i]);
            if (pathset.pts != null)
            {
                if (transform != null)
                    for (int i = 0; i < pathset.ptsCnt; ++i) { var pt = pathset.pts[i]; TvgMath.TransformInPlace(ref pt, *transform); @out.pts.Push(pt); }
                else
                    for (int i = 0; i < pathset.ptsCnt; ++i) @out.pts.Push(pathset.pts[i]);
            }
        }

        private static void CopyPathSetCmds(PathSet pathset, RenderPath @out)
        {
            if (pathset.cmds != null) for (int i = 0; i < pathset.cmdsCnt; ++i) @out.cmds.Push(pathset.cmds[i]);
        }
    }

    // LottieColorStop
    public class LottieColorStop : LottieProperty
    {
        public List<LottieScalarFrame<ColorStop>>? frames;
        public ColorStop value = new();
        public ushort count;
        public bool populated;

        public LottieColorStop() : base(PropertyType.ColorStop) { }
        public LottieColorStop(LottieColorStop rhs) : base(PropertyType.ColorStop) { CopyFrom(rhs, false); }

        public void Release() { exp = null; value = new ColorStop(); frames = null; }

        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieScalarFrame<ColorStop>, ScalarFrameNo<ColorStop>>(frames, frameNo);
        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieScalarFrame<ColorStop>, ScalarFrameNo<ColorStop>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) =>
            LottiePropertyHelper.DoLoop<LottieScalarFrame<ColorStop>, ScalarFrameNo<ColorStop>>(frames, frameNo, key, mode, inout);

        private bool _nextReady;

        public LottieScalarFrame<ColorStop> NewFrame()
        {
            frames ??= new List<LottieScalarFrame<ColorStop>>();
            if (_nextReady) { _nextReady = false; return frames[^1]; }
            var f = new LottieScalarFrame<ColorStop> { value = new ColorStop() };
            frames.Add(f);
            return f;
        }

        public LottieScalarFrame<ColorStop> NextFrame()
        {
            frames ??= new List<LottieScalarFrame<ColorStop>>();
            var f = new LottieScalarFrame<ColorStop> { value = new ColorStop() };
            frames.Add(f);
            _nextReady = true;
            return f;
        }

        public Result Evaluate(float frameNo, Fill fill, LottieExpressions? exps = null)
        {
            if (exps != null && exp != null) { if (exps.ResultColorStop(this, frameNo, fill, exp)) return Result.Success; }
            if (frames == null || frames.Count == 0) return fill.SetColorStops(value.data, count);
            if (frames.Count == 1 || frameNo <= frames[0].no) return fill.SetColorStops(frames[0].value.data, count);
            if (frameNo >= frames[^1].no) return fill.SetColorStops(frames[^1].value.data, count);
            var key = (int)LottiePropertyHelper.BSearch<LottieScalarFrame<ColorStop>, ScalarFrameNo<ColorStop>>(frames, frameNo);
            if (TvgMath.Equal(frames[key].no, frameNo)) return fill.SetColorStops(frames[key].value.data, count);
            var t = (frameNo - frames[key].no) / (frames[key + 1].no - frames[key].no);
            var interp = frames[key].interpolator;
            if (interp != null) t = interp.Progress(t);
            if (frames[key].hold) { return fill.SetColorStops((t < 1.0f) ? frames[key].value.data : frames[key + 1].value.data, count); }
            var s = frames[key].value.data; var e = frames[key + 1].value.data;
            if (s == null || e == null) return fill.SetColorStops(s, count);
            var result = new Fill.ColorStop[count];
            for (int i = 0; i < count && i < s.Length && i < e.Length; ++i)
                result[i] = new Fill.ColorStop(TvgMath.Lerp(s[i].offset, e[i].offset, t), TvgMath.Lerp(s[i].r, e[i].r, t), TvgMath.Lerp(s[i].g, e[i].g, t), TvgMath.Lerp(s[i].b, e[i].b, t), TvgMath.Lerp(s[i].a, e[i].a, t));
            return fill.SetColorStops(result, count);
        }

        public Result Evaluate(float frameNo, Fill fill, Tween tween, LottieExpressions? exps)
        {
            if (!tween.active || frames == null || frames.Count <= 1) return Evaluate(frameNo, fill, exps);
            return Tweening(frameNo, fill, tween, exps);
        }

        private Result Tweening(float frameNo, Fill fill, Tween tween, LottieExpressions? exps)
        {
            // Step 1: Evaluate at frameNo
            Evaluate(frameNo, fill, exps);

            // Step 2: Get color stops from the fill after first evaluation
            var cnt1 = fill.GetColorStops(out var stops1);
            if (stops1 == null || cnt1 == 0) return Result.Success;

            // Step 3: Evaluate at tween.frameNo into a temporary fill
            var tmpFill = fill is LinearGradient ? (Fill)LinearGradient.Gen() : (Fill)RadialGradient.Gen();
            Evaluate(tween.frameNo, tmpFill, exps);

            var cnt2 = tmpFill.GetColorStops(out var stops2);
            if (stops2 == null || cnt2 == 0) return Result.Success;

            // Step 4: Lerp each color stop by tween.progress
            var cnt = Math.Min(cnt1, cnt2);
            var result = new Fill.ColorStop[cnt];
            for (int i = 0; i < cnt; ++i)
            {
                result[i] = new Fill.ColorStop(
                    TvgMath.Lerp(stops1[i].offset, stops2[i].offset, tween.progress),
                    TvgMath.Lerp(stops1[i].r, stops2[i].r, tween.progress),
                    TvgMath.Lerp(stops1[i].g, stops2[i].g, tween.progress),
                    TvgMath.Lerp(stops1[i].b, stops2[i].b, tween.progress),
                    TvgMath.Lerp(stops1[i].a, stops2[i].a, tween.progress));
            }
            return fill.SetColorStops(result, cnt);
        }

        public void CopyFrom(LottieColorStop rhs, bool shallow = true)
        {
            if (Copy(rhs, shallow)) return;
            if (rhs.frames != null)
            {
                if (shallow) { frames = rhs.frames; rhs.frames = null; }
                else
                {
                    frames = new List<LottieScalarFrame<ColorStop>>();
                    foreach (var f in rhs.frames)
                    {
                        var newFrame = new LottieScalarFrame<ColorStop> { no = f.no, interpolator = f.interpolator, hold = f.hold, value = new ColorStop() };
                        newFrame.value.Copy(f.value, rhs.count);
                        frames.Add(newFrame);
                    }
                }
            }
            else { frames = null; if (shallow) { value = rhs.value; rhs.value = new ColorStop(); } else { value = new ColorStop(); value.Copy(rhs.value, rhs.count); } }
            populated = rhs.populated;
            count = rhs.count;
        }

        public void Prepare() { }
    }

    // LottieTextDoc
    public class LottieTextDoc : LottieProperty
    {
        public List<LottieScalarFrame<TextDocument>>? frames;
        public TextDocument value = new();

        public LottieTextDoc() : base(PropertyType.TextDoc) { }
        public LottieTextDoc(LottieTextDoc rhs) : base(PropertyType.TextDoc) { CopyFrom(rhs, false); }

        public void Release() { exp = null; value = new TextDocument(); frames = null; }

        public override uint Nearest(float frameNo) => LottiePropertyHelper.NearestFrame<LottieScalarFrame<TextDocument>, ScalarFrameNo<TextDocument>>(frames, frameNo);
        public override uint FrameCnt() => frames != null ? (uint)frames.Count : 1;
        public override float FrameNo(int key) => LottiePropertyHelper.GetFrameNo<LottieScalarFrame<TextDocument>, ScalarFrameNo<TextDocument>>(frames, key);
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) =>
            LottiePropertyHelper.DoLoop<LottieScalarFrame<TextDocument>, ScalarFrameNo<TextDocument>>(frames, frameNo, key, mode, inout);

        private bool _nextReady;

        public LottieScalarFrame<TextDocument> NewFrame()
        {
            frames ??= new List<LottieScalarFrame<TextDocument>>();
            if (_nextReady) { _nextReady = false; return frames[^1]; }
            var f = new LottieScalarFrame<TextDocument> { value = new TextDocument() };
            frames.Add(f);
            return f;
        }

        public LottieScalarFrame<TextDocument> NextFrame()
        {
            frames ??= new List<LottieScalarFrame<TextDocument>>();
            var f = new LottieScalarFrame<TextDocument> { value = new TextDocument() };
            frames.Add(f);
            _nextReady = true;
            return f;
        }

        public TextDocument Evaluate(float frameNo)
        {
            if (frames == null || frames.Count == 0) return value;
            if (frames.Count == 1 || frameNo <= frames[0].no) return frames[0].value;
            if (frameNo >= frames[^1].no) return frames[^1].value;
            var key = (int)LottiePropertyHelper.BSearch<LottieScalarFrame<TextDocument>, ScalarFrameNo<TextDocument>>(frames, frameNo);
            return frames[key].value;
        }

        public TextDocument Evaluate(float frameNo, LottieExpressions? exps)
        {
            var @out = Evaluate(frameNo);
            if (exps != null && exp != null) exps.ResultTextDoc(frameNo, @out, exp);
            return @out;
        }

        public void CopyFrom(LottieTextDoc rhs, bool shallow = true)
        {
            if (Copy(rhs, shallow)) return;
            if (rhs.frames != null)
            {
                if (shallow) { frames = rhs.frames; rhs.frames = null; }
                else
                {
                    frames = new List<LottieScalarFrame<TextDocument>>();
                    foreach (var f in rhs.frames)
                    {
                        var newFrame = new LottieScalarFrame<TextDocument> { no = f.no, interpolator = f.interpolator, hold = f.hold, value = new TextDocument() };
                        newFrame.value.Copy(f.value);
                        frames.Add(newFrame);
                    }
                }
            }
            else { frames = null; if (shallow) { value = rhs.value; rhs.value = new TextDocument(); } else { value = new TextDocument(); value.Copy(rhs.value); } }
        }

        public void Prepare() { }
    }

    // LottieBitmap
    public class LottieBitmap : LottieProperty
    {
        public string? data;
        public string? path { get => data; set => data = value; }
        public byte[]? b64Data;   // decoded binary content for embedded images
        public Picture? picture;
        public string? mimeType;
        public uint size;
        public float width;
        public float height;

        public LottieBitmap() : base(PropertyType.Image) { }
        public LottieBitmap(LottieBitmap rhs) : base(PropertyType.Image) { CopyFrom(rhs, false); }

        public void Release() { picture = null; data = null; mimeType = null; }

        public override uint FrameCnt() => 0;
        public override uint Nearest(float frameNo) => 0;
        public override float FrameNo(int key) => 0;
        public override float DoLoop(float frameNo, uint key, Loop mode, float inout) => frameNo;

        public void CopyFrom(LottieBitmap rhs, bool shallow = true)
        {
            if (Copy(rhs, shallow)) return;
            Release();
            if (rhs.picture != null) { picture = rhs.picture; picture.Ref(); }
            width = rhs.width; height = rhs.height;
        }
    }
}
