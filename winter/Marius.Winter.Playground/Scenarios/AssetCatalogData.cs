using System;
using System.Collections.Generic;
using System.Linq;

namespace Marius.Winter.Scenarios;

public sealed record CatalogAsset(string Key, string Name, string Type, int Size, string Thumbnail);

public static class AssetCatalogData
{
    public static string Key(CatalogAsset a) => a.Key;
    public static readonly IComparer<CatalogAsset> NameComparer = Comparer<CatalogAsset>.Create((a,b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
    public static readonly IComparer<CatalogAsset> TypeComparer = Comparer<CatalogAsset>.Create((a,b) => StringComparer.Ordinal.Compare(a.Type, b.Type));
    public static readonly IComparer<CatalogAsset> SizeComparer = Comparer<CatalogAsset>.Create((a,b) => a.Size.CompareTo(b.Size));
    public static string Thumbnail(string color) => $"<svg xmlns='http://www.w3.org/2000/svg' width='28' height='28'><rect width='28' height='28' rx='4' fill='{color}'/><circle cx='20' cy='8' r='4' fill='#ffe09a'/><path d='M2 24l8-12 7 9 4-5 5 8z' fill='#d5eee2'/></svg>";
    public static CatalogAsset[] Create() => [
        new("harbor", "Harbor.svg", "SVG", 24, Thumbnail("#236a9a")),
        new("forest", "Forest.svg", "SVG", 18, Thumbnail("#276947")),
        new("cafe", "Café.png", "PNG", 84, Thumbnail("#975b35")),
        new("japan", "日本語.svg", "SVG", 32, Thumbnail("#a54764")),
        new("team", "Team 👩‍💻.png", "PNG", 128, Thumbnail("#69489d")),
        new("harbor-alt", "Harbor.svg", "SVG", 24, Thumbnail("#2d8293")),
        new("wide", "Presentation – wide landscape for review.svg", "SVG", 64, Thumbnail("#47669a")),
        new("notes", "Žąsis é.txt", "Text", 4, Thumbnail("#8b782a"))];
    public static CatalogAsset[] Filter(IEnumerable<CatalogAsset> items, string filter) => items.Where(a => a.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) || a.Type.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
    public static CatalogAsset[] Order(IEnumerable<CatalogAsset> items, TableSort? sort)
    {
        if (sort == null) return items.ToArray();
        var comparer = sort.Column switch { "name" => NameComparer, "type" => TypeComparer, "size" => SizeComparer, _ => throw new ArgumentException("Unknown catalog sort.") };
        return (sort.Direction == SortDirection.Ascending ? items.OrderBy(a => a, comparer) : items.OrderByDescending(a => a, comparer)).ToArray();
    }
    public static string Details(CatalogAsset? item) => item == null ? "Select an asset to see its details." : $"Selected: {item.Name} · {item.Type} · {item.Size} KB · {item.Key}";
}
