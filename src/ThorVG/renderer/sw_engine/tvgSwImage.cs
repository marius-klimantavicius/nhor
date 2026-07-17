// Ported from ThorVG/src/renderer/sw_engine/tvgSwImage.cpp

using System;

namespace ThorVG
{
    public static unsafe class SwImageOps
    {
        private static SwOutline* _genOutline(SwImage image, SwMpool mpool, uint tid)
        {
            var outline = mpool.Outline(tid);

            outline->input.Reserve(5);
            outline->types.Reserve(5);
            outline->cntrs.Reserve(1);
            outline->closed.Reserve(1);

            var w = (float)image.w;
            var h = (float)image.h;
            outline->input.Push(new Point(0, 0));
            outline->input.Push(new Point(w, 0));
            outline->input.Push(new Point(w, h));
            outline->input.Push(new Point(0, h));
            outline->input.Push(new Point(0, 0));
            for (var i = 0; i < 5; ++i) outline->types.Push(SwConstants.SW_CURVE_TYPE_POINT);
            outline->cntrs.Push(outline->input.count - 1);
            outline->closed.Push(true);
            outline->fillRule = FillRule.NonZero;

            return outline;
        }

        public static bool imagePrepare(SwImage image, in Matrix transform, in RenderRegion clipBox, ref RenderRegion renderBox, SwMpool mpool, uint tid)
        {
            image.direct = TvgMath.Equal(transform.e11, 1.0f) && TvgMath.Equal(transform.e22, 1.0f) && TvgMath.Zero(transform.e12) && TvgMath.Zero(transform.e21);

            if (image.direct)
            {
                image.ox = -(int)MathF.Round(transform.e13);
                image.oy = -(int)MathF.Round(transform.e23);
            }
            else
            {
                image.scale = TvgMath.Scaling(transform);
                image.scaled = TvgMath.Zero(transform.e12) && TvgMath.Zero(transform.e21);
            }

            var outline = _genOutline(image, mpool, tid);
            if (outline == null) return false;
            SwUtil.Export(outline, transform, out var bbox);
            image.outline = *outline;
            image.hasOutline = true;
            return SwUtil.BBox(bbox, clipBox, ref renderBox, image.direct);
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
