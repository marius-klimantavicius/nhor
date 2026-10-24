using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

/// <summary>A nonphysical top-level menu in a MenuBar.</summary>
public class Menu : WinterComponentBase
{
    static Menu() => ElementHandlerRegistry.RegisterElementHandler<Menu>(r => new MenuContainerHandler(r, MenuContainerKind.Bar));
    internal static readonly Dictionary<Element, Marius.Winter.Menu> ElementToMenu = new();
    internal static readonly Dictionary<Element, MenuBinding> Bindings = new();
    [Parameter] public string? Title { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    protected override RenderFragment? GetChildContent() => ChildContent;
    protected override void RenderAttributes(AttributesBuilder builder)
    { base.RenderAttributes(builder); builder.AddAttribute("Title", Title ?? ""); }
}

internal enum MenuContainerKind { Bar, Context }
internal sealed record MenuBinding(Marius.Winter.Menu Menu, Func<Element?> Owner, Marius.Winter.Menu? ContextRoot = null);

internal sealed class MenuContainerHandler : WinterElementHandler, IIndexedNonPhysicalChild, IWinterContainerElementHandler
{
    private readonly MenuContainerKind _kind;
    private Marius.Winter.MenuBar? _bar;
    private Marius.Winter.Menu? _menu, _previousContext;
    private Element? _contextOwner;
    private string _title = "";
    public MenuContainerHandler(NativeComponentRenderer renderer, MenuContainerKind kind) : base(renderer, new Marius.Winter.Panel()) => _kind = kind;
    public override void ApplyAttribute(ulong id, string name, object value, string updates)
    {
        if (name is "Title" or "Label")
        {
            _title = AttributeHelper.GetString(value) ?? "";
            if (_menu != null) _bar?.UpdateMenu(_menu, _title);
        }
        else base.ApplyAttribute(id, name, value, updates);
    }
    public void SetParent(object parentElement)
    {
        Func<Element?> owner;
        if (_kind == MenuContainerKind.Bar && parentElement is Marius.Winter.MenuBar bar)
        { _bar = bar; _menu = bar.AddMenu(_title); owner = () => bar; }
        else if (_kind == MenuContainerKind.Context && parentElement is Element contextOwner)
        { _contextOwner = contextOwner; _previousContext = contextOwner.ContextMenu; _menu = new Marius.Winter.Menu(); contextOwner.ContextMenu = _menu; owner = () => contextOwner; }
        else throw new InvalidOperationException("Menu requires MenuBar; ContextMenu requires a physical owner.");
        Menu.ElementToMenu.Add(ElementControl, _menu);
        Menu.Bindings.Add(ElementControl, new MenuBinding(_menu, owner, _kind == MenuContainerKind.Context ? _menu : null));
    }
    public void Remove()
    {
        Menu.ElementToMenu.Remove(ElementControl); Menu.Bindings.Remove(ElementControl);
        if (_menu != null) _bar?.RemoveMenu(_menu);
        if (_contextOwner != null && ReferenceEquals(_contextOwner.ContextMenu, _menu)) _contextOwner.ContextMenu = _previousContext;
        _bar = null; _menu = _previousContext = null; _contextOwner = null;
    }
    public void MoveTo(int index)
    { if (_menu != null) _bar?.MoveMenu(_menu, index); }
    public override void Dispose() { if (IsDisposed) return; Remove(); base.Dispose(); }
    public void AddChild(Element child, int index) { }
    public void RemoveChild(Element child) { }
    public void ReorderChildren(List<Element> order) { }
    public int GetChildIndex(Element child) => -1;
}
