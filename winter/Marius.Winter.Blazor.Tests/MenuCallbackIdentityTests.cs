using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Marius.Winter.Blazor.Tests;

[Collection("Native rendering")]
public class MenuCallbackIdentityTests
{
    private sealed class Receiver : IHandleEvent
    {
        public Task HandleEventAsync(EventCallbackWorkItem callback, object? arg) => callback.InvokeAsync(arg);
    }
    private sealed class Model
    {
        public List<bool> Checks = new();
        public int Clicks;
        public void Check(bool value) => Checks.Add(value);
        public void OtherCheck(bool value) => Checks.Add(value);
        public void Click() => Clicks++;
        public void OtherClick() => Clicks++;
    }

    [Theory]
    [InlineData(false, "same")] [InlineData(true, "same")]
    [InlineData(false, "target")] [InlineData(true, "target")]
    [InlineData(false, "receiver")] [InlineData(true, "receiver")]
    [InlineData(false, "method")] [InlineData(true, "method")]
    [InlineData(false, "clear")] [InlineData(true, "clear")]
    [InlineData(false, "target-back")] [InlineData(true, "target-back")]
    [InlineData(false, "receiver-back")] [InlineData(true, "receiver-back")]
    [InlineData(false, "method-back")] [InlineData(true, "method-back")]
    [InlineData(false, "clear-back")] [InlineData(true, "clear-back")]
    public async Task ExplicitReceiverCallbacksPreserveEquivalenceButInvalidateRealSubscriptionChanges(bool checkedCallback, string change)
    {
        using var window = new Window(400, 250, "C09 explicit callback subscriptions");
        using var renderer = new TestRenderer(); var root = new Panel(); window.AddChild(root);
        var model = new Model(); var other = new Model(); var receiver = new Receiver(); var otherReceiver = new Receiver();
        var transition = change.Replace("-back", "");
        Dictionary<string, object> Parameters(bool replacement) => new()
        {
            ["Check"] = checkedCallback ? (replacement && transition == "clear" ? default(EventCallback<bool>) : EventCallback.Factory.Create<bool>(
                replacement && transition == "receiver" ? otherReceiver : receiver,
                replacement && transition == "target" ? other.Check : replacement && transition == "method" ? model.OtherCheck : model.Check)) : default(EventCallback<bool>),
            ["Click"] = !checkedCallback && replacement && transition == "clear" ? default(EventCallback) : EventCallback.Factory.Create(
                !checkedCallback && replacement && transition == "receiver" ? otherReceiver : receiver,
                !checkedCallback && replacement && transition == "target" ? other.Click : !checkedCallback && replacement && transition == "method" ? model.OtherClick : model.Click)
        };
        var probe = await renderer.AddComponent<MenuCallbackIdentityProbe>(TestRenderer.CreateRootHandler(renderer, root), Parameters(false));
        var item = ((MenuBar)root.Children.Single()).Menus[0].Entries[0];
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            item.Action!(); item.Action!();
            await probe.SetParametersAsync(ParameterView.FromDictionary(Parameters(true).ToDictionary(p => p.Key, p => (object?)p.Value)));
            if (change.EndsWith("-back", StringComparison.Ordinal)) await probe.SetParametersAsync(ParameterView.FromDictionary(Parameters(false).ToDictionary(p => p.Key, p => (object?)p.Value)));
            while (window.DispatcherQueue.TryDequeue(out var action)) action();
        });
        Assert.Equal(change == "same" ? 2 : 0, model.Clicks);
        Assert.Equal(checkedCallback && change == "same" ? new[] { true, false } : Array.Empty<bool>(), model.Checks);
        Assert.Equal(0, other.Clicks); Assert.Empty(other.Checks);
    }
}
