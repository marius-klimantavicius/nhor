using System;
using System.Linq;
using System.Reflection;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class RichGlyphAlphaTests
{
    private const int Width = 500, Height = 300;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RgbaGlyphPixelsSurviveSlotReuseRestoreAndThemeRefresh(bool gl) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(Width, Height, "Rich glyph RGBA", Theme.Light, gl ? RenderBackend.GL : RenderBackend.SW);
        var editor = new RichTextBox { FontSize = 24 };
        window.AddChild(editor); editor.Arrange(new RectF(20, 20, 460, 200));
        var blank = Frame(window, gl);
        var opaqueDocument = new RichDocument("Horizon note").WithStyle(0, 12, new(TextFormat.None, Blue(1)));
        editor.Document = opaqueDocument;
        var opaque = Frame(window, gl);
        int[] ink = Enumerable.Range(28, 52).SelectMany(y => Enumerable.Range(28, 322).Select(x => y * Width + x))
            .Where(i => (opaque[i] & 0xffffff) == 0x1a33cc).ToArray();
        Assert.True(ink.Length > 100, "Opaque baseline must have substantial fully covered blue glyph ink.");
        foreach (float alpha in new[] { .5f, 0f, 1f })
        {
            editor.SetColor(0, editor.Text.Length, Blue(alpha));
            var frame = Frame(window, gl);
            AssertFade(blank, opaque, frame, ink, alpha);
            if (alpha == 0) Assert.Equal(blank, frame);
            if (alpha == 1) Assert.Equal(opaque, frame); // Keep non-alpha baseline pixels strict.
        }

        // Restored alpha must reach reused and newly grown slots, not only new paints.
        var restored = new RichDocument("Horizon note").WithStyle(0, 7, new(TextFormat.Bold, Blue(.5f)))
            .WithStyle(7, 5, new(TextFormat.Italic, Blue(0)));
        var snapshot = RichDocument.FromJson(restored.ToJson());
        Assert.Equal(restored, snapshot); editor.Document = snapshot;
        var beforeTheme = Frame(window, gl);
        window.Theme = Theme.Dark; Frame(window, gl);
        window.Theme = Theme.Light;
        Assert.Equal(beforeTheme, Frame(window, gl));
        Assert.Equal(snapshot, editor.Document);
        var textPaints = Content(editor).Paints().OfType<Text>().Where(t => !string.IsNullOrEmpty(t.GetText())).ToArray();
        Assert.Equal(new byte[] { 128, 0 }, textPaints.Select(t => t.Opacity()));

        editor.Select(editor.Text.Length, 0); editor.OnTextInput("Ω");
        Assert.EndsWith("Ω", editor.Text); Assert.Equal(0, editor.Document.StyleAt(editor.Text.Length - 1).Color!.Value.A);
        Assert.True(editor.Undo()); Assert.Equal(snapshot, editor.Document);
        Assert.True(editor.Redo()); Assert.EndsWith("Ω", editor.Text);
        Frame(window, gl);
        // A visible theme-colored run must reset an old transparent slot's opacity.
        editor.Document = new RichDocument("Horizon note"); Frame(window, gl);
        window.Theme.TextColor = Blue(.5f); window.Theme = window.Theme;
        Assert.Equal(128, Content(editor).Paints().OfType<Text>().First().Opacity());
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GlyphAndDecorationAlphaComposeOnceWithElementOpacity(bool gl) => WindowRegressionOwner.Run(() =>
    {
        var theme = Theme.Light;
        using var window = new Window(Width, Height, "Rich alpha composition", theme, gl ? RenderBackend.GL : RenderBackend.SW);
        var editor = new RichTextBox { FontSize = 24, Style = new Style
        { FontSize = 24, Background = Color4.Transparent, Border = Color4.Transparent, BorderFocused = Color4.Transparent } };
        window.AddChild(editor); editor.Arrange(new RectF(20, 20, 460, 200));
        var blank = Frame(window, gl);
        editor.Document = new RichDocument("Horizon note").WithStyle(0, 12, new(TextFormat.None, Blue(1)));
        var glyphOnly = Frame(window, gl);
        foreach (var format in new[] { TextFormat.None, TextFormat.Underline, TextFormat.Strikethrough })
        {
            editor.Opacity = 1;
            editor.Document = new RichDocument("Horizon note").WithStyle(0, 12, new(format, Blue(1)));
            var opaque = Frame(window, gl);
            int[] samples = Enumerable.Range(28, 52).SelectMany(y => Enumerable.Range(28, 322).Select(x => y * Width + x))
                .Where(i => format == TextFormat.None ? (opaque[i] & 0xffffff) == 0x1a33cc :
                    opaque[i] != blank[i] && glyphOnly[i] == blank[i]).ToArray();
            // A one-pixel GL strike can cover two raster rows fractionally.
            // Its actual opaque coverage is the reference, including AA edges.
            Assert.True(samples.Length >= 20, "Require substantial glyph or decoration-only pixel coverage.");
            foreach (var (alpha, opacity) in new[] { (.5f, 1f), (.5f, .5f), (0f, 1f) })
            {
                editor.SetColor(0, 12, Blue(alpha)); editor.Opacity = opacity;
                var frame = Frame(window, gl);
                AssertFade(blank, opaque, frame, samples, alpha * opacity);
                if (alpha == 0) Assert.Equal(blank, frame);
            }
        }
    });

    private static Color4 Blue(float alpha) => new(.1f, .2f, .8f, alpha);
    private static Scene Content(RichTextBox editor) => (Scene)typeof(RichTextBox).GetField("_contentScene", Private)!.GetValue(editor)!;
    private static void AssertFade(uint[] blank, uint[] opaque, uint[] actual, int[] samples, float factor)
    {
        foreach (int i in samples)
            foreach (int shift in new[] { 0, 8, 16 })
            {
                float expected = ((blank[i] >> shift) & 255) * (1 - factor) + ((opaque[i] >> shift) & 255) * factor;
                Assert.InRange((int)((actual[i] >> shift) & 255), (int)expected - 3, (int)expected + 3);
            }
        Console.WriteLine($"factor={factor}, verified reference-coverage pixels={samples.Length}");
    }
    private static unsafe uint[] Frame(Window window, bool gl)
    {
        Contracts.Draw((Canvas)typeof(Window).GetField("_canvas", Private)!.GetValue(window)!, true);
        if (!gl) return (uint[])WindowRuntimeTests.Buffer(window).Clone();
        var finish = (delegate* unmanaged[Cdecl]<void>)Glfw.Glfw.glfwGetProcAddress("glFinish");
        var bind = (delegate* unmanaged[Cdecl]<uint, uint, void>)Glfw.Glfw.glfwGetProcAddress("glBindFramebuffer");
        var buffer = (delegate* unmanaged[Cdecl]<uint, void>)Glfw.Glfw.glfwGetProcAddress("glReadBuffer");
        var read = (delegate* unmanaged[Cdecl]<int, int, int, int, uint, uint, void*, void>)Glfw.Glfw.glfwGetProcAddress("glReadPixels");
        var error = (delegate* unmanaged[Cdecl]<uint>)Glfw.Glfw.glfwGetProcAddress("glGetError");
        Assert.True(finish != null && bind != null && buffer != null && read != null && error != null);
        finish(); bind(0x8CA8, 0); buffer(0x0405);
        var raw = new uint[Width * Height]; fixed (uint* p = raw) read(0, 0, Width, Height, 0x1908, 0x1401, p);
        Assert.Equal(0u, error());
        var result = new uint[raw.Length];
        for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
        { uint c = raw[(Height - 1 - y) * Width + x]; result[y * Width + x] = (c & 0xff00ff00) | ((c & 255) << 16) | ((c >> 16) & 255); }
        return result;
    }
}
