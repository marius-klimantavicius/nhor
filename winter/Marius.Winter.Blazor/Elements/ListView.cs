using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

/// <summary>A bounded, single-select native list with keyed Razor row templates.</summary>
public class ListView<TItem> : WinterComponentBase
{
    static ListView() => ElementHandlerRegistry.RegisterElementHandler<ListView<TItem>>(r => new Handler(r, false));
    [Parameter] public IEnumerable<TItem>? Items { get; set; }
    [Parameter, EditorRequired] public Func<TItem, string> KeySelector { get; set; } = null!;
    [Parameter] public string? SelectedKey { get; set; }
    [Parameter] public EventCallback<string?> SelectedKeyChanged { get; set; }
    [Parameter] public EventCallback<string?> OnSelectionChanged { get; set; }
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }
    [Parameter] public RenderFragment? EmptyContent { get; set; }
    [Parameter] public EventCallback<ItemActivation<TItem>> OnActivate { get; set; }
    /// <summary>Increment for an explicit reset, including a same-value reset while callbacks await.</summary>
    [Parameter] public long ResetVersion { get; set; }
    private TItem[] _renderItems = [];
    private readonly EventCallback<ChangeEventArgs> _delivery;
    public ListView() => _delivery = EventCallback.Factory.Create<ChangeEventArgs>(this, Accept);
    public Marius.Winter.ListView<TItem>? NativeView => (ElementHandler as Handler)?.List;
    public TItem? SelectedItem => NativeView is { } view ? view.SelectedItem : default;
    public void FocusRow(string key) => NativeView?.FocusRow(key);
    protected virtual TableColumn<TItem>[] GetColumns() => [];
    protected virtual TableSort? CurrentSort { get => null; set { } }
    protected virtual EventCallback<TableSort?> SortCallback => default;

    private sealed record Callbacks(EventCallback<string?> Selection, EventCallback<ItemActivation<TItem>> Activate, EventCallback<TableSort?> Sort)
    {
        private readonly object? _selection = Identity(Selection), _activation = Identity(Activate), _sort = Identity(Sort);
        internal bool Same(Callbacks other) => Equals(_selection, other._selection) && Equals(_activation, other._activation) && Equals(_sort, other._sort);
    }
    private static object? Identity<T>(EventCallback<T> callback)
    {
        if (!callback.HasDelegate) return null;
        using var builder = new RenderTreeBuilder();
        builder.OpenElement(0, "item-action"); builder.AddAttribute(1, "onaction", callback); builder.CloseElement();
        return builder.GetFrames().Array[1].AttributeValue;
    }
    private enum ActionKind { Select, Activate, Sort }
    private sealed record Delivery(ActionKind Kind, object Value, Callbacks Callbacks, Func<bool> IsCurrent, Action Acknowledge);
    private sealed record Configuration(Dictionary<string, TItem> Source, TItem[] Ordered, Func<TItem, string> Key, string? Selected, long Reset,
        TableColumn<TItem>[] Columns, TableSort? Sort, Callbacks Callbacks);

    private Task Accept(ChangeEventArgs args)
    {
        var delivery = (Delivery)args.Value!;
        if (!delivery.IsCurrent()) return Task.CompletedTask;
        // Establish the accepted value before application code can render. Its
        // feedback is then distinguishable from an authoritative parent change.
        delivery.Acknowledge();
        // No continuation writes component state or starts another application callback.
        switch (delivery.Kind)
        {
            case ActionKind.Select:
                SelectedKey = ((ItemSelection<TItem>)delivery.Value).Key;
                return delivery.Callbacks.Selection.InvokeAsync(SelectedKey);
            case ActionKind.Activate:
                return delivery.Callbacks.Activate.InvokeAsync((ItemActivation<TItem>)delivery.Value);
            default:
                CurrentSort = (TableSort)delivery.Value;
                return delivery.Callbacks.Sort.InvokeAsync(CurrentSort);
        }
    }
    protected override void RenderAttributes(AttributesBuilder builder)
    {
        base.RenderAttributes(builder);
        ArgumentNullException.ThrowIfNull(KeySelector);
        var source = Items?.ToArray() ?? [];
        var keyed = new Dictionary<string, TItem>(StringComparer.Ordinal);
        foreach (var item in source)
        {
            var key = KeySelector(item);
            if (string.IsNullOrEmpty(key) || !keyed.TryAdd(key, item)) throw new ArgumentException("List item keys must be nonempty and unique.", nameof(Items));
        }
        var columns = GetColumns();
        _renderItems = source;
        if (CurrentSort is { } sort)
        {
            var column = columns.FirstOrDefault(c => c.Key == sort.Column);
            if (column?.Comparer == null) throw new ArgumentException("Sort requires a sortable named column.");
            _renderItems = (sort.Direction == SortDirection.Ascending
                ? source.OrderBy(i => i, column.Comparer) : source.OrderByDescending(i => i, column.Comparer)).ToArray();
        }
        var callbacks = new Callbacks(SelectedKeyChanged.HasDelegate ? SelectedKeyChanged : OnSelectionChanged, OnActivate, SortCallback);
        builder.AddAttribute("Configuration", new Configuration(keyed, _renderItems, KeySelector, SelectedKey, ResetVersion, columns, CurrentSort, callbacks));
        builder.AddAttribute("ondelivery", _delivery);
    }
    protected virtual RenderFragment RenderItem(TItem item) => ItemTemplate?.Invoke(item) ?? (b =>
    {
        b.OpenComponent<Label>(0); b.AddAttribute(1, nameof(Label.Text), item?.ToString() ?? ""); b.CloseComponent();
    });
    protected override RenderFragment GetChildContent() => builder =>
    {
        foreach (var item in _renderItems)
        {
            var key = KeySelector(item);
            builder.OpenComponent<ItemRowSlot<TItem>>(0); builder.SetKey(key);
            builder.AddAttribute(1, nameof(ItemRowSlot<TItem>.Key), key);
            builder.AddAttribute(2, nameof(ItemRowSlot<TItem>.ChildContent), RenderItem(item)); builder.CloseComponent();
        }
        builder.OpenComponent<ItemRowSlot<TItem>>(3); builder.SetKey(EmptyKey);
        builder.AddAttribute(4, nameof(ItemRowSlot<TItem>.ChildContent), EmptyContent ?? (b =>
        { b.OpenComponent<Label>(0); b.AddAttribute(1, nameof(Label.Text), "No items"); b.CloseComponent(); })); builder.CloseComponent();
    };
    private static readonly object EmptyKey = new();

    protected class Handler : WinterElementHandler
    {
        internal Marius.Winter.ListView<TItem> List => ElementControl is Marius.Winter.BasicTable<TItem> table ? table.Body : (Marius.Winter.ListView<TItem>)ElementControl;
        private Marius.Winter.BasicTable<TItem>? Table => ElementControl as Marius.Winter.BasicTable<TItem>;
        private Configuration? _configuration, _pending;
        private ulong _deliveryId;
        private long _generation, _subscriptions, _selectionGeneration, _sortGeneration;
        private int _pendingSelection, _pendingSort;
        public Handler(NativeComponentRenderer renderer, bool table) : base(renderer, table
            ? new Marius.Winter.BasicTable<TItem>(new ItemController<TItem>(_ => ""), templated: true)
            : new Marius.Winter.ListView<TItem>(new ItemController<TItem>(_ => "")))
        {
            List.UserSelectionChanged += Selected; List.Activated += Activated;
            if (Table != null) Table.SortChanged += Sorted;
        }
        private void Selected(ItemSelection<TItem> selection) => Queue(ActionKind.Select, selection, selection.Key);
        private void Activated(ItemActivation<TItem> activation) => Queue(ActionKind.Activate, activation, activation.Key);
        private void Sorted(TableSort sort) => Queue(ActionKind.Sort, sort, null);
        private void Queue(ActionKind kind, object value, string? key)
        {
            if (IsDisposed || _deliveryId == 0 || _configuration == null) return;
            var id = _deliveryId; var generation = _generation; var subscription = _subscriptions;
            var selectionGeneration = _selectionGeneration; var sortGeneration = _sortGeneration;
            var controller = List.Controller; var row = key == null ? null : controller.Find(key);
            var sortColumn = value is TableSort sort ? Table?.Columns.FirstOrDefault(c => c.Key == sort.Column) : null;
            bool PendingCurrent() => generation == _generation &&
                (kind != ActionKind.Select || selectionGeneration == _selectionGeneration) &&
                (kind != ActionKind.Sort || sortGeneration == _sortGeneration);
            bool Current() => !IsDisposed && id == _deliveryId && PendingCurrent() && subscription == _subscriptions &&
                ReferenceEquals(controller, List.Controller) && (key == null || controller.Find(key) == row) &&
                (kind != ActionKind.Sort || (sortColumn != null && Table!.Columns.Contains(sortColumn)));
            var delivery = new Delivery(kind, value, _configuration.Callbacks, Current, () => Acknowledge(kind, value));
            if (kind == ActionKind.Select) _pendingSelection++;
            if (kind == ActionKind.Sort) _pendingSort++;
            void Deliver()
            {
                if (IsDisposed || !PendingCurrent()) return;
                if (kind == ActionKind.Select) _pendingSelection--;
                if (kind == ActionKind.Sort) _pendingSort--;
                if (Current()) _ = Renderer.Dispatcher.InvokeAsync(() => Renderer.DispatchNativeEventAsync(id, null, new ChangeEventArgs { Value = delivery }));
                else Synchronize();
            }
            if (List.OwnerWindow is { } window) window.DispatcherQueue.Enqueue(Deliver); else Deliver();
        }
        private void Acknowledge(ActionKind kind, object value)
        {
            if (_configuration is not { } current) return;
            _configuration = kind switch
            {
                ActionKind.Select => current with { Selected = ((ItemSelection<TItem>)value).Key },
                ActionKind.Sort => current with { Sort = (TableSort)value },
                _ => current
            };
        }
        public override void ApplyAttribute(ulong id, string name, object value, string updates)
        {
            if (name == "Configuration") _pending = WeakObjectStore.Get<Configuration>(value);
            else if (name == "ondelivery") { RegisterEvent(id, old => { if (_deliveryId == old) _deliveryId = 0; }); _deliveryId = id; }
            else base.ApplyAttribute(id, name, value, updates);
        }
        public override void CommitAttributes()
        {
            if (IsDisposed || _pending is not { } next) return;
            _pending = null; var previous = _configuration;
            bool replaced = previous == null || previous.Key != next.Key;
            // A source-order change (including accepted sort feedback) retains
            // key/item identities. Insert/remove/update still invalidate actions.
            bool dataChanged = previous == null || previous.Source.Count != next.Source.Count ||
                previous.Source.Any(pair => !next.Source.TryGetValue(pair.Key, out var item) || !EqualityComparer<TItem>.Default.Equals(pair.Value, item));
            bool reset = replaced || dataChanged || previous!.Reset != next.Reset;
            if (previous == null || !previous.Callbacks.Same(next.Callbacks)) _subscriptions++;
            if (reset) { _generation++; _pendingSelection = _pendingSort = 0; }
            // A parent scalar override leaves independent activation/other-state
            // actions valid. Accepted callback feedback was acknowledged above.
            if (previous != null && previous.Selected != next.Selected) { _selectionGeneration++; _pendingSelection = 0; }
            if (previous != null && previous.Sort != next.Sort) { _sortGeneration++; _pendingSort = 0; }
            if (previous != null && previous.Reset != next.Reset) List.Viewport.Scroll = 0;
            if (replaced) List.SetController(new ItemController<TItem>(next.Key));
            _configuration = next;
            Table?.SetColumns(next.Columns);
            Synchronize();
        }
        private void Synchronize()
        {
            if (IsDisposed || _configuration is not { } next) return;
            List.Controller.Configure(_pendingSort > 0 ? List.Controller.Items.Select(i => i.Item) : next.Ordered,
                _pendingSelection > 0 ? List.SelectedKey : next.Selected);
            if (Table != null && _pendingSort == 0) Table.ApplySort(next.Sort);
        }
        public override void Dispose()
        {
            if (IsDisposed) return;
            _generation++; List.UserSelectionChanged -= Selected; List.Activated -= Activated;
            if (Table != null) Table.SortChanged -= Sorted;
            base.Dispose();
        }
    }
}

// Nonphysical slots attach reusable renderer-owned content through the native row API.
// The native view owns the rows; the renderer owns these templates and their subscriptions.
internal sealed class ItemRowSlot<TItem> : WinterComponentBase
{
    static ItemRowSlot() => ElementHandlerRegistry.RegisterElementHandler<ItemRowSlot<TItem>>(r => new Handler(r));
    [Parameter] public string? Key { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    protected override void RenderAttributes(AttributesBuilder builder) => builder.AddAttribute(nameof(Key), Key ?? "");
    protected override RenderFragment? GetChildContent() => ChildContent;
    private sealed class Handler(NativeComponentRenderer renderer) : WinterElementHandler(renderer,
        new ItemContentHost { Layout = ItemSlotLayout.Instance, Padding = new Thickness(0) }), INonPhysicalChild, IWinterContainerElementHandler
    {
        private Marius.Winter.ListView<TItem>? _owner;
        private string? _key;
        public override void ApplyAttribute(ulong id, string name, object value, string updates)
        {
            if (name == nameof(Key)) _key = value as string;
            else base.ApplyAttribute(id, name, value, updates);
        }
        public void SetParent(object parent)
        {
            _owner = parent is Marius.Winter.BasicTable<TItem> table ? table.Body : parent as Marius.Winter.ListView<TItem>
                ?? throw new InvalidOperationException("An item slot requires its keyed list/table parent.");
            _owner.RowsChanged += Attach;
            Attach();
        }
        private void Attach()
        {
            if (_owner == null || IsDisposed) return;
            if (string.IsNullOrEmpty(_key)) _owner.SetEmptyContent(ElementControl);
            else if (_owner.FindRow(_key) != null) _owner.SetRowContent(_key, ElementControl);
        }
        public void Remove()
        {
            if (_owner == null) return;
            _owner.RowsChanged -= Attach;
            if (string.IsNullOrEmpty(_key)) { if (ElementControl.Parent == _owner.Viewport) _owner.SetEmptyContent(null); }
            else if (_owner.FindRow(_key)?.Content == ElementControl) _owner.SetRowContent(_key, null);
            _owner = null;
        }
        public void AddChild(Element child, int index) => ElementControl.InsertChild(child, Math.Min(index, ElementControl.Children.Count));
        public void RemoveChild(Element child) => ElementControl.RemoveChild(child);
        public int GetChildIndex(Element child)
        {
            for (int i = 0; i < ElementControl.Children.Count; i++) if (ReferenceEquals(ElementControl.Children[i], child)) return i;
            return -1;
        }
        public void ReorderChildren(List<Element> order) => ElementControl.ReorderChildren(order);
        public override void Dispose() { Remove(); base.Dispose(); }
    }
}
