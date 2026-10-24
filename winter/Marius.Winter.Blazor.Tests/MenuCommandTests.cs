using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;
using B = Marius.Winter.Blazor.Elements;

namespace Marius.Winter.Blazor.Tests;

public class MenuCommandTests
{
    [Fact]
    public async Task CheckedCallbacksUseAuthoritativeChangedThenLegacyFallbackAndSilentReset()
    {
        using var renderer = new TestRenderer(); using var root = new Panel(); int changed = 0, legacy = 0;
        var primary = EventCallback.Factory.Create<bool>(this, _ => changed++); var fallback = EventCallback.Factory.Create<bool>(this, _ => legacy++);
        RenderFragment View(bool conventional, bool state, long reset) => b =>
        {
            b.OpenComponent<B.MenuBar>(0); b.AddAttribute(1, "ChildContent", (RenderFragment)(m =>
            {
                m.OpenComponent<B.Menu>(0); m.AddAttribute(1, "Title", "Preview"); m.AddAttribute(2, "ChildContent", (RenderFragment)(i =>
                {
                    i.OpenComponent<B.MenuItem>(0); i.AddAttribute(1, "IsChecked", state); i.AddAttribute(2, "ResetVersion", reset);
                    i.AddAttribute(3, "IsCheckedChanged", conventional ? primary : default); i.AddAttribute(4, "OnCheckedChanged", fallback); i.CloseComponent();
                })); m.CloseComponent();
            })); b.CloseComponent();
        };
        var host = await renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = View(true, false, 0) });
        var item = ((MenuBar)root.Children.Single()).Menus[0].Entries[0];
        await renderer.Dispatcher.InvokeAsync(() => item.Action!.Invoke()); Assert.Equal(1, changed); Assert.Equal(0, legacy);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(false, true, 1))); Assert.True(item.IsChecked); Assert.Equal(0, legacy);
        await renderer.Dispatcher.InvokeAsync(() => item.Action!.Invoke()); Assert.Equal(1, changed); Assert.Equal(1, legacy);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(View(false, false, 2))); Assert.False(item.IsChecked); Assert.Equal(1, legacy);
    }
    [Fact]
    public async Task ParentCanRejectACheckAndContextRemovalRestoresBorrowedMenu()
    {
        using var renderer = new TestRenderer(); using var root = new Panel();
        var probe = await renderer.AddComponent<MenuCheckProbe>(TestRenderer.CreateRootHandler(renderer, root));
        var item = ((MenuBar)root.Children.Single()).Menus[0].Entries[0];
        await renderer.Dispatcher.InvokeAsync(() => item.Action!.Invoke());
        Assert.Equal(1, probe.Calls); Assert.False(item.IsChecked);
        int borrowedCalls = 0; var borrowed = new Menu().AddItem("Borrowed", () => borrowedCalls++); root.ContextMenu = borrowed;
        RenderFragment Context() => b => { b.OpenComponent<B.ContextMenu>(0); b.CloseComponent(); };
        var host = await renderer.AddComponent<InputBindingTests.Host>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Content"] = Context() });
        Assert.NotSame(borrowed, root.ContextMenu);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(_ => { })); Assert.Same(borrowed, root.ContextMenu);
        await renderer.Dispatcher.InvokeAsync(() => host.Render(Context()));
        var replacement = new Menu().AddItem("Replacement"); root.ContextMenu = replacement;
        await renderer.Dispatcher.InvokeAsync(() => host.Render(_ => { })); Assert.Same(replacement, root.ContextMenu);
        borrowed.Entries[0].Action!.Invoke(); Assert.Equal(1, borrowedCalls);
    }
    [Fact]
    public async Task CompiledCheckedBindingAndKeyedNestedDiffsUpdateActualDefinitions()
    {
        using var renderer = new TestRenderer(); using var root = new Panel(); int legacy = 0;
        var probe = await renderer.AddComponent<MenuProbe>(TestRenderer.CreateRootHandler(renderer, root), new() { ["Detailed"] = true, ["LegacyCheck"] = EventCallback.Factory.Create<bool>(this, _ => legacy++) });
        var bar = (MenuBar)root.Children.Single().Children.Last(); var menu = bar.Menus.Single();
        var detail = menu.Entries[1]; Assert.True(detail.IsChecked); Assert.Equal("Ctrl+D", detail.Shortcut); Assert.False(menu.Entries[0].Enabled);
        var zoom = menu.Entries[3]; var old = zoom.SubMenu!.Entries.ToArray(); var separator = menu.Entries[2];
        await renderer.Dispatcher.InvokeAsync(() => detail.Action?.Invoke());
        Assert.False(detail.IsChecked); Assert.False(probe.Detailed); Assert.Equal(0, legacy);
        await renderer.Dispatcher.InvokeAsync(() => probe.Update(true, true, ["150%", "175%", "125%"]));
        Assert.True(detail.IsChecked); Assert.True(menu.Entries[0].Enabled); Assert.Same(separator, menu.Entries[2]); Assert.Same(zoom, menu.Entries[3]);
        Assert.Equal(new[] { "150%", "175%", "125%" }, zoom.SubMenu.Entries.Select(e => e.Label));
        Assert.Same(old[2], zoom.SubMenu.Entries[0]); Assert.Same(old[1], zoom.SubMenu.Entries[2]); Assert.Null(old[0].Action);
        var button = root.Children.Single().Children.First(); var context = button.ContextMenu;
        Assert.NotNull(context); Assert.Equal(3, context.Entries.Count); Assert.Empty(button.Children);
        await renderer.Dispatcher.InvokeAsync(() => probe.Context(false)); Assert.Null(button.ContextMenu);
        await renderer.Dispatcher.InvokeAsync(() => probe.Context(true)); Assert.NotSame(context, button.ContextMenu); Assert.Null(context.Entries.FirstOrDefault()?.Action);
    }

    [Fact]
    public async Task DisabledDeclarativeCommandGuardsTheActualNativeAction()
    {
        using var renderer = new TestRenderer(); using var root = new Panel(); int calls = 0;
        var host = await renderer.AddComponent<MenuHost>(TestRenderer.CreateRootHandler(renderer, root), new Dictionary<string, object> { ["Click"] = (Action)(() => calls++) });
        var bar = Assert.IsType<MenuBar>(Assert.Single(root.Children)); var item = Assert.Single(Assert.Single(bar.Menus).Entries);
        await renderer.Dispatcher.InvokeAsync(() => item.Action?.Invoke());
        Assert.Equal(0, calls);
        await renderer.Dispatcher.InvokeAsync(() => host.SetEnabled(true));
        await renderer.Dispatcher.InvokeAsync(() => item.Action?.Invoke()); Assert.Equal(1, calls);
    }

    public class MenuHost : ComponentBase
    {
        [Parameter] public Action? Click { get; set; }
        private bool _enabled;
        public void SetEnabled(bool enabled) { _enabled = enabled; StateHasChanged(); }
        protected override void BuildRenderTree(RenderTreeBuilder b)
        {
            b.OpenComponent<B.MenuBar>(0); b.AddAttribute(1, "ChildContent", (RenderFragment)(m =>
            {
                m.OpenComponent<B.Menu>(0); m.AddAttribute(1, "Title", "Preview"); m.AddAttribute(2, "ChildContent", (RenderFragment)(i =>
                {
                    i.OpenComponent<B.MenuItem>(0); i.AddAttribute(1, "Label", "Save preview"); i.AddAttribute(2, "Enabled", _enabled);
                    i.AddAttribute(3, "OnClick", EventCallback.Factory.Create(this, () => Click?.Invoke())); i.CloseComponent();
                })); m.CloseComponent();
            })); b.CloseComponent();
        }
    }
}
