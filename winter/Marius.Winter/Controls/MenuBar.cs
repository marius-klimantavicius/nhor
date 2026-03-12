using System;
using System.Collections.Generic;
using System.Numerics;
using ThorVG;

namespace Marius.Winter;

/// <summary>
/// Definition of a single menu item (label + action, separator, or submenu).
/// </summary>
internal record MenuItemDef(string Label, Action? Action, bool IsSeparator, Menu? SubMenu);

/// <summary>
/// A menu definition. Created via <see cref="MenuBar.AddMenu"/> for menu bars,
/// or directly for context menus (see <see cref="Element.ContextMenu"/>).
/// </summary>
public class Menu
{
    internal readonly string Title;
    internal readonly List<MenuItemDef> Items = new();

    public Menu() : this("") { }
    public Menu(string title) => Title = title;

    public Menu AddItem(string label, Action? action = null)
    {
        Items.Add(new MenuItemDef(label, action, false, null));
        return this;
    }

    public Menu AddSeparator()
    {
        Items.Add(new MenuItemDef("", null, true, null));
        return this;
    }

    /// <summary>
    /// Add a submenu item. Returns the child <see cref="Menu"/> for populating.
    /// </summary>
    public Menu AddSubMenu(string label)
    {
        var sub = new Menu(label);
        Items.Add(new MenuItemDef(label, null, false, sub));
        return sub;
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

    private const float BarPadV = 3f;
    private const float TitlePadH = 10f;

    public MenuBar() { }

    public Menu AddMenu(string title)
    {
        var menu = new Menu(title);
        _menus.Add(menu);
        if (_shapesCreated)
            RebuildTitles();
        return menu;
    }

    protected override void OnAttached()
    {
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
        ApplyThemeColors();
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
        if (_openIndex >= 0)
        {
            // Left/Right to switch menus
            if (key == 263 /* LEFT */)
            {
                int next = (_openIndex - 1 + _menus.Count) % _menus.Count;
                OpenMenu(next);
                return;
            }
            if (key == 262 /* RIGHT */)
            {
                int next = (_openIndex + 1) % _menus.Count;
                OpenMenu(next);
                return;
            }
            if (key == 256 /* ESCAPE */)
            {
                CloseMenu();
                return;
            }
            // Forward Up/Down/Enter to popup
            _popup?.OnKeyDown(key, mods, repeat);
            return;
        }

        base.OnKeyDown(key, mods, repeat);
    }

    // --- Menu popup management ---

    private void OpenMenu(int index)
    {
        if (index < 0 || index >= _menus.Count) return;
        var win = OwnerWindow;
        if (win == null) return;

        CloseMenu();

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

        _popup = new MenuPopup(win, menu.Items,
            onItemSelected: item => { CloseMenu(); item.Action?.Invoke(); },
            onDismiss: () => CloseMenu());
        var popupSize = _popup.MeasurePopup();

        float popupW = MathF.Max(popupSize.X, _titleW[index]);

        // Clamp to window bounds
        if (popupX + popupW > win.Bounds.W - 4)
            popupX = win.Bounds.W - 4 - popupW;
        if (popupX < 4) popupX = 4;

        _popup.Arrange(new RectF(popupX, popupY, popupW, popupSize.Y));
        win.ShowOverlay(_popup);

        // Fade-in animation
        _popup.Opacity = 0;
        win.Animator.Cancel("menu_popup");
        win.Animator.Start(new Animation
        {
            Duration = 0.10f,
            Easing = Easings.EaseOutCubic,
            Tag = "menu_popup",
            Apply = t => { if (_popup != null) _popup.Opacity = t; }
        });

        MarkDirty();
    }

    internal void CloseMenu()
    {
        if (_popup != null)
        {
            _popup.CloseSubPopup();
            OwnerWindow?.Animator?.Cancel("menu_popup");
            OwnerWindow?.RemoveOverlay(_popup);
            _popup = null;
        }
        _openIndex = -1;
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

/// <summary>
/// Dropdown popup for a menu. Rendered as an overlay. Supports nested submenus.
/// Used by both <see cref="MenuBar"/> and the context menu system.
/// </summary>
internal class MenuPopup : Element
{
    private readonly Window _window;
    private readonly List<MenuItemDef> _items;
    private readonly Action<MenuItemDef>? _onItemSelected;
    private readonly Action? _onDismiss;
    private int _hoveredIndex = -1;
    private Shape? _backgroundShape;
    private Shape? _borderShape;
    private Shape? _highlightShape;
    private readonly List<Scene> _itemScenes = new();
    private readonly List<Text> _itemTexts = new();
    private readonly List<Shape> _separators = new();
    private readonly List<Shape> _arrows = new(); // submenu arrow indicators
    private bool _shapesCreated;
    private float _itemHeight;
    private float _separatorHeight = 9;
    private readonly List<float> _itemY = new(); // y offset of each item slot
    private readonly List<float> _itemH = new(); // height of each item slot

    // Submenu state
    private int _subMenuIndex = -1;
    private MenuPopup? _subPopup;

    private const float ArrowAreaW = 16; // space reserved for submenu arrow on the right

    public MenuPopup(Window window, List<MenuItemDef> items,
        Action<MenuItemDef>? onItemSelected = null, Action? onDismiss = null)
    {
        _window = window;
        _items = items;
        _onItemSelected = onItemSelected;
        _onDismiss = onDismiss;
    }

    private Theme GetTheme() => (OwnerWindow ?? _window)?.Theme ?? Theme.Dark;

    /// <summary>
    /// Measures the popup size without creating shapes.
    /// </summary>
    public Vector2 MeasurePopup()
    {
        var style = GetTheme().MenuBar;
        float itemH = style.FontSize + 8;
        float sepH = 9;
        float popupH = 4; // top/bottom padding
        float maxW = 0;
        bool hasSubMenu = false;

        float fontSize = EffectiveFontSize(style.FontSize);
        foreach (var item in _items)
        {
            if (item.IsSeparator)
            {
                popupH += sepH;
            }
            else
            {
                popupH += itemH;
                if (item.SubMenu != null) hasSubMenu = true;
                var m = ThorVG.Text.Gen();
                if (m != null)
                {
                    m.SetFont(style.FontName);
                    m.SetFontSize(fontSize);
                    m.SetText(item.Label);
                    m.Bounds(out _, out _, out float bw, out _);
                    if (bw > maxW) maxW = bw;
                }
            }
        }

        float popupW = maxW + 24 + (hasSubMenu ? ArrowAreaW : 0);
        return new Vector2(popupW, popupH);
    }

    private void EnsureShapes()
    {
        if (_shapesCreated) return;
        _shapesCreated = true;

        var theme = GetTheme();
        var style = theme.MenuBar;
        _itemHeight = style.FontSize + 8;

        // Background
        _backgroundShape = Shape.Gen();
        _backgroundShape!.SetFill(theme.WindowBackground.R8, theme.WindowBackground.G8,
            theme.WindowBackground.B8, 245);
        AddPaint(_backgroundShape);

        // Border
        _borderShape = Shape.Gen();
        _borderShape!.StrokeWidth(1f);
        _borderShape.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, theme.BorderDark.A8);
        _borderShape.SetFill(0, 0, 0, 0);
        AddPaint(_borderShape);

        // Highlight
        _highlightShape = Shape.Gen();
        var sel = theme.SelectionColor;
        _highlightShape!.SetFill(sel.R8, sel.G8, sel.B8, sel.A8);
        _highlightShape.Visible(false);
        AddPaint(_highlightShape);

        // Items
        float fontSize = EffectiveFontSize(style.FontSize);
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].IsSeparator)
            {
                var sep = Shape.Gen();
                sep!.StrokeWidth(1f);
                sep.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, 128);
                sep.SetFill(0, 0, 0, 0);
                AddPaint(sep);
                _separators.Add(sep);
                _itemTexts.Add(null!);
                _itemScenes.Add(null!);
                _arrows.Add(null!);
            }
            else
            {
                var text = ThorVG.Text.Gen();
                text!.SetFont(style.FontName);
                text.SetFontSize(fontSize);
                text.SetText(_items[i].Label);
                text.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);

                var scene = Scene.Gen()!;
                scene.Add(text);
                AddPaint(scene);

                _itemTexts.Add(text);
                _itemScenes.Add(scene);
                _separators.Add(null!);

                // Submenu arrow indicator
                if (_items[i].SubMenu != null)
                {
                    var arrow = Shape.Gen();
                    arrow!.StrokeWidth(1.2f);
                    arrow.StrokeFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8, theme.TextColor.A8);
                    arrow.StrokeCap(StrokeCap.Round);
                    arrow.StrokeJoin(StrokeJoin.Round);
                    arrow.SetFill(0, 0, 0, 0);
                    AddPaint(arrow);
                    _arrows.Add(arrow);
                }
                else
                {
                    _arrows.Add(null!);
                }
            }
        }
    }

    protected override void OnSizeChanged()
    {
        EnsureShapes();

        float w = Bounds.W, h = Bounds.H;

        _backgroundShape?.ResetShape();
        _backgroundShape?.AppendRect(0, 0, w, h, 3, 3);

        _borderShape?.ResetShape();
        _borderShape?.AppendRect(0.5f, 0.5f, w - 1, h - 1, 3, 3);

        PositionItems();
    }

    private void PositionItems()
    {
        _itemY.Clear();
        _itemH.Clear();

        var style = GetTheme().MenuBar;
        float fontSize = EffectiveFontSize(style.FontSize);
        float y = 2; // top padding

        for (int i = 0; i < _items.Count; i++)
        {
            _itemY.Add(y);

            if (_items[i].IsSeparator)
            {
                _itemH.Add(_separatorHeight);
                // Draw separator line
                var sep = _separators[i];
                if (sep != null)
                {
                    sep.ResetShape();
                    float sepY = y + _separatorHeight / 2f;
                    sep.MoveTo(8, sepY);
                    sep.LineTo(Bounds.W - 8, sepY);
                }
                y += _separatorHeight;
            }
            else
            {
                _itemH.Add(_itemHeight);

                // Measure for baseline compensation
                var m = ThorVG.Text.Gen();
                if (m != null)
                {
                    m.SetFont(style.FontName);
                    m.SetFontSize(fontSize);
                    m.SetText(_items[i].Label);
                    m.Bounds(out float bx, out float by, out _, out float bh);
                    if (bh > 0)
                    {
                        float tx = 12 - bx;
                        float ty = y + (_itemHeight - bh) / 2f - by;
                        _itemScenes[i]?.Translate(tx, ty);
                    }
                }

                // Position submenu arrow ">"
                var arrow = _arrows[i];
                if (arrow != null)
                {
                    arrow.ResetShape();
                    float ax = Bounds.W - 12;
                    float ay = y + _itemHeight / 2f;
                    float sz = 3.5f;
                    arrow.MoveTo(ax - sz, ay - sz);
                    arrow.LineTo(ax + sz, ay);
                    arrow.LineTo(ax - sz, ay + sz);
                }

                y += _itemHeight;
            }
        }

        UpdateHighlight();
    }

    private void UpdateHighlight()
    {
        if (_highlightShape == null) return;
        if (_hoveredIndex < 0 || _hoveredIndex >= _items.Count || _items[_hoveredIndex].IsSeparator)
        {
            _highlightShape.Visible(false);
            return;
        }

        _highlightShape.Visible(true);
        _highlightShape.ResetShape();
        _highlightShape.AppendRect(2, _itemY[_hoveredIndex], Bounds.W - 4, _itemH[_hoveredIndex], 2, 2);
    }

    private int GetItemAtY(float localY)
    {
        for (int i = 0; i < _itemY.Count; i++)
        {
            if (localY >= _itemY[i] && localY < _itemY[i] + _itemH[i])
            {
                if (_items[i].IsSeparator) return -1;
                return i;
            }
        }
        return -1;
    }

    public override Element? HitTest(float x, float y)
    {
        if (!Visible) return null;

        // Check sub-popup first (it's a separate overlay but we manage it)
        if (_subPopup != null)
        {
            var subHit = _subPopup.HitTest(x, y);
            if (subHit != null) return subHit;
        }

        float localX = x - Bounds.X;
        float localY = y - Bounds.Y;
        if (new RectF(0, 0, Bounds.W, Bounds.H).Contains(localX, localY))
            return this;

        return null;
    }

    public override void OnMouseMove(float x, float y)
    {
        WindowToLocal(x, y, out _, out float ly);
        int index = GetItemAtY(ly);
        if (index != _hoveredIndex)
        {
            _hoveredIndex = index;
            UpdateHighlight();
            MarkDirty();

            // Open/close submenu based on hovered item
            UpdateSubMenu();
        }
    }

    private void UpdateSubMenu()
    {
        // If hovered item has a submenu, open it
        if (_hoveredIndex >= 0 && _hoveredIndex < _items.Count && _items[_hoveredIndex].SubMenu != null)
        {
            if (_subMenuIndex != _hoveredIndex)
                OpenSubMenu(_hoveredIndex);
        }
        else
        {
            // Close submenu if hovering a different item
            CloseSubPopup();
        }
    }

    private void OpenSubMenu(int index)
    {
        CloseSubPopup();

        var win = OwnerWindow ?? _window;
        if (win == null) return;

        var subMenu = _items[index].SubMenu;
        if (subMenu == null || subMenu.Items.Count == 0) return;

        _subMenuIndex = index;
        _subPopup = new MenuPopup(win, subMenu.Items, _onItemSelected, _onDismiss);
        var subSize = _subPopup.MeasurePopup();

        // Position to the right of this popup, aligned with the item
        float subX = Bounds.X + Bounds.W - 2;
        float subY = Bounds.Y + _itemY[index];

        // Flip left if it would go off-screen
        if (subX + subSize.X > win.Bounds.W - 4)
            subX = Bounds.X - subSize.X + 2;

        // Clamp vertically
        if (subY + subSize.Y > win.Bounds.H - 4)
            subY = win.Bounds.H - 4 - subSize.Y;
        if (subY < 4) subY = 4;

        _subPopup.Arrange(new RectF(subX, subY, subSize.X, subSize.Y));
        win.ShowOverlay(_subPopup);

        // Fade-in animation
        _subPopup.Opacity = 0;
        win.Animator.Cancel("menu_sub_popup");
        win.Animator.Start(new Animation
        {
            Duration = 0.10f,
            Easing = Easings.EaseOutCubic,
            Tag = "menu_sub_popup",
            Apply = t => { if (_subPopup != null) _subPopup.Opacity = t; }
        });

        MarkDirty();
    }

    internal void CloseSubPopup()
    {
        if (_subPopup != null)
        {
            _subPopup.CloseSubPopup(); // recursive
            (OwnerWindow ?? _window)?.RemoveOverlay(_subPopup);
            _subPopup = null;
            _subMenuIndex = -1;
        }
    }

    public override bool OnMouseDown(int button, float x, float y) => button == 0;
    public override bool OnMouseUp(int button, float x, float y) => button == 0;

    public override void OnClick()
    {
        if (_hoveredIndex >= 0 && _hoveredIndex < _items.Count && !_items[_hoveredIndex].IsSeparator)
        {
            // If it's a submenu item, open it (don't close)
            if (_items[_hoveredIndex].SubMenu != null)
            {
                OpenSubMenu(_hoveredIndex);
                return;
            }
            _onItemSelected?.Invoke(_items[_hoveredIndex]);
        }
        else
        {
            _onDismiss?.Invoke();
        }
    }

    public override void OnKeyDown(int key, int mods, bool repeat)
    {
        // If a submenu is open, forward to it first
        if (_subPopup != null && (key == 264 || key == 265 || key == 257 || key == 32 || key == 262))
        {
            if (key == 263 /* LEFT */)
            {
                // Left arrow closes submenu
                CloseSubPopup();
                return;
            }
            if (key == 262 /* RIGHT */)
            {
                // Already in submenu, forward
                _subPopup.OnKeyDown(key, mods, repeat);
                return;
            }
            // Forward Up/Down/Enter to submenu
            _subPopup.OnKeyDown(key, mods, repeat);
            return;
        }

        if (key == 265 /* UP */)
        {
            MoveFocus(-1);
            return;
        }
        if (key == 264 /* DOWN */)
        {
            MoveFocus(1);
            return;
        }
        if (key == 262 /* RIGHT */)
        {
            // Open submenu if current item has one
            if (_hoveredIndex >= 0 && _hoveredIndex < _items.Count && _items[_hoveredIndex].SubMenu != null)
            {
                OpenSubMenu(_hoveredIndex);
                _subPopup?.MoveFocus(1); // focus first item
                return;
            }
            // Otherwise let parent MenuBar handle it (switch to next menu)
            return;
        }
        if (key == 263 /* LEFT */)
        {
            // Let parent MenuBar handle (switch to prev menu)
            return;
        }
        if (key == 257 /* ENTER */ || key == 32 /* SPACE */)
        {
            if (_hoveredIndex >= 0 && _hoveredIndex < _items.Count && !_items[_hoveredIndex].IsSeparator)
            {
                if (_items[_hoveredIndex].SubMenu != null)
                {
                    OpenSubMenu(_hoveredIndex);
                    _subPopup?.MoveFocus(1);
                }
                else
                {
                    _onItemSelected?.Invoke(_items[_hoveredIndex]);
                }
            }
            return;
        }
    }

    internal void MoveFocus(int direction)
    {
        int count = _items.Count;
        if (count == 0) return;

        int start = _hoveredIndex;
        if (start < 0) start = direction > 0 ? -1 : count;

        for (int attempt = 0; attempt < count; attempt++)
        {
            start += direction;
            if (start < 0) start = count - 1;
            else if (start >= count) start = 0;

            if (!_items[start].IsSeparator)
            {
                _hoveredIndex = start;
                UpdateHighlight();
                UpdateSubMenu();
                MarkDirty();
                return;
            }
        }
    }
}
