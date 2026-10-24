using System;
using System.Collections.Generic;
using System.Linq;

namespace Marius.Winter;

/// <summary>Typed native ComboBox facade. Configure options atomically when changing selectors and items together.</summary>
public class Select<TItem, TValue> : ComboBox
{
    private long _optionsVersion = -1;
    public SelectController<TItem, TValue> Controller { get; } = new();
    public event Action<TValue>? ValueChanged;
    public event Action<TValue>? UserValueChanged;

    public Select()
    {
        Controller.StateChanged += Synchronize;
        Controller.ValueChanged += OnValueChanged;
        Controller.UserValueChanged += OnUserValueChanged;
        UserSelectionChanged += OnUserIndex;
    }

    public new IEnumerable<TItem>? Items { get => Controller.Items; set => Controller.SetItems(value); }
    public Func<TItem, string> TextSelector { get => Controller.TextSelector; set => Controller.TextSelector = value; }
    public Func<TItem, TValue>? ValueSelector { get => Controller.ValueSelector; set => Controller.ValueSelector = value; }
    public IEqualityComparer<TValue> ValueComparer { get => Controller.ValueComparer; set => Controller.ValueComparer = value; }
    public TValue Value { get => Controller.Value; set => Controller.Value = value; }
    public bool HasSelection => Controller.HasSelection;
    public TItem? SelectedOption => Controller.SelectedItem;
    public new int SelectedIndex { get => base.SelectedIndex; set => Controller.SelectIndex(value); }

    public void Configure(IEnumerable<TItem>? items, Func<TItem, string> textSelector,
        Func<TItem, TValue> valueSelector, TValue value, IEqualityComparer<TValue>? comparer = null)
        => Controller.Configure(items, textSelector, valueSelector, value, comparer);

    public void ClearSelection() => Controller.ClearSelection();
    private void Synchronize()
    {
        if (_optionsVersion == Controller.OptionsVersion && base.SelectedIndex == Controller.SelectedIndex) return;
        _optionsVersion = Controller.OptionsVersion;
        SetItemsAndIndex(Controller.Labels.ToArray(), Controller.SelectedIndex);
    }
    private void OnUserIndex(int index) => Controller.SelectIndex(index, fromUser: true);
    private void OnValueChanged(TValue value) => ValueChanged?.Invoke(value);
    private void OnUserValueChanged(TValue value) => UserValueChanged?.Invoke(value);

    protected override void OnDisposed()
    {
        UserSelectionChanged -= OnUserIndex;
        Controller.StateChanged -= Synchronize;
        Controller.ValueChanged -= OnValueChanged;
        Controller.UserValueChanged -= OnUserValueChanged;
        base.OnDisposed();
    }
}
