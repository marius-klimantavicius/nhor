using Xunit;

namespace ThorVG.Tests
{
    public class tvgSvgPathTests
    {
        [Fact]
        public void ToShape_MoveTo()
        {
            var path = new RenderPath();
            SvgPath.ToShape("M 10 20", path);
            Assert.Equal(1u, path.cmds.count);
            unsafe { Assert.Equal(PathCommand.MoveTo, path.cmds.data[0]); }
            unsafe { Assert.Equal(10.0f, path.pts.data[0].x, 3); }
            unsafe { Assert.Equal(20.0f, path.pts.data[0].y, 3); }
        }

        [Fact]
        public void ToShape_LineTo()
        {
            var path = new RenderPath();
            SvgPath.ToShape("M 0 0 L 10 20", path);
            Assert.Equal(2u, path.cmds.count);
            unsafe { Assert.Equal(PathCommand.MoveTo, path.cmds.data[0]); }
            unsafe { Assert.Equal(PathCommand.LineTo, path.cmds.data[1]); }
            unsafe { Assert.Equal(10.0f, path.pts.data[1].x, 3); }
            unsafe { Assert.Equal(20.0f, path.pts.data[1].y, 3); }
        }

        [Fact]
        public void ToShape_Close()
        {
            var path = new RenderPath();
            SvgPath.ToShape("M 0 0 L 10 0 L 10 10 Z", path);
            Assert.Equal(4u, path.cmds.count);
            unsafe { Assert.Equal(PathCommand.Close, path.cmds.data[3]); }
        }

        [Fact]
        public void ToShape_HorizontalAndVerticalLine()
        {
            var path = new RenderPath();
            SvgPath.ToShape("M 0 0 H 10 V 20", path);
            Assert.Equal(3u, path.cmds.count);
            unsafe
            {
                Assert.Equal(PathCommand.LineTo, path.cmds.data[1]);
                Assert.Equal(10.0f, path.pts.data[1].x, 3);
                Assert.Equal(0.0f, path.pts.data[1].y, 3);
                Assert.Equal(PathCommand.LineTo, path.cmds.data[2]);
                Assert.Equal(10.0f, path.pts.data[2].x, 3);
                Assert.Equal(20.0f, path.pts.data[2].y, 3);
            }
        }

        [Fact]
        public void ToShape_CubicBezier()
        {
            var path = new RenderPath();
            SvgPath.ToShape("M 0 0 C 10 20 30 40 50 60", path);
            Assert.Equal(2u, path.cmds.count);
            unsafe { Assert.Equal(PathCommand.CubicTo, path.cmds.data[1]); }
            Assert.Equal(4u, path.pts.count); // M + 3 control points
        }

        [Fact]
        public void ToShape_SmoothCubicBezier()
        {
            var path = new RenderPath();
            SvgPath.ToShape("M 0 0 C 10 20 30 40 50 60 S 80 90 100 110", path);
            Assert.Equal(3u, path.cmds.count);
            unsafe { Assert.Equal(PathCommand.CubicTo, path.cmds.data[2]); }
        }

        [Fact]
        public void ToShape_QuadraticBezier()
        {
            var path = new RenderPath();
            SvgPath.ToShape("M 0 0 Q 10 20 30 40", path);
            // Q is converted to cubic
            Assert.Equal(2u, path.cmds.count);
            unsafe { Assert.Equal(PathCommand.CubicTo, path.cmds.data[1]); }
        }

        [Fact]
        public void ToShape_RelativeMoveTo()
        {
            var path = new RenderPath();
            SvgPath.ToShape("M 10 20 m 5 5", path);
            Assert.Equal(2u, path.cmds.count);
            unsafe
            {
                Assert.Equal(PathCommand.MoveTo, path.cmds.data[1]);
                Assert.Equal(15.0f, path.pts.data[1].x, 3);
                Assert.Equal(25.0f, path.pts.data[1].y, 3);
            }
        }

        [Fact]
        public void ToShape_RelativeLineTo()
        {
            var path = new RenderPath();
            SvgPath.ToShape("M 10 20 l 5 5", path);
            Assert.Equal(2u, path.cmds.count);
            unsafe
            {
                Assert.Equal(PathCommand.LineTo, path.cmds.data[1]);
                Assert.Equal(15.0f, path.pts.data[1].x, 3);
                Assert.Equal(25.0f, path.pts.data[1].y, 3);
            }
        }

        [Fact]
        public void ToShape_EmptyPath()
        {
            var path = new RenderPath();
            var result = SvgPath.ToShape("", path);
            // Empty string is valid (no commands to parse), returns true but produces no commands
            Assert.True(result);
            Assert.Equal(0u, path.cmds.count);
        }

        [Fact]
        public void ToShape_MultipleSubpaths()
        {
            var path = new RenderPath();
            SvgPath.ToShape("M 0 0 L 10 10 Z M 20 20 L 30 30 Z", path);
            Assert.Equal(6u, path.cmds.count);
        }

        [Fact]
        public void ToShape_ArcCommand()
        {
            var path = new RenderPath();
            SvgPath.ToShape("M 10 80 A 25 25 0 0 1 50 80", path);
            Assert.True(path.cmds.count >= 2); // M + at least one CubicTo from arc conversion
        }

        [Fact]
        public void ToShape_CommaAndSpaceSeparators()
        {
            var path = new RenderPath();
            SvgPath.ToShape("M10,20L30,40", path);
            Assert.Equal(2u, path.cmds.count);
            unsafe
            {
                Assert.Equal(10.0f, path.pts.data[0].x, 3);
                Assert.Equal(20.0f, path.pts.data[0].y, 3);
                Assert.Equal(30.0f, path.pts.data[1].x, 3);
                Assert.Equal(40.0f, path.pts.data[1].y, 3);
            }
        }
    }
}
