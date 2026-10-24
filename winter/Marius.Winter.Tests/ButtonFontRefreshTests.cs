using System;
using System.Linq;
using System.Reflection;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class ButtonFontRefreshTests
{
    [Theory]
    [InlineData(false, 14, false)]
    [InlineData(true, 14, false)]
    [InlineData(false, 24, false)]
    [InlineData(true, 24, false)]
    [InlineData(false, 14, true)]
    [InlineData(true, 14, true)]
    [InlineData(false, 24, true)]
    [InlineData(true, 24, true)]
    public void RetainedGlyphsAndMeasurementMatchFreshButtonAcrossAttachmentAndFontChanges(bool preattached, int initialSize, bool pixelsFirst)
    {
        WindowRegressionOwner.Run(() =>
        {
            var theme = Theme.Light; theme.Button.FontSize = initialSize;
            using var window = new Window(400, 210, "Button retained fonts", theme, RenderBackend.SW);
            var parent = new Panel();
            var button = MakeButton();
            if (preattached) parent.AddChild(button); // Paints created with detached defaults.
            window.AddChild(parent);
            if (!preattached) parent.AddChild(button);
            foreach (int size in new[] { initialSize, 14, 24, 14, 24, 14 })
            {
                var next = size == 24 ? Theme.Light : Theme.Dark;
                next.Button.FontSize = size;
                window.Theme = next;
                CompareWithFresh();
            }
            window.Theme.Button.FontName = "default-bold";
            CompareWithFresh();
            window.Theme.Button.FontName = "default";
            CompareWithFresh();

            void CompareWithFresh()
            {
                using var fresh = MakeButton();
                parent.AddChild(fresh);
                try
                {
                    var expected = fresh.Measure(380, 90);
                    var actual = button.Measure(380, 90);
                    void CheckMeasurement()
                    {
                        Assert.InRange(MathF.Abs(actual.X - expected.X), 0, .01f);
                        Assert.InRange(MathF.Abs(actual.Y - expected.Y), 0, .01f);
                    }
                    if (!pixelsFirst) CheckMeasurement();
                    button.Arrange(new RectF(10, 10, 370, 80));
                    fresh.Arrange(new RectF(10, 10, 370, 80));
                    var canvas = (Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                    fresh.Visible = false;
                    Contracts.Draw(canvas, true);
                    var pixels = WindowRuntimeTests.Buffer(window);
                    bool Red(uint p) => ((p >> 16) & 255) > 160 && ((p >> 8) & 255) < 60 && (p & 255) < 60;
                    var subject = Enumerable.Range(0, 400 * 100).Select(i => Red(pixels[i])).ToArray();
                    button.Visible = false;
                    fresh.Visible = true;
                    Contracts.Draw(canvas, true);
                    var reference = Enumerable.Range(0, 400 * 100).Select(i => Red(pixels[i])).ToArray();
                    button.Visible = true;
                    Assert.Contains(true, reference);
                    Assert.Equal(reference, subject); // Actual glyph paths, including retained shadow.
                    if (pixelsFirst) CheckMeasurement();
                    foreach (int i in Enumerable.Range(0, subject.Length).Where(i => subject[i]))
                    {
                        Assert.InRange(i % 400, 11, 378);
                        Assert.InRange(i / 400, 11, 88);
                    }
                }
                finally { parent.RemoveChild(fresh); }
            }
        });
    }

    // A uniform background keeps antialiased glyph pixels independent of the
    // gradient's lifetime/size, which has separate first-frame regressions.
    private static Button MakeButton() => new("Change orientation") { Color = new Color4(1, 0, 0, 1), Background = Color4.Black };
}
