// Ported from ThorVG/src/renderer/sw_engine/tvgSwImage.cpp

using System;

namespace ThorVG
{
    public static unsafe class SwImageOps
    {
        private static bool _onlyShifted(in Matrix m)
        {
            if (TvgMath.Equal(m.e11, 1.0f) && TvgMath.Equal(m.e22, 1.0f) && TvgMath.Zero(m.e12) && TvgMath.Zero(m.e21)) return true;
            return false;
        }

        private static bool _genOutline(SwImage image, in Matrix transform, SwMpool mpool, uint tid)
        {
            var outline = mpool.Outline(tid);

            outline->pts.Reserve(5);
            outline->types.Reserve(5);
            outline->cntrs.Reserve(1);
            outline->closed.Reserve(1);

            var w = (float)image.w;
            var h = (float)image.h;
            var to = stackalloc Point[4];
            to[0] = new Point(0, 0);
            to[1] = new Point(w, 0);
            to[2] = new Point(w, h);
            to[3] = new Point(0, h);

            for (int i = 0; i < 4; i++)
            {
                outline->pts.Push(SwMath.mathTransform(to[i], transform));
                outline->types.Push(SwConstants.SW_CURVE_TYPE_POINT);
            }

            outline->pts.Push(outline->pts[0]);
            outline->types.Push(SwConstants.SW_CURVE_TYPE_POINT);
            outline->cntrs.Push(outline->pts.count - 1);
            outline->closed.Push(true);
            outline->fillRule = FillRule.NonZero;

            image.outline = *outline;
            image.hasOutline = true;
            return true;
        }

        public static bool imagePrepare(SwImage image, in Matrix transform, in RenderRegion clipBox, ref RenderRegion renderBox, SwMpool mpool, uint tid)
        {
            image.direct = _onlyShifted(transform);

            if (image.direct)
            {
                image.ox = -(int)MathF.Round(transform.e13);
                image.oy = -(int)MathF.Round(transform.e23);
            }
            else
            {
                var scaleX = MathF.Sqrt((transform.e11 * transform.e11) + (transform.e21 * transform.e21));
                var scaleY = MathF.Sqrt((transform.e22 * transform.e22) + (transform.e12 * transform.e12));
                image.scale = (MathF.Abs(scaleX - scaleY) > 0.01f) ? 1.0f : scaleX;

                if (TvgMath.Zero(transform.e12) && TvgMath.Zero(transform.e21)) image.scaled = true;
                else image.scaled = false;
            }

            if (!_genOutline(image, transform, mpool, tid)) return false;
            if (image.hasOutline)
            {
                fixed (SwOutline* outlinePtr = &image.outline)
                {
                    return SwMath.mathUpdateOutlineBBox(outlinePtr, clipBox, ref renderBox, image.direct);
                }
            }
            return false;
        }

        public static bool imageGenRle(SwImage image, in RenderRegion renderBox, SwMpool mpool, uint tid, bool antiAlias)
        {
            if (image.hasOutline)
            {
                fixed (SwOutline* outlinePtr = &image.outline)
                {
                    image.hasRle = SwRleOps.rleRender(ref image.rle, outlinePtr, renderBox, mpool, tid, antiAlias);
                }
            }
            return image.hasRle;
        }

        public static void imageDelOutline(SwImage image, SwMpool mpool, uint tid)
        {
            image.hasOutline = false;
        }

        public static void imageReset(SwImage image)
        {
            if (image.hasRle)
            {
                SwRleOps.rleReset(ref image.rle);
            }
        }

        public static void imageFree(SwImage image)
        {
            image.hasRle = false;
        }
    }
}
