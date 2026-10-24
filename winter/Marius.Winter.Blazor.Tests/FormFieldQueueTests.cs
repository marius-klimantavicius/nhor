using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.AspNetCore.Components.Forms;
using Marius.Winter;
using Marius.Winter.Blazor;
using Marius.Winter.Blazor.Core;
using Xunit;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class FormFieldQueueTests
{
    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)] [InlineData(false, 3)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)] [InlineData(true, 3)]
    public void UnrelatedParentRenderPreservesQueuedFieldBinding(bool production, int slot) => Run(production, slot, false, true);

    public static IEnumerable<object[]> Controls()
    {
        foreach (bool production in new[] { false, true })
            for (int slot = 0; slot < 4; slot++)
                foreach (bool setter in new[] { false, true })
                    foreach (bool renders in new[] { false, true })
                        if (setter || !renders) yield return new object[] { production, slot, setter, renders };
    }

    [Theory]
    [MemberData(nameof(Controls))]
    public void CompiledSetterAndNoRenderControls(bool production, int slot, bool setter, bool renders) => Run(production, slot, setter, renders);

    private static void Run(bool production, int slot, bool setter, bool renders) => OnOwner(() =>
    {
        using var window = new Window(700, 500, "C04 independent queued field binding", Theme.Light, RenderBackend.SW);
        using var root = new Panel(); if (!production) window.AddChild(root);
        using var test = production ? null : new TestRenderer();
        var host = production ? window.UseBlazor() : null;
        NativeComponentRenderer renderer = production ? host!.Renderer : test!;
        FieldQueueProbe probe = setter
            ? production ? host!.AddComponent<FieldSetterQueueProbe>().GetAwaiter().GetResult() : test!.AddComponent<FieldSetterQueueProbe>(TestRenderer.CreateRootHandler(test, root)).GetAwaiter().GetResult()
            : production ? host!.AddComponent<FieldQueueProbe>().GetAwaiter().GetResult() : test!.AddComponent<FieldQueueProbe>(TestRenderer.CreateRootHandler(test, root)).GetAwaiter().GetResult();
        var surface = production ? (Element)window : root;
        var controls = All(surface).OfType<Field>().Select(f => f.Editor!).ToArray();
        renderer.Dispatcher.InvokeAsync(() =>
        {
            switch (slot)
            {
                case 0: controls[0].OnKeyDown(269, 0, false); controls[0].OnTextInput("!"); break;
                case 1: controls[1].OnKeyDown(269, 2, false); controls[1].OnTextInput("!"); break;
                case 2: ((ComboBox)controls[2]).SelectFromUser(1); break;
                case 3: controls[3].OnClick(); break;
            }
        }).GetAwaiter().GetResult();
        Assert.Equal(0, probe.Model.Calls[slot]);
        if (renders)
        {
            renderer.Dispatcher.InvokeAsync(probe.RenderAgain).GetAwaiter().GetResult();
            renderer.Dispatcher.InvokeAsync(probe.RenderAgain).GetAwaiter().GetResult();
            Assert.Contains(All(surface).OfType<Label>(), label => label.Text == "Render 2");
        }
        Assert.Equal(0, probe.Model.Calls[slot]);
        renderer.Dispatcher.InvokeAsync(() => Drain(window)).GetAwaiter().GetResult();
        Console.WriteLine($"production={production}, slot={slot}, calls={probe.Model.Calls[slot]}, model={probe.Model.Name}|{probe.Model.Biography}|{probe.Model.Environment}|{probe.Model.Shortcut}, native={((TextBox)controls[0]).Text}|{((TextArea)controls[1]).Text}|{((ComboBox)controls[2]).SelectedIndex}|{((Checkbox)controls[3]).IsChecked}, modified={probe.Form.CurrentEditContext.IsModified()}");
        Assert.Equal(1, probe.Model.Calls[slot]);
        Assert.Equal(1, probe.Model.Calls.Sum());
        Assert.True(probe.Form.CurrentEditContext.IsModified(new FieldIdentifier(probe.Model, new[] { "Name", "Biography", "Environment", "Shortcut" }[slot])));
        Assert.Equal(slot == 0 ? "Note!" : "Note", probe.Model.Name);
        Assert.Equal(probe.Model.Name, ((TextBox)controls[0]).Text);
        Assert.Equal(slot == 1 ? "Note!" : "Note", probe.Model.Biography);
        Assert.Equal(probe.Model.Biography, ((TextArea)controls[1]).Text);
        Assert.Equal(slot == 2 ? "b" : "a", probe.Model.Environment);
        Assert.Equal(probe.Model.Environment, ((Select<string, string>)controls[2]).Value);
        Assert.Equal(slot == 3, probe.Model.Shortcut);
        Assert.Equal(probe.Model.Shortcut, ((Checkbox)controls[3]).IsChecked);
    });
    static IEnumerable<Element> All(Element root) => root.Children.SelectMany(c => new[] { c }.Concat(All(c)));
    static void Drain(Window window) { int count = 0; while (window.DispatcherQueue.TryDequeue(out var a)) { Assert.True(++count < 1000); a(); } }
    static void OnOwner(Action a)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { a(); } catch (Exception e) { error = e; } }) { IsBackground = true };
        thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(20)));
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
