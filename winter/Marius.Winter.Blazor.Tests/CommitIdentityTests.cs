using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Xunit;
using B = Marius.Winter.Blazor.Elements;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class CommitIdentityTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (bool multiline in new[] { false, true })
        foreach (bool blur in new[] { false, true })
        foreach (string transition in new[] { "same", "replace", "clear", "unmount" })
            yield return new object[] { multiline, blur, transition };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CommitKeepsItsOriginatingRegistrationAcrossReentrantTextFlush(bool multiline, bool blur, string transition)
    {
        using var window = new Window(300, 160, "Commit identity", Theme.Light, RenderBackend.SW);
        using var renderer = new TestRenderer();
        var root = new Panel(); window.AddChild(root);
        InputBindingTests.Host host = null!;
        var calls = new List<string>();
        var first = EventCallback.Factory.Create<TextCommitEventArgs>(this, a => calls.Add("old:" + a.Reason + ":" + a.Text));
        var second = EventCallback.Factory.Create<TextCommitEventArgs>(this, a => calls.Add("new:" + a.Text));
        var current = first;
        EventCallback<string> changed = default;
        RenderFragment View(string text) => b =>
        {
            b.OpenComponent(0, multiline ? typeof(B.TextArea) : typeof(B.TextBox));
            b.AddAttribute(1, "Text", text);
            b.AddAttribute(2, "TextChanged", changed);
            b.AddAttribute(3, "TextCommitted", current);
            b.CloseComponent();
        };
        changed = EventCallback.Factory.Create<string>(this, text =>
        {
            calls.Add("edit:" + text);
            current = transition switch { "replace" => second, "clear" => default, _ => first };
            host.Render(transition == "unmount" ? b => { } : View(text));
        });
        host = await renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View("") });
        var input = Assert.Single(root.Children);
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            input.OnFocus();
            input.OnTextInput("first e\u0301");
            Assert.Empty(calls); // Real native owner defers the scalar notification.
            if (blur) input.OnBlur();
            else input.OnKeyDown(257, multiline ? 2 : 0, false);
            input.OnKeyDown(257, multiline ? 2 : 0, true);
            input.OnBlur(); // No duplicate commit after Enter or prior blur.
            while (window.DispatcherQueue.TryDequeue(out var action)) action();
        });
        Assert.Equal(transition == "same"
            ? new[] { "edit:first e\u0301", "old:" + (blur ? "Blur" : "Enter") + ":first e\u0301" }
            : new[] { "edit:first e\u0301" }, calls);
        if (transition == "unmount") Assert.Empty(root.Children);
    }
}
