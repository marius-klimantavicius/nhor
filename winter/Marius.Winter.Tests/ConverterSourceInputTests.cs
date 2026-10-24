using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Marius.Winter.Blazor;
using Xunit;
using static Marius.Winter.Tests.ConverterCorrectionTests;

namespace Marius.Winter.Tests;

public class ConverterSourceInputTests
{
    private const string Lottie = """
{"v":"5.7.4","fr":10,"ip":0,"op":2,"w":32,"h":32,"nm":"Fixture","ddd":0,"assets":[],"layers":[{"ddd":0,"ind":1,"ty":4,"nm":"Shape","sr":1,"ks":{"o":{"a":0,"k":100},"r":{"a":0,"k":0},"p":{"a":0,"k":[16,16,0]},"a":{"a":0,"k":[0,0,0]},"s":{"a":0,"k":[100,100,100]}},"shapes":[{"ty":"rc","p":{"a":0,"k":[0,0]},"s":{"a":0,"k":[20,20]},"r":{"a":0,"k":2}},{"ty":"fl","c":{"a":0,"k":[0.2,0.6,0.8,1]},"o":{"a":0,"k":100},"r":1}],"ip":0,"op":2,"st":0,"bm":0}]}
""";
    [Theory]
    [InlineData(RenderBackend.SW)] [InlineData(RenderBackend.GL)]
    public void ActualDraftAcceptanceKeepsPreviewKindSaveNameAndPixelsCoherent(RenderBackend backend) => OnOwner(() =>
    {
        var root = Path.Combine(Environment.GetEnvironmentVariable("WINTER_C07_EVIDENCE")!, "source-identity-" + backend); Directory.CreateDirectory(root);
        string Svg(string color) => $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"32\" height=\"32\"><rect width=\"32\" height=\"32\" fill=\"{color}\"/></svg>";
        var red = Path.Combine(root, "red.svg"); var blue = Path.Combine(root, "blue.svg"); var motion = Path.Combine(root, "motion.json");
        File.WriteAllText(red, Svg("#ff0000")); File.WriteAllText(blue, Svg("#0000ff")); File.WriteAllText(motion, Lottie);
        using var window = new Window(1000, 780, "C07 accepted source identity", Theme.Light, backend);
        var host = window.UseBlazor();
        var mounted = host.AddComponent<Blazor.Converter.App>(new() { ["OwnerWindow"] = window, ["WorkspaceDirectory"] = root, ["SettingsDirectory"] = Path.Combine(root, "settings") });
        Assert.True(mounted.IsCompletedSuccessfully); var app = mounted.Result;
        var input = Task.Run(async () =>
        {
            using var x = new FormX11(Xid(window));
            Button Button(string text) => Walk(window).OfType<Button>().Single(b => b.Text == text);
            TextBox Editor() => Walk(window).OfType<PathPicker>().Single().TextEditor;
            Task Status(string message) => Wait(window, () => Walk(window).OfType<Label>().Any(l => l.Text.Contains(message)), message);
            async Task Save(string name, bool overwrite = false)
            {
                await Click(window, x, () => Button("Save As…"));
                FilePicker Picker() => Walk(window).OfType<FilePicker>().Single(p => p.Dialog.IsOpen);
                await Wait(window, () => Picker().Controller!.State.LoadState == FilePickerLoadState.Ready, "source identity save listing");
                await window.Dispatcher.InvokeAsync(() => Assert.Equal(name, Picker().Controller!.State.FileName));
                await Click(window, x, () => Walk(Picker().Dialog.Footer!).OfType<Button>().Single(b => b.Text == "Save"));
                if (overwrite)
                {
                    await Wait(window, () => Picker().Controller!.State.Overwrite != null, "existing accepted source output confirmation");
                    await Click(window, x, () => Walk(Picker().Dialog.Footer!).OfType<Button>().Single(b => b.Text == "Overwrite"));
                }
                await Status("Saved to " + Path.Combine(root, name));
            }
            async Task OutputCleared()
            {
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.False(Button("Save As…").Enabled);
                    Assert.Contains(Walk(window).OfType<Label>(), l => l.Text == "Not yet converted");
                });
            }
            try
            {
                await Task.Delay(150); x.Focus();
                await Type(window, x, Editor, red); x.Key(0xff0d); await Status("Loaded red.svg");
                await Click(window, x, () => Button("Convert")); await Status("Converted to PNG"); await Save("red.png");
                Assert.Equal((255, 0, 0, 255), PngCenter(Path.Combine(root, "red.png")));

                await Type(window, x, Editor, blue); await OutputCleared();
                await window.Dispatcher.InvokeAsync(() => Assert.Contains("#ff0000", Walk(window).OfType<SvgImage>().Single().Svg));
                await Click(window, x, () => Button("Convert")); await Status("Converted to PNG");
                await window.Dispatcher.InvokeAsync(() => Assert.Contains("#0000ff", Walk(window).OfType<SvgImage>().Single().Svg));
                await Capture(window, x, "source-" + backend + "-blue-accepted-by-convert", 1000, 780);
                await Save("blue.png"); Assert.Equal((0, 0, 255, 255), PngCenter(Path.Combine(root, "blue.png")));

                // Explicit Enter accepts the same source identity and clears prior output.
                await Type(window, x, Editor, red); x.Key(0xff0d); await Status("Loaded red.svg");
                await Type(window, x, Editor, blue); x.Key(0xff0d); await Status("Loaded blue.svg"); await OutputCleared();
                await Click(window, x, () => Button("Convert")); await Status("Converted to PNG"); await Save("blue.png", overwrite: true);
                Assert.Equal((0, 0, 255, 255), PngCenter(Path.Combine(root, "blue.png")));

                // A valid cross-kind draft must load its preview and detected kind together.
                await Type(window, x, Editor, motion); await OutputCleared();
                await Click(window, x, () => Button("Convert")); await Status("Converted to GIF");
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.Empty(Walk(window).OfType<SvgImage>());
                    Assert.True(Walk(window).OfType<LottieImage>().Single().TotalFrames > 0);
                    Assert.Equal(motion, typeof(Blazor.Converter.App).GetField("_lottiePath", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app));
                    Assert.Equal(new PickerPath(motion), typeof(Blazor.Converter.App).GetField("_loadedPath", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(app));
                    Assert.Single(Walk(window).OfType<GifImage>());
                });
                await Capture(window, x, "source-" + backend + "-lottie-draft-accepted", 1000, 780);
                await Save("motion.gif"); Assert.Equal("GIF89a", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(root, "motion.gif")), 0, 6));

                var missing = Path.Combine(root, "retry.svg");
                await Type(window, x, Editor, missing); await Click(window, x, () => Button("Convert")); await Status("Read error:"); await OutputCleared();
                await window.Dispatcher.InvokeAsync(() =>
                {
                    Assert.Empty(Walk(window).OfType<SvgImage>()); Assert.Empty(Walk(window).OfType<LottieImage>());
                });
                File.WriteAllText(missing, Svg("#ff0000")); await Click(window, x, () => Button("Retry")); await Status("Loaded retry.svg");
                await Click(window, x, () => Button("Convert")); await Status("Converted to PNG"); await Save("retry.png");
                Assert.Equal((255, 0, 0, 255), PngCenter(Path.Combine(root, "retry.png")));
                await Click(window, x, () => Button("Reload source")); await Status("Loaded retry.svg"); await OutputCleared();
            }
            finally { x.CloseNormally(); }
        });
        window.Run(); input.GetAwaiter().GetResult(); Assert.True(window.IsDisposed); Assert.True(host.DisposeAsync().IsCompletedSuccessfully);
    });

    // Decode the saved PNG independently of Winter's image/preview controls.
    internal static (int R, int G, int B, int A) PngCenter(string path)
    {
        using var file = File.OpenRead(path); using var reader = new BinaryReader(file);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, reader.ReadBytes(8));
        using var compressed = new MemoryStream(); int width = 0, height = 0;
        while (file.Position < file.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4)); var type = Encoding.ASCII.GetString(reader.ReadBytes(4));
            var data = reader.ReadBytes(length); Assert.Equal(length, data.Length); reader.ReadBytes(4);
            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(data); height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4));
                Assert.Equal(8, data[8]); Assert.Equal(6, data[9]); Assert.Equal(0, data[12]);
            }
            if (type == "IDAT") compressed.Write(data);
        }
        Assert.Equal(32, width); Assert.Equal(32, height); compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress); var stride = width * 4;
        var raw = new byte[height * (stride + 1)]; zlib.ReadExactly(raw);
        var pixels = new byte[height * stride];
        for (int y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)]; Assert.InRange(filter, (byte)0, (byte)4);
            for (int x = 0; x < stride; x++)
            {
                int left = x >= 4 ? pixels[y * stride + x - 4] : 0;
                int up = y > 0 ? pixels[(y - 1) * stride + x] : 0;
                int corner = y > 0 && x >= 4 ? pixels[(y - 1) * stride + x - 4] : 0;
                int p = left + up - corner, a = Math.Abs(p - left), b = Math.Abs(p - up), c = Math.Abs(p - corner);
                var predict = filter switch { 0 => 0, 1 => left, 2 => up, 3 => (left + up) / 2, _ => a <= b && a <= c ? left : b <= c ? up : corner };
                pixels[y * stride + x] = unchecked((byte)(raw[y * (stride + 1) + x + 1] + predict));
            }
        }
        var center = height / 2 * stride + width / 2 * 4;
        return (pixels[center], pixels[center + 1], pixels[center + 2], pixels[center + 3]);
    }
}
