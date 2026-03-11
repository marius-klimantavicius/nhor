using System;
using System.Collections.Generic;
using System.Numerics;
using ThorVG;

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
            t.SetFont(style.FontName);
            t.SetFontSize(EffectiveFontSize(style.FontSize));
            foreach (var item in _items)
            {
                t.SetText(item);
                t.Bounds(out _, out _, out float bw, out _);
                if (bw > maxW) maxW = bw;
            }
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
        m.SetFont(style.FontName);
        m.SetFontSize(EffectiveFontSize(style.FontSize));
        m.SetText(text);
        m.Bounds(out float bx, out float by, out float bw, out float bh);
        if (bw <= 0 || bh <= 0) return;

        float x = 10 - bx; // left-aligned with padding
        float y = (Bounds.H - bh) / 2f - by;
        _labelScene.Translate(x, y);
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

    private void OpenPopup()
    {
        var win = OwnerWindow;
        if (win == null || _items.Count == 0) return;

        _isOpen = true;

        // Calculate popup position in window coordinates
        float popupX = 0, popupY = 0;
        // Walk up from this element to get absolute position
        Element? e = this;
        while (e != null && e != win)
        {
            popupX += e.Bounds.X;
            popupY += e.Bounds.Y;
            e = e.Parent;
        }

        _popup = new ComboBoxPopup(this, _items, _selectedIndex);
        float itemH = Style.FontSize + 8;
        float popupH = Math.Min(_items.Count * itemH + 4, 200);
        _popup.Arrange(new RectF(popupX, popupY + Bounds.H + 2, Bounds.W, popupH));

        win.ShowOverlay(_popup);

        // Fade-in animation
        _popup.Opacity = 0;
        win.Animator.Cancel("combobox_popup");
        win.Animator.Start(new Animation
        {
            Duration = 0.12f,
            Easing = Easings.EaseOutCubic,
            Tag = "combobox_popup",
            Apply = t => { if (_popup != null) _popup.Opacity = t; }
        });
    }

    internal void ClosePopup()
    {
        if (!_isOpen) return;
        _isOpen = false;
        if (_popup != null)
        {
            OwnerWindow?.Animator?.Cancel("combobox_popup");
            OwnerWindow?.RemoveOverlay(_popup);
            _popup = null;
        }
    }

    internal void SelectFromPopup(int index)
    {
        SelectedIndex = index;
        ClosePopup();
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
        MarkDirty();
    }

}

/// <summary>
/// The dropdown popup displayed when ComboBox is open. Rendered as an overlay.
/// </summary>
internal class ComboBoxPopup : Element
{
    private readonly ComboBox _owner;
    private readonly List<string> _items;
    private int _hoveredIndex = -1;
    private Shape? _backgroundShape;
    private Shape? _borderShape;
    private Shape? _highlightShape;
    private readonly List<Scene> _itemScenes = new();
    private readonly List<Text> _itemTexts = new();
    private bool _shapesCreated;
    private float _itemHeight;

    public ComboBoxPopup(ComboBox owner, List<string> items, int selectedIndex)
    {
        _owner = owner;
        _items = items;
        _hoveredIndex = selectedIndex;
    }

    protected override void OnAttached()
    {
        // We're an overlay, so OnAttached may not be called via AddChild.
        // Initialize shapes on first size change instead.
    }

    private void EnsureShapes()
    {
        if (_shapesCreated) return;
        _shapesCreated = true;

        var theme = _owner.OwnerWindow?.Theme ?? Theme.Dark;
        var style = _owner.Style;
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

        // Item texts
        float fontSize = EffectiveFontSize(style.FontSize);
        for (int i = 0; i < _items.Count; i++)
        {
            var text = ThorVG.Text.Gen();
            text!.SetFont(style.FontName);
            text.SetFontSize(fontSize);
            text.SetText(_items[i]);
            text.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);

            var scene = Scene.Gen()!;
            scene.Add(text);
            AddPaint(scene);

            _itemTexts.Add(text);
            _itemScenes.Add(scene);
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
        var style = _owner.Style;
        float fontSize = EffectiveFontSize(style.FontSize);

        for (int i = 0; i < _itemScenes.Count && i < _items.Count; i++)
        {
            // Measure for by compensation
            var m = ThorVG.Text.Gen();
            if (m == null) continue;
            m.SetFont(style.FontName);
            m.SetFontSize(fontSize);
            m.SetText(_items[i]);
            m.Bounds(out float bx, out float by, out _, out float bh);
            if (bh <= 0) continue;

            float x = 8 - bx;
            float y = 2 + i * _itemHeight + (_itemHeight - bh) / 2f - by;
            _itemScenes[i].Translate(x, y);
        }

        UpdateHighlight();
    }

    private void UpdateHighlight()
    {
        if (_highlightShape == null) return;
        if (_hoveredIndex < 0 || _hoveredIndex >= _items.Count)
        {
            _highlightShape.Visible(false);
            return;
        }

        _highlightShape.Visible(true);
        _highlightShape.ResetShape();
        float y = 2 + _hoveredIndex * _itemHeight;
        _highlightShape.AppendRect(2, y, Bounds.W - 4, _itemHeight, 2, 2);
    }

    public override Element? HitTest(float x, float y)
    {
        if (!Visible) return null;
        float localX = x - Bounds.X;
        float localY = y - Bounds.Y;
        if (new RectF(0, 0, Bounds.W, Bounds.H).Contains(localX, localY))
            return this;
        return null;
    }

    public override void OnMouseMove(float x, float y)
    {
        WindowToLocal(x, y, out _, out float ly);
        int index = (int)((ly - 2) / _itemHeight);
        if (index < 0 || index >= _items.Count) index = -1;
        if (index != _hoveredIndex)
        {
            _hoveredIndex = index;
            UpdateHighlight();
            MarkDirty();
        }
    }

    public override bool OnMouseDown(int button, float x, float y)
    {
        return button == 0;
    }

    public override bool OnMouseUp(int button, float x, float y)
    {
        return button == 0;
    }

    public override void OnClick()
    {
        if (_hoveredIndex >= 0 && _hoveredIndex < _items.Count)
            _owner.SelectFromPopup(_hoveredIndex);
        else
            _owner.ClosePopup();
    }

}
