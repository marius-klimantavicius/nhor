using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Marius.Winter.Blazor;
using ThorVG;
using Xunit;

namespace Marius.Winter.Tests;

public class RichDocumentFirstFrameTests
{
    [Fact]
    public void ActualRazorNotesFirstFrameShowsBothCompleteLinesWithoutUnrequestedScrolling()
    {
        WindowRegressionOwner.Run(() =>
        {
            using var window = new Window(1000, 720, "Notes first frame", Theme.Light, RenderBackend.SW);
            var host = window.UseBlazor();
            var add = host.AddComponent<Marius.Winter.Blazor.Playground.Scenarios.Notes>(new Dictionary<string, object> { ["OwnerWindow"] = window });
            Assert.True(add.IsCompletedSuccessfully, "Component mount must complete on the owner before drawing.");
            WindowRegressionOwner.Flush(window);
            var editor = Walk(window).OfType<RichTextBox>().Single();
            Contracts.Draw((Canvas)typeof(Window).GetField("_canvas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!, true);
            var pixels = WindowRuntimeTests.Buffer(window);
            editor.WindowToLocal(0, 0, out var nx, out var ny);
            int left = (int)-nx + 8, top = (int)-ny + 8;
            bool Ink(uint c) => ((c >> 16) & 255) < 120 && ((c >> 8) & 255) < 120 && (c & 255) < 120;
            // R at the start of "Review", and K at the start of "Keep" must
            // appear in distinct ink bands at the editor's left padding.
            Assert.Contains(Enumerable.Range(top, 18).SelectMany(y => Enumerable.Range(left, 8).Select(x => pixels[y * 1000 + x])), Ink);
            Assert.Contains(Enumerable.Range(top + 22, 18).SelectMany(y => Enumerable.Range(left, 8).Select(x => pixels[y * 1000 + x])), Ink);
            Assert.Equal(0f, (float)typeof(RichTextBox).GetField("_scrollX", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(editor)!);
            Assert.Equal(0f, (float)typeof(RichTextBox).GetField("_scrollY", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(editor)!);
        });
    }
    private static IEnumerable<Element> Walk(Element element)
    {
        yield return element;
        foreach (var child in element.Children) foreach (var descendant in Walk(child)) yield return descendant;
    }
}
