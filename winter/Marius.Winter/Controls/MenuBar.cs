using System;
using System.Collections.Generic;
using System.Numerics;
using ThorVG;

namespace Marius.Winter;

/// <summary>
/// Definition of a single menu item (label + action, separator, or submenu).
/// </summary>
public sealed class MenuItemDef
{
    private readonly Menu _owner;
    private string _label;
    private Action? _action;
    private bool _enabled = true, _checked;
    private string? _shortcut;
    public string Label { get => _label; set { if (_label == value) return; _label = value ?? ""; _owner.NotifyChanged(); } }
    /// <summary>A guarded action; a captured delegate also becomes inert after removal or disabling.</summary>
    public Action? Action { get => _action == null ? null : Invoke; internal set => _action = value; }
    public bool Enabled { get => _enabled; set { if (_enabled == value) return; _enabled = value; Revision++; _owner.NotifyChanged(); } }
    public bool IsChecked { get => _checked; set { if (_checked == value) return; _checked = value; _owner.NotifyChanged(); } }
    /// <summary>Informational display text; does not register a global shortcut.</summary>
    public string? Shortcut { get => _shortcut; set { if (_shortcut == value) return; _shortcut = value; _owner.NotifyChanged(); } }
    public bool IsSeparator { get; }
    public Menu? SubMenu { get; }
    internal long Revision { get; private set; }
    internal long ActivationRevision => Revision + (_owner.ParentItem?.ActivationRevision ?? 0);
    internal bool CanActivate => Enabled && !IsSeparator && _owner.Available && _owner.Items.Contains(this) && (_owner.ParentItem?.CanActivate ?? true);
    private void Invoke() { if (CanActivate) _action?.Invoke(); }

    internal MenuItemDef(Menu owner, string label, Action? action, bool isSeparator, Menu? subMenu)
    {
        _owner = owner; _label = label; _action = action; IsSeparator = isSeparator; SubMenu = subMenu;
        if (subMenu != null) subMenu.ParentItem = this;
    }
}

/// <summary>
/// A menu definition. Created via <see cref="MenuBar.AddMenu"/> for menu bars,
/// or directly for context menus (see <see cref="Element.ContextMenu"/>).
/// </summary>
public class Menu
{
    internal string Title;
    internal readonly List<MenuItemDef> Items = new();
    internal event Action? Changed;
    internal MenuItemDef? ParentItem;
    internal bool Available = true;
    internal void NotifyChanged() => Changed?.Invoke();
    public IReadOnlyList<MenuItemDef> Entries => Items;

    public Menu() : this("") { }
    public Menu(string title) => Title = title;

    public Menu AddItem(string label, Action? action = null)
    {
        InsertItem(Items.Count, label, action);
        return this;
    }

    public MenuItemDef InsertItem(int index, string label, Action? action = null)
    {
        var item = new MenuItemDef(this, label, action, false, null);
        Items.Insert(index, item);
        NotifyChanged();
        return item;
    }

    public void UpdateItem(MenuItemDef item, string label)
    {
        if (!Items.Contains(item) || item.Label == label) return;
        item.Label = label;
    }

    public void RemoveItem(MenuItemDef item)
    {
        if (!Items.Remove(item)) return;
        item.Action = null;
        Changed?.Invoke();
    }

    public void MoveItem(MenuItemDef item, int index)
    {
        var oldIndex = Items.IndexOf(item);
        if (oldIndex < 0 || oldIndex == index) return;
        if ((uint)index >= (uint)Items.Count) throw new ArgumentOutOfRangeException(nameof(index));
        Items.RemoveAt(oldIndex);
        Items.Insert(index, item);
        Changed?.Invoke();
    }

    public Menu AddSeparator()
    {
        InsertSeparator(Items.Count);
        return this;
    }
    public MenuItemDef InsertSeparator(int index)
    {
        var item = new MenuItemDef(this, "", null, true, null);
        Items.Insert(index, item); NotifyChanged(); return item;
    }

    /// <summary>
    /// Add a submenu item. Returns the child <see cref="Menu"/> for populating.
    /// </summary>
    public Menu AddSubMenu(string label)
    {
        return InsertSubMenu(Items.Count, label).SubMenu!;
    }
    public MenuItemDef InsertSubMenu(int index, string label)
    {
        var item = new MenuItemDef(this, label, null, false, new Menu(label));
        Items.Insert(index, item); NotifyChanged(); return item;
    }
}

/// <summary>
/// Horizontal menu bar with dropdown popup menus (File, Edit, View, etc.).
/// </summary>
public class MenuBar : Element
{
    private readonly List<Menu> _menus = new();
    private readonly List<Scene> _titleScenes = new();
    private readonly List<Text> _titleTexts = new();
    private readonly List<float> _titleX = new();   // left x of each title
    private readonly List<float> _titleW = new();   // width of each title cell
    private Shape? _backgroundShape;
    private Shape? _highlightShape;
    private bool _shapesCreated;

    private int _openIndex = -1;
    private int _hoveredIndex = -1;
    private MenuPopup? _popup;
    private Element? _restoreFocus;
    internal bool IsMenuOpen => _popup?._overlayOwner != null;

    private const float BarPadV = 3f;
    private const float TitlePadH = 10f;

    public MenuBar() { Focusable = true; }

    public Menu AddMenu(string title)
        => InsertMenu(_menus.Count, title);

    public IReadOnlyList<Menu> Menus => _menus;

    public Menu InsertMenu(int index, string title)
    {
        var menu = new Menu(title);
        _menus.Insert(index, menu);
        CollectionChanged();
        return menu;
    }

    public void UpdateMenu(Menu menu, string title)
    {
        if (!_menus.Contains(menu) || menu.Title == title) return;
        menu.Title = title;
        CollectionChanged();
    }

    public void RemoveMenu(Menu menu)
    {
        if (!_menus.Remove(menu)) return;
        menu.Available = false;
        CollectionChanged();
    }

    public void MoveMenu(Menu menu, int index)
    {
        var oldIndex = _menus.IndexOf(menu);
        if (oldIndex < 0 || oldIndex == index) return;
        if ((uint)index >= (uint)_menus.Count) throw new ArgumentOutOfRangeException(nameof(index));
        _menus.RemoveAt(oldIndex);
        _menus.Insert(index, menu);
        CollectionChanged();
    }

    private void CollectionChanged()
    {
        // A popup snapshots row geometry. Close it before mutating its collection.
        CloseMenu();
        _hoveredIndex = -1;
        if (_shapesCreated) RebuildTitles();
        InvalidateMeasure();
        MarkDirty();
    }

    protected override void OnAttached()
    {
        foreach (var menu in _menus) menu.Available = true;
        if (!_shapesCreated)
        {
            _shapesCreated = true;

            _backgroundShape = Shape.Gen();
            AddPaint(_backgroundShape!);

            _highlightShape = Shape.Gen();
            _highlightShape!.Visible(false);
            AddPaint(_highlightShape);

            RebuildTitles();
        }
    }

    private void RebuildTitles()
    {
        // Remove old title scenes
        foreach (var scene in _titleScenes)
            RemovePaint(scene);
        _titleScenes.Clear();
        _titleTexts.Clear();
        _titleX.Clear();
        _titleW.Clear();

        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        var style = theme.MenuBar;
        float fontSize = EffectiveFontSize(style.FontSize);

        for (int i = 0; i < _menus.Count; i++)
        {
            var text = ThorVG.Text.Gen();
            text!.SetFont(style.FontName);
            text.SetFontSize(fontSize);
            text.SetText(_menus[i].Title);
            text.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);

            var scene = Scene.Gen()!;
            scene.Add(text);
            AddPaint(scene);

            _titleTexts.Add(text);
            _titleScenes.Add(scene);
        }

        PositionTitles();
        ApplyThemeColors();
        InvalidateMeasure();
        MarkDirty();
    }

    private void PositionTitles()
    {
        _titleX.Clear();
        _titleW.Clear();

        var style = (OwnerWindow?.Theme ?? Theme.Dark).MenuBar;
        float fontSize = EffectiveFontSize(style.FontSize);
        float x = 4; // left margin

        for (int i = 0; i < _menus.Count; i++)
        {
            // Measure with temp text for reliable bounds
            var m = ThorVG.Text.Gen();
            if (m == null) continue;
            try
            {
                m.SetFont(style.FontName);
                m.SetFontSize(fontSize);
                m.SetText(_menus[i].Title);
                m.Bounds(out float bx, out float by, out float bw, out float bh);
                if (bh <= 0) bh = style.FontSize;
                if (bw <= 0) bw = 40;

                float cellW = bw + TitlePadH * 2;
                float barH = Bounds.H > 0 ? Bounds.H : style.FontSize + BarPadV * 2;

                _titleX.Add(x);
                _titleW.Add(cellW);

                // Position text centered in cell
                float tx = x + TitlePadH - bx;
                float ty = (barH - bh) / 2f - by;
                _titleScenes[i].Translate(tx, ty);

                x += cellW;
            }
            finally { m.Unref(); }
        }
    }

    private void ApplyThemeColors()
    {
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        var style = theme.MenuBar;

        _backgroundShape?.SetFill(style.Background.R8, style.Background.G8, style.Background.B8, style.Background.A8);

        var sel = theme.SelectionColor;
        _highlightShape?.SetFill(sel.R8, sel.G8, sel.B8, sel.A8);

        for (int i = 0; i < _titleTexts.Count; i++)
            _titleTexts[i].SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);
    }

    protected override void OnSizeChanged()
    {
        if (!_shapesCreated) return;

        float w = Bounds.W, h = Bounds.H;
        _backgroundShape?.ResetShape();
        _backgroundShape?.AppendRect(0, 0, w, h);

        PositionTitles();
        UpdateHighlight();
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        var style = (OwnerWindow?.Theme ?? Theme.Dark).MenuBar;
        float h = style.FontSize + BarPadV * 2;
        return new Vector2(availableWidth, h);
    }

    protected override Style GetDefaultStyle()
    {
        return (OwnerWindow?.Theme ?? Theme.Dark).MenuBar;
    }

    protected override void OnThemeChanged()
    {
        if (!_shapesCreated) return;
        RebuildTitles();
        UpdateHighlight();
        MarkDirty();
    }

    // --- Hit testing ---

    private int GetTitleAtX(float localX)
    {
        for (int i = 0; i < _titleX.Count; i++)
        {
            if (localX >= _titleX[i] && localX < _titleX[i] + _titleW[i])
                return i;
        }
        return -1;
    }

    // --- Input ---

    public override void OnMouseMove(float x, float y)
    {
        // Detect if popup was externally dismissed (e.g. DismissAllOverlays)
        if (_openIndex >= 0 && (_popup == null || _popup._overlayOwner == null))
        {
            _openIndex = -1;
            _popup = null;
        }

        WindowToLocal(x, y, out float lx, out _);
        int idx = GetTitleAtX(lx);

        if (idx != _hoveredIndex)
        {
            _hoveredIndex = idx;
            UpdateHighlight();
            MarkDirty();

            // If a menu is open and we hover a different title, switch popup
            if (_openIndex >= 0 && idx >= 0 && idx != _openIndex)
                OpenMenu(idx);
        }
    }

    public override void OnMouseLeave()
    {
        if (_openIndex < 0)
        {
            _hoveredIndex = -1;
            UpdateHighlight();
            MarkDirty();
        }
    }

    public override bool OnMouseDown(int button, float x, float y)
    {
        if (button != 0) return false;
        WindowToLocal(x, y, out float lx, out _);
        int idx = GetTitleAtX(lx);
        if (!IsMenuOpen) _restoreFocus = OwnerWindow?.FocusedElement;
        _hoveredIndex = idx;
        if (idx >= 0) return true;
        return false;
    }

    public override void OnClick()
    {
        if (_hoveredIndex < 0) return;

        if (_openIndex == _hoveredIndex)
            CloseMenu();
        else
            OpenMenu(_hoveredIndex);
    }

    public override void OnKeyDown(int key, int mods, bool repeat)
    {
        if (IsMenuOpen) _popup!.OnKeyDown(key, mods, repeat);
        else if (key is 257 or 32 or 264 or 265 or 299) EnterKeyboard();
        else base.OnKeyDown(key, mods, repeat);
    }

    internal void EnterKeyboard()
    {
        if (!IsActive || _menus.Count == 0) return;
        _restoreFocus = OwnerWindow?.FocusedElement;
        OpenMenu(0); _popup?.MoveFocus(1);
    }

    // --- Menu popup management ---

    private void OpenMenu(int index)
    {
        if (index < 0 || index >= _menus.Count) return;
        var win = OwnerWindow;
        if (win == null) return;

        CloseMenu(restoreFocus: false);

        _openIndex = index;
        _hoveredIndex = index;
        UpdateHighlight();

        var menu = _menus[index];
        if (menu.Items.Count == 0) return;

        // Calculate popup position in window coordinates
        float popupX = 0, popupY = 0;
        Element? e = this;
        while (e != null && e != win)
        {
            popupX += e.Bounds.X;
            popupY += e.Bounds.Y;
            e = e.Parent;
        }
        popupX += _titleX[index];
        popupY += Bounds.H;

        _popup = new MenuPopup(win, menu,
            onItemSelected: item => { CloseMenu(); item.Action?.Invoke(); },
            onDismiss: () => CloseMenu(), switchMenu: direction =>
            { OpenMenu((_openIndex + direction + _menus.Count) % _menus.Count); _popup?.MoveFocus(1); });
        _popup.PositionAt(popupX, popupY, _titleW[index]);
        win.ShowOverlay(_popup, this);

        MarkDirty();
    }

    protected override void OnActivityChanged(bool active)
    {
        foreach (var menu in _menus) menu.Available = active;
        if (!active) CloseMenu();
    }

    protected override void OnDetaching() => CloseMenu();

    public override void OnBlur() { if (IsMenuOpen) CloseMenu(); }

    internal void CloseMenu(bool restoreFocus = true)
    {
        if (_popup != null)
        {
            var popup = _popup;
            _popup = null;
            popup.CloseSubPopup();
            OwnerWindow?.Animator?.Cancel("menu_popup");
            OwnerWindow?.RemoveOverlay(popup);
            popup.Dispose();
        }
        _openIndex = -1;
        if (restoreFocus)
        {
            var focus = _restoreFocus; _restoreFocus = null;
            if (focus is { IsActive: true, IsDisposed: false }) OwnerWindow?.SetFocus(focus);
        }
        UpdateHighlight();
        MarkDirty();
    }

    private void UpdateHighlight()
    {
        if (_highlightShape == null) return;

        int idx = _openIndex >= 0 ? _openIndex : _hoveredIndex;
        if (idx < 0 || idx >= _titleX.Count)
        {
            _highlightShape.Visible(false);
            return;
        }

        _highlightShape.Visible(true);
        _highlightShape.ResetShape();
        float h = Bounds.H > 0 ? Bounds.H : 24;
        _highlightShape.AppendRect(_titleX[idx], 1, _titleW[idx], h - 2, 2, 2);
    }
}
