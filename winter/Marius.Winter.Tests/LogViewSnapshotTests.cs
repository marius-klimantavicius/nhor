using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Marius.Winter.Blazor;
using ThorVG;
using Xunit;
using Xunit.Abstractions;
using static Glfw.GLFW;

namespace Marius.Winter.Tests;

public class LogViewSnapshotTests(ITestOutputHelper output)
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static string Snapshot(int repetitions = 1) => new RichDocument(string.Concat(Enumerable.Repeat(
        "Review the blue horizon.\nKeep the trail open. ", repetitions)))
        .WithStyle(11, 12, new(TextFormat.Bold, new Color4(.12f, .45f, .88f))).ToJson();

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public void StyledSnapshotSelectionHasBoundedRendererWork(int repetitions) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(900, 300, "Log snapshot work", Theme.Light);
        using var font = new CountingFont();
        using var log = new LogView { FontName = font.name!, Text = Snapshot(repetitions) };
        var cold = Measure(font, () => { window.AddChild(log); log.Arrange(new RectF(0, 0, 860, 100)); });
        var warm = Measure(font, () =>
        {
            for (int i = 0; i < 12; i++)
            {
                log.OnMouseDown(0, 20, 18);
                log.OnMouseMove(650 + i, 18);
                log.OnMouseUp(0, 650 + i, 18);
            }
        });
        Assert.NotEmpty(log.SelectedText);
        Assert.Contains(log.SelectedText, log.Text);
        var farEnd = Measure(font, () => Assert.Equal(log.Text.Length, Hit(log, 100000, 18)));
        var result = new { repetitions, characters = log.Text.Length, cold, warm, farEnd };
        output.WriteLine(JsonSerializer.Serialize(result));
        if (Environment.GetEnvironmentVariable("WINTER_LOGVIEW_EVIDENCE") is { } dir)
            File.WriteAllText(Path.Combine(dir, $"profile-{repetitions}.json"), JsonSerializer.Serialize(result));
        // Work counters wrap the real ThorVG font loader, not a timing proxy or
        // implementation hook. Warm pointer input must not rebuild long prefixes.
        Assert.True(warm.Characters <= 4096, $"Repeated selection measured {warm.Characters} UTF-16 units in {warm.Calls} ThorVG loads.");
        Assert.True(cold.Characters <= log.Text.Length * 32L + 4096, "Initial geometry must use bounded linear work.");
        Assert.True(farEnd.Characters <= log.Text.Length * 4L + 4096, "Far-end hit testing must not scan successively longer prefixes.");
    });

    [Theory]
    [InlineData("default")]
    [InlineData("monospace")]
    public void HitTestsFollowRendererAdvancesIncludingKerningSpacesAndUnicode(string font) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(700, 200, "Log advances");
        using var log = new LogView { Text = "AVATAR To e\u0301 👩‍💻 Ž   ", FontName = font, FontSize = 16 };
        window.AddChild(log); log.Arrange(new RectF(0, 0, 680, 100));
        var measure = Text.Gen();
        try
        {
            measure.SetFont(font); measure.SetFontSize(16);
            var boundaries = StringInfo.ParseCombiningCharacters(log.Text).Append(log.Text.Length).ToArray();
            for (int i = 1; i < boundaries.Length; i++)
            {
                measure.SetText(log.Text[..boundaries[i]]); measure.GetTextSize(out float width, out _);
                Assert.Equal(boundaries[i], Hit(log, 8 + width - .1f, 18));
            }
        }
        finally { measure.Unref(); }
    });

    [Theory]
    [InlineData(RenderBackend.SW)]
    [InlineData(RenderBackend.GL)]
    public void HorizontalEndWheelScrollbarAndScrolledCopyReachExactSnapshot(RenderBackend backend) => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(500, 220, "Log horizontal navigation", Theme.Light, backend);
        using var log = new LogView { Text = Snapshot(12) };
        window.AddChild(log); log.Arrange(new RectF(0, 0, 460, 100)); Draw(window);
        log.OnKeyDown(GLFW_KEY_A, GLFW_MOD_CONTROL, false);
        log.OnKeyDown(GLFW_KEY_C, GLFW_MOD_CONTROL, false);
        Assert.Equal(log.Text, Glfw.Glfw.glfwGetClipboardString(window.GlfwWindow!));
        log.OnKeyDown(GLFW_KEY_END, 0, false);
        Assert.Equal(log.Text.Length, Hit(log, 452, 18));
        log.OnMouseDown(0, 260, 18); log.OnMouseMove(452, 18); log.OnMouseUp(0, 452, 18);
        Assert.NotEmpty(log.SelectedText); Assert.EndsWith(log.SelectedText, log.Text);
        log.OnKeyDown(GLFW_KEY_C, GLFW_MOD_CONTROL, false);
        Assert.Equal(log.SelectedText, Glfw.Glfw.glfwGetClipboardString(window.GlfwWindow!));
        log.OnKeyDown(GLFW_KEY_HOME, 0, false); Assert.Equal(0, Hit(log, 8, 18));
        log.OnScroll(-10000, 0); Assert.Equal(log.Text.Length, Hit(log, 452, 18));
        log.OnKeyDown(GLFW_KEY_HOME, 0, false);
        log.OnMouseDown(0, 30, 88); log.OnMouseMove(448, 88); log.OnMouseUp(0, 448, 88);
        Assert.Equal(log.Text.Length, Hit(log, 452, 18));
        Draw(window);
    });

    [Fact]
    public void KeyboardSelectionAndPointerAutoscrollKeepWholeGraphemes() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(500, 220, "Log graphemes");
        using var log = new LogView { Text = string.Concat(Enumerable.Repeat("e\u0301👩‍💻Ž ", 100)) };
        window.AddChild(log); log.Arrange(new RectF(0, 0, 460, 100));
        log.OnKeyDown(GLFW_KEY_HOME, 0, false);
        log.OnKeyDown(GLFW_KEY_RIGHT, GLFW_MOD_SHIFT, false);
        Assert.Equal("e\u0301", log.SelectedText);
        log.OnKeyDown(GLFW_KEY_RIGHT, GLFW_MOD_SHIFT, false);
        Assert.Equal("e\u0301👩‍💻", log.SelectedText);
        log.OnMouseDown(0, 20, 18); log.OnMouseMove(480, 18);
        int before = Hit(log, 440, 18);
        for (int i = 0; i < 15; i++) window.Animator.Tick(.02f);
        Assert.True(Hit(log, 440, 18) > before, "Held pointer beyond the viewport must continue scrolling.");
        var boundaries = StringInfo.ParseCombiningCharacters(log.Text).Append(log.Text.Length).ToArray();
        Assert.Contains(log.SelectionStart, boundaries);
        Assert.Contains(log.SelectionStart + log.SelectionLength, boundaries);
        log.OnMouseUp(0, 480, 18);
        Assert.False(window.Animator.HasActiveAnimations);
    });

    [Fact]
    public void LongLinesRenderOnlyVisibleGlyphsAndInvalidateForFontThemeResize() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(600, 300, "Log visible glyphs", Theme.Light);
        using var log = new LogView { Text = Snapshot(12) };
        window.AddChild(log); log.Arrange(new RectF(0, 0, 300, 100)); Draw(window);
        Assert.True(Rendered(log).Length < log.Text.Length / 2, "The canvas should receive viewport glyphs, retaining the exact source separately.");
        int old = Hit(log, 270, 18);
        log.FontSize = 28;
        Assert.True(Hit(log, 270, 18) < old);
        log.FontName = "default";
        log.OnKeyDown(GLFW_KEY_END, 0, false); Assert.Equal(log.Text.Length, Hit(log, 292, 18));
        window.Theme = Theme.Dark;
        log.Arrange(new RectF(0, 0, 550, 130));
        log.OnKeyDown(GLFW_KEY_END, 0, false); Assert.Equal(log.Text.Length, Hit(log, 542, 18));
        log.ResetFontSize();
        log.Text = "short";
        Assert.Equal(0, log.SelectionLength); Assert.Equal(0, Hit(log, 8, 18));
        Assert.Equal("short", Rendered(log));
        log.Clear(); Assert.Equal(0, Hit(log, 8, 18)); Draw(window);
    });

    [Fact]
    public void BufferAppendRetentionResetAndReattachmentPreserveInspection() => WindowRegressionOwner.Run(() =>
    {
        using var window = new Window(500, 300, "Log buffer");
        var buffer = new LogBuffer(8, 1000);
        buffer.Append(string.Join('\n', Enumerable.Range(0, 8).Select(i => $"line{i} " + new string('x', 45))));
        using var parent = new Panel(); window.AddChild(parent);
        using var log = new LogView { Buffer = buffer };
        parent.AddChild(log); log.Arrange(new RectF(0, 0, 300, 90)); Draw(window);
        Assert.True(log.ScrollOffset > 0); Assert.True(log.FollowTail);
        log.OnKeyDown(GLFW_KEY_HOME, 0, false);
        log.OnMouseDown(0, 8, 18); log.OnMouseMove(60, 18); log.OnMouseUp(0, 60, 18);
        string selected = log.SelectedText; Assert.NotEmpty(selected);
        buffer.Append(" suffix"); Assert.Equal(selected, log.SelectedText); Assert.False(log.FollowTail);
        log.OnKeyDown(GLFW_KEY_A, GLFW_MOD_CONTROL, false);
        buffer.Append("\nline8");
        Assert.Equal(buffer.Snapshot.Text[..^6], log.SelectedText);
        log.JumpToLatest(); Assert.True(log.FollowTail); Assert.True(log.ScrollOffset > 0);
        // New glyph slots and cache state must survive same-turn removal.
        buffer.Append("\nline9"); parent.RemoveChild(log); parent.AddChild(log);
        log.Arrange(new RectF(0, 0, 300, 90)); Draw(window);
        Assert.Equal(buffer.Snapshot.Text, log.Text);
        buffer.Clear(); Assert.Equal(0, log.SelectionLength); Assert.Equal(0, log.ScrollOffset);
        log.Buffer = null; log.Text = Snapshot(); log.OnKeyDown(GLFW_KEY_END, 0, false);
        log.Text = "replaced"; Assert.Equal(0, Hit(log, 8, 18));
        parent.RemoveChild(log); buffer.Append("detached producer"); Assert.Equal("replaced", log.Text);
    });

    [Fact]
    public void BothShippedNotesScenariosSaveExactStyledSnapshotAndAllowEndNavigation() => WindowRegressionOwner.Run(() =>
    {
        foreach (bool razor in new[] { true, false })
        {
            using var window = new Window(900, 720, "Notes snapshot integration");
            if (razor) Assert.True(window.UseBlazor().AddComponent<Blazor.Playground.Scenarios.Notes>(
                new Dictionary<string, object> { ["OwnerWindow"] = window }).IsCompletedSuccessfully);
            else { var scenario = new Playground.Scenarios.NotesScenario(window); window.AddChild(scenario.Content); }
            using var scope = window.Dispatcher.EnterScope();
            var elements = Walk(window).ToArray();
            var editor = elements.OfType<RichTextBox>().Single();
            editor.Select(11, 12); editor.ToggleBold(); editor.SetSelectionColor(new(.12f, .45f, .88f));
            elements.OfType<Button>().Single(b => b.Text == "Save snapshot").OnClick();
            WindowRegressionOwner.Flush(window);
            var log = Walk(window).OfType<LogView>().Single();
            log.Arrange(new RectF(0, 0, 400, 100));
            Assert.Equal(editor.Document.ToJson(), log.Text);
            Assert.Equal(editor.Document, RichDocument.FromJson(log.Text));
            log.OnKeyDown(GLFW_KEY_A, GLFW_MOD_CONTROL, false); log.OnKeyDown(GLFW_KEY_C, GLFW_MOD_CONTROL, false);
            Assert.Equal(log.Text, Glfw.Glfw.glfwGetClipboardString(window.GlfwWindow!));
            log.OnKeyDown(GLFW_KEY_END, 0, false); Assert.Equal(log.Text.Length, Hit(log, 392, 18));
        }
    });

    private static IEnumerable<Element> Walk(Element element)
    { yield return element; foreach (var child in element.Children) foreach (var nested in Walk(child)) yield return nested; }
    private static int Hit(LogView log, float x, float y) => (int)typeof(LogView).GetMethod("GetCharIndexAtPosition", Private)!.Invoke(log, [x, y])!;
    private static string Rendered(LogView log) => string.Concat(((Scene)typeof(LogView).GetField("_contentScene", Private)!.GetValue(log)!).Paints().OfType<Text>().Select(t => t.GetText()));
    private static void Draw(Window window) => Contracts.Draw((Canvas)typeof(Window).GetField("_canvas", Private)!.GetValue(window)!, true);
    private sealed record Work(long Calls, long Characters, long Allocated, double Milliseconds);
    private static Work Measure(CountingFont font, Action action)
    {
        long calls = font.Calls, chars = font.Characters, allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew(); action(); watch.Stop();
        return new(font.Calls - calls, font.Characters - chars, GC.GetAllocatedBytesForCurrentThread() - allocated, watch.Elapsed.TotalMilliseconds);
    }

    // Counts actual renderer input while forwarding every operation to the
    // bundled monospace font. The alias holds one lease and leaves no globals.
    private sealed class CountingFont : FontLoader, IDisposable
    {
        private readonly FontLoader _inner;
        private readonly Inlist<Loader> _loaders;
        internal long Calls, Characters;
        internal CountingFont() : base(FileType.Sfnt)
        {
            _inner = (FontLoader)LoaderMgr.Font("monospace")!;
            name = "log-counting-font"; cached = true; sharing = 1;
            _loaders = (Inlist<Loader>)typeof(LoaderMgr).GetField("_activeLoaders", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            _loaders.Back(this);
        }
        public override bool Get(FontMetrics fm, string? text, uint len, RenderPath path)
        { Calls++; Characters += text?.Length ?? 0; return _inner.Get(fm, text, len, path); }
        public override void Transform(Paint paint, FontMetrics fm, float shear) => _inner.Transform(paint, fm, shear);
        public override void Release(FontMetrics fm) => _inner.Release(fm);
        public override void Metrics(FontMetrics fm, out TextMetrics metrics) => _inner.Metrics(fm, out metrics);
        public override bool GlyphMetrics(FontMetrics fm, string ch, out GlyphMetrics metrics) => _inner.GlyphMetrics(fm, ch, out metrics);
        public override void Copy(FontMetrics input, FontMetrics result) => _inner.Copy(input, result);
        public void Dispose() { Assert.Equal(1, sharing); _loaders.Remove(this); LoaderMgr.Retrieve(_inner); }
    }
}
