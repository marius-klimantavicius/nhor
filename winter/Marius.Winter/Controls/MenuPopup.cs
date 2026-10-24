using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ThorVG;

namespace Marius.Winter;

/// <summary>A menu-owned transient; rows retain definition identity through live edits.</summary>
internal sealed class MenuPopup : Element
{
    private readonly Window _window;
    private readonly Menu _menu;
    private readonly Action<MenuItemDef>? _selected;
    private readonly Action? _dismiss;
    private readonly Action<int>? _switchMenu;
    private readonly MenuPopup? _parentPopup;
    private readonly List<Paint> _paints = [];
    private readonly List<float> _tops = [], _heights = [];
    private MenuItemDef[] _rows = [];
    private MenuItemDef? _highlighted, _pressed, _childItem;
    private MenuPopup? _child;
    private Shape? _highlight, _clip;
    private Scene? _rowScene;
    private float _anchorX, _anchorY, _minimumWidth, _scroll, _contentHeight;
    private bool _created, _subscribed;
    private Theme? _paintTheme;
    private float _paintFont;
    internal bool IsRoot => _parentPopup == null;
    internal MenuItemDef? HighlightedItem => _highlighted;
    private Theme CurrentTheme => _window.Theme;
    protected override Style GetDefaultStyle() => _window.Theme.MenuBar;

    internal MenuPopup(Window window, Menu menu, Action<MenuItemDef>? onItemSelected = null,
        Action? onDismiss = null, Action<int>? switchMenu = null, MenuPopup? parentPopup = null)
    {
        _window = window; _menu = menu; _selected = onItemSelected; _dismiss = onDismiss;
        _switchMenu = switchMenu; _parentPopup = parentPopup;
    }

    private (float X, float Y, float W, float H) Metrics(string text)
    {
        var t = Text.Gen()!;
        try
        {
            t.SetFont(CurrentTheme.MenuBar.FontName); t.SetFontSize(EffectiveFontSize(CurrentTheme.MenuBar.FontSize)); t.SetText(text);
            t.Bounds(out var x, out var y, out var w, out var h); return (x, y, w, h);
        }
        finally { t.Unref(); }
    }

    public Vector2 MeasurePopup()
    {
        float width = 70, height = 4;
        foreach (var item in _menu.Items)
        {
            height += item.IsSeparator ? 9 : CurrentTheme.MenuBar.FontSize + 10;
            if (!item.IsSeparator) width = Math.Max(width, Metrics(item.Label).W + 46 + (string.IsNullOrEmpty(item.Shortcut) ? 0 : Metrics(item.Shortcut).W + 24));
        }
        return new Vector2(width, height);
    }

    internal void PositionAt(float x, float y, float minimumWidth = 0)
    {
        _anchorX = x; _anchorY = y; _minimumWidth = minimumWidth; UpdatePlacement();
    }

    internal void UpdatePlacement()
    {
        if (IsDisposed) return;
        var size = MeasurePopup();
        float vw = Math.Max(0, _window.Bounds.W), vh = Math.Max(0, _window.Bounds.H);
        float width = Math.Min(Math.Max(size.X, _minimumWidth), Math.Max(0, vw - 8));
        float height = Math.Min(size.Y, Math.Max(0, vh - 8));
        float x = _anchorX, y = _anchorY;
        if (_parentPopup != null && _parentPopup._childItem is { } item)
        {
            int i = Array.IndexOf(_parentPopup._rows, item);
            if (i < 0 || !item.CanActivate) { _parentPopup.CloseSubPopup(); return; }
            x = _parentPopup.Bounds.X + _parentPopup.Bounds.W - 2;
            y = _parentPopup.Bounds.Y + _parentPopup._tops[i] - _parentPopup._scroll;
            if (x + width > vw - 4) x = _parentPopup.Bounds.X - width + 2;
        }
        var bounds = new RectF(Math.Clamp(x, Math.Min(4, vw), Math.Max(Math.Min(4, vw), vw - width - 4)),
            Math.Clamp(y, Math.Min(4, vh), Math.Max(Math.Min(4, vh), vh - height - 4)), width, height);
        bool themeChanged = _paintTheme != CurrentTheme || _paintFont != CurrentTheme.MenuBar.FontSize;
        if (_created && themeChanged) Rebuild();
        if (!Bounds.Equals(bounds)) Arrange(bounds);
        _child?.UpdatePlacement();
    }

    protected override void OnAttached()
    {
        if (!_subscribed) { _subscribed = true; _menu.Changed += Refresh; }
        if (!_created) Rebuild();
    }
    protected override void OnSizeChanged() { if (!_created) Rebuild(); else RebuildPaints(); }
    protected override void OnThemeChanged() { if (_created) Rebuild(); }
    private void Refresh()
    {
        if (IsDisposed) return;
        if (_highlighted != null && (!_menu.Items.Contains(_highlighted) || !_highlighted.CanActivate)) _highlighted = null;
        if (_childItem != null && (!_menu.Items.Contains(_childItem) || !_childItem.CanActivate)) CloseSubPopup();
        _pressed = null; Rebuild(); UpdatePlacement(); _child?.UpdatePlacement();
    }
    private void Rebuild()
    {
        _rows = _menu.Items.ToArray(); _tops.Clear(); _heights.Clear(); float y = 2;
        foreach (var item in _rows) { float h = item.IsSeparator ? 9 : CurrentTheme.MenuBar.FontSize + 10; _tops.Add(y); _heights.Add(h); y += h; }
        _contentHeight = y + 2; _created = true; RebuildPaints();
    }
    private void RebuildPaints()
    {
        foreach (var paint in _paints) RemovePaint(paint); _paints.Clear();
        _paintTheme = CurrentTheme; _paintFont = CurrentTheme.MenuBar.FontSize;
        var theme = CurrentTheme;
        var bg = Shape.Gen()!; bg.SetFill(theme.WindowBackground.R8, theme.WindowBackground.G8, theme.WindowBackground.B8, 255);
        bg.AppendRect(0, 0, Bounds.W, Bounds.H, 3, 3); Add(bg);
        var border = Shape.Gen()!; border.SetFill(0, 0, 0, 0); border.StrokeWidth(1);
        border.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, 255);
        border.AppendRect(.5f, .5f, Math.Max(0, Bounds.W - 1), Math.Max(0, Bounds.H - 1), 3, 3); Add(border);
        _rowScene = Scene.Gen()!; _clip = Shape.Gen()!; _clip.AppendRect(2, 2, Math.Max(0, Bounds.W - 4), Math.Max(0, Bounds.H - 4));
        _rowScene.Clip(_clip); Add(_rowScene);
        _highlight = Shape.Gen()!; var sel = theme.SelectionColor; _highlight.SetFill(sel.R8, sel.G8, sel.B8, sel.A8); _rowScene.Add(_highlight);
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _contentHeight - Bounds.H));
        for (int i = 0; i < _rows.Length; i++)
        {
            var item = _rows[i]; float top = _tops[i] - _scroll, mid = top + _heights[i] / 2;
            if (item.IsSeparator)
            {
                var sep = Shape.Gen()!; sep.SetFill(0, 0, 0, 0); sep.StrokeWidth(1); sep.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, 150);
                sep.MoveTo(8, mid); sep.LineTo(Bounds.W - 8, mid); _rowScene.Add(sep); continue;
            }
            byte alpha = item.CanActivate ? (byte)255 : (byte)100;
            AddText(item.Label, 25, top, _heights[i], alpha);
            if (!string.IsNullOrEmpty(item.Shortcut)) AddText(item.Shortcut, Bounds.W - 20 - Metrics(item.Shortcut).W, top, _heights[i], alpha);
            if (item.IsChecked || item.SubMenu != null)
            {
                var mark = Shape.Gen()!; mark.SetFill(0, 0, 0, 0); mark.StrokeWidth(1.7f); mark.StrokeCap(StrokeCap.Round); mark.StrokeJoin(StrokeJoin.Round);
                mark.StrokeFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8, alpha);
                if (item.IsChecked) { mark.MoveTo(8, mid); mark.LineTo(12, mid + 4); mark.LineTo(18, mid - 4); }
                if (item.SubMenu != null) { mark.MoveTo(Bounds.W - 13, mid - 4); mark.LineTo(Bounds.W - 9, mid); mark.LineTo(Bounds.W - 13, mid + 4); }
                _rowScene.Add(mark);
            }
        }
        UpdateHighlight(); MarkDirty();
    }
    private void Add(Paint paint) { _paints.Add(paint); AddPaint(paint); }
    private void AddText(string value, float x, float top, float height, byte alpha)
    {
        var text = Text.Gen()!; text.SetFont(CurrentTheme.MenuBar.FontName); text.SetFontSize(EffectiveFontSize(CurrentTheme.MenuBar.FontSize)); text.SetText(value);
        text.SetFill(CurrentTheme.TextColor.R8, CurrentTheme.TextColor.G8, CurrentTheme.TextColor.B8); text.Opacity(alpha);
        text.Bounds(out var bx, out var by, out _, out var bh);
        var scene = Scene.Gen()!; scene.Add(text); scene.Translate(x - bx, top + (height - bh) / 2 - by); _rowScene!.Add(scene);
    }
    private void UpdateHighlight()
    {
        if (_highlight == null) return;
        int i = _highlighted == null ? -1 : Array.IndexOf(_rows, _highlighted);
        _highlight.Visible(i >= 0 && _highlighted!.CanActivate); _highlight.ResetShape();
        if (i >= 0) _highlight.AppendRect(2, _tops[i] - _scroll, Math.Max(0, Bounds.W - 4), _heights[i], 2, 2);
        MarkDirty();
    }
    private MenuItemDef? At(float x, float y)
    {
        WindowToLocal(x, y, out var lx, out var ly);
        if (lx < 2 || lx >= Bounds.W - 2 || ly < 2 || ly >= Bounds.H - 2) return null;
        for (int i = 0; i < _rows.Length; i++)
            if (ly + _scroll >= _tops[i] && ly + _scroll < _tops[i] + _heights[i]) return _rows[i].CanActivate ? _rows[i] : null;
        return null;
    }
    public override void OnMouseMove(float x, float y)
    {
        var item = At(x, y);
        if (ReferenceEquals(item, _highlighted)) return;
        _highlighted = item; UpdateHighlight();
        if (_childItem != item) CloseSubPopup();
        if (item?.SubMenu != null) OpenSubMenu(item, false);
    }
    public override bool OnMouseDown(int button, float x, float y) { _pressed = button == 0 ? At(x, y) : null; return button == 0; }
    public override bool OnMouseUp(int button, float x, float y) { if (!ReferenceEquals(_pressed, At(x, y))) _pressed = null; return button == 0; }
    public override void OnClick()
    {
        var item = _pressed; _pressed = null;
        if (item?.CanActivate != true || !_menu.Items.Contains(item)) return;
        if (item.SubMenu != null) OpenSubMenu(item, false); else _selected?.Invoke(item);
    }
    public override void OnScroll(float dx, float dy)
    {
        CloseSubPopup(); _scroll = Math.Clamp(_scroll - dy * (CurrentTheme.MenuBar.FontSize + 10), 0, Math.Max(0, _contentHeight - Bounds.H)); RebuildPaints();
    }
    internal void MoveFocus(int direction)
    {
        int index = _highlighted == null ? (direction > 0 ? -1 : 0) : Array.IndexOf(_rows, _highlighted);
        for (int n = 0; n < _rows.Length; n++)
        {
            index = (index + direction + _rows.Length) % _rows.Length;
            if (!_rows[index].CanActivate) continue;
            _highlighted = _rows[index]; CloseSubPopup();
            float top = _tops[index], bottom = top + _heights[index];
            if (top < _scroll + 2) _scroll = top - 2;
            if (bottom > _scroll + Bounds.H - 2) _scroll = bottom - Bounds.H + 2;
            RebuildPaints(); return;
        }
        _highlighted = null; UpdateHighlight();
    }
    public override void OnKeyDown(int key, int mods, bool repeat)
    {
        if (_child != null)
        {
            if (key is 256 or 263 && _child._child == null) { CloseSubPopup(); return; }
            _child.OnKeyDown(key, mods, repeat); return;
        }
        switch (key)
        {
            case 256: _dismiss?.Invoke(); return;
            case 265: MoveFocus(-1); return;
            case 264: MoveFocus(1); return;
            case 268: _highlighted = null; MoveFocus(1); return;
            case 269: _highlighted = null; MoveFocus(-1); return;
            case 263: _switchMenu?.Invoke(-1); return;
            case 262:
                if (_highlighted?.SubMenu != null) OpenSubMenu(_highlighted, true); else _switchMenu?.Invoke(1);
                return;
            case 257: case 335: case 32:
                if (_highlighted?.CanActivate != true) return;
                if (_highlighted.SubMenu != null) OpenSubMenu(_highlighted, true); else _selected?.Invoke(_highlighted);
                return;
        }
    }
    private void OpenSubMenu(MenuItemDef item, bool keyboard)
    {
        if (!item.CanActivate || item.SubMenu == null || item.SubMenu.Items.Count == 0) return;
        if (_childItem != item)
        {
            CloseSubPopup(); _childItem = item;
            _child = new MenuPopup(_window, item.SubMenu, _selected, _dismiss, parentPopup: this);
            _child.UpdatePlacement(); _window.ShowOverlay(_child, this);
        }
        if (keyboard) _child?.MoveFocus(1);
    }
    internal void CloseSubPopup()
    {
        var child = _child; _child = null; _childItem = null;
        if (child == null) return;
        child.CloseSubPopup(); _window.RemoveOverlay(child); child.Dispose();
    }
    public override void OnBlur() { if (!IsDisposed) _dismiss?.Invoke(); }
    protected override void OnDetaching()
    {
        if (_subscribed) { _menu.Changed -= Refresh; _subscribed = false; }
        _pressed = null; CloseSubPopup();
        // A renderer batch can edit rows and immediately remove their owner,
        // before a canvas prepares the new paints. Release these menu-owned
        // paints before the retained parent scene calculates damage bounds.
        foreach (var paint in _paints) RemovePaint(paint);
        _paints.Clear(); _created = false; _rowScene = null; _highlight = _clip = null;
    }
}
