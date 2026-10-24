using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Marius.Winter;

/// <summary>A named bounded column. Width shrinks to MinimumWidth; Stretch receives spare space. Overflow is clipped.</summary>
public sealed record TableColumn<TItem>(string Key, string Header, float Width = 140, float MinimumWidth = 50, float Stretch = 0,
    Func<TItem, string>? Format = null, IItemElementFactory<TItem>? Factory = null, IComparer<TItem>? Comparer = null);

/// <summary>Native table composition over the same keyed collection/selection as ListView. Sorting is stable and mutates Controller order.</summary>
public sealed class BasicTable<TItem> : Element
{
    private TableColumn<TItem>[] _columns = [];
    private readonly Panel _headers;
    private readonly CellFactory _factory;
    public ItemController<TItem> Controller => Body.Controller;
    public ListView<TItem> Body { get; }
    public IReadOnlyList<TableColumn<TItem>> Columns => Array.AsReadOnly(_columns);
    public TableSort? Sort { get; private set; }
    public event Action<TableSort>? SortChanged;
    public string? SelectedKey { get => Controller.SelectedKey; set => Controller.Select(value); }
    public TItem? SelectedItem => Controller.SelectedItem;
    internal override bool ManagesOwnChildLayout => true;
    /// <param name="templated">True when the renderer supplies row/cell slots rather than native factories.</param>
    public BasicTable(ItemController<TItem> controller, IEnumerable<TableColumn<TItem>>? columns = null, bool templated = false)
    {
        ArgumentNullException.ThrowIfNull(controller);
        _factory = new CellFactory(this);
        Body = new ListView<TItem>(controller, templated ? null : _factory);
        _headers = new ItemContentHost { Padding = new Thickness(5), Layout = new TableCellsLayout<TItem>(() => _columns) };
        AddChild(_headers); AddChild(Body);
        SetColumns(columns ?? []);
    }
    public void SetColumns(IEnumerable<TableColumn<TItem>> columns)
    {
        var next = columns.ToArray(); var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in next)
            if (string.IsNullOrEmpty(c.Key) || !keys.Add(c.Key) || string.IsNullOrEmpty(c.Header) || !float.IsFinite(c.Width) ||
                !float.IsFinite(c.MinimumWidth) || !float.IsFinite(c.Stretch) || c.MinimumWidth < 0 || c.Width < c.MinimumWidth || c.Stretch < 0)
                throw new ArgumentException("Columns require unique nonempty names/headers and finite nonnegative width/stretch, with Width >= MinimumWidth.", nameof(columns));
        if (_columns.SequenceEqual(next)) return;
        _columns = next;
        foreach (var child in _headers.Children.ToArray()) child.Dispose();
        foreach (var column in _columns)
        {
            Element header;
            if (column.Comparer != null)
            {
                string key = column.Key;
                header = new Button(column.Header) { Clicked = () => ToggleSort(key), Tooltip = "Sort by " + column.Header };
            }
            else header = new Label(column.Header);
            _headers.AddChild(new ItemContentHost { Padding = new Thickness(3), Layout = ItemSlotLayout.Instance });
            _headers.Children[^1].AddChild(header);
        }
        if (Sort != null && !_columns.Any(c => c.Key == Sort.Column && c.Comparer != null)) Sort = null;
        _factory.Rebuild(); UpdateHeaderText(); InvalidateMeasure();
    }
    public void ToggleSort(string key)
    {
        var column = _columns.FirstOrDefault(c => c.Key == key);
        if (column?.Comparer == null) return;
        var next = new TableSort(key, Sort?.Column == key && Sort.Direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending);
        ApplySort(next); SortChanged?.Invoke(next);
    }
    /// <summary>Programmatic sort does not echo SortChanged. Null clears the indicator and retains current data order.</summary>
    public void ApplySort(TableSort? sort)
    {
        if (sort != null)
        {
            var column = _columns.FirstOrDefault(c => c.Key == sort.Column);
            if (column?.Comparer == null) throw new ArgumentException("Sort requires a sortable named column.", nameof(sort));
            Controller.Sort(column.Comparer, sort.Direction);
        }
        Sort = sort; UpdateHeaderText();
    }
    private void UpdateHeaderText()
    {
        for (int i = 0; i < _columns.Length; i++)
            if (_headers.Children[i].Children[0] is Button button)
                button.Text = _columns[i].Header + (Sort?.Column == _columns[i].Key ? Sort.Direction == SortDirection.Ascending ? " ^" : " v" : "");
    }
    protected override Vector2 MeasureCore(float width, float height)
    {
        var header = _headers.Measure(Math.Max(0, width - 10), height);
        Body.Measure(width, Math.Max(0, height - header.Y));
        return new(float.IsFinite(width) ? width : 400, float.IsFinite(height) ? height : 260);
    }
    protected override void ArrangeCore(RectF bounds)
    {
        float header = _headers.Measure(Math.Max(0, bounds.W - 10), float.PositiveInfinity).Y;
        _headers.Arrange(new RectF(0, 0, Math.Max(0, bounds.W - 10), header));
        Body.Arrange(new RectF(0, header, Math.Max(0, bounds.W), Math.Max(0, bounds.H - header)));
    }
    public override void Dispose() { SortChanged = null; base.Dispose(); }

    private sealed class CellFactory(BasicTable<TItem> owner) : IItemElementFactory<TItem>
    {
        public bool OwnsContent => true;
        public Element Create(ItemSnapshot<TItem> item)
        {
            var panel = new NativeCells(owner); panel.SetItem(item); return panel;
        }
        public void Update(Element content, ItemSnapshot<TItem> item) => ((NativeCells)content).SetItem(item);
        internal void Rebuild()
        {
            foreach (var row in owner.Body.Rows)
                if (row.Content is NativeCells cells) cells.SetItem(row.Item, true);
        }
    }
    private sealed class NativeCells(BasicTable<TItem> owner) : Panel
    {
        private readonly List<(TableColumn<TItem> Column, ItemContentHost Host, Element Content)> _cells = [];
        private ItemSnapshot<TItem>? _item;
        internal void SetItem(ItemSnapshot<TItem> item, bool rebuild = false)
        {
            if (_item == null || rebuild)
            {
                Release(); Layout = new TableCellsLayout<TItem>(() => owner._columns); Padding = new Thickness(0);
                foreach (var column in owner._columns)
                {
                    var host = new ItemContentHost { Layout = ItemSlotLayout.Instance, Padding = new Thickness(3) };
                    var content = column.Factory?.Create(item) ?? new Label(column.Format?.Invoke(item.Item) ?? item.Item?.ToString() ?? "");
                    host.AddChild(content); AddChild(host); _cells.Add((column, host, content));
                }
            }
            else foreach (var cell in _cells)
            {
                if (cell.Column.Factory != null) cell.Column.Factory.Update(cell.Content, item);
                else ((Label)cell.Content).Text = cell.Column.Format?.Invoke(item.Item) ?? item.Item?.ToString() ?? "";
            }
            _item = item; InvalidateMeasure();
        }
        private void Release()
        {
            foreach (var cell in _cells)
            {
                if (cell.Column.Factory is { OwnsContent: false }) cell.Host.RemoveChild(cell.Content);
                cell.Host.Dispose();
            }
            _cells.Clear();
        }
        public override void Dispose() { if (IsDisposed) return; Release(); base.Dispose(); }
    }
}

/// <summary>Shared header/cell layout: shrink proportionally to minima, distribute spare width by stretch, clip beyond available width.</summary>
public sealed class TableCellsLayout<TItem>(Func<IReadOnlyList<TableColumn<TItem>>> columns) : ILayout
{
    public float[] Widths(float available)
    {
        var cols = columns(); var widths = cols.Select(c => c.Width).ToArray();
        float spare = Math.Max(0, available) - widths.Sum();
        if (spare > 0)
        {
            float stretch = cols.Sum(c => c.Stretch);
            if (stretch > 0) for (int i = 0; i < widths.Length; i++) widths[i] += spare * cols[i].Stretch / stretch;
        }
        else if (spare < 0)
        {
            float shrink = cols.Select(c => c.Width - c.MinimumWidth).Sum();
            if (shrink > 0) for (int i = 0; i < widths.Length; i++) widths[i] -= Math.Min(-spare, shrink) * (cols[i].Width - cols[i].MinimumWidth) / shrink;
        }
        return widths;
    }
    public Vector2 Measure(Element parent, float w, float h)
    {
        var padding = parent.Style.Padding;
        var widths = Widths(w - padding.HorizontalTotal); float height = 0;
        for (int i = 0; i < Math.Min(widths.Length, parent.Children.Count); i++)
            height = Math.Max(height, parent.Children[i].Measure(widths[i], h).Y);
        return new(Math.Max(0, w), height + padding.VerticalTotal);
    }
    public void Arrange(Element parent, RectF bounds)
    {
        var p = parent.Style.Padding; var widths = Widths(bounds.W - p.HorizontalTotal); float x = bounds.X + p.Left;
        for (int i = 0; i < Math.Min(widths.Length, parent.Children.Count); i++)
        {
            parent.Children[i].Arrange(new RectF(x, bounds.Y + p.Top, widths[i], Math.Max(0, bounds.H - p.VerticalTotal))); x += widths[i];
        }
    }
}
