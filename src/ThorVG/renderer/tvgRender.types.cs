// Ported from ThorVG/src/renderer/tvgRender.h — type definitions
// RenderRegion, RenderSurface, RenderCompositor, RenderPath, RenderTrimPath,
// RenderStroke, RenderShape, RenderEffect hierarchy, and helper functions.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ThorVG
{
    /************************************************************************/
    /* InlineArray types — stack-allocated fixed-size buffers for classes    */
    /************************************************************************/

    [InlineArray(2)]
    public struct Float2 { private float _element0; }

    [InlineArray(4)]
    public struct Float4 { private float _element0; }

    [InlineArray(12)]
    public struct Float12 { private float _element0; }

    [InlineArray(3)]
    public struct Byte3 { private byte _element0; }

    [InlineArray(4)]
    public struct Byte4 { private byte _element0; }

    [Flags]
    public enum RenderUpdateFlag : ushort
    {
        None = 0,
        Path = 1,
        Color = 2,
        Gradient = 4,
        Stroke = 8,
        Transform = 16,
        Image = 32,
        GradientStroke = 64,
        Blend = 128,
        Clip = 256,
        All = 0xffff
    }

    [Flags]
    public enum CompositionFlag : byte
    {
        Invalid = 0,
        Opacity = 1,
        Blending = 2,
        Masking = 4,
        PostProcessing = 8
    }

    public enum ContextFlag : byte
    {
        Default = 0,
        FastTrack = 1
    }

    /// <summary>Render region with min/max corners. Mirrors C++ RenderRegion.</summary>
    public struct RenderRegion
    {
        public struct MinPt { public int x, y; }
        public struct MaxPt { public int x, y; }

        public MinPt min;
        public MaxPt max;

        public RenderRegion(int minX, int minY, int maxX, int maxY)
        {
            min.x = minX; min.y = minY;
            max.x = maxX; max.y = maxY;
        }

        public static RenderRegion Intersect(in RenderRegion lhs, in RenderRegion rhs)
        {
            var ret = new RenderRegion(
                Math.Max(lhs.min.x, rhs.min.x), Math.Max(lhs.min.y, rhs.min.y),
                Math.Min(lhs.max.x, rhs.max.x), Math.Min(lhs.max.y, rhs.max.y));
            if (ret.min.x > ret.max.x) ret.max.x = ret.min.x;
            if (ret.min.y > ret.max.y) ret.max.y = ret.min.y;
            return ret;
        }

        public static RenderRegion Add(in RenderRegion lhs, in RenderRegion rhs)
        {
            return new RenderRegion(
                Math.Min(lhs.min.x, rhs.min.x), Math.Min(lhs.min.y, rhs.min.y),
                Math.Max(lhs.max.x, rhs.max.x), Math.Max(lhs.max.y, rhs.max.y));
        }

        public void IntersectWith(in RenderRegion rhs)
        {
            if (min.x < rhs.min.x) min.x = rhs.min.x;
            if (min.y < rhs.min.y) min.y = rhs.min.y;
            if (max.x > rhs.max.x) max.x = rhs.max.x;
            if (max.y > rhs.max.y) max.y = rhs.max.y;
            if (max.x < min.x) max.x = min.x;
            if (max.y < min.y) max.y = min.y;
        }

        public void AddWith(in RenderRegion rhs)
        {
            if (rhs.min.x < min.x) min.x = rhs.min.x;
            if (rhs.min.y < min.y) min.y = rhs.min.y;
            if (rhs.max.x > max.x) max.x = rhs.max.x;
            if (rhs.max.y > max.y) max.y = rhs.max.y;
        }

        public readonly bool Contained(in RenderRegion rhs)
        {
            return (min.x <= rhs.min.x && max.x >= rhs.max.x && min.y <= rhs.min.y && max.y >= rhs.max.y);
        }

        public readonly bool Intersected(in RenderRegion rhs)
        {
            return (rhs.min.x < max.x && rhs.max.x > min.x && rhs.min.y < max.y && rhs.max.y > min.y);
        }

        public static bool operator ==(in RenderRegion lhs, in RenderRegion rhs)
        {
            return lhs.min.x == rhs.min.x && lhs.min.y == rhs.min.y &&
                   lhs.max.x == rhs.max.x && lhs.max.y == rhs.max.y;
        }

        public static bool operator !=(in RenderRegion lhs, in RenderRegion rhs)
        {
            return !(lhs == rhs);
        }

        public override readonly bool Equals(object? obj) => obj is RenderRegion r && this == r;
        public override readonly int GetHashCode() => HashCode.Combine(min.x, min.y, max.x, max.y);

        public void Reset() { min.x = min.y = max.x = max.y = 0; }
        public readonly bool Valid() => max.x > min.x && max.y > min.y;
        public readonly bool Invalid() => !Valid();

        public readonly int Sx() => min.x;
        public readonly int Sy() => min.y;
        public readonly int Sw() => max.x - min.x;
        public readonly int Sh() => max.y - min.y;
        public readonly uint X() => (uint)Sx();
        public readonly uint Y() => (uint)Sy();
        public readonly uint W() => (uint)Sw();
        public readonly uint H() => (uint)Sh();
    }

    /// <summary>Mirrors C++ RenderSurface. Provides raw pointer access (buf32/buf8) via pinning or native alloc.</summary>
    public unsafe class RenderSurface : IDisposable
    {
        public uint[]? data;
        public Key key = new Key();
        public uint stride;
        public uint w, h;
        public ColorSpace cs = ColorSpace.Unknown;
        public byte channelSize;
        public bool premultiplied;

        // Raw pointer access (mirrors C++ union { pixel_t* data; uint32_t* buf32; uint8_t* buf8; })
        internal GCHandle _pinHandle;
        internal bool nativeOwned;
        public uint* buf32;
        public byte* buf8 { get { return (byte*)buf32; } }

        /// <summary>Pin the managed data array so raw pointers can be used. Call after setting data.</summary>
        public void Pin()
        {
            Unpin();
            if (data != null && data.Length > 0)
            {
                _pinHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
                buf32 = (uint*)_pinHandle.AddrOfPinnedObject();
            }
        }

        /// <summary>Unpin previously pinned data.</summary>
        public void Unpin()
        {
            if (_pinHandle.IsAllocated)
            {
                _pinHandle.Free();
            }
            if (!nativeOwned) buf32 = null;
        }

        /// <summary>Allocate native memory for the surface buffer. Caller must set stride/w/h/cs separately.</summary>
        public void AllocNative(uint stride, uint h)
        {
            FreeNative();
            Unpin();
            var byteSize = (nuint)(stride * h) * (nuint)sizeof(uint);
            buf32 = (uint*)NativeMemory.AllocZeroed(byteSize);
            nativeOwned = true;
            data = null;
        }

        /// <summary>Free natively allocated buffer.</summary>
        public void FreeNative()
        {
            if (nativeOwned && buf32 != null)
            {
                NativeMemory.Free(buf32);
                buf32 = null;
                nativeOwned = false;
            }
        }

        public void Dispose()
        {
            FreeNative();
            Unpin();
            GC.SuppressFinalize(this);
        }

        ~RenderSurface()
        {
            FreeNative();
            Unpin();
        }

        public RenderSurface() { }

        public RenderSurface(RenderSurface rhs)
        {
            data = rhs.data;
            stride = rhs.stride;
            w = rhs.w;
            h = rhs.h;
            cs = rhs.cs;
            channelSize = rhs.channelSize;
            premultiplied = rhs.premultiplied;
            buf32 = rhs.buf32;  // share pointer (not owned)
        }
    }

    /// <summary>Mirrors C++ RenderCompositor.</summary>
    public class RenderCompositor
    {
        public MaskMethod method;
        public byte opacity;
    }

    /// <summary>Mirrors C++ RenderPath.</summary>
    public unsafe class RenderPath
    {
        public Array<PathCommand> cmds;
        public Array<Point> pts;

        public bool Empty() => pts.count == 0;

        public void Clear()
        {
            pts.Clear();
            cmds.Clear();
        }

        public void Close()
        {
            if (cmds.count > 0 && cmds.Last() == PathCommand.Close) return;
            cmds.Push(PathCommand.Close);
        }

        public void MoveTo(Point pt)
        {
            pts.Push(pt);
            cmds.Push(PathCommand.MoveTo);
        }

        public void LineTo(Point pt)
        {
            pts.Push(pt);
            cmds.Push(PathCommand.LineTo);
        }

        public void CubicTo(Point cnt1, Point cnt2, Point end)
        {
            pts.Push(cnt1);
            pts.Push(cnt2);
            pts.Push(end);
            cmds.Push(PathCommand.CubicTo);
        }

        /// <summary>
        /// Get a point along the path at the given progress [0,1].
        /// Mirrors C++ RenderPath::point(float progress).
        /// </summary>
        public unsafe Point PointAt(float progress)
        {
            if (progress <= 0.0f) return pts.First();
            else if (progress >= 1.0f) return pts.Last();

            var pleng = TvgMath.Length(cmds.data, cmds.count, pts.data, pts.count) * progress;
            var cleng = 0.0f;
            var p = pts.data;
            var c = cmds.data;
            Point curr = default, start = default, next;

            while (c < cmds.data + cmds.count)
            {
                switch (*c)
                {
                    case PathCommand.MoveTo:
                    {
                        curr = start = *p++;
                        break;
                    }
                    case PathCommand.LineTo:
                    {
                        next = *p;
                        var segLen = TvgMath.PointLength(curr, next);
                        if (cleng + segLen >= pleng)
                        {
                            var t = (pleng - cleng) / segLen;
                            return new Point(curr.x + (next.x - curr.x) * t, curr.y + (next.y - curr.y) * t);
                        }
                        cleng += segLen;
                        curr = *p++;
                        break;
                    }
                    case PathCommand.CubicTo:
                    {
                        var bz = new Bezier { start = curr, ctrl1 = *p, ctrl2 = *(p + 1), end = *(p + 2) };
                        var segLen = bz.Length();
                        if (cleng + segLen >= pleng) return bz.At((pleng - cleng) / segLen);
                        cleng += segLen;
                        curr = *(p + 2);
                        p += 3;
                        break;
                    }
                    case PathCommand.Close:
                    {
                        var segLen = TvgMath.PointLength(curr, start);
                        if (cleng + segLen >= pleng)
                        {
                            var t = (pleng - cleng) / segLen;
                            return new Point(curr.x + (start.x - curr.x) * t, curr.y + (start.y - curr.y) * t);
                        }
                        cleng += segLen;
                        curr = start;
                        break;
                    }
                }
                ++c;
            }
            return curr;
        }

        /// <summary>
        /// Optimize path in screen space by collapsing zero length lines
        /// and removing unnecessary cubic beziers. Mirrors C++ RenderPath::optimize().
        /// </summary>
        public void Optimize(RenderPath @out, in Matrix matrix, out bool thin)
        {
            const float PX_TOLERANCE = 0.25f;

            thin = false;
            if (Empty()) return;

            @out.cmds.Clear();
            @out.pts.Clear();
            @out.cmds.Reserve(cmds.count);
            @out.pts.Reserve(pts.count);

            var cmdData = cmds.data;
            var cmdCnt = cmds.count;
            var ptData = pts.data;

            Point lastOutT = default;
            Point subpathStartT = default;
            Point thinLineStart = default;
            Point thinLineVec = default;
            var drawableSubpathCnt = 0u;
            var thinLineVecLen = 0.0f;
            var thinCandidate = true;
            var thinLineReady = false;
            var subpathOpen = false;
            var subpathHasSegment = false;

            // Local helper: project point onto line (start, start+vec), update maxDist/minT/maxT
            static void Point2Line(in Point point, in Point start, in Point vec, float vecLen, ref float maxDist, ref float minT, ref float maxT)
            {
                var offset = new Point { x = point.x - start.x, y = point.y - start.y };
                var dist = MathF.Abs(TvgMath.Cross(vec, offset)) / vecLen;
                if (dist > maxDist) maxDist = dist;
                var t = TvgMath.Dot(offset, vec) / (vecLen * vecLen);
                if (t < minT) minT = t;
                if (t > maxT) maxT = t;
            }

            for (uint i = 0; i < cmdCnt; i++)
            {
                switch (cmdData[i])
                {
                    case PathCommand.MoveTo:
                    {
                        // finalizeSubpath
                        if (subpathHasSegment)
                        {
                            ++drawableSubpathCnt;
                            if (drawableSubpathCnt > 1) thinCandidate = false;
                            subpathHasSegment = false;
                        }

                        var ptT = TvgMath.Transform(*ptData, matrix);
                        @out.cmds.Push(PathCommand.MoveTo);
                        @out.pts.Push(ptT);
                        lastOutT = ptT;
                        subpathStartT = ptT;
                        subpathOpen = true;
                        ptData++;
                        break;
                    }
                    case PathCommand.LineTo:
                    {
                        var startT = lastOutT;
                        var ptT = TvgMath.Transform(*ptData, matrix);
                        if (TvgMath.Closed(startT, ptT, PX_TOLERANCE))
                        {
                            ptData++;
                            break;
                        }
                        // addLineCmd
                        @out.cmds.Push(PathCommand.LineTo);
                        @out.pts.Push(ptT);
                        lastOutT = ptT;
                        // collectThinSegment(startT, ptT)
                        subpathHasSegment = true;
                        if (thinCandidate)
                        {
                            if (!thinLineReady)
                            {
                                if (!TvgMath.Closed(startT, ptT, PX_TOLERANCE))
                                {
                                    thinLineStart = startT;
                                    thinLineVec = new Point { x = ptT.x - startT.x, y = ptT.y - startT.y };
                                    thinLineVecLen = MathF.Sqrt(thinLineVec.x * thinLineVec.x + thinLineVec.y * thinLineVec.y);
                                    if (!TvgMath.Zero(thinLineVecLen)) thinLineReady = true;
                                }
                            }
                            else
                            {
                                // checkThinPoint(startT)
                                var dist0 = MathF.Abs(TvgMath.Cross(thinLineVec, new Point { x = startT.x - thinLineStart.x, y = startT.y - thinLineStart.y })) / thinLineVecLen;
                                if (dist0 > PX_TOLERANCE) thinCandidate = false;
                                if (thinCandidate)
                                {
                                    // checkThinPoint(ptT)
                                    var dist1 = MathF.Abs(TvgMath.Cross(thinLineVec, new Point { x = ptT.x - thinLineStart.x, y = ptT.y - thinLineStart.y })) / thinLineVecLen;
                                    if (dist1 > PX_TOLERANCE) thinCandidate = false;
                                }
                            }
                        }
                        ptData++;
                        break;
                    }
                    case PathCommand.CubicTo:
                    {
                        var ctrl1T = TvgMath.Transform(ptData[0], matrix);
                        var ctrl2T = TvgMath.Transform(ptData[1], matrix);
                        var endT = TvgMath.Transform(ptData[2], matrix);
                        var startT3 = lastOutT;

                        if (!TvgMath.Closed(startT3, endT, PX_TOLERANCE))
                        {
                            // validateCubic
                            var vec3 = new Point { x = endT.x - startT3.x, y = endT.y - startT3.y };
                            var vecLen3 = MathF.Sqrt(vec3.x * vec3.x + vec3.y * vec3.y);
                            float maxDist3 = 0.0f;
                            float minT3 = float.MaxValue;
                            float maxT3 = float.MinValue;
                            Point2Line(ctrl1T, startT3, vec3, vecLen3, ref maxDist3, ref minT3, ref maxT3);
                            Point2Line(ctrl2T, startT3, vec3, vecLen3, ref maxDist3, ref minT3, ref maxT3);

                            var flat = maxDist3 <= PX_TOLERANCE;
                            var tEps3 = PX_TOLERANCE / vecLen3;
                            var inSpan = minT3 >= -tEps3 && maxT3 <= 1.0f + tEps3;

                            if (flat && inSpan)
                            {
                                // addLineCmd(startT3, endT)
                                @out.cmds.Push(PathCommand.LineTo);
                                @out.pts.Push(endT);
                                lastOutT = endT;
                                // collectThinSegment(startT3, endT)
                                subpathHasSegment = true;
                                if (thinCandidate)
                                {
                                    if (!thinLineReady)
                                    {
                                        if (!TvgMath.Closed(startT3, endT, PX_TOLERANCE))
                                        {
                                            thinLineStart = startT3;
                                            thinLineVec = new Point { x = endT.x - startT3.x, y = endT.y - startT3.y };
                                            thinLineVecLen = MathF.Sqrt(thinLineVec.x * thinLineVec.x + thinLineVec.y * thinLineVec.y);
                                            if (!TvgMath.Zero(thinLineVecLen)) thinLineReady = true;
                                        }
                                    }
                                    else
                                    {
                                        var dist0 = MathF.Abs(TvgMath.Cross(thinLineVec, new Point { x = startT3.x - thinLineStart.x, y = startT3.y - thinLineStart.y })) / thinLineVecLen;
                                        if (dist0 > PX_TOLERANCE) thinCandidate = false;
                                        if (thinCandidate)
                                        {
                                            var dist1 = MathF.Abs(TvgMath.Cross(thinLineVec, new Point { x = endT.x - thinLineStart.x, y = endT.y - thinLineStart.y })) / thinLineVecLen;
                                            if (dist1 > PX_TOLERANCE) thinCandidate = false;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                @out.cmds.Push(PathCommand.CubicTo);
                                @out.pts.Push(ctrl1T);
                                @out.pts.Push(ctrl2T);
                                @out.pts.Push(endT);
                                lastOutT = endT;
                                subpathHasSegment = true;
                                thinCandidate = false;
                            }
                        }
                        ptData += 3;
                        break;
                    }
                    case PathCommand.Close:
                    {
                        if (subpathOpen && !TvgMath.Closed(lastOutT, subpathStartT, PX_TOLERANCE))
                        {
                            // collectThinSegment(lastOutT, subpathStartT)
                            subpathHasSegment = true;
                            if (thinCandidate)
                            {
                                if (!thinLineReady)
                                {
                                    if (!TvgMath.Closed(lastOutT, subpathStartT, PX_TOLERANCE))
                                    {
                                        thinLineStart = lastOutT;
                                        thinLineVec = new Point { x = subpathStartT.x - lastOutT.x, y = subpathStartT.y - lastOutT.y };
                                        thinLineVecLen = MathF.Sqrt(thinLineVec.x * thinLineVec.x + thinLineVec.y * thinLineVec.y);
                                        if (!TvgMath.Zero(thinLineVecLen)) thinLineReady = true;
                                    }
                                }
                                else
                                {
                                    var dist0 = MathF.Abs(TvgMath.Cross(thinLineVec, new Point { x = lastOutT.x - thinLineStart.x, y = lastOutT.y - thinLineStart.y })) / thinLineVecLen;
                                    if (dist0 > PX_TOLERANCE) thinCandidate = false;
                                    if (thinCandidate)
                                    {
                                        var dist1 = MathF.Abs(TvgMath.Cross(thinLineVec, new Point { x = subpathStartT.x - thinLineStart.x, y = subpathStartT.y - thinLineStart.y })) / thinLineVecLen;
                                        if (dist1 > PX_TOLERANCE) thinCandidate = false;
                                    }
                                }
                            }
                        }
                        @out.cmds.Push(PathCommand.Close);
                        lastOutT = subpathStartT;
                        break;
                    }
                    default: break;
                }
            }
            // finalizeSubpath (final)
            if (subpathHasSegment)
            {
                ++drawableSubpathCnt;
                if (drawableSubpathCnt > 1) thinCandidate = false;
            }
            thin = thinCandidate && thinLineReady && (drawableSubpathCnt == 1);
        }

        public bool Bounds(Matrix* m, ref BBox box)
        {
            if (cmds.Empty() || cmds.First() == PathCommand.CubicTo) return false;

            var pt = pts.Begin();
            var cmd = cmds.Begin();

            while (cmd < cmds.End())
            {
                switch (*cmd)
                {
                    case PathCommand.MoveTo:
                    {
                        if (cmd + 1 < cmds.End())
                        {
                            var next = *(cmd + 1);
                            if (next == PathCommand.LineTo || next == PathCommand.CubicTo)
                            {
                                var p = m != null ? TvgMath.Transform(*pt, *m) : *pt;
                                if (p.x < box.min.x) box.min.x = p.x;
                                if (p.y < box.min.y) box.min.y = p.y;
                                if (p.x > box.max.x) box.max.x = p.x;
                                if (p.y > box.max.y) box.max.y = p.y;
                            }
                        }
                        ++pt;
                        break;
                    }
                    case PathCommand.LineTo:
                    {
                        var p = m != null ? TvgMath.Transform(*pt, *m) : *pt;
                        if (p.x < box.min.x) box.min.x = p.x;
                        if (p.y < box.min.y) box.min.y = p.y;
                        if (p.x > box.max.x) box.max.x = p.x;
                        if (p.y > box.max.y) box.max.y = p.y;
                        ++pt;
                        break;
                    }
                    case PathCommand.CubicTo:
                    {
                        var p0 = m != null ? TvgMath.Transform(pt[-1], *m) : pt[-1];
                        var p1 = m != null ? TvgMath.Transform(pt[0], *m) : pt[0];
                        var p2 = m != null ? TvgMath.Transform(pt[1], *m) : pt[1];
                        var p3 = m != null ? TvgMath.Transform(pt[2], *m) : pt[2];
                        Bezier.Bounds(ref box, p0, p1, p2, p3);
                        pt += 3;
                        break;
                    }
                    default: break;
                }
                ++cmd;
            }
            return true;
        }
    }

    /// <summary>Mirrors C++ RenderTrimPath.</summary>
    public struct RenderTrimPath
    {
        public float begin;
        public float end;
        public bool simultaneous;

        private const float TRIM_EPSILON = 1e-4f;

        public RenderTrimPath()
        {
            begin = 0.0f;
            end = 1.0f;
            simultaneous = true;
        }

        public readonly bool Valid()
        {
            if (begin != 0.0f || end != 1.0f) return true;
            return false;
        }

        /// <summary>
        /// Trim the input path according to begin/end parameters.
        /// Mirrors C++ RenderTrimPath::trim(const RenderPath&amp;, RenderPath&amp;).
        /// </summary>
        public unsafe bool Trim(RenderPath @in, RenderPath @out)
        {
            if (@in.pts.count < 2 || TvgMath.Zero(begin - end)) return false;

            float b = begin, e = end;
            _Get(ref b, ref e);

            @out.cmds.Reserve(@in.cmds.count * 2);
            @out.pts.Reserve(@in.pts.count * 2);

            var pts = @in.pts.data;
            var cmds = @in.cmds.data;

            if (simultaneous)
            {
                var startCmds = cmds;
                var startPts = pts;
                uint i = 0;
                while (i < @in.cmds.count)
                {
                    switch (@in.cmds.data[i])
                    {
                        case PathCommand.MoveTo:
                        {
                            if (startCmds != cmds)
                                _Trim(startCmds, (uint)(cmds - startCmds), startPts, (uint)(pts - startPts), b, e, *(cmds - 1) == PathCommand.Close, @out);
                            startPts = pts;
                            startCmds = cmds;
                            ++pts;
                            ++cmds;
                            break;
                        }
                        case PathCommand.LineTo:
                        {
                            ++pts;
                            ++cmds;
                            break;
                        }
                        case PathCommand.CubicTo:
                        {
                            pts += 3;
                            ++cmds;
                            break;
                        }
                        case PathCommand.Close:
                        {
                            ++cmds;
                            if (startCmds != cmds)
                                _Trim(startCmds, (uint)(cmds - startCmds), startPts, (uint)(pts - startPts), b, e, *(cmds - 1) == PathCommand.Close, @out);
                            startPts = pts;
                            startCmds = cmds;
                            break;
                        }
                    }
                    i++;
                }
                if (startCmds != cmds)
                    _Trim(startCmds, (uint)(cmds - startCmds), startPts, (uint)(pts - startPts), b, e, *(cmds - 1) == PathCommand.Close, @out);
            }
            else
            {
                _Trim(@in.cmds.data, @in.cmds.count, @in.pts.data, @in.pts.count, b, e, false, @out);
            }

            return @out.pts.count >= 2;
        }

        private static void _Get(ref float begin, ref float end)
        {
            var loop = true;

            if (begin > 1.0f && end > 1.0f) loop = false;
            if (begin < 0.0f && end < 0.0f) loop = false;
            if (begin >= 0.0f && begin <= 1.0f && end >= 0.0f && end <= 1.0f) loop = false;

            if (begin > 1.0f) begin -= 1.0f;
            if (begin < 0.0f) begin += 1.0f;
            if (end > 1.0f) end -= 1.0f;
            if (end < 0.0f) end += 1.0f;

            if ((loop && begin < end) || (!loop && begin > end))
            {
                var tmp = begin; begin = end; end = tmp;
            }
        }

        private static unsafe void _Trim(PathCommand* inCmds, uint inCmdsCnt, Point* inPts, uint inPtsCnt, float begin, float end, bool connect, RenderPath @out)
        {
            var totalLength = TvgMath.Length(inCmds, inCmdsCnt, inPts, inPtsCnt);
            var trimStart = begin * totalLength;
            var trimEnd = end * totalLength;

            if (begin >= end)
            {
                _TrimPath(inCmds, inCmdsCnt, inPts, inPtsCnt, trimStart, totalLength, @out, false);
                _TrimPath(inCmds, inCmdsCnt, inPts, inPtsCnt, 0.0f, trimEnd, @out, connect);
            }
            else
            {
                _TrimPath(inCmds, inCmdsCnt, inPts, inPtsCnt, trimStart, trimEnd, @out, false);
            }
        }

        private static unsafe void _TrimPath(PathCommand* inCmds, uint inCmdsCnt, Point* inPts, uint inPtsCnt, float trimStart, float trimEnd, RenderPath @out, bool connect)
        {
            var cmds = inCmds;
            var pts = inPts;
            var moveToTrimmed = *pts;
            var moveTo = *pts;
            var len = 0.0f;
            var start = !connect;

            for (uint i = 0; i < inCmdsCnt; ++i)
            {
                // compute segment length
                float dLen;
                switch (*cmds)
                {
                    case PathCommand.MoveTo: dLen = 0.0f; break;
                    case PathCommand.LineTo: dLen = TvgMath.PointLength(*(pts - 1), *pts); break;
                    case PathCommand.CubicTo: dLen = new Bezier(*(pts - 1), *pts, *(pts + 1), *(pts + 2)).Length(); break;
                    case PathCommand.Close: dLen = TvgMath.PointLength(*(pts - 1), moveTo); break;
                    default: dLen = 0.0f; break;
                }

                if (len <= trimStart)
                {
                    if (len + dLen > trimEnd)
                    {
                        _TrimAt(cmds, pts, ref moveToTrimmed, trimStart - len, trimEnd - trimStart, start, @out);
                        start = false;
                    }
                    else if (len + dLen > trimStart + TRIM_EPSILON)
                    {
                        _TrimAt(cmds, pts, ref moveToTrimmed, trimStart - len, len + dLen - trimStart, start, @out);
                        start = false;
                    }
                }
                else if (len <= trimEnd - TRIM_EPSILON)
                {
                    if (len + dLen > trimEnd)
                    {
                        _TrimAt(cmds, pts, ref moveTo, 0.0f, trimEnd - len, start, @out);
                        start = true;
                    }
                    else if (len + dLen > trimStart + TRIM_EPSILON)
                    {
                        _Add(cmds, pts, moveTo, ref start, @out);
                    }
                }

                len += dLen;

                // shift
                switch (*cmds)
                {
                    case PathCommand.MoveTo:
                        moveTo = *pts;
                        moveToTrimmed = *pts;
                        ++pts;
                        break;
                    case PathCommand.LineTo:
                        ++pts;
                        break;
                    case PathCommand.CubicTo:
                        pts += 3;
                        break;
                    case PathCommand.Close:
                        break;
                }
                ++cmds;
            }
        }

        private static unsafe void _TrimAt(PathCommand* cmds, Point* pts, ref Point moveTo, float at1, float at2, bool start, RenderPath @out)
        {
            switch (*cmds)
            {
                case PathCommand.LineTo:
                {
                    Line tmp, left, right;
                    new Line { pt1 = *(pts - 1), pt2 = *pts }.Split(at1, out left, out tmp);
                    tmp.Split(at2, out left, out right);
                    if (start)
                    {
                        @out.pts.Push(left.pt1);
                        moveTo = left.pt1;
                        @out.cmds.Push(PathCommand.MoveTo);
                    }
                    @out.pts.Push(left.pt2);
                    @out.cmds.Push(PathCommand.LineTo);
                    break;
                }
                case PathCommand.CubicTo:
                {
                    Bezier tmp, left, right;
                    new Bezier { start = *(pts - 1), ctrl1 = *pts, ctrl2 = *(pts + 1), end = *(pts + 2) }.Split(at1, out left, out tmp);
                    tmp.Split(at2, out left, out right);
                    if (start)
                    {
                        moveTo = left.start;
                        @out.pts.Push(left.start);
                        @out.cmds.Push(PathCommand.MoveTo);
                    }
                    @out.pts.Push(left.ctrl1);
                    @out.pts.Push(left.ctrl2);
                    @out.pts.Push(left.end);
                    @out.cmds.Push(PathCommand.CubicTo);
                    break;
                }
                case PathCommand.Close:
                {
                    Line tmp, left, right;
                    new Line { pt1 = *(pts - 1), pt2 = moveTo }.Split(at1, out left, out tmp);
                    tmp.Split(at2, out left, out right);
                    if (start)
                    {
                        moveTo = left.pt1;
                        @out.pts.Push(left.pt1);
                        @out.cmds.Push(PathCommand.MoveTo);
                    }
                    @out.pts.Push(left.pt2);
                    @out.cmds.Push(PathCommand.LineTo);
                    break;
                }
                default: break;
            }
        }

        private static unsafe void _Add(PathCommand* cmds, Point* pts, Point moveTo, ref bool start, RenderPath @out)
        {
            switch (*cmds)
            {
                case PathCommand.MoveTo:
                {
                    @out.cmds.Push(PathCommand.MoveTo);
                    @out.pts.Push(*pts);
                    start = false;
                    break;
                }
                case PathCommand.LineTo:
                {
                    if (start)
                    {
                        @out.cmds.Push(PathCommand.MoveTo);
                        @out.pts.Push(*(pts - 1));
                    }
                    @out.cmds.Push(PathCommand.LineTo);
                    @out.pts.Push(*pts);
                    start = false;
                    break;
                }
                case PathCommand.CubicTo:
                {
                    if (start)
                    {
                        @out.cmds.Push(PathCommand.MoveTo);
                        @out.pts.Push(*(pts - 1));
                    }
                    @out.cmds.Push(PathCommand.CubicTo);
                    @out.pts.Push(*pts);
                    @out.pts.Push(*(pts + 1));
                    @out.pts.Push(*(pts + 2));
                    start = false;
                    break;
                }
                case PathCommand.Close:
                {
                    if (start)
                    {
                        @out.cmds.Push(PathCommand.MoveTo);
                        @out.pts.Push(*(pts - 1));
                    }
                    @out.cmds.Push(PathCommand.LineTo);
                    @out.pts.Push(moveTo);
                    start = true;
                    break;
                }
            }
        }
    }

    /// <summary>Mirrors C++ RenderStroke.</summary>
    public class RenderStroke
    {
        public float width;
        public RGBA color;
        public Fill? fill;
        public float[]? dashPattern;
        public uint dashCount;
        public float dashOffset;
        public float dashLength;
        public float miterlimit = 4.0f;
        public RenderTrimPath trim = new RenderTrimPath();
        public StrokeCap cap = StrokeCap.Square;
        public StrokeJoin join = StrokeJoin.Bevel;
        public bool first;

        public void CopyFrom(RenderStroke rhs)
        {
            width = rhs.width;
            color = rhs.color;
            fill = rhs.fill?.Duplicate();
            if (rhs.dashCount > 0 && rhs.dashPattern != null)
            {
                dashPattern = new float[rhs.dashCount];
                System.Array.Copy(rhs.dashPattern, dashPattern, rhs.dashCount);
            }
            else
            {
                dashPattern = null;
            }
            dashCount = rhs.dashCount;
            dashOffset = rhs.dashOffset;
            dashLength = rhs.dashLength;
            miterlimit = rhs.miterlimit;
            trim = rhs.trim;
            cap = rhs.cap;
            join = rhs.join;
            first = rhs.first;
        }
    }

    /// <summary>Mirrors C++ RenderShape.</summary>
    public class RenderShape
    {
        public RenderPath path = new RenderPath();
        public Fill? fill;
        public RGBA color;
        public RenderStroke? stroke;
        public FillRule rule = FillRule.NonZero;

        public void FillColor(out byte r, out byte g, out byte b, out byte a)
        {
            r = color.r;
            g = color.g;
            b = color.b;
            a = color.a;
        }

        public bool Trimpath()
        {
            return stroke != null && stroke.trim.Valid();
        }

        public bool StrokeFirst()
        {
            return stroke != null && stroke.first;
        }

        public float StrokeWidth()
        {
            return stroke != null ? stroke.width : 0.0f;
        }

        public bool StrokeFill(out byte r, out byte g, out byte b, out byte a)
        {
            if (stroke == null) { r = g = b = a = 0; return false; }
            r = stroke.color.r;
            g = stroke.color.g;
            b = stroke.color.b;
            a = stroke.color.a;
            return true;
        }

        public Fill? StrokeFillGradient()
        {
            return stroke?.fill;
        }

        public StrokeCap StrokeCap()
        {
            return stroke != null ? stroke.cap : ThorVG.StrokeCap.Square;
        }

        public StrokeJoin StrokeJoin()
        {
            return stroke != null ? stroke.join : ThorVG.StrokeJoin.Bevel;
        }

        public float StrokeMiterlimit()
        {
            return stroke != null ? stroke.miterlimit : 4.0f;
        }

        /// <summary>
        /// Generate dashed stroke path. Mirrors C++ RenderShape::strokeDash(RenderPath&, const Matrix*).
        /// </summary>
        public bool StrokeDash(RenderPath @out, in Matrix matrix)
        {
            if (stroke == null || stroke.dashCount == 0 || stroke.dashLength < RenderHelper.DASH_PATTERN_THRESHOLD) return false;

            @out.cmds.Reserve(20 * path.cmds.count);
            @out.pts.Reserve(20 * path.pts.count);

            var allowDot = stroke.cap != ThorVG.StrokeCap.Butt;

            if (Trimpath())
            {
                var tpath = new RenderPath();
                if (stroke.trim.Trim(path, tpath))
                    return StrokeDashGen(tpath, @out, allowDot, matrix);
                else
                    return false;
            }
            return StrokeDashGen(path, @out, allowDot, matrix);
        }

        /// <summary>
        /// Full dashed stroke path generation ported from C++ StrokeDashPath.
        /// Handles lines and cubic bezier segments properly (no flattening).
        /// Mirrors C++ StrokeDashPath struct and its gen()/lineTo()/cubicTo()/segment() methods.
        /// </summary>
        private unsafe bool StrokeDashGen(RenderPath src, RenderPath @out, bool allowDot, in Matrix matrix)
        {
            if (stroke == null || src.Empty()) return false;

            var dashPattern = stroke.dashPattern;
            var dashCnt = (int)stroke.dashCount;
            var dashOffset = stroke.dashOffset;
            var dashLength = stroke.dashLength;
            if (dashPattern == null || dashCnt == 0) return false;

            var state = new StrokeDashState
            {
                dashPattern = dashPattern,
                dashCnt = dashCnt,
                curLen = 0.0f,
                curIdx = 0,
                curPos = default,
                opGap = false,
                move = true,
                transform = matrix,
                applyTransform = !TvgMath.IsIdentity(matrix)
            };

            // --- Offset handling: compute initial dash index and remaining length ---
            int idx = 0;
            var offset = dashOffset;
            var gap = false;
            if (!TvgMath.Zero(dashOffset))
            {
                var length = (dashCnt % 2 != 0) ? dashLength * 2 : dashLength;
                offset = offset % length;
                if (offset < 0) offset += length;

                for (uint i = 0; i < (uint)dashCnt * (uint)(dashCnt % 2 + 1); ++i, ++idx)
                {
                    var curPattern = dashPattern[i % (uint)dashCnt];
                    if (offset < curPattern) break;
                    offset -= curPattern;
                    gap = !gap;
                }
                idx = idx % dashCnt;
            }

            // --- Main path walk ---
            var ptData = src.pts.data;
            Point start = default;

            for (uint ci = 0; ci < src.cmds.count; ci++)
            {
                switch (src.cmds.data[ci])
                {
                    case PathCommand.Close:
                    {
                        DashLineSegment(ref state, @out, start, allowDot);
                        break;
                    }
                    case PathCommand.MoveTo:
                    {
                        // Reset the dash state on each new subpath
                        state.curIdx = idx;
                        state.curLen = dashPattern[idx] - offset;
                        state.opGap = gap;
                        state.move = true;
                        start = state.curPos = *ptData;
                        ptData++;
                        break;
                    }
                    case PathCommand.LineTo:
                    {
                        DashLineSegment(ref state, @out, *ptData, allowDot);
                        ptData++;
                        break;
                    }
                    case PathCommand.CubicTo:
                    {
                        DashCubicSegment(ref state, @out, ptData[0], ptData[1], ptData[2], allowDot);
                        ptData += 3;
                        break;
                    }
                    default: break;
                }
            }
            return true;
        }

        /// <summary>Mutable dash state, equivalent to the C++ StrokeDashPath member variables.</summary>
        private struct StrokeDashState
        {
            public float[] dashPattern;
            public int dashCnt;
            public float curLen;
            public int curIdx;
            public Point curPos;
            public bool opGap;
            public bool move;
            public Matrix transform;
            public bool applyTransform;
        }

        private const float MIN_CURR_LEN_THRESHOLD = 0.1f;

        /// <summary>Apply transform to a point if applyTransform is set. Mirrors C++ StrokeDashPath::map().</summary>
        private static Point Map(ref StrokeDashState s, Point pt)
        {
            return s.applyTransform ? TvgMath.Transform(pt, s.transform) : pt;
        }

        /// <summary>Emit a zero-length dash point (for non-butt caps). Mirrors C++ StrokeDashPath::point().</summary>
        private static void DashPoint(ref StrokeDashState s, RenderPath @out, Point p)
        {
            if (s.move || s.dashPattern[s.curIdx] < MathConstants.FLOAT_EPSILON)
            {
                @out.MoveTo(Map(ref s, p));
                s.move = false;
            }
            @out.LineTo(Map(ref s, p));
        }

        /// <summary>Dash a line segment. Mirrors C++ StrokeDashPath::lineTo().</summary>
        private static void DashLineSegment(ref StrokeDashState s, RenderPath @out, Point to, bool allowDot)
        {
            var line = new Line { pt1 = s.curPos, pt2 = to };
            var diff = new Point(to.x - s.curPos.x, to.y - s.curPos.y);
            var len = TvgMath.PointLength(diff);

            // Inline segment() for Line
            if (TvgMath.Zero(len))
            {
                @out.MoveTo(Map(ref s, s.curPos));
            }
            else if (len <= s.curLen)
            {
                s.curLen -= len;
                if (!s.opGap)
                {
                    if (s.move)
                    {
                        @out.MoveTo(Map(ref s, s.curPos));
                        s.move = false;
                    }
                    @out.LineTo(Map(ref s, line.pt2));
                }
            }
            else
            {
                Line left, right;
                while (len - s.curLen > RenderHelper.DASH_PATTERN_THRESHOLD)
                {
                    if (s.curLen > 0.0f)
                    {
                        line.Split(s.curLen, out left, out right);
                        len -= s.curLen;
                        if (!s.opGap)
                        {
                            if (s.move || s.dashPattern[s.curIdx] - s.curLen < MathConstants.FLOAT_EPSILON)
                            {
                                @out.MoveTo(Map(ref s, left.pt1));
                                s.move = false;
                            }
                            @out.LineTo(Map(ref s, left.pt2));
                        }
                    }
                    else
                    {
                        if (allowDot && !s.opGap) DashPoint(ref s, @out, line.pt1);
                        right = line;
                    }

                    s.curIdx = (s.curIdx + 1) % s.dashCnt;
                    s.curLen = s.dashPattern[s.curIdx];
                    s.opGap = !s.opGap;
                    line = right;
                    s.curPos = line.pt1;
                    s.move = true;
                }
                s.curLen -= len;
                if (!s.opGap)
                {
                    if (s.move)
                    {
                        @out.MoveTo(Map(ref s, line.pt1));
                        s.move = false;
                    }
                    @out.LineTo(Map(ref s, line.pt2));
                }
                if (s.curLen < MIN_CURR_LEN_THRESHOLD)
                {
                    s.curIdx = (s.curIdx + 1) % s.dashCnt;
                    s.curLen = s.dashPattern[s.curIdx];
                    s.opGap = !s.opGap;
                }
            }
            s.curPos = to;
        }

        /// <summary>Dash a cubic bezier segment. Mirrors C++ StrokeDashPath::cubicTo().</summary>
        private static void DashCubicSegment(ref StrokeDashState s, RenderPath @out, Point cnt1, Point cnt2, Point end, bool allowDot)
        {
            var curve = new Bezier { start = s.curPos, ctrl1 = cnt1, ctrl2 = cnt2, end = end };
            var len = curve.Length();

            // Inline segment() for Bezier
            if (TvgMath.Zero(len))
            {
                @out.MoveTo(Map(ref s, s.curPos));
            }
            else if (len <= s.curLen)
            {
                s.curLen -= len;
                if (!s.opGap)
                {
                    if (s.move)
                    {
                        @out.MoveTo(Map(ref s, s.curPos));
                        s.move = false;
                    }
                    @out.CubicTo(Map(ref s, curve.ctrl1), Map(ref s, curve.ctrl2), Map(ref s, curve.end));
                }
            }
            else
            {
                Bezier left, right;
                while (len - s.curLen > RenderHelper.DASH_PATTERN_THRESHOLD)
                {
                    if (s.curLen > 0.0f)
                    {
                        curve.Split(s.curLen, out left, out right);
                        len -= s.curLen;
                        if (!s.opGap)
                        {
                            if (s.move || s.dashPattern[s.curIdx] - s.curLen < MathConstants.FLOAT_EPSILON)
                            {
                                @out.MoveTo(Map(ref s, left.start));
                                s.move = false;
                            }
                            @out.CubicTo(Map(ref s, left.ctrl1), Map(ref s, left.ctrl2), Map(ref s, left.end));
                        }
                    }
                    else
                    {
                        if (allowDot && !s.opGap) DashPoint(ref s, @out, curve.start);
                        right = curve;
                    }

                    s.curIdx = (s.curIdx + 1) % s.dashCnt;
                    s.curLen = s.dashPattern[s.curIdx];
                    s.opGap = !s.opGap;
                    curve = right;
                    s.curPos = curve.start;
                    s.move = true;
                }
                s.curLen -= len;
                if (!s.opGap)
                {
                    if (s.move)
                    {
                        @out.MoveTo(Map(ref s, curve.start));
                        s.move = false;
                    }
                    @out.CubicTo(Map(ref s, curve.ctrl1), Map(ref s, curve.ctrl2), Map(ref s, curve.end));
                }
                if (s.curLen < MIN_CURR_LEN_THRESHOLD)
                {
                    s.curIdx = (s.curIdx + 1) % s.dashCnt;
                    s.curLen = s.dashPattern[s.curIdx];
                    s.opGap = !s.opGap;
                }
            }
            s.curPos = end;
        }
    }

    /// <summary>Base class for render effects. Mirrors C++ RenderEffect.</summary>
    public class RenderEffect
    {
        public object? rd;
        public RenderRegion extend;
        public SceneEffect type;
        public bool valid;
    }

    public class RenderEffectGaussianBlur : RenderEffect
    {
        public float sigma;
        public byte direction;
        public byte border;
        public byte quality;

        public static RenderEffectGaussianBlur Gen(float sigma, int direction, int border, int quality)
        {
            return new RenderEffectGaussianBlur
            {
                sigma = MathF.Max(sigma, 0.0f),
                direction = (byte)TvgMath.Clamp(direction, 0, 2),
                border = (byte)Math.Min(border, 1),
                quality = (byte)Math.Min(quality, 100),
                type = SceneEffect.GaussianBlur
            };
        }
    }

    public class RenderEffectDropShadow : RenderEffect
    {
        public Byte4 color; // rgba
        public float angle;
        public float distance;
        public float sigma;
        public byte quality;

        public static RenderEffectDropShadow Gen(int r, int g, int b, int a,
            float angle, float distance, float sigma, int quality)
        {
            var inst = new RenderEffectDropShadow();
            inst.color[0] = (byte)r;
            inst.color[1] = (byte)g;
            inst.color[2] = (byte)b;
            inst.color[3] = (byte)a;
            inst.angle = angle;
            inst.distance = distance;
            inst.sigma = MathF.Max(sigma, 0.0f);
            inst.quality = (byte)Math.Min(quality, 100);
            inst.type = SceneEffect.DropShadow;
            return inst;
        }
    }

    public class RenderEffectFill : RenderEffect
    {
        public Byte4 color; // rgba

        public static RenderEffectFill Gen(int r, int g, int b, int a)
        {
            var inst = new RenderEffectFill();
            inst.color[0] = (byte)r;
            inst.color[1] = (byte)g;
            inst.color[2] = (byte)b;
            inst.color[3] = (byte)a;
            inst.type = SceneEffect.Fill;
            return inst;
        }
    }

    public class RenderEffectTint : RenderEffect
    {
        public Byte3 black; // rgb
        public Byte3 white; // rgb
        public byte intensity;

        public static RenderEffectTint Gen(int br, int bg, int bb,
            int wr, int wg, int wb, double intensityPct)
        {
            var inst = new RenderEffectTint();
            inst.black[0] = (byte)br; inst.black[1] = (byte)bg; inst.black[2] = (byte)bb;
            inst.white[0] = (byte)wr; inst.white[1] = (byte)wg; inst.white[2] = (byte)wb;
            inst.intensity = (byte)((float)intensityPct * 2.55f);
            inst.type = SceneEffect.Tint;
            return inst;
        }
    }

    public class RenderEffectTritone : RenderEffect
    {
        public Byte3 shadow;
        public Byte3 midtone;
        public Byte3 highlight;
        public byte blender;

        public static RenderEffectTritone Gen(int sr, int sg, int sb,
            int mr, int mg, int mb, int hr, int hg, int hb, int blender)
        {
            var inst = new RenderEffectTritone();
            inst.shadow[0] = (byte)sr; inst.shadow[1] = (byte)sg; inst.shadow[2] = (byte)sb;
            inst.midtone[0] = (byte)mr; inst.midtone[1] = (byte)mg; inst.midtone[2] = (byte)mb;
            inst.highlight[0] = (byte)hr; inst.highlight[1] = (byte)hg; inst.highlight[2] = (byte)hb;
            inst.blender = (byte)blender;
            inst.type = SceneEffect.Tritone;
            return inst;
        }
    }

    /// <summary>Static render helpers. Mirrors C++ free functions in tvgRender.h.</summary>
    public static class RenderHelper
    {
        public const float DASH_PATTERN_THRESHOLD = 0.001f;

        public static bool MaskRegionMerging(MaskMethod method)
        {
            switch (method)
            {
                case MaskMethod.Alpha:
                case MaskMethod.InvAlpha:
                case MaskMethod.Luma:
                case MaskMethod.InvLuma:
                case MaskMethod.Subtract:
                case MaskMethod.Intersect:
                    return false;
                case MaskMethod.Add:
                case MaskMethod.Difference:
                case MaskMethod.Lighten:
                case MaskMethod.Darken:
                    return true;
                default:
                    return false;
            }
        }

        public static byte ChannelSize(ColorSpace cs)
        {
            switch (cs)
            {
                case ColorSpace.ABGR8888:
                case ColorSpace.ABGR8888S:
                case ColorSpace.ARGB8888:
                case ColorSpace.ARGB8888S:
                    return 4;
                case ColorSpace.Grayscale8:
                    return 1;
                default:
                    return 0;
            }
        }

        public static ColorSpace MaskToColorSpace(RenderMethod renderer, MaskMethod method)
        {
            switch (method)
            {
                case MaskMethod.Alpha:
                case MaskMethod.InvAlpha:
                case MaskMethod.Add:
                case MaskMethod.Difference:
                case MaskMethod.Subtract:
                case MaskMethod.Intersect:
                case MaskMethod.Lighten:
                case MaskMethod.Darken:
                    return ColorSpace.Grayscale8;
                case MaskMethod.Luma:
                case MaskMethod.InvLuma:
                    return renderer.ColorSpaceValue();
                default:
                    return ColorSpace.Unknown;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Multiply(byte c, byte a)
        {
            return (byte)((c * a + 0xff) >> 8);
        }
    }
}
