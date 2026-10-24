using System;
using System.Collections.Generic;
using System.Linq;

namespace Marius.Winter;

/// <summary>Frontend-independent typed selection. Values are unique keys; labels need not be unique.</summary>
public sealed class SelectController<TItem, TValue>
{
    private TItem[] _items = Array.Empty<TItem>();
    private TValue[] _values = Array.Empty<TValue>();
    private string[] _labels = Array.Empty<string>();
    private Func<TItem, string> _textSelector = item => item?.ToString() ?? "";
    private Func<TItem, TValue>? _valueSelector;
    private IEqualityComparer<TValue> _comparer = EqualityComparer<TValue>.Default;
    private TValue _value = default!;
    private bool _hasValue = default(TValue) is not null;

    public event Action? StateChanged;
    public event Action<TValue>? ValueChanged;
    public event Action<TValue>? UserValueChanged;
    internal long OptionsVersion { get; private set; }
    public IReadOnlyList<TItem> Items => Array.AsReadOnly(_items);
    public IReadOnlyList<string> Labels => Array.AsReadOnly(_labels);
    public int SelectedIndex { get; private set; } = -1;
    public bool HasSelection => SelectedIndex >= 0;
    public TItem? SelectedItem => HasSelection ? _items[SelectedIndex] : default;

    public Func<TItem, string> TextSelector
    {
        get => _textSelector;
        set => ReplaceOptions(_items, value ?? throw new ArgumentNullException(nameof(value)), _valueSelector, _comparer);
    }
    public Func<TItem, TValue>? ValueSelector
    {
        get => _valueSelector;
        set => ReplaceOptions(_items, _textSelector, value, _comparer);
    }
    public IEqualityComparer<TValue> ValueComparer
    {
        get => _comparer;
        set => ReplaceOptions(_items, _textSelector, _valueSelector, value ?? EqualityComparer<TValue>.Default);
    }
    public TValue Value
    {
        get => _value;
        set => SetValue(value, value is not null, false);
    }

    public void SetItems(IEnumerable<TItem>? items) => ReplaceOptions(items, _textSelector, _valueSelector, _comparer);

    /// <summary>Validates and applies all option attributes and the authoritative value as one state transition.</summary>
    public void Configure(IEnumerable<TItem>? items, Func<TItem, string> textSelector,
        Func<TItem, TValue> valueSelector, TValue value, IEqualityComparer<TValue>? comparer = null)
    {
        var options = Snapshot(items, textSelector, valueSelector, comparer ?? EqualityComparer<TValue>.Default);
        var changed = !_comparer.Equals(_value, value) || _hasValue != (value is not null);
        Apply(options, textSelector, valueSelector, comparer ?? EqualityComparer<TValue>.Default);
        _value = value; _hasValue = value is not null; Resolve(); StateChanged?.Invoke();
        if (changed) ValueChanged?.Invoke(value);
    }

    public void ClearSelection() => SetValue(default!, false, false);

    public void SelectIndex(int index, bool fromUser = false)
    {
        if (index < -1 || index >= _items.Length) throw new ArgumentOutOfRangeException(nameof(index));
        SetValue(index < 0 ? default! : _values[index], index >= 0, fromUser);
    }

    private void SetValue(TValue value, bool hasValue, bool user)
    {
        if (_hasValue == hasValue && _comparer.Equals(_value, value)) return;
        _value = value; _hasValue = hasValue; Resolve(); StateChanged?.Invoke();
        ValueChanged?.Invoke(value);
        if (user) UserValueChanged?.Invoke(value);
    }

    private void Resolve()
    {
        SelectedIndex = -1;
        if (!_hasValue) return;
        for (var i = 0; i < _values.Length; i++)
            if (_comparer.Equals(_values[i], _value)) { SelectedIndex = i; break; }
    }

    private void ReplaceOptions(IEnumerable<TItem>? items, Func<TItem, string> text,
        Func<TItem, TValue>? value, IEqualityComparer<TValue> comparer)
    {
        var options = Snapshot(items, text, value, comparer);
        Apply(options, text, value, comparer); Resolve(); StateChanged?.Invoke();
    }

    private static (TItem[] Items, TValue[] Values, string[] Labels) Snapshot(IEnumerable<TItem>? source,
        Func<TItem, string> text, Func<TItem, TValue>? value, IEqualityComparer<TValue> comparer)
    {
        ArgumentNullException.ThrowIfNull(text);
        var items = source?.ToArray() ?? Array.Empty<TItem>();
        if (items.Length > 0 && value == null) throw new InvalidOperationException("Set ValueSelector before supplying options.");
        var values = new TValue[items.Length]; var labels = new string[items.Length];
        var keys = new HashSet<TValue>(comparer);
        for (var i = 0; i < items.Length; i++)
        {
            values[i] = value!(items[i]); labels[i] = text(items[i]) ?? "";
            if (values[i] is null) throw new ArgumentException("Option values cannot be null; null represents no selection.", nameof(source));
            if (!keys.Add(values[i])) throw new ArgumentException("Option values must be unique under ValueComparer.", nameof(source));
        }
        return (items, values, labels);
    }

    private void Apply((TItem[] Items, TValue[] Values, string[] Labels) options,
        Func<TItem, string> text, Func<TItem, TValue>? value, IEqualityComparer<TValue> comparer)
    {
        if (!ReferenceEquals(_comparer, comparer) || !_values.SequenceEqual(options.Values, comparer) || !_labels.SequenceEqual(options.Labels))
            OptionsVersion++;
        (_items, _values, _labels) = options; _textSelector = text; _valueSelector = value; _comparer = comparer;
    }
}
