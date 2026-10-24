using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;
using Xunit;
using B = Marius.Winter.Blazor.Elements;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class OrdinaryBindingTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (bool production in new[] { false, true })
            for (int slot = 0; slot < 8; slot++) yield return new object[] { production, slot };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void CompiledBindingKeepsQueuedInputAcrossUnrelatedRenders(bool production, int slot) => RichRunUnmountTests.OnOwner(() =>
    {
        using var window = new Window(700, 450, "Ordinary compiled input bindings", Theme.Light, RenderBackend.SW);
        using var root = new Panel(); if (!production) window.AddChild(root);
        using var test = production ? null : new TestRenderer();
        var host = production ? window.UseBlazor() : null;
        NativeComponentRenderer renderer = production ? host!.Renderer : test!;
        var probe = production ? host!.AddComponent<OrdinaryBindingProbe>().GetAwaiter().GetResult() :
            test!.AddComponent<OrdinaryBindingProbe>(TestRenderer.CreateRootHandler(test, root)).GetAwaiter().GetResult();
        var controls = (production ? window.Children : root.Children).ToArray();
        Assert.All(probe.Calls, count => Assert.Equal(0, count));
        renderer.Dispatcher.InvokeAsync(() => UserEdit(controls[slot], slot)).GetAwaiter().GetResult();
        Assert.Equal(0, probe.Calls[slot]); // Real Window queue, not immediate headless delivery.
        renderer.Dispatcher.InvokeAsync(probe.RenderAgain).GetAwaiter().GetResult();
        renderer.Dispatcher.InvokeAsync(probe.RenderAgain).GetAwaiter().GetResult();
        Assert.Equal("Render 2", ((Label)controls[^1]).Text); // Actual changed parent diff.
        Assert.Equal(0, probe.Calls[slot]);
        renderer.Dispatcher.InvokeAsync(() => Drain(window)).GetAwaiter().GetResult();
        Console.WriteLine($"production={production} slot={slot} calls={probe.Calls[slot]}");
        switch (slot)
        {
            case 0: Assert.Equal("b", probe.Selected); Assert.Equal("b", ((Select<string, string>)controls[slot]).Value); break;
            case 1: Assert.Equal("b", probe.WithSetter); Assert.Equal("b", ((Select<string, string>)controls[slot]).Value); break;
            case 2: Assert.Equal(1, probe.Active); Assert.Equal(1, ((TabWidget)controls[slot]).ActiveTab); break;
            case 3: Assert.Equal("Note!", probe.Text); Assert.Equal("Note!", ((TextBox)controls[slot]).Text); break;
            case 4: Assert.True(probe.Checked); Assert.True(((Checkbox)controls[slot]).IsChecked); break;
            case 5: Assert.Equal(73, probe.Volume); Assert.Equal(73, ((Slider)controls[slot]).Value); break;
            case 6: Assert.Equal(1, probe.Index); Assert.Equal(1, ((ComboBox)controls[slot]).SelectedIndex); break;
            case 7: Assert.Equal("Note!", probe.Area); Assert.Equal("Note!", ((TextArea)controls[slot]).Text); break;
        }
        Assert.Equal(1, probe.Calls[slot]); Assert.Equal(1, probe.Calls.Sum());
        renderer.Dispatcher.InvokeAsync(probe.Reset).GetAwaiter().GetResult();
        renderer.Dispatcher.InvokeAsync(() => Drain(window)).GetAwaiter().GetResult();
        Assert.Equal("a", ((Select<string, string>)controls[0]).Value); Assert.Equal("a", ((Select<string, string>)controls[1]).Value);
        Assert.Equal(0, ((TabWidget)controls[2]).ActiveTab); Assert.Equal("Note", ((TextBox)controls[3]).Text);
        Assert.False(((Checkbox)controls[4]).IsChecked); Assert.Equal(40, ((Slider)controls[5]).Value);
        Assert.Equal(0, ((ComboBox)controls[6]).SelectedIndex); Assert.Equal("Note", ((TextArea)controls[7]).Text);
        Assert.Equal(1, probe.Calls.Sum()); // Programmatic reset does not echo a user edit.
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompiledResetSupersedesAllQueuedScalarInputs(bool production) => RichRunUnmountTests.OnOwner(() =>
    {
        using var window = new Window(700, 450, "Compiled reset identity", Theme.Light, RenderBackend.SW);
        using var root = new Panel(); if (!production) window.AddChild(root);
        using var test = production ? null : new TestRenderer(); var host = production ? window.UseBlazor() : null;
        NativeComponentRenderer renderer = production ? host!.Renderer : test!;
        var probe = production ? host!.AddComponent<OrdinaryBindingProbe>().GetAwaiter().GetResult() :
            test!.AddComponent<OrdinaryBindingProbe>(TestRenderer.CreateRootHandler(test, root)).GetAwaiter().GetResult();
        var controls = (production ? window.Children : root.Children).ToArray();
        renderer.Dispatcher.InvokeAsync(() => { for (int slot = 0; slot < 8; slot++) UserEdit(controls[slot], slot); }).GetAwaiter().GetResult();
        renderer.Dispatcher.InvokeAsync(probe.Reset).GetAwaiter().GetResult();
        renderer.Dispatcher.InvokeAsync(() => Drain(window)).GetAwaiter().GetResult();
        Assert.All(probe.Calls, count => Assert.Equal(0, count));
        Assert.Equal("a", ((Select<string, string>)controls[0]).Value); Assert.Equal("a", ((Select<string, string>)controls[1]).Value);
        Assert.Equal(0, ((TabWidget)controls[2]).ActiveTab); Assert.Equal("Note", ((TextBox)controls[3]).Text);
        Assert.False(((Checkbox)controls[4]).IsChecked); Assert.Equal(40, ((Slider)controls[5]).Value);
        Assert.Equal(0, ((ComboBox)controls[6]).SelectedIndex); Assert.Equal("Note", ((TextArea)controls[7]).Text);
    });

    [Theory]
    [InlineData(false, "equivalent")]
    [InlineData(false, "target")]
    [InlineData(false, "receiver")]
    [InlineData(false, "clear")]
    [InlineData(true, "equivalent")]
    [InlineData(true, "target")]
    [InlineData(true, "receiver")]
    [InlineData(true, "clear")]
    public void MethodTargetAndExplicitReceiverDefineSubscriptionIdentity(bool tabs, string transition) => RichRunUnmountTests.OnOwner(() =>
    {
        using var window = new Window(500, 300, "Explicit callback identity", Theme.Light, RenderBackend.SW);
        using var root = new Panel(); window.AddChild(root); using var renderer = new TestRenderer();
        var first = new Target(); var next = new Target(); var receiver = new Receiver(); var nextReceiver = new Receiver();
        RenderFragment View(bool replace) => b =>
        {
            var target = replace && transition == "target" ? next : first;
            var owner = replace && transition == "receiver" ? nextReceiver : receiver;
            EventCallback<int> callback = replace && transition == "clear" ? default : EventCallback.Factory.Create<int>(owner, target.Accept);
            if (tabs)
            {
                b.OpenComponent<B.TabWidget>(0); b.AddAttribute(1, "ActiveTab", 0); b.AddAttribute(2, "ActiveTabChanged", callback);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(c =>
                { c.OpenComponent<B.Tab>(0); c.AddAttribute(1, "Label", "First"); c.CloseComponent(); c.OpenComponent<B.Tab>(2); c.AddAttribute(3, "Label", "Second"); c.CloseComponent(); }));
            }
            else
            {
                b.OpenComponent<B.Select<int, int>>(0); b.AddAttribute(1, "Items", new[] { 0, 1 });
                b.AddAttribute(2, "TextSelector", (Func<int, string>)Text); b.AddAttribute(3, "ValueSelector", (Func<int, int>)Identity);
                b.AddAttribute(4, "Value", 0); b.AddAttribute(5, "ValueChanged", callback);
            }
            b.CloseComponent();
        };
        var host = renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View(false) }).GetAwaiter().GetResult();
        var control = root.Children[0];
        renderer.Dispatcher.InvokeAsync(() => { if (tabs) ((TabWidget)control).SelectFromUser(1); else ((ComboBox)control).SelectFromUser(1); }).GetAwaiter().GetResult();
        renderer.Dispatcher.InvokeAsync(() => host.Render(View(true))).GetAwaiter().GetResult();
        renderer.Dispatcher.InvokeAsync(() => Drain(window)).GetAwaiter().GetResult();
        Assert.Empty(next.Values); Assert.Equal(0, nextReceiver.Calls);
        if (transition == "equivalent") { Assert.Equal(new[] { 1 }, first.Values); Assert.Equal(1, receiver.Calls); }
        else { Assert.Empty(first.Values); Assert.Equal(0, receiver.Calls); }
    });

    private static string Text(int value) => value.ToString();
    private static int Identity(int value) => value;
    private sealed class Target { public List<int> Values { get; } = []; public void Accept(int value) => Values.Add(value); }
    private sealed class Receiver : IHandleEvent
    {
        public int Calls;
        public Task HandleEventAsync(EventCallbackWorkItem callback, object? arg) { Calls++; return callback.InvokeAsync(arg); }
    }
    private static void UserEdit(Element control, int slot)
    {
        switch (slot)
        {
            case 0: case 1: case 6: ((ComboBox)control).SelectFromUser(1); break;
            case 2: ((TabWidget)control).SelectFromUser(1); break;
            case 3: control.OnKeyDown(269, 0, false); control.OnTextInput("!"); break;
            case 4: control.OnClick(); break;
            case 5: ((Slider)control).SetValueFromUser(73); break;
            case 7: control.OnKeyDown(269, 2, false); control.OnTextInput("!"); break;
        }
    }
    private static void Drain(Window window)
    {
        int count = 0;
        while (window.DispatcherQueue.TryDequeue(out var action)) { Assert.True(++count < 1000); action(); }
    }
}
