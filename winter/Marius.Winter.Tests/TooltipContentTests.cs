using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ThorVG;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Marius.Winter.Tests;

public class TooltipContentTests
{
    private const string Star = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64">
          <polygon points="32,4 40,24 62,24 44,38 50,58 32,46 14,58 20,38 2,24 24,24"
                   fill="#FFD700" stroke="#DAA520" stroke-width="2"/>
        </svg>
        """;

    [Fact]
    public async Task TwoStarsSurroundRenderedTextAndClampWithIndependentOwners()
    {
        using var pump = new NativePump(w => w.Theme = Theme.Light);
        await pump.Call(window =>
        {
            using var textOnly = new TooltipOverlay("Star icon");
            textOnly.PositionNear(0, 0, 200, 120, window.Theme);
            using var tooltip = new TooltipOverlay(new TooltipContent("Star icon", Star) { TrailingSvg = Star });
            window.ShowOverlay(tooltip);
            tooltip.PositionNear(195, 115, 200, 120, window.Theme);

            var pictures = Descendants(tooltip.Scene).OfType<Picture>().ToArray();
            Assert.Equal(2, pictures.Length);
            Assert.NotSame(pictures[0], pictures[1]);
            var left = Assert.IsType<Scene>(pictures[0].Parent());
            var right = Assert.IsType<Scene>(pictures[1].Parent());
            Assert.NotSame(left, right);
            Assert.Same(left.Parent(), right.Parent());
            Assert.Equal(textOnly.Bounds.W + 48 * 2 + 6 * 2, tooltip.Bounds.W, 3);
            Assert.Equal(196 - tooltip.Bounds.W, tooltip.Bounds.X, 3);
            Assert.Equal(115 - tooltip.Bounds.H - 4, tooltip.Bounds.Y, 3);
            float padL = window.Theme.Tooltip.Padding.Left;
            Assert.Equal(padL, left.Transform().e13, 3);
            Assert.Equal(tooltip.Bounds.W - window.Theme.Tooltip.Padding.Right - 48, right.Transform().e13, 3);
            Assert.Equal(window.Theme.Tooltip.Padding.Top, left.Transform().e23, 3);
            Assert.Equal(left.Transform().e23, right.Transform().e23);

            AssertRendered(window, tooltip, left, right);
            tooltip.PositionNear(-20, -30, 200, 120, window.Theme);
            Assert.Equal(4, tooltip.Bounds.X);
            Assert.Equal(4, tooltip.Bounds.Y);
            AssertRendered(window, tooltip, left, right);

            window.RemoveOverlay(tooltip);
            window.ShowOverlay(tooltip);
            foreach (var paint in pictures.Cast<Paint>().Concat(new Paint[] { left, right }))
                Assert.Equal(1, paint.RefCnt());
            tooltip.Dispose();
            foreach (var paint in pictures.Cast<Paint>().Concat(new Paint[] { left, right }))
            {
                Assert.Equal(0, paint.RefCnt());
                Assert.Null(paint.Parent());
            }
            Assert.Empty(left.Paints());
            Assert.Empty(right.Paints());
        });
    }

    [Fact]
    public async Task DefaultsAndEmptyContentKeepOnlyActualContentGaps()
    {
        var original = new TooltipContent("Star icon", Star);
        var (text, svg) = original;
        Assert.Equal("Star icon", text);
        Assert.Equal(Star, svg);
        Assert.Null(original.TrailingSvg);
        using var pump = new NativePump();
        await pump.Call(window =>
        {
            using var textOnly = new TooltipOverlay(text!);
            textOnly.PositionNear(0, 0, 200, 120, window.Theme);
            var padding = window.Theme.Tooltip.Padding;
            float textW = textOnly.Bounds.W - padding.HorizontalTotal;
            foreach (var (hasText, leading, trailing) in new[]
            {
                (false, false, false), (true, false, false), (false, true, false),
                (true, true, false), (false, false, true), (true, false, true),
                (false, true, true), (true, true, true),
            })
            {
                using var tooltip = new TooltipOverlay(new TooltipContent(hasText ? text : "", leading ? Star : "")
                { TrailingSvg = trailing ? Star : "" });
                window.ShowOverlay(tooltip);
                tooltip.PositionNear(0, 0, 200, 120, window.Theme);
                int icons = (leading ? 1 : 0) + (trailing ? 1 : 0);
                int parts = icons + (hasText ? 1 : 0);
                float width = padding.HorizontalTotal + icons * 48 + (hasText ? textW : 0) + Math.Max(0, parts - 1) * 6;
                Assert.Equal(width, tooltip.Bounds.W, 3);
                Assert.Equal(icons, Descendants(tooltip.Scene).OfType<Picture>().Count());
                float x = padding.Left;
                foreach (var picture in Descendants(tooltip.Scene).OfType<Picture>())
                {
                    if (!leading && hasText) x += textW + 6;
                    Assert.Equal(x, picture.Parent()!.Transform().e13, 3);
                    x += 48 + 6 + (hasText ? textW + 6 : 0);
                }
            }
        });
    }

    [Fact]
    public async Task EachSvgKeepsItsOwnAspectRatioHeightLimitAndVerticalCenter()
    {
        const string wide = "<svg xmlns='http://www.w3.org/2000/svg' width='128' height='64'><rect width='128' height='64'/></svg>";
        const string small = "<svg xmlns='http://www.w3.org/2000/svg' width='32' height='16'><rect width='32' height='16'/></svg>";
        using var pump = new NativePump();
        await pump.Call(window =>
        {
            using var tooltip = new TooltipOverlay(new TooltipContent(Svg: wide) { TrailingSvg = small });
            window.ShowOverlay(tooltip);
            tooltip.PositionNear(0, 0, 200, 120, window.Theme);
            var pictures = Descendants(tooltip.Scene).OfType<Picture>().ToArray();
            Assert.Equal(2, pictures.Length);
            pictures[0].GetSize(out float lw, out float lh);
            pictures[1].GetSize(out float rw, out float rh);
            Assert.Equal(96, lw); Assert.Equal(48, lh);
            Assert.Equal(32, rw); Assert.Equal(16, rh);
            var padding = window.Theme.Tooltip.Padding;
            Assert.Equal(96 + 6 + 32 + padding.HorizontalTotal, tooltip.Bounds.W);
            Assert.Equal(48 + padding.VerticalTotal, tooltip.Bounds.H);
            Assert.Equal(padding.Top, pictures[0].Parent()!.Transform().e23);
            Assert.Equal(padding.Top + 16, pictures[1].Parent()!.Transform().e23);
        });
    }

    private static IEnumerable<Paint> Descendants(Scene scene)
    {
        foreach (var paint in scene.Paints())
        {
            yield return paint;
            if (paint is Scene child)
                foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void AssertRendered(Window window, TooltipOverlay tooltip, Scene left, Scene right)
    {
        var canvas = (Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        Contracts.Draw(canvas, true);
        var pixels = WindowRuntimeTests.Buffer(window);
        int stride = (int)window.FramebufferWidth;
        foreach (var icon in new[] { left, right })
        {
            int x = (int)(tooltip.Bounds.X + icon.Transform().e13 + 24);
            int y = (int)(tooltip.Bounds.Y + icon.Transform().e23 + 24);
            Assert.Equal(0xffd700u, pixels[y * stride + x] & 0xffffffu);
        }
        // Actual foreground ink must appear between the two rendered SVG stars.
        int ink = 0;
        for (int y = (int)tooltip.Bounds.Y + 4; y < tooltip.Bounds.Y + tooltip.Bounds.H - 4; y++)
            for (int x = (int)(tooltip.Bounds.X + left.Transform().e13 + 48 + 6); x < tooltip.Bounds.X + right.Transform().e13 - 6; x++)
            {
                uint rgb = pixels[y * stride + x] & 0xffffffu;
                if (((rgb >> 16) & 255) < 100 && ((rgb >> 8) & 255) < 100 && (rgb & 255) < 100) ink++;
            }
        Assert.True(ink > 10, $"Expected Star icon text ink between SVGs, got {ink} pixels.");
    }
}
