using System;
using System.Collections.Generic;
using System.Numerics;
using ThorVG;
using static Glfw.GLFW;

namespace Marius.Winter;

/// <summary>
/// Dropdown list control. Displays the selected item and opens a popup list when clicked.
/// </summary>
public class ComboBox : Element
{
    private Shape? _backgroundShape;
    private Shape? _borderShape;
    private Shape? _arrowShape;
    private Shape? _labelClip;
    private Scene? _labelScene;
    private Text? _labelText;
    private bool _shapesCreated;
    private bool _isOpen;

    private readonly List<string> _items = new();
    private int _selectedIndex = -1;
    private ComboBoxPopup? _popup;

    public Action<int>? SelectionChanged;
    public event Action<int>? UserSelectionChanged;
    public bool IsDropDownOpen => _isOpen;
    public int HighlightedIndex => _popup?.HighlightedIndex ?? -1;

    /// <summary>Commits a user selection. Programmatic SelectedIndex and item batches retain legacy notifications only.</summary>
    public void SelectFromUser(int index)
    {
        if (!IsEffectivelyEnabled || !IsEffectivelyVisible || IsDisposed) return;
        var previous = _selectedIndex;
        SelectedIndex = index;
        if (previous != _selectedIndex) UserSelectionChanged?.Invoke(_selectedIndex);
    }

    public ComboBox(string[] items = null!)
    {
        Cursor = CursorType.Hand;
        Focusable = true;
        if (items != null)
        {
            _items.AddRange(items);
            if (_items.Count > 0) _selectedIndex = 0;
        }
    }

    public IReadOnlyList<string> Items => _items;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < -1 || value >= _items.Count) return;
            if (_selectedIndex == value) return;
            ClosePopup();
            _selectedIndex = value;
            UpdateLabel();
            MarkDirty();
            SelectionChanged?.Invoke(value);
        }
    }

    public string? SelectedItem =>
        _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

    public void SetItems(string[] items)
    {
        ClosePopup();
        _items.Clear();
        _items.AddRange(items);
        if (_selectedIndex >= _items.Count) _selectedIndex = _items.Count > 0 ? 0 : -1;
        UpdateLabel();
        InvalidateMeasure();
        MarkDirty();
    }

    /// <summary>
    /// Atomically set items and selected index. Applies items first, then clamps
    /// index, then fires SelectionChanged once if the index changed.
    /// Used by Blazor handler to avoid attribute-ordering bugs.
    /// </summary>
    public void SetItemsAndIndex(string[] items, int selectedIndex)
    {
        // An option batch invalidates the tentative popup snapshot, even when its index is equal.
        ClosePopup();
        _items.Clear();
        _items.AddRange(items);
        int clamped = _items.Count > 0
            ? Math.Clamp(selectedIndex, -1, _items.Count - 1)
            : -1;
        bool changed = _selectedIndex != clamped;
        _selectedIndex = clamped;
        UpdateLabel();
        InvalidateMeasure();
        MarkDirty();
        if (changed) SelectionChanged?.Invoke(_selectedIndex);
    }

    protected override void OnAttached()
    {
        if (!_shapesCreated)
        {
            _shapesCreated = true;
            var style = Style;
            var theme = OwnerWindow?.Theme ?? Theme.Dark;

            // Background — same gradient as button
            _backgroundShape = Shape.Gen();
            ApplyButtonGradient(theme.ButtonGradientTopUnfocused, theme.ButtonGradientBotUnfocused);
            AddPaint(_backgroundShape!);

            // Border
            _borderShape = Shape.Gen();
            _borderShape!.StrokeWidth(1f);
            _borderShape.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, theme.BorderDark.A8);
            _borderShape.SetFill(0, 0, 0, 0);
            AddPaint(_borderShape);

            // Arrow (downward chevron)
            _arrowShape = Shape.Gen();
            _arrowShape!.StrokeWidth(1.5f);
            _arrowShape.StrokeFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8, theme.TextColor.A8);
            _arrowShape.StrokeCap(StrokeCap.Round);
            _arrowShape.StrokeJoin(StrokeJoin.Round);
            _arrowShape.SetFill(0, 0, 0, 0);
            AddPaint(_arrowShape);

            // Label text
            _labelText = ThorVG.Text.Gen();
            _labelText!.SetFont(style.FontName);
            _labelText.SetFontSize(EffectiveFontSize(style.FontSize));
            _labelText.SetText(SelectedItem ?? "");
            _labelText.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);

            _labelScene = Scene.Gen()!;
            _labelScene.Add(_labelText);
            _labelClip = Shape.Gen();
            _labelScene.Clip(_labelClip);
            AddPaint(_labelScene);
        }
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        var style = Style;
        float maxW = 60; // minimum width
        var t = ThorVG.Text.Gen();
        if (t != null)
        {
            try
            {
                t.SetFont(style.FontName);
                t.SetFontSize(EffectiveFontSize(style.FontSize));
                foreach (var item in _items)
                {
                    t.SetText(item);
                    t.Bounds(out _, out _, out float bw, out _);
                    if (bw > maxW) maxW = bw;
                }
            }
            finally { t.Unref(); }
        }
        return new Vector2(maxW + 40, style.FontSize + 10); // +40 for arrow + padding
    }

    protected override void OnSizeChanged()
    {
        float w = Bounds.W, h = Bounds.H;
        float r = (OwnerWindow?.Theme ?? Theme.Dark).ButtonCornerRadius;

        _backgroundShape?.ResetShape();
        _backgroundShape?.AppendRect(1, 1, w - 2, h - 2, r - 1, r - 1);

        _borderShape?.ResetShape();
        _borderShape?.AppendRect(0.5f, 0.5f, w - 1, h - 1, r, r);

        // Clip label text to prevent overflow into arrow area
        _labelClip?.ResetShape();
        _labelClip?.AppendRect(0, 0, w - 28, h, 0, 0);

        // Arrow chevron on right side
        UpdateArrow();
        CenterLabel();
        PositionPopup();
    }

    private void UpdateArrow()
    {
        if (_arrowShape == null) return;
        float w = Bounds.W, h = Bounds.H;
        float arrowX = w - 18;
        float arrowY = h / 2f;
        float arrowSize = 4f;

        _arrowShape.ResetShape();
        _arrowShape.MoveTo(arrowX - arrowSize, arrowY - arrowSize * 0.5f);
        _arrowShape.LineTo(arrowX, arrowY + arrowSize * 0.5f);
        _arrowShape.LineTo(arrowX + arrowSize, arrowY - arrowSize * 0.5f);
    }

    private void CenterLabel()
    {
        if (_labelScene == null) return;
        var style = Style;
        string text = SelectedItem ?? "";
        if (string.IsNullOrEmpty(text)) return;

        var m = ThorVG.Text.Gen();
        if (m == null) return;
        try
        {
            m.SetFont(style.FontName);
            m.SetFontSize(EffectiveFontSize(style.FontSize));
            m.SetText(text);
            m.Bounds(out float bx, out float by, out float bw, out float bh);
            if (bw <= 0 || bh <= 0) return;

            float x = 10 - bx; // left-aligned with padding
            float y = (Bounds.H - bh) / 2f - by;
            _labelScene.Translate(x, y);
        }
        finally { m.Unref(); }
    }

    private void UpdateLabel()
    {
        string text = SelectedItem ?? "";
        _labelText?.SetText(text);
        CenterLabel();
    }

    private void ApplyButtonGradient(Color4 top, Color4 bot)
    {
        if (_backgroundShape == null) return;
        float h = Bounds.H > 0 ? Bounds.H : 30;
        var grad = LinearGradient.Gen();
        grad.Linear(0, 0, 0, h);
        grad.SetColorStops(new Fill.ColorStop[]
        {
            new(0, top.R8, top.G8, top.B8, top.A8),
            new(1, bot.R8, bot.G8, bot.B8, bot.A8)
        }, 2);
        _backgroundShape.SetFill(grad);
    }

    protected override void OnStateChanged(ElementState oldState, ElementState newState)
    {
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        Color4 gradTop, gradBot;

        if ((newState & ElementState.Pressed) != 0)
        {
            gradTop = theme.ButtonGradientTopPushed;
            gradBot = theme.ButtonGradientBotPushed;
        }
        else if ((newState & ElementState.Hovered) != 0)
        {
            gradTop = theme.ButtonGradientTopFocused;
            gradBot = theme.ButtonGradientBotFocused;
        }
        else
        {
            gradTop = theme.ButtonGradientTopUnfocused;
            gradBot = theme.ButtonGradientBotUnfocused;
        }

        ApplyButtonGradient(gradTop, gradBot);
        var border = (newState & ElementState.Focused) != 0 ? Style.BorderFocused : theme.BorderDark;
        _borderShape?.StrokeFill(border.R8, border.G8, border.B8, border.A8);
        _borderShape?.StrokeWidth((newState & ElementState.Focused) != 0 ? 2 : 1);
        MarkDirty();
    }

    public override void OnMouseEnter()
    {
        if (!Enabled) return;
        State = State | ElementState.Hovered;
    }

    public override void OnMouseLeave()
    {
        State = State & ~ElementState.Hovered & ~ElementState.Pressed;
    }

    public override bool OnMouseDown(int button, float x, float y)
    {
        if (!Enabled || button != 0) return false;
        State = State | ElementState.Pressed;
        return true;
    }

    public override bool OnMouseUp(int button, float x, float y)
    {
        if (button != 0) return false;
        State = State & ~ElementState.Pressed;
        return true;
    }

    public override void OnClick()
    {
        if (!Enabled) return;
        if (_isOpen)
            ClosePopup();
        else
            OpenPopup();
    }

    public override void OnBlur() => ClosePopup();

    public override void OnKeyDown(int key, int mods, bool repeat)
    {
        if (!IsEffectivelyEnabled || !IsEffectivelyVisible || IsDisposed) return;
        if (key == GLFW_KEY_ESCAPE) { ClosePopup(); return; }
        if (key == GLFW_KEY_ENTER || key == GLFW_KEY_KP_ENTER || key == GLFW_KEY_SPACE)
        {
            if (repeat) return;
            if (_popup != null) SelectFromPopup(_popup.HighlightedIndex);
            else OpenPopup();
            return;
        }
        if (key == GLFW_KEY_UP || key == GLFW_KEY_DOWN || key == GLFW_KEY_HOME || key == GLFW_KEY_END)
        {
            if (_popup == null) OpenPopup();
            _popup?.Navigate(key);
            return;
        }
        base.OnKeyDown(key, mods, repeat);
    }

    private void OpenPopup()
    {
        var win = OwnerWindow;
        if (win == null || !IsActive || _items.Count == 0) return;

        _isOpen = true;
        _popup = new ComboBoxPopup(this, _items.ToArray(), _selectedIndex);
        win.ShowOverlay(_popup, this);
        PositionPopup();
        _popup.RevealHighlight();
    }

    internal void PositionPopup()
    {
        var popup = _popup; var win = OwnerWindow;
        if (popup == null || win == null) return;
        WindowToLocal(0, 0, out var nx, out var ny);
        float x = -nx, y = -ny;
        float vw = Math.Max(0, win.Bounds.W), vh = Math.Max(0, win.Bounds.H);
        float below = Math.Max(0, vh - (y + Bounds.H + 2));
        float above = Math.Max(0, y - 2);
        float desired = Math.Min(popup.ContentHeight + 4, Math.Max(200, popup.ItemHeight * 6 + 4));
        bool flip = below < desired && above > below;
        float height = Math.Min(desired, Math.Min(vh, flip ? above : below));
        // If the owner itself is outside the usable viewport, still keep the list reachable.
        if (height < Math.Min(vh, popup.ItemHeight + 4)) height = Math.Min(vh, popup.ItemHeight + 4);
        float width = Math.Min(vw, Math.Max(0, Bounds.W));
        float py = flip ? y - 2 - height : y + Bounds.H + 2;
        popup.Arrange(new RectF(Math.Clamp(x, 0, Math.Max(0, vw - width)),
            Math.Clamp(py, 0, Math.Max(0, vh - height)), width, height));
    }

    protected override void OnPositionChanged() => PositionPopup();
    protected override void ArrangeCore(RectF finalBounds) => PositionPopup();

    protected override void OnActivityChanged(bool active)
    {
        if (!active) ClosePopup();
    }

    protected override void OnDetaching() => ClosePopup();

    internal void ClosePopup()
    {
        if (!_isOpen) return;
        _isOpen = false;
        if (_popup != null)
        {
            var popup = _popup;
            _popup = null;
            OwnerWindow?.RemoveOverlay(popup);
            popup.Dispose();
        }
    }

    internal void SelectFromPopup(int index)
    {
        // Close the old snapshot before application callbacks can replace/unmount this control.
        ClosePopup();
        SelectFromUser(index);
    }

    protected override Style GetDefaultStyle()
    {
        return OwnerWindow?.Theme.Button ?? new Style();
    }

    protected override void OnThemeChanged()
    {
        if (!_shapesCreated) return;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        ApplyButtonGradient(theme.ButtonGradientTopUnfocused, theme.ButtonGradientBotUnfocused);
        _borderShape?.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, theme.BorderDark.A8);
        _arrowShape?.StrokeFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8, theme.TextColor.A8);
        _labelText?.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);
        _labelText?.SetFont(Style.FontName);
        _labelText?.SetFontSize(EffectiveFontSize(Style.FontSize));
        if (_popup != null) ClosePopup();
        MarkDirty();
    }

}

/// <summary>A clipped, scrollable snapshot. Keyboard focus stays on the owning ComboBox.</summary>
internal sealed class ComboBoxPopup : Element
{
    private readonly ComboBox _owner;
    private readonly string[] _items;
    private int _highlightedIndex, _pressedIndex = -1, _clickedIndex = -1;
    private float _scroll, _dragStartY, _dragStartScroll;
    private bool _scrollPress, _dragging;
    private Shape? _background, _border, _highlight, _clip, _track, _thumb;
    private Scene? _rows;
    private readonly List<Scene> _itemScenes = new();
    private readonly List<(float X, float Y, float Height)> _textMetrics = new();
    private bool _created;
    internal int HighlightedIndex => _highlightedIndex;
    internal float ItemHeight => Math.Max(1, _owner.Style.FontSize) + 8;
    internal float ContentHeight => _items.Length * ItemHeight;
    private float ViewHeight => Math.Max(0, Bounds.H - 4);
    private float MaxScroll => Math.Max(0, ContentHeight - ViewHeight);
    private bool HasScrollbar => MaxScroll > 0;
    private float RowWidth => Math.Max(0, Bounds.W - 4 - (HasScrollbar ? 12 : 0));
    private float ThumbHeight => Math.Min(ViewHeight, Math.Max(16, ViewHeight * ViewHeight / Math.Max(1, ContentHeight)));
    private float ThumbY => 2 + (MaxScroll > 0 ? _scroll / MaxScroll * (ViewHeight - ThumbHeight) : 0);

    public ComboBoxPopup(ComboBox owner, string[] items, int selectedIndex)
    {
        _owner = owner; _items = items; _highlightedIndex = selectedIndex >= 0 ? selectedIndex : 0;
    }

    public override void OnBlur() { if (!IsDisposed) _owner.ClosePopup(); }

    protected override void OnAttached()
    {
        if (_created) return;
        _created = true;
        var theme = _owner.OwnerWindow?.Theme ?? Theme.Dark;
        _background = Shape.Gen()!;
        _background.SetFill(theme.WindowBackground.R8, theme.WindowBackground.G8, theme.WindowBackground.B8, 255);
        AddPaint(_background);
        _border = Shape.Gen()!; _border.StrokeWidth(1);
        _border.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, 255);
        _border.SetFill(0, 0, 0, 0); AddPaint(_border);
        _rows = Scene.Gen()!;
        _clip = Shape.Gen()!; _rows.Clip(_clip); AddPaint(_rows);
        _highlight = Shape.Gen()!;
        var selection = theme.SelectionColor;
        _highlight.SetFill(selection.R8, selection.G8, selection.B8, selection.A8);
        _rows.Add(_highlight);
        var style = _owner.Style;
        foreach (var item in _items)
        {
            var text = Text.Gen()!; text.SetFont(style.FontName); text.SetFontSize(EffectiveFontSize(style.FontSize));
            text.SetText(item); text.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);
            // Measure before parenting: Paint.Bounds includes ancestor transforms after attachment.
            text.Bounds(out var bx, out var by, out _, out var bh);
            _textMetrics.Add((bx, by, bh));
            var scene = Scene.Gen()!; scene.Add(text); _rows.Add(scene); _itemScenes.Add(scene);
        }
        _track = Shape.Gen()!; _track.SetFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, 90); AddPaint(_track);
        _thumb = Shape.Gen()!; _thumb.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8, 180); AddPaint(_thumb);
    }

    protected override void OnSizeChanged()
    {
        if (!_created) OnAttached();
        _background!.ResetShape(); _background.AppendRect(0, 0, Bounds.W, Bounds.H, 3, 3);
        _border!.ResetShape(); _border.AppendRect(.5f, .5f, Math.Max(0, Bounds.W - 1), Math.Max(0, Bounds.H - 1), 3, 3);
        _clip!.ResetShape(); _clip.AppendRect(2, 2, RowWidth, ViewHeight, 0, 0);
        _scroll = Math.Clamp(_scroll, 0, MaxScroll);
        PositionRows();
    }

    private void PositionRows()
    {
        if (!_created) return;
        for (var i = 0; i < _items.Length; i++)
        {
            var metrics = _textMetrics[i];
            _itemScenes[i].Translate(8 - metrics.X, 2 + i * ItemHeight - _scroll + (ItemHeight - metrics.Height) / 2 - metrics.Y);
            _itemScenes[i].Visible((i + 1) * ItemHeight > _scroll && i * ItemHeight < _scroll + ViewHeight);
        }
        _highlight!.ResetShape();
        _highlight.Visible(_highlightedIndex >= 0);
        if (_highlightedIndex >= 0) _highlight.AppendRect(2, 2 + _highlightedIndex * ItemHeight - _scroll, RowWidth, ItemHeight, 2, 2);
        _track!.Visible(HasScrollbar); _thumb!.Visible(HasScrollbar);
        _track.ResetShape(); _thumb.ResetShape();
        if (HasScrollbar)
        {
            _track.AppendRect(Bounds.W - 12, 2, 10, ViewHeight, 3, 3);
            _thumb.AppendRect(Bounds.W - 11, ThumbY, 8, ThumbHeight, 3, 3);
        }
        MarkDirty();
    }

    internal void RevealHighlight()
    {
        if (_highlightedIndex < 0) return;
        var top = _highlightedIndex * ItemHeight;
        if (top < _scroll) _scroll = top;
        if (top + ItemHeight > _scroll + ViewHeight) _scroll = top + ItemHeight - ViewHeight;
        _scroll = Math.Clamp(_scroll, 0, MaxScroll);
        PositionRows();
    }

    internal void UpdatePlacement()
    {
        var previous = Bounds; _owner.PositionPopup();
        if (previous.W != Bounds.W || previous.H != Bounds.H) RevealHighlight();
    }

    internal bool BelongsTo(Element? element) => ReferenceEquals(_owner, element);

    internal void Navigate(int key)
    {
        _highlightedIndex = key switch
        {
            GLFW_KEY_HOME => 0, GLFW_KEY_END => _items.Length - 1,
            GLFW_KEY_UP => Math.Max(0, _highlightedIndex - 1),
            _ => Math.Min(_items.Length - 1, _highlightedIndex + 1)
        };
        RevealHighlight();
    }

    private int RowAt(float x, float y)
    {
        WindowToLocal(x, y, out var lx, out var ly);
        if (lx < 2 || lx >= 2 + RowWidth || ly < 2 || ly >= Bounds.H - 2) return -1;
        var row = (int)Math.Floor((ly - 2 + _scroll) / ItemHeight);
        return row >= 0 && row < _items.Length ? row : -1;
    }

    public override void OnMouseMove(float x, float y)
    {
        if (_dragging)
        {
            WindowToLocal(x, y, out _, out var ly);
            var travel = ViewHeight - ThumbHeight;
            _scroll = Math.Clamp(_dragStartScroll + (travel > 0 ? (ly - _dragStartY) / travel * MaxScroll : 0), 0, MaxScroll);
            PositionRows(); return;
        }
        var row = RowAt(x, y);
        if (row >= 0 && row != _highlightedIndex) { _highlightedIndex = row; PositionRows(); }
    }

    public override bool OnMouseDown(int button, float x, float y)
    {
        if (button != 0) return false;
        WindowToLocal(x, y, out var lx, out var ly);
        _pressedIndex = _clickedIndex = -1;
        _scrollPress = HasScrollbar && lx >= Bounds.W - 14;
        if (_scrollPress)
        {
            _dragging = ly >= ThumbY && ly < ThumbY + ThumbHeight;
            _dragStartY = ly; _dragStartScroll = _scroll;
            if (!_dragging) { _scroll = Math.Clamp(_scroll + (ly < ThumbY ? -ViewHeight : ViewHeight), 0, MaxScroll); PositionRows(); }
        }
        else
        {
            _pressedIndex = RowAt(x, y);
            if (_pressedIndex >= 0) { _highlightedIndex = _pressedIndex; PositionRows(); }
        }
        return true;
    }

    public override bool OnMouseUp(int button, float x, float y)
    {
        if (button != 0) return false;
        _dragging = false;
        _clickedIndex = !_scrollPress && _pressedIndex >= 0 && RowAt(x, y) == _pressedIndex ? _pressedIndex : -1;
        return true;
    }

    public override void OnDragEnd() => _dragging = false;
    public override void OnClick()
    {
        var index = _clickedIndex; _clickedIndex = _pressedIndex = -1;
        if (index >= 0) _owner.SelectFromPopup(index);
    }

    public override void OnScroll(float dx, float dy)
    {
        _scroll = Math.Clamp(_scroll - dy * ItemHeight * 3, 0, MaxScroll); PositionRows();
    }
}
