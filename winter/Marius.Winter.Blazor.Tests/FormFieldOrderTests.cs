using System;
using System.Linq;
using Xunit;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class FormFieldOrderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacingOnlyFirstFieldsCallbackDoesNotMoveItsInvalidFocusPriority(bool replace) => RichRunUnmountTests.OnOwner(() =>
    {
        using var window = new Window(500, 400, "C04 invalid focus order", Theme.Light, RenderBackend.SW);
        var host = window.UseBlazor(); var probe = host.AddComponent<FieldOrderProbe>().GetAwaiter().GetResult();
        var model = probe.Model; var context = probe.Form.CurrentEditContext;
        var fields = FormBindingTests.All(window).OfType<Field>().ToArray();
        var editors = fields.Select(f => f.Editor).ToArray();
        Assert.Equal(new[] { "First", "Second" }, fields.Select(f => f.Label));
        Assert.All(fields, field => Assert.Empty(((TextBox)field.Editor!).Text));
        if (replace) host.Renderer.Dispatcher.InvokeAsync(probe.ReplaceCallback).GetAwaiter().GetResult();
        var after = FormBindingTests.All(window).OfType<Field>().ToArray();
        Assert.Same(fields[0], after[0]); Assert.Same(fields[1], after[1]);
        Assert.Same(editors[0], after[0].Editor); Assert.Same(editors[1], after[1].Editor);
        Assert.Same(model, probe.Model); Assert.Same(context, probe.Form.CurrentEditContext);
        Assert.Equal(new[] { "First", "Second" }, after.Select(f => f.Label));
        host.Renderer.Dispatcher.InvokeAsync(() => probe.Form.SubmitAsync()).GetAwaiter().GetResult();
        Console.WriteLine($"replace={replace}, focused={fields.Single(f => ReferenceEquals(f.Editor, window.FocusedElement)).Label}");
        Assert.Same(editors[0], window.FocusedElement);
        Assert.Equal(2, context.GetValidationMessages().Count());
    });
}
