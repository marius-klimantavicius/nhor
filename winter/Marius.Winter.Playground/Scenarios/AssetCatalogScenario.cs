using System;
using System.Linq;
using System.Threading.Tasks;
using Marius.Winter.Scenarios;

namespace Marius.Winter.Playground.Scenarios;

/// <summary>Bounded catalog: reusable native row/cell factories, shared selection, real sorting and caller-owned filtering.</summary>
public sealed class AssetCatalogScenario : IDisposable
{
    private readonly Window _window;
    private readonly ItemController<CatalogAsset> _items = new(AssetCatalogData.Key);
    private readonly TextBox _filter;
    private readonly Label _details = new() { TextWrapping = ThorVG.TextWrap.Word };
    private readonly Label _result = new() { TextWrapping = ThorVG.TextWrap.Word };
    private readonly ListView<CatalogAsset> _list;
    private readonly BasicTable<CatalogAsset> _table;
    private CatalogAsset[] _all = [];
    private int _insert;
    private bool _disposed;
    public Element Content { get; }

    public AssetCatalogScenario(Window window)
    {
        _window = window;
        var content = new Panel { Layout = new FlexLayout { Direction = Orientation.Vertical, Gap = 7 }, Padding = new Thickness(8) };
        Content = content;
        content.AddChild(new Label("Asset catalog") { FontSize = 22 });
        content.AddChild(new Label("Select a row. Enter or double-click opens it; Preview is a separate action. Sort using table headers.") { TextWrapping = ThorVG.TextWrap.Word });
        var filterRow = Actions(); filterRow.AddChild(new Label("Filter"));
        _filter = new TextBox { Placeholder = "Name or type", LayoutData = new FlexItem { Grow = 1, Basis = 160 } };
        filterRow.AddChild(_filter); Add(filterRow, "Reset", Reset); content.AddChild(filterRow);
        var actions = Actions();
        Add(actions, "Insert", Insert); Add(actions, "Remove", Remove); Add(actions, "Reverse", Reverse);
        Add(actions, "Rename", Rename); Add(actions, "Update size", UpdateSize); content.AddChild(actions);
        var modes = Actions(); Add(modes, "List", () => ShowTable(false)); Add(modes, "Table", () => ShowTable(true)); content.AddChild(modes);
        _list = new ListView<CatalogAsset>(_items, new ItemElementFactory<CatalogAsset>(a => new AssetRow(a, Preview), (e,a) => ((AssetRow)e).Update(a))) { LayoutData = new FlexItem { Grow = 1, Basis = 0 }, MinHeight = 100 };
        _table = new BasicTable<CatalogAsset>(_items, [
            new("name", "Name", 230, 80, 1, Factory: new ItemElementFactory<CatalogAsset>(a => new AssetName(a), (e,a) => ((AssetName)e).Update(a)), Comparer: AssetCatalogData.NameComparer),
            new("type", "Type", 65, 45, Format: a => a.Type, Comparer: AssetCatalogData.TypeComparer),
            new("size", "KB", 70, 48, Format: a => a.Size.ToString(), Comparer: AssetCatalogData.SizeComparer),
            new("action", "Action", 95, 78, Factory: new ItemElementFactory<CatalogAsset>(a => new PreviewButton(a, Preview), (e,a) => ((PreviewButton)e).Update(a)))])
            { LayoutData = new FlexItem { Grow = 1, Basis = 0 }, Visible = false, MinHeight = 100 };
        _list.SetEmptyContent(new Label("No matching assets. Clear Filter or Reset."), true);
        _table.Body.SetEmptyContent(new Label("No matching assets. Clear Filter or Reset."), true);
        content.AddChild(_list); content.AddChild(_table); content.AddChild(_details); content.AddChild(_result);
        _items.SelectionChanged += Selected;
        _items.Activated += Activated;
        _table.SortChanged += Sorted;
        _filter.TextChanged = _ => Apply();
        Reset(); _window.Closing += Closing;
    }
    private static Panel Actions() => new() { Layout = new FlexLayout { Wrap = FlexWrap.Wrap, Gap = 5 }, Padding = new Thickness(0) };
    private static void Add(Panel row, string title, Action action) => row.AddChild(new Button(title) { Clicked = action });
    private void Selected(ItemSelection<CatalogAsset> selection) => _details.Text = AssetCatalogData.Details(selection.Item);
    private void Activated(ItemActivation<CatalogAsset> action) => _result.Text = "Opened: " + action.Item.Name + " · " + action.Key;
    private void Preview(CatalogAsset asset) => _result.Text = "Preview: " + asset.Name + " · " + asset.Key;
    private void Sorted(TableSort sort) => _result.Text = $"Sorted: {sort.Column} · {sort.Direction}";
    private void ShowTable(bool table) { _list.Visible = !table; _table.Visible = table; }
    private void Apply()
    {
        _items.SetItems(AssetCatalogData.Order(AssetCatalogData.Filter(_all, _filter.Text), _table.Sort));
        _details.Text = AssetCatalogData.Details(_items.SelectedItem);
    }
    private CatalogAsset? Target => _items.SelectedItem ?? _items.Items.FirstOrDefault()?.Item;
    private void Insert()
    {
        var asset = new CatalogAsset("new-" + ++_insert, "New landscape " + _insert + ".svg", "SVG", 12, AssetCatalogData.Thumbnail("#236a9a"));
        _all = [asset, .._all]; Apply(); _result.Text = "Inserted: " + asset.Name;
    }
    private void Remove()
    {
        if (Target is not { } asset) return;
        _all = _all.Where(a => a.Key != asset.Key).ToArray(); Apply(); _result.Text = "Removed: " + asset.Name;
    }
    private void Reverse() { _all = _all.AsEnumerable().Reverse().ToArray(); _table.ApplySort(null); Apply(); _result.Text = "Reversed asset order."; }
    private void Rename()
    {
        if (Target is not { } asset) return;
        _all = _all.Select(a => a.Key == asset.Key ? a with { Name = "Renamed · " + a.Name } : a).ToArray(); Apply(); _result.Text = "Renamed: " + asset.Key;
    }
    private void UpdateSize()
    {
        if (Target is not { } asset) return;
        _all = _all.Select(a => a.Key == asset.Key ? a with { Size = a.Size + 1 } : a).ToArray(); Apply(); _result.Text = "Updated size: " + asset.Key;
    }
    private void Reset()
    {
        _all = AssetCatalogData.Create(); _insert = 0; _filter.Text = ""; _table.ApplySort(null);
        _items.Configure(_all, "harbor"); _list.Viewport.Scroll = _table.Body.Viewport.Scroll = 0;
        ShowTable(false); _details.Text = AssetCatalogData.Details(_items.SelectedItem); _result.Text = "Ready: 8 assets.";
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true; _window.Closing -= Closing;
        _items.SelectionChanged -= Selected; _items.Activated -= Activated; _table.SortChanged -= Sorted;
        _filter.TextChanged = null; Content.Dispose();
    }
    private ValueTask Closing() { Dispose(); return ValueTask.CompletedTask; }

    private sealed class AssetName : Panel
    {
        private readonly SvgImage _image = new() { RequestedWidth = 28, RequestedHeight = 28 };
        private readonly Label _name = new() { TextWrapping = ThorVG.TextWrap.Word, LayoutData = new FlexItem { Grow = 1, Basis = 80 } };
        internal AssetName(CatalogAsset asset)
        {
            Layout = new FlexLayout { Gap = 5, AlignItems = Alignment.Center }; Padding = new Thickness(0);
            AddChild(_image); AddChild(_name); Update(asset);
        }
        internal void Update(CatalogAsset asset) { _image.Svg = asset.Thumbnail; _name.Text = asset.Name; }
    }
    private sealed class PreviewButton : Button
    {
        private CatalogAsset _asset;
        internal PreviewButton(CatalogAsset asset, Action<CatalogAsset> preview) : base("Preview") { _asset = asset; Clicked = () => preview(_asset); }
        internal void Update(CatalogAsset asset) => _asset = asset;
    }
    private sealed class AssetRow : Panel
    {
        private readonly AssetName _name;
        private readonly Label _metadata = new();
        private readonly PreviewButton _preview;
        internal AssetRow(CatalogAsset asset, Action<CatalogAsset> preview)
        {
            Layout = new FlexLayout { Gap = 7, AlignItems = Alignment.Center }; Padding = new Thickness(0);
            _name = new AssetName(asset) { LayoutData = new FlexItem { Grow = 1, Basis = 120 } };
            _preview = new PreviewButton(asset, preview);
            AddChild(_name); AddChild(_metadata); AddChild(_preview); Update(asset);
        }
        internal void Update(CatalogAsset asset) { _name.Update(asset); _metadata.Text = $"{asset.Type} · {asset.Size} KB"; _preview.Update(asset); }
    }
}
