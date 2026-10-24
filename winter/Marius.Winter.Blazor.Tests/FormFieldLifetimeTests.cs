using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using Marius.Winter.Blazor.Core;
using Xunit;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class FormFieldLifetimeTests
{
    public static IEnumerable<object[]> Transitions()
    {
        foreach (bool production in new[] { false, true })
            foreach (string transition in new[] { "equivalent", "method", "target", "receiver", "clear", "fallback", "legacy", "reset", "form-reset", "model", "context", "expression", "unmount" })
                yield return new object[] { production, transition };
    }

    [Theory]
    [MemberData(nameof(Transitions))]
    public void QueuedEditsKeepOnlyTheirOriginatingSubscription(bool production, string transition) => Run(production, (window, renderer, probe, controls) =>
    {
        var model = probe.Model; var context = probe.Form.CurrentEditContext;
        probe.SetFallback(); // Paired callbacks are authoritative, even with a legacy fallback.
        renderer.Dispatcher.InvokeAsync(probe.Refresh).GetAwaiter().GetResult();
        renderer.Dispatcher.InvokeAsync(() => EditAll(controls)).GetAwaiter().GetResult();
        Assert.Equal(0, probe.Original.Calls);
        renderer.Dispatcher.InvokeAsync(() =>
        {
            switch (transition)
            {
                case "equivalent": probe.SetCallbacks(probe.Original, probe.FirstReceiver); break;
                case "method": probe.ReplaceMethods(); break;
                case "target": probe.SetCallbacks(probe.Replacement, probe.FirstReceiver); break;
                case "receiver": probe.SetCallbacks(probe.Original, probe.SecondReceiver); break;
                case "clear":
                    probe.SetCallbacks(null, probe.FirstReceiver);
                    probe.NameLegacy = default; probe.BiographyLegacy = default; probe.EnvironmentLegacy = default; probe.ShortcutLegacy = default;
                    break;
                case "fallback": probe.SetCallbacks(null, probe.FirstReceiver); break;
                case "legacy": probe.NameLegacy = default; probe.BiographyLegacy = default; probe.EnvironmentLegacy = default; probe.ShortcutLegacy = default; break;
                case "reset": probe.ResetVersion++; break;
                case "form-reset": probe.Form.ResetAsync().GetAwaiter().GetResult(); break;
                case "model": probe.Model = new(); break;
                case "context": probe.UseContext = true; probe.Context = new EditContext(model); break;
                case "expression": probe.OtherExpression = true; break;
                case "unmount": probe.Mounted = false; break;
            }
            probe.Refresh(); probe.Refresh();
        }).GetAwaiter().GetResult();
        renderer.Dispatcher.InvokeAsync(() => Drain(window)).GetAwaiter().GetResult();
        bool deliver = transition is "equivalent" or "legacy";
        Assert.Equal(deliver ? 4 : 0, probe.Original.Calls);
        Assert.Equal(0, probe.Replacement.Calls); Assert.Equal(0, probe.Fallback.Calls); Assert.Equal(0, probe.SecondReceiver.Calls);
        Assert.Equal(deliver ? 4 : 0, model.Calls.Sum());
        Assert.Equal(deliver, context.IsModified());
        Assert.Equal(deliver, probe.Form.CurrentEditContext.IsModified());
        Assert.Empty(probe.Form.CurrentEditContext.GetValidationMessages());
        if (transition is "reset" or "form-reset") AssertInitial(controls);
        if (transition is "model" or "context" or "unmount") Assert.All(controls, c => Assert.True(c.IsDisposed));
        if (transition is "method" or "target" or "receiver" or "fallback")
        {
            // A fresh edit reaches the new registration, never the discarded old edit.
            renderer.Dispatcher.InvokeAsync(() => { controls[0].OnKeyDown(269, 0, false); controls[0].OnTextInput("?"); }).GetAwaiter().GetResult();
            renderer.Dispatcher.InvokeAsync(() => Drain(window)).GetAwaiter().GetResult();
            Assert.Equal(transition is "method" or "receiver" ? 1 : 0, probe.Original.Calls);
            Assert.Equal(transition == "target" ? 1 : 0, probe.Replacement.Calls);
            Assert.Equal(transition == "fallback" ? 1 : 0, probe.Fallback.Calls);
            Assert.Equal(1, model.Calls.Sum());
            Assert.True(context.IsModified(new FieldIdentifier(model, nameof(model.Name))));
        }
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ParentRejectionCorrectsAllEditorsWithoutAnotherUserNotification(bool production) => Run(production, (window, renderer, probe, controls) =>
    {
        probe.Original.Reject = true;
        renderer.Dispatcher.InvokeAsync(() => EditAll(controls)).GetAwaiter().GetResult();
        renderer.Dispatcher.InvokeAsync(() => Drain(window)).GetAwaiter().GetResult();
        renderer.Dispatcher.InvokeAsync(probe.Refresh).GetAwaiter().GetResult();
        AssertInitial(controls); Assert.Equal(4, probe.Original.Calls);
        Assert.Equal(0, probe.Model.Calls.Sum()); Assert.True(probe.Form.CurrentEditContext.IsModified());
    });

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void AwaitingCallbacksCannotNotifyReplacementContextOrUnmountedFields(bool production, bool unmount) => Run(production, (window, renderer, probe, controls) =>
    {
        var previous = probe.Form.CurrentEditContext; var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int previousNotifications = 0, replacementNotifications = 0;
        previous.OnFieldChanged += (_, _) => previousNotifications++;
        probe.Original.Gate = gate;
        probe.Original.DuringCallback = () =>
        {
            if (unmount) probe.Mounted = false;
            else { probe.UseContext = true; probe.Context = new EditContext(probe.Model); }
            probe.Refresh();
        };
        renderer.Dispatcher.InvokeAsync(() => controls[0].OnTextInput("!")).GetAwaiter().GetResult();
        renderer.Dispatcher.InvokeAsync(() => Drain(window)).GetAwaiter().GetResult();
        Assert.Equal(1, probe.Original.Calls); Assert.All(controls, c => Assert.True(c.IsDisposed));
        // A render requested inside the handler is batched until it returns.
        // The delivered edit notifies its original field once, before the await.
        Assert.True(previous.IsModified()); Assert.Equal(1, previousNotifications);
        var current = probe.Form.CurrentEditContext;
        if (!unmount) { Assert.NotSame(previous, current); Assert.False(current.IsModified()); }
        current.OnFieldChanged += (_, _) => replacementNotifications++;
        gate.SetResult();
        Assert.True(probe.FirstReceiver.Pending.Wait(TimeSpan.FromSeconds(5)));
        renderer.Dispatcher.InvokeAsync(() => Drain(window)).GetAwaiter().GetResult();
        Assert.Equal(1, previousNotifications); Assert.Equal(0, replacementNotifications);
        if (!unmount) Assert.False(current.IsModified());
        Assert.Empty(probe.Form.CurrentEditContext.GetValidationMessages());
    });

    private static void Run(bool production, Action<Window, NativeComponentRenderer, FieldLifetimeProbe, Element[]> action) => RichRunUnmountTests.OnOwner(() =>
    {
        using var window = new Window(700, 500, "Form field subscription lifetime", Theme.Light, RenderBackend.SW);
        using var root = new Panel(); if (!production) window.AddChild(root);
        using var test = production ? null : new TestRenderer(); var host = production ? window.UseBlazor() : null;
        NativeComponentRenderer renderer = production ? host!.Renderer : test!;
        var probe = production ? host!.AddComponent<FieldLifetimeProbe>().GetAwaiter().GetResult() :
            test!.AddComponent<FieldLifetimeProbe>(TestRenderer.CreateRootHandler(test, root)).GetAwaiter().GetResult();
        var controls = FormBindingTests.All(production ? window : root).OfType<Field>().Select(f => f.Editor!).ToArray();
        action(window, renderer, probe, controls);
    });
    private static void EditAll(Element[] controls)
    {
        controls[0].OnKeyDown(269, 0, false); controls[0].OnTextInput("!");
        controls[1].OnKeyDown(269, 2, false); controls[1].OnTextInput("!");
        ((ComboBox)controls[2]).SelectFromUser(1); controls[3].OnClick();
    }
    private static void AssertInitial(Element[] controls)
    {
        Assert.Equal("Note", ((TextBox)controls[0]).Text); Assert.Equal("Note", ((TextArea)controls[1]).Text);
        Assert.Equal("a", ((Select<string, string>)controls[2]).Value); Assert.False(((Checkbox)controls[3]).IsChecked);
    }
    private static void Drain(Window window)
    {
        int count = 0; while (window.DispatcherQueue.TryDequeue(out var action)) { Assert.True(++count < 1000); action(); }
    }
}
