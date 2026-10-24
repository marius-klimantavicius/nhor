using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ThorVG;

namespace Marius.Winter;

/// <summary>A native composition factory. Owned output is disposed when its key is removed; borrowed output is detached.</summary>
public interface IItemElementFactory<TItem>
{
    bool OwnsContent { get; }
    Element Create(ItemSnapshot<TItem> item);
    void Update(Element content, ItemSnapshot<TItem> item);
}

public sealed class ItemElementFactory<TItem>(Func<TItem, Element> create, Action<Element, TItem> update, bool ownsContent = true) : IItemElementFactory<TItem>
{
    public bool OwnsContent => ownsContent;
    public Element Create(ItemSnapshot<TItem> item) => create(item.Item);
    public void Update(Element content, ItemSnapshot<TItem> item) => update(content, item.Item);
}

/// <summary>Bounded keyed rows. The caller owns Controller; the view owns row containers and factory output marked owned.</summary>
public class ListView<TItem> : Element
{
    private readonly Dictionary<string, ItemRow<TItem>> _rows = new(StringComparer.Ordinal);
    private readonly IItemElementFactory<TItem>? _factory;
    private ItemSnapshot<TItem>[] _rendered = [];
    private Element? _empty;
    private bool _ownsEmpty;
    public ItemController<TItem> Controller { get; private set; }
    public ScrollPanel Viewport { get; }
    public IReadOnlyList<ItemRow<TItem>> Rows => Controller.Items.Select(i => _rows[i.Key]).ToArray();
    public string? SelectedKey { get => Controller.SelectedKey; set => Controller.Select(value); }
    public TItem? SelectedItem => Controller.SelectedItem;
    public event Action<ItemActivation<TItem>>? Activated;
    public event Action<ItemSelection<TItem>>? UserSelectionChanged;
    /// <summary>Row containers have been reconciled; template hosts may now attach their keyed content.</summary>
    public event Action? RowsChanged;
    internal override bool ManagesOwnChildLayout => true;

    public ListView(ItemController<TItem> controller, IItemElementFactory<TItem>? factory = null)
    {
        Controller = controller ?? throw new ArgumentNullException(nameof(controller)); _factory = factory;
        Viewport = new ScrollPanel { Layout = new RowLayout(), HorizontalScroll = ScrollMode.Never, Padding = new Thickness(0) };
        AddChild(Viewport);
        Controller.StateChanged += Refresh;
        Controller.UserSelectionChanged += Selected;
        Controller.Activated += Activate;
        SetEmptyContent(new Label("No items"), true);
        Refresh();
    }
    public ItemRow<TItem>? FindRow(string key) => _rows.GetValueOrDefault(key);
    public void SetController(ItemController<TItem> controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        if (ReferenceEquals(Controller, controller)) return;
        Controller.StateChanged -= Refresh; Controller.UserSelectionChanged -= Selected; Controller.Activated -= Activate;
        Controller = controller;
        Controller.StateChanged += Refresh; Controller.UserSelectionChanged += Selected; Controller.Activated += Activate;
        Refresh();
    }
    public void FocusRow(string key)
    {
        if (_rows.TryGetValue(key, out var row) && row.IsActive) OwnerWindow?.SetFocus(row);
    }
    /// <summary>Detaches the previous empty element. Only explicitly owned empty elements are disposed.</summary>
    public void SetEmptyContent(Element? content, bool ownsContent = false)
    {
        if (ReferenceEquals(content, _empty)) return;
        if (content?.Parent != null) throw new InvalidOperationException("Empty content is already parented.");
        if (_empty != null) { Viewport.RemoveChild(_empty); if (_ownsEmpty) _empty.Dispose(); }
        _empty = content; _ownsEmpty = ownsContent;
        if (_empty != null) { Viewport.AddChild(_empty); _empty.Visible = Controller.Items.Count == 0; }
        InvalidateMeasure();
    }
    /// <summary>Attaches renderer/caller-owned reusable content to an existing row. Previous borrowed content is detached.</summary>
    public void SetRowContent(string key, Element? content, bool ownsContent = false)
    {
        if (!_rows.TryGetValue(key, out var row)) throw new ArgumentException("Unknown row key.", nameof(key));
        row.SetContent(content, ownsContent);
    }
    private void Selected(ItemSelection<TItem> value) => UserSelectionChanged?.Invoke(value);
    private void Activate(ItemActivation<TItem> value) => Activated?.Invoke(value);
    private void Refresh()
    {
        if (IsDisposed) return;
        var items = Controller.Items.ToArray();
        var keys = items.Select(i => i.Key).ToHashSet(StringComparer.Ordinal);
        var focused = OwnerWindow?.FocusedElement;
        int removedFocus = Array.FindIndex(_rendered, i => _rows.TryGetValue(i.Key, out var r) && Contains(r, focused) && !keys.Contains(i.Key));
        foreach (var key in _rows.Keys.Where(k => !keys.Contains(k)).ToArray())
        {
            var row = _rows[key]; _rows.Remove(key); row.Dispose();
        }
        foreach (var item in items)
        {
            if (!_rows.TryGetValue(item.Key, out var row))
            {
                row = new ItemRow<TItem>(this, item); _rows.Add(item.Key, row); Viewport.AddChild(row);
                if (_factory != null) row.SetContent(_factory.Create(item), _factory.OwnsContent);
            }
            else if (row.Item != item)
            {
                row.Item = item;
                if (_factory != null && row.Content != null) _factory.Update(row.Content, item);
            }
            row.UpdateAppearance();
        }
        var order = items.Select(i => (Element)_rows[i.Key]).ToList();
        if (_empty != null) { _empty.Visible = items.Length == 0; order.Add(_empty); }
        Viewport.ReorderChildren(order);
        _rendered = items;
        RowsChanged?.Invoke();
        if (removedFocus >= 0 && items.Length > 0) FocusRow(items[Math.Min(removedFocus, items.Length - 1)].Key);
        InvalidateMeasure();
    }
    private static bool Contains(Element parent, Element? child)
    {
        for (var e = child; e != null; e = e.Parent) if (ReferenceEquals(e, parent)) return true;
        return false;
    }
    internal void Navigate(ItemRow<TItem> current, int key)
    {
        var items = Controller.Items; int index = -1;
        for (int i = 0; i < items.Count; i++) if (items[i].Key == current.Item.Key) { index = i; break; }
        if (index < 0) return;
        int target = key switch { 265 => Math.Max(0, index - 1), 264 => Math.Min(items.Count - 1, index + 1), 268 => 0, 269 => items.Count - 1, _ => index };
        string targetKey = items[target].Key;
        Controller.Select(targetKey, true); FocusRow(targetKey); EnsureVisible(targetKey);
    }
    public void EnsureVisible(string key)
    {
        if (!_rows.TryGetValue(key, out var row)) return;
        float total = Viewport.Children.Where(c => c.Visible).Sum(c => c.Bounds.H);
        float extent = Math.Max(0, total - Viewport.Bounds.H);
        if (extent <= 0) { Viewport.Scroll = 0; return; }
        float offset = extent * Viewport.Scroll;
        float top = row.Bounds.Y + offset;
        if (top < offset) Viewport.Scroll = top / extent;
        else if (top + row.Bounds.H > offset + Viewport.Bounds.H) Viewport.Scroll = (top + row.Bounds.H - Viewport.Bounds.H) / extent;
    }
    protected override Vector2 MeasureCore(float width, float height)
    {
        Viewport.Measure(width, height);
        return new(float.IsFinite(width) ? width : 240, float.IsFinite(height) ? height : 240);
    }
    protected override void ArrangeCore(RectF bounds) => Viewport.Arrange(new RectF(0, 0, Math.Max(0, bounds.W), Math.Max(0, bounds.H)));
    public override void Dispose()
    {
        if (IsDisposed) return;
        Controller.StateChanged -= Refresh; Controller.UserSelectionChanged -= Selected; Controller.Activated -= Activate;
        UserSelectionChanged = null; Activated = null; RowsChanged = null;
        if (_empty != null && !_ownsEmpty) Viewport.RemoveChild(_empty);
        base.Dispose(); _rows.Clear();
    }
    private sealed class RowLayout : ILayout
    {
        public Vector2 Measure(Element parent, float w, float h)
        {
            float height = 0;
            foreach (var child in parent.Children.Where(c => c.Visible)) height += child.Measure(Math.Max(0, w), float.PositiveInfinity).Y;
            return new(Math.Max(0, w), height);
        }
        public void Arrange(Element parent, RectF bounds)
        {
            // ScrollPanel refreshes its style during attachment, before it has
            // a viewport. Wait for actual geometry before arranging templates.
            if (bounds.W <= 0 || bounds.H <= 0) return;
            float y = bounds.Y;
            foreach (var child in parent.Children.Where(c => c.Visible))
            {
                float h = child.Measure(bounds.W, float.PositiveInfinity).Y;
                child.Arrange(new RectF(bounds.X, y, Math.Max(0, bounds.W), h)); y += h;
            }
        }
    }
}

/// <summary>A focusable row; only exact row focus handles navigation/activation. Interactive descendants keep their own input.</summary>
public sealed class ItemRow<TItem> : ItemContentHost
{
    private readonly ListView<TItem> _owner;
    private bool _ownsContent;
    public ItemSnapshot<TItem> Item { get; internal set; }
    public Element? Content { get; private set; }
    internal ItemRow(ListView<TItem> owner, ItemSnapshot<TItem> item)
    {
        _owner = owner; Item = item; Focusable = true;
        Padding = new Thickness(5); Layout = ItemSlotLayout.Instance;
    }
    internal void SetContent(Element? content, bool ownsContent)
    {
        if (ReferenceEquals(Content, content)) return;
        if (content?.Parent != null) throw new InvalidOperationException("Row content is already parented.");
        if (Content != null) { RemoveChild(Content); if (_ownsContent) Content.Dispose(); }
        Content = content; _ownsContent = ownsContent;
        if (content != null) AddChild(content);
        InvalidateMeasure();
    }
    internal void UpdateAppearance()
    {
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        bool selected = _owner.SelectedKey == Item.Key;
        Background = selected ? theme.SelectionColor : new Color4(0, 0, 0, 0);
        var focus = Style.BorderFocused.A > 0 ? Style.BorderFocused : theme.TextBox.BorderFocused;
        BorderColor = (State & ElementState.Focused) != 0 ? focus : new Color4(0, 0, 0, 0);
    }
    protected override Vector2 MeasureCore(float w, float h)
    {
        var size = base.MeasureCore(w, h);
        return new(Math.Max(0, w), Math.Max(size.Y, Style.FontSize + 14));
    }
    public override Element? HitTest(float x, float y)
    {
        var hit = base.HitTest(x, y);
        if (hit == null) return null;
        // Passive labels/thumbnails select the row. A focusable descendant (or
        // a child inside one) retains click, editor and button semantics.
        for (var e = hit; e != null && e != this; e = e.Parent) if (e.Focusable || e is ScrollPanel) return hit;
        return this;
    }
    public override bool OnMouseDown(int button, float x, float y)
    {
        if (!Enabled || button != 0) return false;
        _owner.Controller.Select(Item.Key, true); return true;
    }
    public override bool OnMouseUp(int button, float x, float y) => button == 0;
    public override void OnDoubleClick()
    {
        if (Enabled) _owner.Controller.Activate(Item.Key, ItemActivationKind.DoubleClick);
    }
    public override void OnKeyDown(int key, int mods, bool repeat)
    {
        if (OwnerWindow?.FocusedElement != this) { base.OnKeyDown(key, mods, repeat); return; }
        if (key is 264 or 265 or 268 or 269) _owner.Navigate(this, key);
        else if (key == 257) _owner.Controller.Activate(Item.Key);
        else if (key == 32) _owner.Controller.Select(Item.Key, true);
        else base.OnKeyDown(key, mods, repeat);
    }
    public override void OnFocus() { UpdateAppearance(); _owner.EnsureVisible(Item.Key); }
    public override void OnBlur() => UpdateAppearance();
    protected override void OnStateChanged(ElementState oldState, ElementState newState) => UpdateAppearance();
    protected override void OnAttached() { base.OnAttached(); UpdateAppearance(); }
    protected override void OnThemeChanged() { base.OnThemeChanged(); UpdateAppearance(); }
    public override void Dispose()
    {
        if (IsDisposed) return;
        if (Content != null && !_ownsContent) RemoveChild(Content);
        Content = null; base.Dispose();
    }
}

/// <summary>A clipped native slot used by row and cell composition.</summary>
public class ItemContentHost : Panel
{
    private Shape? _clip;
    protected override void OnAttached()
    {
        base.OnAttached();
        if (_clip == null) { _clip = Shape.Gen(); Scene.Clip(_clip); }
        Clip();
    }
    protected override void OnPositionChanged() { base.OnPositionChanged(); Clip(); }
    protected override void OnSizeChanged() { base.OnSizeChanged(); Clip(); }
    private void Clip()
    {
        _clip?.ResetShape(); _clip?.AppendRect(Bounds.X, Bounds.Y, Math.Max(0, Bounds.W), Math.Max(0, Bounds.H), 0, 0);
        Scene.pImpl.Mark(RenderUpdateFlag.Clip | RenderUpdateFlag.Transform);
    }
}

/// <summary>Single roots fill a slot; multiple roots stack vertically and use measured logical text heights.</summary>
public sealed class ItemSlotLayout : ILayout
{
    public static ItemSlotLayout Instance { get; } = new();
    private readonly StackLayout _stack = new() { Spacing = 3, CrossAlignment = Alignment.Stretch };
    public Vector2 Measure(Element container, float width, float height) => _stack.Measure(container, width, height);
    public void Arrange(Element container, RectF bounds)
    {
        if (container.Children.Count != 1) { _stack.Arrange(container, bounds); return; }
        var p = container.Style.Padding;
        container.Children[0].Arrange(new RectF(bounds.X + p.Left, bounds.Y + p.Top, Math.Max(0, bounds.W - p.Left - p.Right), Math.Max(0, bounds.H - p.Top - p.Bottom)));
    }
}
