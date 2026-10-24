using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Marius.Winter.Blazor.Core;

namespace Marius.Winter.Blazor.Elements;

/// <summary>A named Razor cell template or formatter with native width and optional stable sort comparison.</summary>
public sealed record BasicTableColumn<TItem>(string Key, string Header, float Width = 140, float MinimumWidth = 50, float Stretch = 0,
    RenderFragment<TItem>? CellTemplate = null, Func<TItem, string>? Format = null, IComparer<TItem>? Comparer = null);

public class BasicTable<TItem> : ListView<TItem>
{
    static BasicTable() => ElementHandlerRegistry.RegisterElementHandler<BasicTable<TItem>>(r => new Handler(r, true));
    [Parameter, EditorRequired] public IReadOnlyList<BasicTableColumn<TItem>> Columns { get; set; } = [];
    [Parameter] public TableSort? Sort { get; set; }
    [Parameter] public EventCallback<TableSort?> SortChanged { get; set; }
    [Parameter] public EventCallback<TableSort?> OnSortChanged { get; set; }
    protected override TableSort? CurrentSort { get => Sort; set => Sort = value; }
    protected override EventCallback<TableSort?> SortCallback => SortChanged.HasDelegate ? SortChanged : OnSortChanged;
    protected override TableColumn<TItem>[] GetColumns() => Columns.Select(c =>
        new TableColumn<TItem>(c.Key, c.Header, c.Width, c.MinimumWidth, c.Stretch, c.Format, Comparer: c.Comparer)).ToArray();
    protected override RenderFragment RenderItem(TItem item) => builder =>
    {
        builder.OpenComponent<Panel>(0);
        builder.AddAttribute(1, nameof(Panel.Layout), new TableCellsLayout<TItem>(GetColumns));
        builder.AddAttribute(2, nameof(Panel.Padding), new Thickness(0));
        builder.AddAttribute(3, nameof(Panel.ChildContent), (RenderFragment)(cells =>
        {
            foreach (var column in Columns)
            {
                cells.OpenComponent<ItemCell>(0); cells.SetKey(column.Key);
                cells.AddAttribute(1, nameof(ItemCell.ChildContent), column.CellTemplate?.Invoke(item) ?? (b =>
                { b.OpenComponent<Label>(0); b.AddAttribute(1, nameof(Label.Text), column.Format?.Invoke(item) ?? item?.ToString() ?? ""); b.CloseComponent(); }));
                cells.CloseComponent();
            }
        }));
        builder.CloseComponent();
    };
    private sealed class ItemCell : WinterComponentBase
    {
        static ItemCell() => ElementHandlerRegistry.RegisterElementHandler<ItemCell>(r => new Handler(r));
        [Parameter] public RenderFragment? ChildContent { get; set; }
        protected override RenderFragment? GetChildContent() => ChildContent;
        private sealed class Handler(NativeComponentRenderer renderer) : WinterElementHandler(renderer,
            new ItemContentHost { Layout = ItemSlotLayout.Instance, Padding = new Thickness(3) }), IWinterContainerElementHandler
        {
            public void AddChild(Element child, int index) => ElementControl.InsertChild(child, Math.Min(index, ElementControl.Children.Count));
            public void RemoveChild(Element child) => ElementControl.RemoveChild(child);
            public int GetChildIndex(Element child)
            {
                for (int i = 0; i < ElementControl.Children.Count; i++) if (ReferenceEquals(ElementControl.Children[i], child)) return i;
                return -1;
            }
            public void ReorderChildren(List<Element> order) => ElementControl.ReorderChildren(order);
        }
    }
}
