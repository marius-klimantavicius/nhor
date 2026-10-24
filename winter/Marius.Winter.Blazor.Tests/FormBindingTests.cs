using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Xunit;

namespace Marius.Winter.Blazor.Tests;

public class FormBindingTests
{
    [Fact]
    public async Task AdapterCallbackPrecedenceReplacementClearAndProgrammaticSyncHaveNoEcho()
    {
        using var renderer = new TestRenderer(); using var root = new Panel();
        var standard = 0; var replacement = 0; var fallback = 0;
        var first = EventCallback.Factory.Create<string>(this, _ => standard++);
        var second = EventCallback.Factory.Create<string>(this, _ => replacement++);
        var legacy = EventCallback.Factory.Create<string>(this, _ => fallback++);
        RenderFragment View(string value, EventCallback<string> callback) => b =>
        {
            b.OpenComponent<Elements.TextField>(0); b.AddAttribute(1, "Value", value);
            b.AddAttribute(2, "ValueChanged", callback); b.AddAttribute(3, "OnValueChanged", legacy); b.CloseComponent();
        };
        var host = await renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View("MyApp", first) });
        var text = All(root).OfType<TextBox>().Single();
        await renderer.Dispatcher.InvokeAsync(() => text.OnTextInput("1")); Assert.Equal(1, standard); Assert.Equal(0, fallback);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View("Replacement", second)));
        Assert.Equal(0, replacement); Assert.Equal(0, fallback);
        await renderer.Dispatcher.InvokeAsync(() => text.OnTextInput("2")); Assert.Equal(1, replacement);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View("Fallback", default)));
        await renderer.Dispatcher.InvokeAsync(() => text.OnTextInput("3")); Assert.Equal(1, fallback);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(_ => { })); Assert.True(text.IsDisposed);
    }

    [Fact]
    public async Task CorrectingInvalidPathCharacterByCharacterKeepsInsertionPoint()
    {
        using var renderer = new TestRenderer(); using var root = new Panel();
        var probe = await renderer.AddComponent<FormBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var destination = All(root).OfType<TextBox>().Skip(1).First();
        await renderer.Dispatcher.InvokeAsync(() => { destination.OnKeyDown(65, 2, false); destination.OnTextInput("relative/path"); });
        await renderer.Dispatcher.InvokeAsync(() => probe.Form.SubmitAsync());
        await renderer.Dispatcher.InvokeAsync(() => destination.OnKeyDown(65, 2, false));
        string expected = "";
        foreach (var character in "/opt/myapp")
        {
            expected += character;
            await renderer.Dispatcher.InvokeAsync(() => destination.OnTextInput(character.ToString()));
            Assert.Equal(expected, destination.Text); Assert.Equal(expected, probe.Model.Destination);
            Assert.Equal(expected.Length, (int)typeof(TextBox).GetField("_cursorPos", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(destination)!);
        }
    }

    [Fact]
    public async Task RejectionReadOnlyCustomRulesAndStaleAsyncCallbacksPreserveReplacementIdentity()
    {
        using var renderer = new TestRenderer(); using var root = new Panel();
        var probe = await renderer.AddComponent<FormBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var name = All(root).OfType<TextBox>().First();
        await renderer.Dispatcher.InvokeAsync(() => { probe.RejectName = true; probe.Refresh(); name.OnTextInput("reject"); });
        Assert.Equal("MyApp", probe.Model.Name); Assert.Equal("MyApp", name.Text);
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            probe.RejectName = false; probe.ReadOnly = true; probe.CustomRule = true; probe.Refresh();
            name.OnKeyDown(65, 2, false); name.OnTextInput("Reserved");
        });
        Assert.Contains("Choose another application name.", probe.Form.CurrentEditContext.GetValidationMessages());
        var destination = All(root).OfType<TextBox>().Skip(1).First();
        Assert.True(destination.ReadOnly); destination.OnTextInput("blocked"); Assert.Equal("/opt/myapp", destination.Text);
        var before = probe.Form.CurrentEditContext; var oldModel = probe.Model;
        probe.ChangeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await renderer.Dispatcher.InvokeAsync(() => { name.OnKeyDown(65, 2, false); name.OnTextInput("pending"); });
        await renderer.Dispatcher.InvokeAsync(() => probe.Replace(true));
        var after = probe.Form.CurrentEditContext; probe.ChangeGate.SetResult();
        await renderer.Dispatcher.InvokeAsync(() => probe.Refresh());
        Assert.True(name.IsDisposed); name = All(root).OfType<TextBox>().First();
        Assert.Equal("Replacement", probe.Model.Name); Assert.Equal("Replacement", name.Text);
        Assert.False(after.IsModified()); Assert.Empty(after.GetValidationMessages());
        Assert.Empty(before.GetValidationMessages());
        probe.ResetGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task pendingReset = Task.CompletedTask;
        await renderer.Dispatcher.InvokeAsync(() => { pendingReset = probe.Form.ResetAsync(); });
        await renderer.Dispatcher.InvokeAsync(() => probe.Replace(true));
        var replacementContext = probe.Form.CurrentEditContext;
        Assert.True(name.IsDisposed); name = All(root).OfType<TextBox>().First();
        probe.ResetGate.SetResult(); await pendingReset;
        Assert.Same(replacementContext, probe.Form.CurrentEditContext); Assert.Equal("Replacement", probe.Model.Name);
        Assert.False(replacementContext.IsModified()); Assert.Equal("Replacement", name.Text);
    }

    [Fact]
    public async Task ConcurrentValidSubmitsAreGuardedAndUnmountDoesNotRevalidate()
    {
        using var renderer = new TestRenderer(); using var root = new Panel();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var calls = 0;
        var model = new FormBindingProbe.Configuration();
        var form = await renderer.AddComponent<Elements.WinterForm>(TestRenderer.CreateRootHandler(renderer, root), new()
        {
            ["Model"] = model,
            ["OnValidSubmit"] = Microsoft.AspNetCore.Components.EventCallback.Factory.Create<EditContext>(this, async _ => { calls++; await gate.Task; })
        });
        Task pending = Task.CompletedTask;
        await renderer.Dispatcher.InvokeAsync(() => { pending = form.SubmitAsync(); });
        await renderer.Dispatcher.InvokeAsync(() => form.SubmitAsync()); Assert.Equal(1, calls);
        var context = form.CurrentEditContext; renderer.Dispose(); gate.SetResult(); await pending;
        model.Name = ""; context.Validate(); Assert.Empty(context.GetValidationMessages());
    }

    internal static System.Collections.Generic.IEnumerable<Element> All(Element root) => root.Children.SelectMany(c => new[] { c }.Concat(All(c)));
    [Fact]
    public async Task CompiledBindingNotifiesEditContextValidatesSubmitsAndResets()
    {
        using var renderer = new TestRenderer(); using var root = new Panel();
        var probe = await renderer.AddComponent<FormBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var name = All(root).OfType<TextBox>().First(); var destination = All(root).OfType<TextBox>().Skip(1).First();
        var context = probe.Form.CurrentEditContext;
        await renderer.Dispatcher.InvokeAsync(() => { name.OnKeyDown(65, 2, false); name.OnKeyDown(259, 0, false); });
        Assert.Empty(probe.Model.Name); Assert.True(context.IsModified(new FieldIdentifier(probe.Model, "Name")));
        Assert.Contains("Enter an application name.", context.GetValidationMessages());
        await renderer.Dispatcher.InvokeAsync(() => probe.Form.SubmitAsync());
        Assert.Equal(0, probe.ValidCalls); Assert.Equal(1, probe.InvalidCalls);
        await renderer.Dispatcher.InvokeAsync(() => { name.OnTextInput("Žąsis 👩🏽‍💻"); destination.OnKeyDown(65, 2, false); destination.OnTextInput("relative/path"); });
        await renderer.Dispatcher.InvokeAsync(() => probe.Form.SubmitAsync());
        Assert.Equal(0, probe.ValidCalls); Assert.Equal(2, probe.InvalidCalls);
        await renderer.Dispatcher.InvokeAsync(() => { destination.OnKeyDown(65, 2, false); destination.OnTextInput("/opt/Žąsis"); });
        await renderer.Dispatcher.InvokeAsync(() => All(root).OfType<Select<FormBindingProbe.Choice, string>>().Single().SelectFromUser(1));
        await renderer.Dispatcher.InvokeAsync(() => All(root).OfType<Checkbox>().Single().OnClick());
        Assert.Equal("development", probe.Model.Environment); Assert.False(probe.Model.Shortcut);
        Assert.True(context.IsModified(new FieldIdentifier(probe.Model, "Environment")));
        await renderer.Dispatcher.InvokeAsync(() => probe.Form.SubmitAsync());
        Assert.Equal(1, probe.ValidCalls); Assert.Empty(context.GetValidationMessages());
        await renderer.Dispatcher.InvokeAsync(() => probe.Form.ResetAsync());
        Assert.Equal("MyApp", name.Text); Assert.Equal("/opt/myapp", destination.Text);
        Assert.False(context.IsModified()); Assert.Empty(context.GetValidationMessages());
        Assert.Contains(renderer.AttributeUpdates, a => a.Name == "Text" && Equals(a.Value, "MyApp"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacementAndUnmountDetachOldContextAndFieldSubscriptions(bool supplied)
    {
        using var renderer = new TestRenderer(); using var root = new Panel();
        var probe = await renderer.AddComponent<FormBindingProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var old = probe.Form.CurrentEditContext; var oldModel = probe.Model;
        await renderer.Dispatcher.InvokeAsync(() => probe.Replace(supplied));
        var current = probe.Form.CurrentEditContext;
        Assert.NotSame(old, current); Assert.Same(probe.Model, current.Model);
        oldModel.Name = ""; old.Validate(); Assert.Empty(old.GetValidationMessages()); Assert.Empty(current.GetValidationMessages());
        var removed = All(root).OfType<TextBox>().Skip(1).First();
        await renderer.Dispatcher.InvokeAsync(() => { probe.ShowDestination = false; probe.Refresh(); });
        Assert.True(removed.IsDisposed);
        current.NotifyFieldChanged(new FieldIdentifier(probe.Model, "Name"));
        renderer.Dispose(); probe.Model.Name = ""; current.Validate(); Assert.Empty(current.GetValidationMessages());
    }
}
