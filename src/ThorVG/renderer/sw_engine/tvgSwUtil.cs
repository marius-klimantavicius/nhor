// Ported from ThorVG/src/renderer/cpu_engine/tvgSwUtil.cpp

using System;

namespace ThorVG
{
    public static unsafe class SwUtil
    {
        public static void Export(SwOutline* outline, in Matrix transform, out BBox bbox)
        {
            outline->output.Reserve(outline->input.count);
            bbox = new BBox
            {
                min = new Point(float.MaxValue, float.MaxValue),
                max = new Point(-float.MaxValue, -float.MaxValue)
            };

            for (uint i = 0; i < outline->input.count; ++i)
            {
                var t = TvgMath.Transform(outline->input[i], transform);
                if (bbox.min.x > t.x) bbox.min.x = t.x;
                if (bbox.max.x < t.x) bbox.max.x = t.x;
                if (bbox.min.y > t.y) bbox.min.y = t.y;
                if (bbox.max.y < t.y) bbox.max.y = t.y;
                outline->output.Push(new SwPoint((int)(t.x * 64.0f), (int)(t.y * 64.0f)));
            }
        }

        public static bool BBox(in BBox bbox, in RenderRegion clipBox, ref RenderRegion renderBox, bool fastTrack)
        {
            if (fastTrack)
            {
                renderBox.min.x = (int)MathF.Round(bbox.min.x, MidpointRounding.AwayFromZero);
                renderBox.min.y = (int)MathF.Round(bbox.min.y, MidpointRounding.AwayFromZero);
                renderBox.max.x = (int)MathF.Round(bbox.max.x, MidpointRounding.AwayFromZero);
                renderBox.max.y = (int)MathF.Round(bbox.max.y, MidpointRounding.AwayFromZero);
            }
            else
            {
                renderBox.min.x = (int)MathF.Floor(bbox.min.x);
                renderBox.min.y = (int)MathF.Floor(bbox.min.y);
                renderBox.max.x = (int)MathF.Ceiling(bbox.max.x);
                renderBox.max.y = (int)MathF.Ceiling(bbox.max.y);
            }
            renderBox.IntersectWith(clipBox);
            return renderBox.Valid();
        }
    }
}
