using System;
using System.Collections.Generic;
using System.Linq;

namespace Marius.Winter;

/// <summary>An immutable keyed value in a bounded collection.</summary>
public sealed record ItemSnapshot<TItem>(string Key, TItem Item)
{
    /// <summary>Changes on explicit Update, including an update of the same caller-owned item reference.</summary>
    public long Version { get; init; }
}
public sealed record ItemSelection<TItem>(string? Key, TItem? Item);
public enum ItemActivationKind { Keyboard, DoubleClick }
public sealed record ItemActivation<TItem>(string Key, TItem Item, ItemActivationKind Kind);
public enum SortDirection { Ascending, Descending }
public sealed record TableSort(string Column, SortDirection Direction);

/// <summary>Frontend-independent single selection over a bounded, keyed snapshot.</summary>
public sealed class ItemController<TItem>
{
    private ItemSnapshot<TItem>[] _items = [];
    public ItemController(Func<TItem, string> keySelector) => KeySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
    public Func<TItem, string> KeySelector { get; }
    public IReadOnlyList<ItemSnapshot<TItem>> Items => Array.AsReadOnly(_items);
    public long Revision { get; private set; }
    public event Action? StateChanged;
    public event Action<ItemSelection<TItem>>? SelectionChanged;
    public event Action<ItemSelection<TItem>>? UserSelectionChanged;
    public event Action<ItemActivation<TItem>>? Activated;
    public string? SelectedKey { get; private set; }
    public bool HasSelection => SelectedKey != null;
    public TItem? SelectedItem => _items.FirstOrDefault(i => i.Key == SelectedKey) is { } row ? row.Item : default;
    public ItemSelection<TItem> Selection => new(SelectedKey, SelectedItem);
    public ItemSnapshot<TItem>? Find(string? key) => _items.FirstOrDefault(i => i.Key == key);
    public void Select(string? key, bool fromUser = false)
    {
        key = Find(key)?.Key;
        if (SelectedKey == key) return;
        SelectedKey = key;
        var selection = Selection;
        StateChanged?.Invoke(); SelectionChanged?.Invoke(selection);
        if (fromUser) UserSelectionChanged?.Invoke(selection);
    }
    public void Activate(string key, ItemActivationKind kind = ItemActivationKind.Keyboard)
    {
        if (Find(key) is not { } row) return;
        // Capture before selection callbacks, which can replace this collection.
        var action = new ItemActivation<TItem>(key, row.Item, kind);
        var revision = Revision;
        Select(key, true);
        if (revision == Revision && Find(key) == row) Activated?.Invoke(action);
    }
    public void SetItems(IEnumerable<TItem> items) => Configure(items, SelectedKey);
    /// <summary>Validates the entire snapshot before applying data and authoritative selection together.</summary>
    public void Configure(IEnumerable<TItem> items, string? selectedKey)
    {
        ArgumentNullException.ThrowIfNull(items);
        var snapshot = items.Select(item =>
        {
            var key = KeySelector(item); var old = Find(key);
            return new ItemSnapshot<TItem>(key, item) { Version = old == null ? 0 : old.Version + (EqualityComparer<TItem>.Default.Equals(old.Item, item) ? 0 : 1) };
        }).ToArray();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in snapshot)
            if (string.IsNullOrEmpty(row.Key) || !keys.Add(row.Key)) throw new ArgumentException("Item keys must be nonempty and unique.", nameof(items));
        var previous = Selection;
        bool dataChanged = !_items.SequenceEqual(snapshot);
        _items = snapshot;
        SelectedKey = selectedKey != null && keys.Contains(selectedKey) ? selectedKey : null;
        if (dataChanged) Revision++;
        if (dataChanged || previous != Selection) StateChanged?.Invoke();
        if (previous != Selection) SelectionChanged?.Invoke(Selection);
    }
    public void Insert(int index, TItem item)
    {
        var next = _items.Select(i => i.Item).ToList(); next.Insert(index, item); SetItems(next);
    }
    public bool Remove(string key)
    {
        if (Find(key) == null) return false;
        SetItems(_items.Where(i => i.Key != key).Select(i => i.Item)); return true;
    }
    public void Update(string key, TItem item)
    {
        if (KeySelector(item) != key) throw new ArgumentException("An update must keep the row key.", nameof(item));
        if (Find(key) == null) throw new ArgumentException("Unknown item key.", nameof(key));
        var next = _items.ToArray(); int index = Array.FindIndex(next, i => i.Key == key);
        next[index] = new ItemSnapshot<TItem>(key, item) { Version = next[index].Version + 1 };
        _items = next; Revision++; StateChanged?.Invoke();
        if (SelectedKey == key) SelectionChanged?.Invoke(Selection);
    }
    public void Reorder(IEnumerable<string> keys)
    {
        var order = keys.ToArray();
        if (order.Length != _items.Length || order.Distinct(StringComparer.Ordinal).Count() != order.Length || order.Any(k => Find(k) == null))
            throw new ArgumentException("Reorder requires each existing key exactly once.", nameof(keys));
        SetItems(order.Select(k => Find(k)!.Item));
    }
    /// <summary>Stable sort: equal values retain their current relative order.</summary>
    public void Sort(IComparer<TItem> comparer, SortDirection direction)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        SetItems((direction == SortDirection.Ascending
            ? _items.OrderBy(i => i.Item, comparer) : _items.OrderByDescending(i => i.Item, comparer)).Select(i => i.Item));
    }
}
