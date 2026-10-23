using System;
using Xunit;

namespace ThorVG.Tests;

public class tvgGlGeometryLifetimeUpdateTests
{
    [Fact]
    public unsafe void DisposingRealTrimmedDashedGeometryReleasesAllOwnedStorage()
    {
        var shape = new RenderShape();
        shape.path.AddRect(4, 4, 32, 24, 3, 4, true);
        shape.stroke = new RenderStroke { width = 2, cap = StrokeCap.Round, join = StrokeJoin.Round, dashPattern = new[] { 2f, 3f }, dashCount = 2, dashLength = 5, trim = new RenderTrimPath { begin = .15f, end = .85f, simultaneous = true } };
        using var geometry = new GlGeometry();
        try
        {
            geometry.SetMatrix(TvgMath.Identity()); geometry.Prepare(shape);
            float multiplier = 1;
            geometry.TesselateShape(shape, ref multiplier);
            Assert.True(geometry.TesselateStroke(shape));
            Assert.True(geometry.optPath.pts.count > 0);
            Assert.True(geometry.optStrokePath.pts.count > 0);
            Assert.True(geometry.stroke.vertex.count > 0 && geometry.stroke.index.count > 0);
            geometry.Dispose(); geometry.Dispose();
            Assert.True(geometry.fill.vertex.data == null && geometry.fill.index.data == null);
            Assert.True(geometry.stroke.vertex.data == null && geometry.stroke.index.data == null);
            Assert.True(geometry.optPath.cmds.data == null && geometry.optPath.pts.data == null);
            Assert.True(geometry.optStrokePath.cmds.data == null && geometry.optStrokePath.pts.data == null);
            Assert.True(shape.path.cmds.count > 0 && shape.path.pts.count > 0, "Source path ownership must remain with the shape.");
        }
        finally { shape.path.cmds.Dispose(); shape.path.pts.Dispose(); }
    }
}
