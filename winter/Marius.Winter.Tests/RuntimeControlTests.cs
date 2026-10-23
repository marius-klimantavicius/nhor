using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class AnimationContractTests
{
    [Theory]
    [InlineData("self")]
    [InlineData("all")]
    [InlineData("other")]
    public void ApplyCanCancelWithoutCompletingCancelledAnimations(string mode)
    {
        var manager = new AnimationManager(); var calls = new List<string>();
        var other = new Marius.Winter.Animation { Duration = 1, Tag = "other", Apply = _ => calls.Add("other") };
        var current = new Marius.Winter.Animation { Duration = 1, Tag = "self", OnComplete = () => calls.Add("complete") };
        current.Apply = _ => { calls.Add("apply"); if (mode == "all") manager.CancelAll(); else manager.Cancel(mode); };
        manager.Start(current); manager.Start(other);
        Assert.Null(Record.Exception(() => manager.Tick(1)));
        Assert.Equal(mode == "other" ? new[] { "apply", "complete" } : new[] { "apply" }, calls);
    }

    [Fact]
    public void CompletionCanCancelAndStartAndNewAnimationWaitsForNextTick()
    {
        var manager = new AnimationManager(); var calls = new List<string>();
        manager.Start(new Marius.Winter.Animation { Duration = 0, Apply = t => { Assert.Equal(1, t); calls.Add("first"); },
            OnComplete = () => { manager.CancelAll(); manager.Start(new Marius.Winter.Animation { Duration = 0, Apply = _ => calls.Add("new") }); } });
        Assert.Null(Record.Exception(() => manager.Tick(0)));
        Assert.Equal(new[] { "first" }, calls); Assert.True(manager.HasActiveAnimations);
        manager.Tick(0); Assert.Equal(new[] { "first", "new" }, calls); Assert.False(manager.HasActiveAnimations);
    }

    [Fact]
    public void StartFromApplyDoesNotRunUntilNextTickAndCallbackOrderIsStable()
    {
        var manager = new AnimationManager(); var calls = new List<int>();
        manager.Start(new Marius.Winter.Animation { Duration = 1, Apply = _ => { calls.Add(1); manager.Start(new Marius.Winter.Animation { Duration = 1, Apply = _ => calls.Add(3) }); } });
        manager.Start(new Marius.Winter.Animation { Duration = 1, Apply = _ => calls.Add(2) });
        manager.Tick(1); Assert.Equal(new[] { 1, 2 }, calls);
        manager.Tick(1); Assert.Equal(new[] { 1, 2, 3 }, calls);
    }
}

public class ClipPixelTests
{
    [Theory]
    [InlineData(false, "position")]
    [InlineData(true, "position")]
    [InlineData(false, "size")]
    [InlineData(true, "size")]
    [InlineData(false, "ancestor")]
    [InlineData(true, "ancestor")]
    [InlineData(false, "nested")]
    [InlineData(true, "nested")]
    public void ParentSpaceClipTracksBoundsAndAncestorTransforms(bool tree, string operation)
    {
        Initializer.Init();
        using var canvas = SwCanvas.Gen()!;
        using var root = new Panel();
        try
        {
            var pixels = new uint[240 * 120]; canvas.Target(pixels, 240, 240, 120, ColorSpace.ARGB8888);
            var ancestor = new Panel(); root.AddChild(ancestor);
            Element clipped = tree ? new TreeView() : new ScrollPanel { VerticalScroll = ScrollMode.Never, HorizontalScroll = ScrollMode.Never };
            clipped.Style = new Style { Background = new Color4(1, 0, 0), CornerRadius = 0 };
            ancestor.AddChild(clipped); clipped.Arrange(new RectF(10, 10, 40, 40));
            canvas.Add(root.Scene); Contracts.Draw(canvas, true);
            Assert.NotEqual(0u, pixels[20 * 240 + 20]);
            if (operation == "position" || operation == "nested") clipped.Arrange(new RectF(100, 10, 40, 40));
            if (operation == "size") clipped.Arrange(new RectF(10, 10, 70, 40));
            if (operation == "ancestor") ancestor.Bounds = new RectF(90, 0, 140, 100);
            if (operation == "nested")
            {
                // A scrolling ancestor changes the incoming parent transform;
                // the child's own clip must still use its parent-space bounds.
                ancestor.Bounds = new RectF(-30, 0, 200, 100);
            }
            Contracts.Draw(canvas, true);
            int expectedX = operation == "size" ? 65 : operation == "nested" ? 80 : 110;
            Assert.NotEqual(0u, pixels[20 * 240 + expectedX]);
            Assert.Equal(0u, pixels[20 * 240 + (operation == "size" ? 85 : 20)]);
            canvas.Remove(root.Scene);
        }
        finally { canvas.Dispose(); Initializer.Term(); }
    }
}

public class ImageInvalidationTests
{
    private const string Svg = "<svg xmlns='http://www.w3.org/2000/svg' width='32' height='24'><rect width='32' height='24' fill='red'/></svg>";

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ClearAndUnavailableSourceInvalidateCachedIntrinsicSize(bool svg, bool failed)
    {
        using var window = new Window(160, 100, "Image source contract");
        Element image = svg ? new SvgImage(Svg) : new Image(); window.AddChild(image);
        if (image is Image raster) raster.SetSource(System.Text.Encoding.UTF8.GetBytes(Svg), "svg");
        Assert.Equal(new Vector2(32, 24), image.Measure(100, 100)); window.Measure(160, 100); window.Dirty = false;
        if (image is SvgImage vector) vector.Svg = failed ? "unavailable.svg" : null;
        else ((Image)image).SetSource(failed ? Array.Empty<byte>() : null, "png");
        Assert.True(image.IsMeasureDirty, "Clearing a source must invalidate intrinsic measurement");
        Assert.True(window.IsMeasureDirty); Assert.True(window.Dirty);
        Assert.Equal(Vector2.Zero, image.Measure(100, 100));
        var own = Assert.IsType<Scene>(Assert.Single(image.Scene.Paints())); Assert.Empty(own.Paints());
    }
}

public class EditorStyleIntegrationTests
{
    [Theory]
    [InlineData("TextBox", false)]
    [InlineData("TextBox", true)]
    [InlineData("TextArea", false)]
    [InlineData("TextArea", true)]
    [InlineData("RichTextBox", false)]
    [InlineData("RichTextBox", true)]
    public void EffectiveFontAndRetainedTextUpdateOnStyleChange(string kind, bool mutate)
    {
        using var window = new Window(400, 240, "Editor font contract");
        Element editor = kind switch { "TextBox" => new TextBox { Text = "Winter" }, "TextArea" => new TextArea { Text = "Winter" }, _ => new RichTextBox { Text = "Winter" } };
        editor.Style = new Style { FontName = "default", FontSize = 12 }; window.AddChild(editor);
        editor.Measure(350, 200); editor.Arrange(new RectF(0, 0, 350, 200));
        float before = (float)editor.GetType().GetField("_effectiveFontSize", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(editor)!;
        if (mutate) { editor.Style.FontSize = 36; editor.Style.FontName = "monospace"; }
        else editor.Style = new Style { FontName = "monospace", FontSize = 36 };
        Assert.True(editor.IsMeasureDirty); editor.Measure(350, 200); editor.Arrange(editor.Bounds);
        float after = (float)editor.GetType().GetField("_effectiveFontSize", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(editor)!;
        Assert.True(after > before * 2, "Cached font size must follow effective style");
        var texts = DescendantPaints(editor.Scene).OfType<Text>().Where(t => { t.GetTextSize(out float w, out _); return w > 0; }).ToArray();
        Assert.NotEmpty(texts); Assert.Contains(texts, t => { t.GetTextSize(out _, out float h); return h > 24; });
    }

    private static IEnumerable<Paint> DescendantPaints(Scene scene)
    {
        foreach (var paint in scene.Paints()) { yield return paint; if (paint is Scene child) foreach (var nested in DescendantPaints(child)) yield return nested; }
    }
}
