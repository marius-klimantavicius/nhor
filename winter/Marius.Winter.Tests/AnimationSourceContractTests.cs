using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Marius.Winter.Blazor;
using Marius.Winter.Blazor.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using ThorVG;
using Xunit;
using B = Marius.Winter.Blazor.Elements;
using Task = System.Threading.Tasks.Task;

namespace Marius.Winter.Tests;

public class AnimationSourceContractTests
{
    // Small valid animations, independent of downloaded/example assets.
    private static string Json(int width = 32) =>
        "{\"v\":\"5.5.2\",\"fr\":30,\"ip\":0,\"op\":30,\"w\":" + width +
        ",\"h\":32,\"nm\":\"Winter\",\"ddd\":0,\"assets\":[],\"layers\":[]}";

    private static byte[] Gif(int frames = 2) => Convert.FromHexString(
        "47494638396101000100800000000000FFFFFF" +
        string.Concat(Enumerable.Repeat("21F90400010000002C0000000001000100000202440100", frames)) + "3B");

    private static ThorVG.Animation Animation(LottieImage image) =>
        Assert.IsType<ThorVG.Animation>(Field(image, "_animation"));

    private static object? Field(object value, string name) => value.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value);

    private static Picture Picture(Element image) => Assert.IsType<Picture>(Field(image, "_picture"));
    private static Scene OwnPaints(Element image) => Assert.IsType<Scene>(Assert.Single(image.Scene.Paints()));

    private sealed class FixtureFile : IDisposable
    {
        public string Name { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "winter-animation-" + Guid.NewGuid().ToString("N"));
        public FixtureFile(byte[] bytes) => File.WriteAllBytes(Name, bytes);
        public void Dispose() => File.Delete(Name);
    }

    public static IEnumerable<object[]> LottieTransitions()
    {
        foreach (bool attached in new[] { false, true })
            foreach (string transition in new[] { "bytes", "string", "path", "clear-bytes", "clear-string", "clear-path", "unavailable-path" })
                yield return [attached, transition];
    }

    // W2-03 reviewer scenario, extended to the other normal replacement/clear paths.
    [Theory]
    [MemberData(nameof(LottieTransitions))]
    public async Task LottieReplacementDisposesPreviousAnimationBeforeRuntimeShutdown(bool attached, string transition)
    {
        using var pump = new NativePump();
        using var file = new FixtureFile(Encoding.UTF8.GetBytes(Json(64)));
        await pump.Call(window =>
        {
            using var image = new LottieImage();
            if (attached) window.AddChild(image);
            image.LoadFromBytes(Encoding.UTF8.GetBytes(Json()));
            var old = Animation(image);
            var picture = old.GetPicture();
            Assert.Equal(attached ? 2 : 1, picture.RefCnt());
            try
            {
                switch (transition)
                {
                    case "bytes": image.LoadFromBytes(Encoding.UTF8.GetBytes(Json(64))); break;
                    case "string": image.Source = Json(64); break;
                    case "path": image.LoadFromPath(file.Name); break;
                    case "clear-bytes": image.LoadFromBytes(null); break;
                    case "clear-string": image.Source = null; break;
                    case "clear-path": image.LoadFromPath(null); break;
                    default: image.LoadFromPath(file.Name + ".missing"); break;
                }
                Assert.Equal(0, picture.RefCnt());
                if (transition.StartsWith("clear", StringComparison.Ordinal) || transition == "unavailable-path")
                {
                    Assert.Null(Field(image, "_animation"));
                    Assert.Empty(OwnPaints(image).Paints());
                    Assert.Equal(0, image.TotalFrames);
                    Assert.False(window.Animator.HasActiveAnimations);
                }
                else
                {
                    var current = Animation(image).GetPicture();
                    Assert.NotSame(picture, current);
                    Assert.Equal(attached ? 2 : 1, current.RefCnt());
                    Assert.Equal(64, image.Measure(100, 100).X);
                    image.Dispose();
                    Assert.Equal(0, current.RefCnt());
                }
            }
            finally { old.Dispose(); } // Baseline RED must also clean up inside the live session.
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LottieCurrentAnimationDisposalBalancesSceneAndAnimationOwners(bool attached)
    {
        using var pump = new NativePump();
        await pump.Call(window =>
        {
            using var image = new LottieImage { Source = Json() };
            if (attached) window.AddChild(image);
            var picture = Animation(image).GetPicture();
            Assert.Equal(attached ? 2 : 1, picture.RefCnt());
            image.Dispose();
            image.Dispose();
            Assert.Equal(0, picture.RefCnt());
            Assert.False(window.Animator.HasActiveAnimations);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LottieDetachRetainsAnimationAndReusesItWithDeclaredPlayback(bool playing)
    {
        using var pump = new NativePump();
        await pump.Call(window =>
        {
            using var image = new LottieImage { Source = Json(), IsPlaying = playing };
            window.AddChild(image);
            var animation = Animation(image);
            var picture = animation.GetPicture();
            Assert.Equal(2, picture.RefCnt());
            window.RemoveChild(image);
            Assert.Same(animation, Animation(image));
            Assert.Equal(2, picture.RefCnt());
            Assert.False(window.Animator.HasActiveAnimations);
            window.AddChild(image);
            Assert.Same(picture, Assert.Single(OwnPaints(image).Paints()));
            Assert.Equal(playing, window.Animator.HasActiveAnimations);
            image.Dispose();
            Assert.Equal(0, picture.RefCnt());
        });
    }

    public sealed class PlaybackApp : ComponentBase
    {
        [Parameter] public bool IsGif { get; set; }
        [Parameter] public string SourceName { get; set; } = "Source";
        [Parameter] public object Source { get; set; } = null!;
        [Parameter] public bool Playing { get; set; }
        public void Replace(object source, bool playing) { Source = source; Playing = playing; StateHasChanged(); }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent(0, IsGif ? typeof(B.GifImage) : typeof(B.LottieImage));
            builder.AddAttribute(1, SourceName, Source);
            builder.AddAttribute(2, "IsPlaying", Playing);
            builder.CloseComponent();
        }
    }

    public static IEnumerable<object[]> PlaybackTransitions()
    {
        foreach (string kind in new[] { "lottie-string", "lottie-bytes", "lottie-path", "gif-bytes", "gif-path" })
            foreach (bool before in new[] { false, true })
                foreach (bool after in new[] { false, true })
                    yield return [kind, before, after];
    }

    // W2-06 reviewer scenario uses the real wrapper and actual source-only diff.
    [Theory]
    [MemberData(nameof(PlaybackTransitions))]
    public async Task ReplacingBlazorAnimationKeepsDeclaredPlaybackState(string kind, bool before, bool after)
    {
        bool gif = kind.StartsWith("gif", StringComparison.Ordinal);
        byte[] first = gif ? Gif() : Encoding.UTF8.GetBytes(Json());
        byte[] second = gif ? Gif(3) : Encoding.UTF8.GetBytes(Json(64));
        using var firstFile = new FixtureFile(first);
        using var secondFile = new FixtureFile(second);
        string name = kind.EndsWith("path", StringComparison.Ordinal) ? "Path" : kind == "lottie-bytes" ? "SourceBytes" : "Source";
        object source = name == "Path" ? firstFile.Name : kind == "lottie-string" ? Json() : first;
        object replacement = name == "Path" ? secondFile.Name : kind == "lottie-string" ? Json(64) : second;
        using var pump = new NativePump();
        await pump.Call(window =>
        {
            var host = window.UseBlazor();
            var app = host.AddComponent<PlaybackApp>(new()
            {
                ["IsGif"] = gif, ["SourceName"] = name, ["Source"] = source, ["Playing"] = before,
            }).GetAwaiter().GetResult();
            var image = Assert.Single(window.Children);
            AssertPlayback(window, image, before);
            var old = image is LottieImage lottie ? Animation(lottie) : null;
            try
            {
                host.Renderer.Dispatcher.InvokeAsync(() => app.Replace(replacement, after)).GetAwaiter().GetResult();
                Assert.Same(image, Assert.Single(window.Children));
                AssertPlayback(window, image, after);
                Assert.Single(OwnPaints(image).Paints());
                Assert.Equal(gif ? 3 : 64, gif ? ((GifImage)image).FrameCount : image.Measure(100, 100).X);
                window.Animator.Tick(0.05f);
                float frame = image is LottieImage loaded ? loaded.CurrentFrame : ((GifImage)image).CurrentFrameIndex;
                Assert.Equal(after, frame > 0);
                var picture = Picture(image);
                host.Renderer.Dispatcher.InvokeAsync(() => app.Replace(replacement, after)).GetAwaiter().GetResult();
                Assert.Same(picture, Picture(image));
                Assert.Equal(frame, image is LottieImage unchanged ? unchanged.CurrentFrame : ((GifImage)image).CurrentFrameIndex);
            }
            finally { image.Dispose(); old?.Dispose(); }
        });
    }

    private static void AssertPlayback(Window window, Element image, bool expected)
    {
        Assert.Equal(expected, image is LottieImage lottie ? lottie.IsPlaying : ((GifImage)image).IsPlaying);
        Assert.Equal(expected, window.Animator.HasActiveAnimations);
    }

    public sealed class AttributeApp : NativeControlComponentBase
    {
        [Parameter] public bool IsGif { get; set; }
        [Parameter] public KeyValuePair<string, object>[] Attributes { get; set; } = [];
        public void Render(params KeyValuePair<string, object>[] attributes) { Attributes = attributes; StateHasChanged(); }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            var control = IsGif ? typeof(B.GifImage) : typeof(B.LottieImage);
            RuntimeHelpers.RunClassConstructor(control.TypeHandle);
            builder.OpenElement(0, control.FullName!);
            var attributes = new AttributesBuilder(builder);
            foreach (var (name, value) in Attributes)
            {
                if (value is byte[] bytes) attributes.AddAttribute(name, bytes);
                else if (value is bool boolean) attributes.AddAttribute(name, boolean);
                else attributes.AddAttribute(name, (string)value);
            }
            builder.CloseElement();
        }
    }

    private static KeyValuePair<string, object> A(string name, object value) => new(name, value);
    private static AttributeApp MountAttributes(Window window, bool gif, params KeyValuePair<string, object>[] attributes) =>
        window.UseBlazor().AddComponent<AttributeApp>(new() { ["IsGif"] = gif, ["Attributes"] = attributes }).GetAwaiter().GetResult();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SourceAndPlaybackRemainUnchangedUntilBatchCommit(bool gif, bool playbackFirst)
    {
        using var pump = new NativePump();
        await pump.Call(window =>
        {
            object initial = gif ? Gif() : Json();
            object replacement = gif ? Gif(3) : Json(64);
            var app = MountAttributes(window, gif, A("Source", initial), A("IsPlaying", false));
            var handler = Assert.IsAssignableFrom<WinterElementHandler>(app.ElementHandler);
            var image = handler.ElementControl;
            var old = image is LottieImage lottie ? Animation(lottie) : null;
            var picture = Picture(image);
            try
            {
                var attributes = new[] { A("Source", replacement), A("IsPlaying", true) };
                if (playbackFirst) Array.Reverse(attributes);
                foreach (var (name, value) in attributes)
                {
                    handler.ApplyAttribute(0, name, value is bool boolean ? boolean ? "1" : "0" : value, null!);
                    Assert.Same(picture, Picture(image));
                    AssertPlayback(window, image, false);
                }
                handler.CommitAttributes();
                Assert.NotSame(picture, Picture(image));
                AssertPlayback(window, image, true);
                var current = Picture(image);
                handler.CommitAttributes();
                Assert.Same(current, Picture(image));
            }
            finally { image.Dispose(); old?.Dispose(); }
        });
    }

    public static IEnumerable<object[]> SourceOrders()
    {
        yield return [true, new[] { "Source", "Path" }];
        yield return [true, new[] { "Path", "Source" }];
        foreach (var names in new[]
        {
            new[] { "Source", "SourceBytes", "Path" }, new[] { "Source", "Path", "SourceBytes" },
            new[] { "SourceBytes", "Source", "Path" }, new[] { "SourceBytes", "Path", "Source" },
            new[] { "Path", "Source", "SourceBytes" }, new[] { "Path", "SourceBytes", "Source" },
        }) yield return [false, names];
    }

    [Theory]
    [MemberData(nameof(SourceOrders))]
    public async Task SourcePrecedenceFallbackAndClearUseFinalBatchValues(bool gif, string[] order)
    {
        using var file = new FixtureFile(gif ? Gif(3) : Encoding.UTF8.GetBytes(Json(64)));
        using var pump = new NativePump();
        await pump.Call(window =>
        {
            var declared = new Dictionary<string, object>
            {
                ["Source"] = gif ? Gif() : Json(), ["SourceBytes"] = Encoding.UTF8.GetBytes(Json(48)), ["Path"] = file.Name,
            };
            KeyValuePair<string, object>[] Attributes(params string[] names) => names.Select(n => A(n, declared[n])).Append(A("IsPlaying", false)).ToArray();
            var app = MountAttributes(window, gif, Attributes(order));
            var image = Assert.Single(window.Children);
            var held = new List<ThorVG.Animation>();
            void Hold() { if (image is LottieImage lottie) held.Add(Animation(lottie)); }
            void Check(int expected)
            {
                Assert.Equal(expected, gif ? ((GifImage)image).FrameCount : image.Measure(100, 100).X);
                AssertPlayback(window, image, false);
                Assert.Single(OwnPaints(image).Paints());
            }
            try
            {
                Hold();
                Check(gif ? 3 : 64);
                var selected = Picture(image);
                // Changing a shadowed fallback must not reload the selected path.
                declared["Source"] = gif ? Gif(4) : Json(33);
                declared["SourceBytes"] = Encoding.UTF8.GetBytes(Json(49));
                app.Render(Attributes(order));
                Assert.Same(selected, Picture(image));
                var fallback = order.Where(n => n != "Path").ToArray();
                app.Render(Attributes(fallback));
                Hold();
                Check(gif ? 4 : 49);
                app.Render(Attributes("Source"));
                Hold();
                Check(gif ? 4 : 33);
                app.Render(A("IsPlaying", false));
                Assert.Empty(OwnPaints(image).Paints());
                Assert.Equal(0, gif ? ((GifImage)image).FrameCount : ((LottieImage)image).TotalFrames);
                Assert.False(window.Animator.HasActiveAnimations);
                app.Render(Attributes("Source"));
                Hold();
                Check(gif ? 4 : 33);
            }
            finally
            {
                image.Dispose();
                foreach (var animation in held) animation.Dispose();
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlaybackAttributeRemovalRestoresAutoPlayAndClearCancelsTicks(bool gif)
    {
        using var pump = new NativePump();
        await pump.Call(window =>
        {
            object source = gif ? Gif() : Json();
            var app = MountAttributes(window, gif, A("Source", source), A("IsPlaying", false));
            var image = Assert.Single(window.Children);
            var old = image is LottieImage lottie ? Animation(lottie) : null;
            try
            {
                app.Render(A("Source", source));
                AssertPlayback(window, image, true);
                app.Render();
                Assert.Empty(OwnPaints(image).Paints());
                Assert.False(window.Animator.HasActiveAnimations);
                app.Render(A("Source", source));
                AssertPlayback(window, image, true);
            }
            finally { image.Dispose(); old?.Dispose(); }
        });
    }
}
