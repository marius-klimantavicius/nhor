// Ported from ThorVG/src/renderer/sw_engine/tvgSwCommon.h

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ThorVG
{
    // Type aliases matching C++ typedefs
    // pixel_t = uint32_t  (just use uint in C#)
    // RenderColor = RGBA  (we add a using-alias below via a struct)
    // Area = long

    /// <summary>RenderColor is an alias for RGBA. Mirrors C++ <c>using RenderColor = tvg::RGBA</c>.</summary>
    public struct RenderColor
    {
        public byte r, g, b, a;

        public RenderColor(byte r, byte g, byte b, byte a)
        {
            this.r = r; this.g = g; this.b = b; this.a = a;
        }
    }

    /// <summary>
    /// Dirty region tracking with spatial partitioning and sweep-line merging.
    /// Mirrors C++ <c>RenderDirtyRegion</c> under THORVG_PARTIAL_RENDER_SUPPORT.
    /// </summary>
    public unsafe class RenderDirtyRegion
    {
        public const int PARTITIONING = 16; // must be N*N

        public bool support = true;
        private bool disabled;
        private Key key = new Key();

        private struct PartitionData
        {
            public RenderRegion region;
            public Array<RenderRegion> list0; // double buffer slot 0
            public Array<RenderRegion> list1; // double buffer slot 1
            public byte current; // 0 or 1
        }

        private PartitionData[] partitions = new PartitionData[PARTITIONING];

        /// <summary>
        /// Returns a ref to the specified list (0 or 1) of the given partition.
        /// All mutations to count/reserved/data go through this ref, avoiding
        /// value-type copy issues with Array&lt;T&gt; (which is a struct).
        /// </summary>
        private ref Array<RenderRegion> ListRef(int partIdx, int listIdx)
        {
            if (listIdx == 0) return ref partitions[partIdx].list0;
            return ref partitions[partIdx].list1;
        }

        private ref Array<RenderRegion> CurrentListRef(int partIdx)
        {
            return ref ListRef(partIdx, partitions[partIdx].current);
        }

        public void Init(uint w, uint h)
        {
            var cnt = (int)Math.Sqrt(PARTITIONING);
            var px = (int)(w / cnt);
            var py = (int)(h / cnt);
            var lx = (int)(w % cnt);
            var ly = (int)(h % cnt);

            for (int y = 0; y < cnt; ++y)
            {
                for (int x = 0; x < cnt; ++x)
                {
                    var pi = y * cnt + x;
                    ListRef(pi, 0).Reserve(64);
                    ref var region = ref partitions[pi].region;
                    region.min.x = x * px;
                    region.min.y = y * py;
                    region.max.x = region.min.x + px;
                    region.max.y = region.min.y + py;
                    if (x == cnt - 1) region.max.x += lx;
                    if (y == cnt - 1) region.max.y += ly;
                }
            }
        }

        public bool Add(in RenderRegion bbox)
        {
            for (int idx = 0; idx < PARTITIONING; ++idx)
            {
                ref var partition = ref partitions[idx];
                if (bbox.max.y <= partition.region.min.y) break;
                if (bbox.Intersected(partition.region))
                {
                    using var lk = new ScopedLock(key);
                    CurrentListRef(idx).Push(RenderRegion.Intersect(bbox, partition.region));
                }
            }
            return true;
        }

        public bool Add(in RenderRegion prv, in RenderRegion cur)
        {
            if (prv == cur) return Add(prv);

            for (int idx = 0; idx < PARTITIONING; ++idx)
            {
                ref var partition = ref partitions[idx];
                if (prv.Intersected(partition.region))
                {
                    using var lk = new ScopedLock(key);
                    CurrentListRef(idx).Push(RenderRegion.Intersect(prv, partition.region));
                }
                if (cur.Intersected(partition.region))
                {
                    using var lk = new ScopedLock(key);
                    CurrentListRef(idx).Push(RenderRegion.Intersect(cur, partition.region));
                }
            }
            return true;
        }

        public void Clear()
        {
            for (int idx = 0; idx < PARTITIONING; ++idx)
            {
                ListRef(idx, 0).Clear();
                ListRef(idx, 1).Clear();
            }
        }

        public bool Deactivate(bool on)
        {
            var prev = disabled;
            disabled = on;
            return prev;
        }

        public bool Deactivated()
        {
            return !support || disabled;
        }

        public RenderRegion Partition(int idx)
        {
            return partitions[idx].region;
        }

        public ref Array<RenderRegion> Get(int idx)
        {
            return ref CurrentListRef(idx);
        }

        private static void Subdivide(ref Array<RenderRegion> targets, int idx, ref RenderRegion lhs, ref RenderRegion rhs)
        {
            Span<RenderRegion> temp = stackalloc RenderRegion[3];
            int cnt = 0;

            // subtract top
            if (rhs.min.y < lhs.min.y)
            {
                temp[cnt++] = new RenderRegion(rhs.min.x, rhs.min.y, rhs.max.x, lhs.min.y);
                rhs.min.y = lhs.min.y;
            }
            // subtract bottom
            if (rhs.max.y > lhs.max.y)
            {
                temp[cnt++] = new RenderRegion(rhs.min.x, lhs.max.y, rhs.max.x, rhs.max.y);
                rhs.max.y = lhs.max.y;
            }
            // subtract right
            if (rhs.max.x > lhs.max.x)
            {
                temp[cnt++] = new RenderRegion(lhs.max.x, rhs.min.y, rhs.max.x, rhs.max.y);
            }

            if (targets.count + cnt - 1 >= targets.reserved) return;

            // Shift data right by (cnt - 1) to make room
            var nmove = (int)(targets.count - idx - 1);
            if (nmove > 0)
            {
                for (int i = nmove - 1; i >= 0; --i)
                    targets[idx + cnt + i] = targets[idx + 1 + i];
            }
            for (int i = 0; i < cnt; ++i)
                targets[idx + i] = temp[i];
            targets.count += (uint)(cnt - 1);

            // sorting by x coord again, only for the updated region
            var endIdx = idx + cnt;
            while (endIdx < (int)targets.count && targets[endIdx].min.x < rhs.max.x) ++endIdx;
            StableSortByX(ref targets, idx, endIdx);
        }

        public void Commit()
        {
            if (disabled) return;

            for (int pidx = 0; pidx < PARTITIONING; ++pidx)
            {
                var current = partitions[pidx].current;
                ref var targets = ref ListRef(pidx, current);
                if (targets.count == 0) continue;

                current = (byte)(current == 0 ? 1 : 0); // swap buffers
                ref var output = ref ListRef(pidx, current);

                targets.Reserve(targets.count * 10);
                output.Reserve(targets.count);

                partitions[pidx].current = current;

                // sort by x coord
                StableSortByX(ref targets, 0, (int)targets.count);

                // Sweep-line algorithm
                for (int i = 0; i < (int)targets.count; ++i)
                {
                    ref var lhs = ref targets[i];
                    if (lhs.Invalid()) continue;
                    var merged = false;

                    for (int j = i + 1; j < (int)targets.count; ++j)
                    {
                        ref var rhs = ref targets[j];
                        if (rhs.Invalid()) continue;
                        if (lhs.max.x < rhs.min.x) break; // line sweeping

                        // fully overlapped, drop lhs
                        if (rhs.Contained(lhs))
                        {
                            merged = true;
                            break;
                        }
                        // fully overlapped, replace lhs with rhs
                        if (lhs.Contained(rhs))
                        {
                            rhs = default;
                            continue;
                        }
                        // merge & expand on x axis
                        if (lhs.min.y == rhs.min.y && lhs.max.y == rhs.max.y)
                        {
                            if (lhs.max.x >= rhs.min.x)
                            {
                                lhs.max.x = rhs.max.x;
                                rhs = default;
                                j = i; // lhs dirty region damaged, retry
                                continue;
                            }
                        }
                        // merge & expand on y axis
                        if (lhs.min.x == rhs.min.x && lhs.max.x == rhs.max.x)
                        {
                            if (lhs.min.y <= rhs.max.y && rhs.min.y <= lhs.max.y)
                            {
                                rhs.min.y = Math.Min(lhs.min.y, rhs.min.y);
                                rhs.max.y = Math.Max(lhs.max.y, rhs.max.y);
                                merged = true;
                                break;
                            }
                        }
                        // subdivide regions
                        if (lhs.Intersected(rhs))
                        {
                            Subdivide(ref targets, j, ref lhs, ref rhs);
                            --j; // rhs dirty region damaged, retry
                        }
                    }
                    if (!merged) output.Push(lhs);
                    lhs = default;
                }
            }
        }

        private static void StableSortByX(ref Array<RenderRegion> arr, int start, int end)
        {
            // Insertion sort — stable and fast for small N (typical dirty region counts)
            for (int i = start + 1; i < end; i++)
            {
                var tmp = arr[i];
                int j = i - 1;
                while (j >= start && arr[j].min.x > tmp.min.x)
                {
                    arr[j + 1] = arr[j];
                    j--;
                }
                arr[j + 1] = tmp;
            }
        }
    }

    // =====================================================================
    //  SW Constants
    // =====================================================================

    public static class SwConstants
    {
        public const byte SW_CURVE_TYPE_POINT = 0;
        public const byte SW_CURVE_TYPE_CUBIC = 1;
        public const long SW_ANGLE_PI = 180L << 16;
        public const long SW_ANGLE_2PI = SW_ANGLE_PI << 1;
        public const long SW_ANGLE_PI2 = SW_ANGLE_PI >> 1;
        public const int SW_COLOR_TABLE = 1024;
        public const uint DEFAULT_POOL_SIZE = 16368;
    }

    // =====================================================================
    //  SwPoint
    // =====================================================================

    public struct SwPoint
    {
        public int x, y;

        public SwPoint(int x, int y) { this.x = x; this.y = y; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SwPoint operator +(SwPoint lhs, SwPoint rhs) => new SwPoint(lhs.x + rhs.x, lhs.y + rhs.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SwPoint operator -(SwPoint lhs, SwPoint rhs) => new SwPoint(lhs.x - rhs.x, lhs.y - rhs.y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SwPoint lhs, SwPoint rhs) => lhs.x == rhs.x && lhs.y == rhs.y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SwPoint lhs, SwPoint rhs) => lhs.x != rhs.x || lhs.y != rhs.y;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Zero() => x == 0 && y == 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Tiny() => (x > -2 && x < 2) && (y > -2 && y < 2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Point ToPoint() => new Point(SwHelper.TO_FLOAT(x), SwHelper.TO_FLOAT(y));

        public override bool Equals(object? obj) => obj is SwPoint p && this == p;
        public override int GetHashCode() => HashCode.Combine(x, y);
    }

    // =====================================================================
    //  SwSize
    // =====================================================================

    public struct SwSize
    {
        public int w, h;
        public SwSize(int w, int h) { this.w = w; this.h = h; }
    }

    // =====================================================================
    //  SwOutline
    // =====================================================================

    public unsafe struct SwOutline
    {
        public Array<SwPoint> pts;
        public Array<uint> cntrs;
        public Array<byte> types;
        public Array<bool> closed;
        public FillRule fillRule;
    }

    // =====================================================================
    //  SwSpan
    // =====================================================================

    public struct SwSpan
    {
        public ushort x, y;
        public ushort len;
        public byte coverage;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Fetch(in RenderRegion bbox, out int ox, out int olen)
        {
            ox = Math.Max((int)x, bbox.min.x);
            olen = Math.Min((int)(x + len), bbox.max.x) - ox;
            return olen > 0;
        }
    }

    // =====================================================================
    //  SwRle
    // =====================================================================

    public unsafe struct SwRle
    {
        public Array<SwSpan> spans;

        public SwSpan* Fetch(in RenderRegion bbox, out SwSpan* end)
        {
            return Fetch(bbox.min.y, (uint)(bbox.max.y - 1), out end);
        }

        public SwSpan* Fetch(int min, uint max, out SwSpan* end)
        {
            SwSpan* begin;

            if (min <= spans.First().y)
            {
                begin = spans.Begin();
            }
            else
            {
                begin = LowerBound(spans.Begin(), spans.End(), min);
            }

            if (max >= spans.Last().y)
            {
                end = spans.End();
            }
            else
            {
                end = UpperBound(spans.Begin(), spans.End(), (int)max);
            }

            return begin;
        }

        public bool Invalid() => spans.Empty();
        public bool Valid() => !Invalid();
        public uint Size() => spans.count;
        public SwSpan* Data() => spans.data;

        // lower_bound: first element where span.y >= min
        private static SwSpan* LowerBound(SwSpan* first, SwSpan* last, int min)
        {
            var count = (int)(last - first);
            while (count > 0)
            {
                var step = count / 2;
                var mid = first + step;
                if (mid->y < min)
                {
                    first = mid + 1;
                    count -= step + 1;
                }
                else
                {
                    count = step;
                }
            }
            return first;
        }

        // upper_bound: first element where span.y > max
        private static SwSpan* UpperBound(SwSpan* first, SwSpan* last, int max)
        {
            var count = (int)(last - first);
            while (count > 0)
            {
                var step = count / 2;
                var mid = first + step;
                if (!(max < mid->y))
                {
                    first = mid + 1;
                    count -= step + 1;
                }
                else
                {
                    count = step;
                }
            }
            return first;
        }
    }

    // =====================================================================
    //  SwCell
    // =====================================================================

    public unsafe struct SwCell
    {
        public int x;
        public int cover;
        public long area; // Area = long
        public SwCell* next;
    }

    // =====================================================================
    //  SwFill
    // =====================================================================

    public unsafe class SwFill
    {
        public struct SwLinear
        {
            public float dx, dy;
            public float offset;
        }

        public struct SwRadial
        {
            public float a11, a12, a13;
            public float a21, a22, a23;
            public float fx, fy, fr;
            public float dx, dy, dr;
            public float invA, a;
        }

        public SwLinear linear;
        public SwRadial radial;
        public unsafe struct ColorTable
        {
            public fixed uint data[SwConstants.SW_COLOR_TABLE];
        }
        public ColorTable ctable;
        public FillSpread spread;
        public bool solid;
        public bool translucent;
    }

    // =====================================================================
    //  SwStrokeBorder
    // =====================================================================

    public unsafe class SwStrokeBorder
    {
        public Array<SwPoint> pts;
        public byte* tags;
        public int start = -1;
        public bool movable;

        ~SwStrokeBorder()
        {
            if (tags != null)
            {
                NativeMemory.Free(tags);
                tags = null;
            }
        }
    }

    // =====================================================================
    //  SwStroke
    // =====================================================================

    public class SwStroke
    {
        public long angleIn;
        public long angleOut;
        public SwPoint center;
        public long lineLength;
        public long subPathAngle;
        public SwPoint ptStartSubPath;
        public long subPathLineLength;
        public long width;
        public long miterlimit;
        public SwFill? fill;
        public SwStrokeBorder[] borders = new SwStrokeBorder[2];
        public float sx, sy;
        public StrokeCap cap;
        public StrokeJoin join;
        public StrokeJoin joinSaved;
        public bool firstPt;
        public bool closedSubPath;
        public bool handleWideStrokes;

        public SwStroke()
        {
            borders[0] = new SwStrokeBorder();
            borders[1] = new SwStrokeBorder();
        }
    }

    // =====================================================================
    //  SwDashStroke
    // =====================================================================

    public class SwDashStroke
    {
        public SwOutline outline;
        public bool hasOutline;
        public float curLen;
        public int curIdx;
        public Point ptStart;
        public Point ptCur;
        public float[]? pattern;
        public uint cnt;
        public bool curOpGap;
        public bool move = true;
    }

    // =====================================================================
    //  SwShape
    // =====================================================================

    public class SwShape
    {
        public SwOutline outline;
        public bool hasOutline;
        public SwStroke? stroke;
        public SwFill? fill;
        public SwRle rle;
        public bool hasRle;
        public SwRle strokeRle;
        public bool hasStrokeRle;
        public SwRle lcdRle;        // [LCD Subpixel] 3x-horizontal-resolution RLE for LCD text rendering
        public bool hasLcdRle;      // [LCD Subpixel]
        public RenderRegion bbox;
        public bool fastTrack;
    }

    // =====================================================================
    //  SwImage
    // =====================================================================

    public unsafe class SwImage
    {
        public SwOutline outline;
        public bool hasOutline;
        public SwRle rle;
        public bool hasRle;
        // Union: data / buf32 / buf8 all point to the same buffer
        public uint* buf32;  // also serves as pixel_t* data
        public byte* buf8 { get { return (byte*)buf32; } }
        public uint w, h, stride;
        public int ox;
        public int oy;
        public float scale;

        public byte channelSize;
        public FilterMethod filter;
        public bool direct;
        public bool scaled;
    }

    // =====================================================================
    //  Delegate types
    // =====================================================================

    public delegate byte SwMask(byte s, byte d, byte a);
    public delegate uint SwBlender(uint s, uint d);
    public delegate uint SwBlenderA(uint s, uint d, byte a);
    public delegate uint SwJoin(byte r, byte g, byte b, byte a);
    public unsafe delegate byte SwAlpha(byte* data);

    // =====================================================================
    //  Struct-based operation interfaces (monomorphized by JIT)
    // =====================================================================

    public interface IMaskOp
    {
        static abstract byte Apply(byte s, byte d, byte a);
    }

    public struct MaskNone : IMaskOp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Apply(byte s, byte d, byte a) => s;
    }

    public struct MaskAdd : IMaskOp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Apply(byte s, byte d, byte a) => (byte)(s + SwHelper.MULTIPLY(d, a));
    }

    public struct MaskSubtract : IMaskOp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Apply(byte s, byte d, byte a) => (byte)SwHelper.MULTIPLY(s, 255 - d);
    }

    public struct MaskIntersect : IMaskOp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Apply(byte s, byte d, byte a) => (byte)SwHelper.MULTIPLY(s, d);
    }

    public struct MaskDifference : IMaskOp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Apply(byte s, byte d, byte a) => (byte)(SwHelper.MULTIPLY(s, 255 - d) + SwHelper.MULTIPLY(d, a));
    }

    public struct MaskLighten : IMaskOp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Apply(byte s, byte d, byte a) => (s > d) ? s : d;
    }

    public struct MaskDarken : IMaskOp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Apply(byte s, byte d, byte a) => (s < d) ? s : d;
    }

    public interface IBlenderAOp
    {
        static abstract uint Apply(uint s, uint d, byte a);
    }

    public struct BlendPreNormal : IBlenderAOp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Apply(uint s, uint d, byte a) => s + SwHelper.ALPHA_BLEND(d, SwHelper.IA(s));
    }

    public struct BlendSrcOver : IBlenderAOp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Apply(uint s, uint d, byte a) => s;
    }

    public struct BlendNormal : IBlenderAOp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Apply(uint s, uint d, byte a)
        {
            var t = SwHelper.ALPHA_BLEND(s, a);
            return t + SwHelper.ALPHA_BLEND(d, SwHelper.IA(t));
        }
    }

    public struct BlendInterp : IBlenderAOp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Apply(uint s, uint d, byte a) => SwHelper.INTERPOLATE(s, d, a);
    }

    public unsafe interface IGradientFill
    {
        static abstract void Fill<TOp>(SwFill fill, uint* dst, uint y, uint x, uint len, byte a) where TOp : struct, IBlenderAOp;
        static abstract void FillMask<TMask>(SwFill fill, byte* dst, uint y, uint x, uint len, byte a) where TMask : struct, IMaskOp;
    }

    public struct LinearGrad : IGradientFill
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Fill<TOp>(SwFill fill, uint* dst, uint y, uint x, uint len, byte a) where TOp : struct, IBlenderAOp
            => SwFillOps.fillLinear<TOp>(fill, dst, y, x, len, a);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void FillMask<TMask>(SwFill fill, byte* dst, uint y, uint x, uint len, byte a) where TMask : struct, IMaskOp
            => SwFillOps.fillLinear<TMask>(fill, dst, y, x, len, a);
    }

    public struct RadialGrad : IGradientFill
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Fill<TOp>(SwFill fill, uint* dst, uint y, uint x, uint len, byte a) where TOp : struct, IBlenderAOp
            => SwFillOps.fillRadial<TOp>(fill, dst, y, x, len, a);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void FillMask<TMask>(SwFill fill, byte* dst, uint y, uint x, uint len, byte a) where TMask : struct, IMaskOp
            => SwFillOps.fillRadial<TMask>(fill, dst, y, x, len, a);
    }

    // =====================================================================
    //  SwSurface
    // =====================================================================

    public class SwSurface : RenderSurface
    {
        public SwJoin? join;
        public SwAlpha?[] alphas = new SwAlpha?[4]; // Alpha:2, InvAlpha:3, Luma:4, InvLuma:5
        public SwBlender? blender;
        public SwCompositor? compositor;
        public BlendMethod blendMethod = BlendMethod.Normal;

        public SwAlpha? Alpha(MaskMethod method)
        {
            var idx = (int)method - 1; // -1 for None
            return alphas[idx > 3 ? 0 : idx];
        }

        public SwSurface() { }

        public SwSurface(SwSurface rhs) : base(rhs)
        {
            join = rhs.join;
            System.Array.Copy(rhs.alphas, alphas, 4);
            blender = rhs.blender;
            compositor = rhs.compositor;
            blendMethod = rhs.blendMethod;
        }
    }

    // =====================================================================
    //  SwCompositor
    // =====================================================================

    public class SwCompositor : RenderCompositor
    {
        public SwSurface? recoverSfc;
        public SwCompositor? recoverCmp;
        public SwImage image = new SwImage();
        public RenderRegion bbox;
        public bool valid;
    }

    // =====================================================================
    //  SwCellPool
    // =====================================================================

    public unsafe class SwCellPool
    {
        public uint size;
        public SwCell* buffer;

        public SwCellPool()
        {
            size = SwConstants.DEFAULT_POOL_SIZE;
            buffer = (SwCell*)NativeMemory.Alloc(size, (nuint)sizeof(SwCell));
        }

        ~SwCellPool()
        {
            if (buffer != null)
            {
                NativeMemory.Free(buffer);
                buffer = null;
            }
        }
    }

    // =====================================================================
    //  SwMpool
    // =====================================================================

    public class SwMpool
    {
        public SwOutline[] outlines;
        public SwStrokeBorder[] lBorders;
        public SwStrokeBorder[] rBorders;
        public SwCellPool[] cellPools;

        public SwMpool(uint threads)
        {
            var allocSize = threads + 1;
            outlines = new SwOutline[allocSize];
            lBorders = new SwStrokeBorder[allocSize];
            rBorders = new SwStrokeBorder[allocSize];
            cellPools = new SwCellPool[allocSize];

            for (uint i = 0; i < allocSize; i++)
            {
                lBorders[i] = new SwStrokeBorder();
                rBorders[i] = new SwStrokeBorder();
                cellPools[i] = new SwCellPool();
            }
        }

        public SwCellPool Cell(uint idx)
        {
            return cellPools[idx];
        }

        public unsafe SwOutline* Outline(uint idx)
        {
            outlines[idx].pts.Clear();
            outlines[idx].cntrs.Clear();
            outlines[idx].types.Clear();
            outlines[idx].closed.Clear();

            fixed (SwOutline* ptr = &outlines[idx])
            {
                return ptr;
            }
        }

        public SwStrokeBorder StrokeLBorder(uint idx)
        {
            lBorders[idx].pts.Clear();
            lBorders[idx].start = -1;
            return lBorders[idx];
        }

        public SwStrokeBorder StrokeRBorder(uint idx)
        {
            rBorders[idx].pts.Clear();
            rBorders[idx].start = -1;
            return rBorders[idx];
        }
    }

    // =====================================================================
    //  Inline helpers — mirrors C++ static inline functions
    // =====================================================================

    public static class SwHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float TO_FLOAT(int val)
        {
            return (float)val / 64.0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TO_SWCOORD(float val)
        {
            return (int)(val * 64.0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint JOIN(byte c0, byte c1, byte c2, byte c3)
        {
            return ((uint)c0 << 24) | ((uint)c1 << 16) | ((uint)c2 << 8) | c3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ALPHA_BLEND(uint c, uint a)
        {
            ++a;
            return (((((c >> 8) & 0x00ff00ffu) * a) & 0xff00ff00u) +
                    ((((c & 0x00ff00ffu) * a) >> 8) & 0x00ff00ffu));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint INTERPOLATE(uint s, uint d, byte a)
        {
            return (((((((s >> 8) & 0xff00ff) - ((d >> 8) & 0xff00ff)) * a) + (d & 0xff00ff00u)) & 0xff00ff00u) +
                    ((((((s & 0xff00ff) - (d & 0xff00ff)) * a) >> 8) + (d & 0xff00ff)) & 0xff00ff));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte INTERPOLATE8(byte s, byte d, byte a)
        {
            return (byte)((((int)s * a + 0xff) >> 8) + (((int)d * (byte)~a + 0xff) >> 8));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HALF_STROKE(float width)
        {
            return TO_SWCOORD(width * 0.5f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte A(uint c) => (byte)(c >> 24);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte IA(uint c) => (byte)(~c >> 24);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte C1(uint c) => (byte)(c >> 16);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte C2(uint c) => (byte)(c >> 8);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte C3(uint c) => (byte)c;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint PREMULTIPLY(uint c, byte a)
        {
            return (c & 0xff000000u) +
                   ((((c >> 8) & 0xffu) * a) & 0xff00u) +
                   ((((c & 0x00ff00ffu) * a) >> 8) & 0x00ff00ffu);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RenderColor BLEND_UPRE(uint c)
        {
            var o = new RenderColor(C1(c), C2(c), C3(c), A(c));
            if (o.a > 0 && o.a < 255)
            {
                o.r = (byte)Math.Min((uint)o.r * 255u / o.a, 255u);
                o.g = (byte)Math.Min((uint)o.g * 255u / o.a, 255u);
                o.b = (byte)Math.Min((uint)o.b * 255u / o.a, 255u);
            }
            return o;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint BLEND_PRE(uint c1, uint c2, byte a)
        {
            if (a == 255) return c1;
            else if (a == 0) return c2;
            return ALPHA_BLEND(c1, a) + ALPHA_BLEND(c2, (uint)(255 - a));
        }

        // MULTIPLY helper (same as RenderHelper.Multiply but for uint math in blend ops)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MULTIPLY(int c, int a)
        {
            return (c * a + 0xff) >> 8;
        }

        /// <summary>
        /// Convert RGB to HSL. Mirrors C++ rasterRGB2HSL.
        /// Placed here (not in SwRaster) because blend ops in SwBlendOps need it.
        /// </summary>
        public static void rasterRGB2HSL(byte r, byte g, byte b, out float h, out float s, out float l)
        {
            var rf = r / 255.0f;
            var gf = g / 255.0f;
            var bf = b / 255.0f;
            var maxVal = MathF.Max(MathF.Max(rf, gf), bf);
            var minVal = MathF.Min(MathF.Min(rf, gf), bf);
            var delta = maxVal - minVal;

            var t = (maxVal + minVal) * 0.5f;
            l = t;

            if (TvgMath.Zero(delta))
            {
                h = 0.0f;
                s = 0.0f;
            }
            else
            {
                s = (t < 0.5f) ? (delta / (maxVal + minVal)) : (delta / (2.0f - maxVal - minVal));

                if (maxVal == rf) h = (gf - bf) / delta + (gf < bf ? 6.0f : 0.0f);
                else if (maxVal == gf) h = (bf - rf) / delta + 2.0f;
                else h = (rf - gf) / delta + 4.0f;
                h *= 60.0f;
            }
        }
    }

    // =====================================================================
    //  Blend operations — mirrors C++ static inline opBlend* functions
    // =====================================================================

    public static class SwBlendOps
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint opBlendInterp(uint s, uint d, byte a)
        {
            return SwHelper.INTERPOLATE(s, d, a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint opBlendNormal(uint s, uint d, byte a)
        {
            var t = SwHelper.ALPHA_BLEND(s, a);
            return t + SwHelper.ALPHA_BLEND(d, SwHelper.IA(t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint opBlendPreNormal(uint s, uint d, byte a)
        {
            return s + SwHelper.ALPHA_BLEND(d, SwHelper.IA(s));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint opBlendSrcOver(uint s, uint d, byte a)
        {
            return s;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint opBlendDifference(uint s, uint d)
        {
            byte F(byte sv, byte dv) => (byte)((sv > dv) ? (sv - dv) : (dv - sv));
            return SwHelper.JOIN(255, F(SwHelper.C1(s), SwHelper.C1(d)),
                                      F(SwHelper.C2(s), SwHelper.C2(d)),
                                      F(SwHelper.C3(s), SwHelper.C3(d)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint opBlendExclusion(uint s, uint d)
        {
            byte F(byte sv, byte dv) => (byte)TvgMath.Clamp(sv + dv - 2 * SwHelper.MULTIPLY(sv, dv), 0, 255);
            return SwHelper.JOIN(255, F(SwHelper.C1(s), SwHelper.C1(d)),
                                      F(SwHelper.C2(s), SwHelper.C2(d)),
                                      F(SwHelper.C3(s), SwHelper.C3(d)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint opBlendAdd(uint s, uint d)
        {
            byte F(byte sv, byte dv) => (byte)Math.Min(sv + dv, 255);
            return SwHelper.JOIN(255, F(SwHelper.C1(s), SwHelper.C1(d)),
                                      F(SwHelper.C2(s), SwHelper.C2(d)),
                                      F(SwHelper.C3(s), SwHelper.C3(d)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint opBlendScreen(uint s, uint d)
        {
            byte F(byte sv, byte dv) => (byte)(sv + dv - SwHelper.MULTIPLY(sv, dv));
            return SwHelper.JOIN(255, F(SwHelper.C1(s), SwHelper.C1(d)),
                                      F(SwHelper.C2(s), SwHelper.C2(d)),
                                      F(SwHelper.C3(s), SwHelper.C3(d)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint opBlendMultiply(uint s, uint d)
        {
            var o = SwHelper.BLEND_UPRE(d);
            byte F(byte sv, byte dv) => (byte)SwHelper.MULTIPLY(sv, dv);
            return SwHelper.BLEND_PRE(SwHelper.JOIN(255, F(SwHelper.C1(s), o.r),
                                                         F(SwHelper.C2(s), o.g),
                                                         F(SwHelper.C3(s), o.b)), s, o.a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint opBlendOverlay(uint s, uint d)
        {
            var o = SwHelper.BLEND_UPRE(d);
            byte F(byte sv, byte dv) => (byte)((dv < 128)
                ? Math.Min(255, 2 * SwHelper.MULTIPLY(sv, dv))
                : (255 - Math.Min(255, 2 * SwHelper.MULTIPLY(255 - sv, 255 - dv))));
            return SwHelper.BLEND_PRE(SwHelper.JOIN(255, F(SwHelper.C1(s), o.r),
                                                         F(SwHelper.C2(s), o.g),
                                                         F(SwHelper.C3(s), o.b)), s, o.a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint opBlendDarken(uint s, uint d)
        {
            var o = SwHelper.BLEND_UPRE(d);
            byte F(byte sv, byte dv) => Math.Min(sv, dv);
            return SwHelper.BLEND_PRE(SwHelper.JOIN(255, F(SwHelper.C1(s), o.r),
                                                         F(SwHelper.C2(s), o.g),
                                                         F(SwHelper.C3(s), o.b)), s, o.a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint opBlendLighten(uint s, uint d)
        {
            byte F(byte sv, byte dv) => Math.Max(sv, dv);
            return SwHelper.JOIN(255, F(SwHelper.C1(s), SwHelper.C1(d)),
                                      F(SwHelper.C2(s), SwHelper.C2(d)),
                                      F(SwHelper.C3(s), SwHelper.C3(d)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint opBlendColorDodge(uint s, uint d)
        {
            var o = SwHelper.BLEND_UPRE(d);
            byte F(byte sv, byte dv) => (byte)(dv == 0 ? 0 : (sv == 255 ? 255 : Math.Min(dv * 255 / (255 - sv), 255)));
            return SwHelper.BLEND_PRE(SwHelper.JOIN(255, F(SwHelper.C1(s), o.r),
                                                         F(SwHelper.C2(s), o.g),
                                                         F(SwHelper.C3(s), o.b)), s, o.a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint opBlendColorBurn(uint s, uint d)
        {
            var o = SwHelper.BLEND_UPRE(d);
            byte F(byte sv, byte dv) => (byte)(dv == 255 ? 255 : (sv == 0 ? 0 : 255 - Math.Min((255 - dv) * 255 / sv, 255)));
            return SwHelper.BLEND_PRE(SwHelper.JOIN(255, F(SwHelper.C1(s), o.r),
                                                         F(SwHelper.C2(s), o.g),
                                                         F(SwHelper.C3(s), o.b)), s, o.a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint opBlendHardLight(uint s, uint d)
        {
            var o = SwHelper.BLEND_UPRE(d);
            byte F(byte sv, byte dv) => (byte)((sv < 128)
                ? Math.Min(255, 2 * SwHelper.MULTIPLY(sv, dv))
                : (255 - Math.Min(255, 2 * SwHelper.MULTIPLY(255 - sv, 255 - dv))));
            return SwHelper.BLEND_PRE(SwHelper.JOIN(255, F(SwHelper.C1(s), o.r),
                                                         F(SwHelper.C2(s), o.g),
                                                         F(SwHelper.C3(s), o.b)), s, o.a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint opBlendSoftLight(uint s, uint d)
        {
            var o = SwHelper.BLEND_UPRE(d);
            byte F(byte sv, byte dv) => (byte)(SwHelper.MULTIPLY(255 - Math.Min(255, 2 * sv),
                                                SwHelper.MULTIPLY(dv, dv)) +
                                                Math.Min(255, 2 * SwHelper.MULTIPLY(sv, dv)));
            return SwHelper.BLEND_PRE(SwHelper.JOIN(255, F(SwHelper.C1(s), o.r),
                                                         F(SwHelper.C2(s), o.g),
                                                         F(SwHelper.C3(s), o.b)), s, o.a);
        }

        public static uint opBlendHue(uint s, uint d)
        {
            var o = SwHelper.BLEND_UPRE(d);
            SwHelper.rasterRGB2HSL(SwHelper.C1(s), SwHelper.C2(s), SwHelper.C3(s), out var sh, out _, out _);
            SwHelper.rasterRGB2HSL(o.r, o.g, o.b, out _, out var ds, out var dl);
            TvgColor.Hsl2Rgb(sh, ds, dl, out var r, out var g, out var b);
            return SwHelper.BLEND_PRE(SwHelper.JOIN(255, r, g, b), s, o.a);
        }

        public static uint opBlendSaturation(uint s, uint d)
        {
            var o = SwHelper.BLEND_UPRE(d);
            SwHelper.rasterRGB2HSL(SwHelper.C1(s), SwHelper.C2(s), SwHelper.C3(s), out _, out var ss, out _);
            SwHelper.rasterRGB2HSL(o.r, o.g, o.b, out var dh, out _, out var dl);
            TvgColor.Hsl2Rgb(dh, ss, dl, out var r, out var g, out var b);
            return SwHelper.BLEND_PRE(SwHelper.JOIN(255, r, g, b), s, o.a);
        }

        public static uint opBlendColor(uint s, uint d)
        {
            var o = SwHelper.BLEND_UPRE(d);
            SwHelper.rasterRGB2HSL(SwHelper.C1(s), SwHelper.C2(s), SwHelper.C3(s), out var sh, out var ss, out _);
            SwHelper.rasterRGB2HSL(o.r, o.g, o.b, out _, out _, out var dl);
            TvgColor.Hsl2Rgb(sh, ss, dl, out var r, out var g, out var b);
            return SwHelper.BLEND_PRE(SwHelper.JOIN(255, r, g, b), s, o.a);
        }

        public static uint opBlendLuminosity(uint s, uint d)
        {
            var o = SwHelper.BLEND_UPRE(d);
            SwHelper.rasterRGB2HSL(SwHelper.C1(s), SwHelper.C2(s), SwHelper.C3(s), out _, out _, out var sl);
            SwHelper.rasterRGB2HSL(o.r, o.g, o.b, out var dh, out var ds, out _);
            TvgColor.Hsl2Rgb(dh, ds, sl, out var r, out var g, out var b);
            return SwHelper.BLEND_PRE(SwHelper.JOIN(255, r, g, b), s, o.a);
        }
    }
}
