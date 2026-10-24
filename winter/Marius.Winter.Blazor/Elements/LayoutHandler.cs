using System;
using System.Collections.Generic;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

internal abstract class LayoutHandler<TLayout> : WinterElementHandler, INonPhysicalChild
    where TLayout : ILayout, new()
{
    protected readonly TLayout Layout = new();
    private ILayoutContainer? _parentContainer;
    private bool _layoutChanged;

    protected LayoutHandler(NativeComponentRenderer renderer)
        : base(renderer, new Marius.Winter.Panel()) { }

    protected void SetLayoutValue<T>(T current, T value, Action<T> setter)
    {
        if (EqualityComparer<T>.Default.Equals(current, value)) return;
        setter(value);
        _layoutChanged = true;
    }

    public override void CommitAttributes()
    {
        if (!_layoutChanged || IsDisposed) return;
        _layoutChanged = false;
        // Reassign through the real container's notification contract once per
        // batch. The handler's placeholder Panel does not own this layout.
        if (_parentContainer != null && ReferenceEquals(_parentContainer.Layout, Layout))
            _parentContainer.Layout = Layout;
    }

    public void SetParent(object parentElement)
    {
        _parentContainer = parentElement as ILayoutContainer;
        if (_parentContainer != null) _parentContainer.Layout = Layout;
        _layoutChanged = false;
    }

    public void Remove()
    {
        var parent = _parentContainer;
        _parentContainer = null;
        _layoutChanged = false;
        if (parent != null && ReferenceEquals(parent.Layout, Layout)) parent.Layout = null;
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        Remove();
        base.Dispose();
    }
}
