using System;
using System.Collections.Generic;
using System.Numerics;
using ThorVG;
using static Glfw.GLFW;

namespace Marius.Winter;

/// <summary>
/// Tabbed container. Each tab has a header label and a content element.
/// Only the active tab's content is visible.
/// </summary>
public class TabWidget : Element
{
    private Shape? _headerBackground;
    private Shape? _activeTabShape;
    private Shape? _contentBorder;
    private Shape? _focusShape;
    private Scene? _headerLayer;
    private Shape? _headerClip;
    private float _headerOffset;
    private readonly List<TabInfo> _tabs = new();

    internal override bool ManagesOwnChildLayout => true;
    private readonly List<Scene> _headerScenes = new();
    private readonly List<Text> _headerTexts = new();
    private int _activeTab = -1;
    private bool _explicitClear;
    private bool _shapesCreated;
    private float _tabHeaderHeight;

    private class TabInfo
    {
        public string Label = "";
        public Element Content = null!;
        public bool Enabled = true;
        public bool Visible = true;
    }

    public TabWidget()
    {
        Focusable = true;
    }

    public event Action<int>? ActiveTabChanged;
    public event Action<int>? UserActiveTabChanged;

    public int ActiveTab
    {
        get => _activeTab;
        set
        {
            if (value < -1 || value >= _tabs.Count || value >= 0 && !Available(value)) return;
            _explicitClear = value == -1;
            SetSelection(value);
        }
    }

    public int TabCount => _tabs.Count;
    public Element? ActiveContent => _activeTab >= 0 ? _tabs[_activeTab].Content : null;

    private bool Available(int index) => _tabs[index].Enabled && _tabs[index].Visible;

    public void SelectFromUser(int index)
    {
        if (!IsEffectivelyEnabled || !IsEffectivelyVisible || IsDisposed || index < 0 || index >= _tabs.Count || !Available(index)) return;
        var previous = ActiveContent;
        ActiveTab = index;
        if (!ReferenceEquals(previous, ActiveContent)) UserActiveTabChanged?.Invoke(_activeTab);
    }

    public void SetTabEnabled(Element content, bool enabled)
    {
        var tab = _tabs.Find(t => ReferenceEquals(t.Content, content));
        if (tab == null || tab.Enabled == enabled) return;
        tab.Enabled = enabled; tab.Content.Enabled = enabled; RestoreSelection(ActiveContent);
    }

    public void SetTabVisible(Element content, bool visible)
    {
        var tab = _tabs.Find(t => ReferenceEquals(t.Content, content));
        if (tab == null || tab.Visible == visible) return;
        tab.Visible = visible; RestoreSelection(ActiveContent);
        if (_shapesCreated) RebuildHeaders();
    }

    public void AddTab(string label, Element content)
        => InsertTab(_tabs.Count, label, content);

    public void InsertTab(int index, string label, Element content)
    {
        var selected = ActiveContent;
        var tab = new TabInfo { Label = label, Content = content };
        _tabs.Insert(index, tab);
        InsertChild(content, index);
        RestoreSelection(selected ?? (!_explicitClear ? content : null));

        if (_shapesCreated)
            RebuildHeaders();

        InvalidateMeasure();
        MarkDirty();
    }

    public void RemoveTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        var tab = _tabs[index];
        var selected = ActiveContent;
        RemoveChild(tab.Content);
        _tabs.RemoveAt(index);

        if (ReferenceEquals(selected, tab.Content))
            selected = _tabs.Count > 0 ? _tabs[Math.Min(index, _tabs.Count - 1)].Content : null;
        RestoreSelection(selected);

        if (_shapesCreated)
            RebuildHeaders();

        InvalidateMeasure();
        MarkDirty();
    }

    public void UpdateTab(Element content, string label)
    {
        var tab = _tabs.Find(t => ReferenceEquals(t.Content, content));
        if (tab == null || tab.Label == label) return;
        tab.Label = label;
        if (_shapesCreated) RebuildHeaders();
        InvalidateMeasure();
        MarkDirty();
    }

    public void MoveTab(Element content, int index)
    {
        var oldIndex = _tabs.FindIndex(t => ReferenceEquals(t.Content, content));
        if (oldIndex < 0 || oldIndex == index) return;
        if ((uint)index >= (uint)_tabs.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var selected = ActiveContent;
        var tab = _tabs[oldIndex];
        _tabs.RemoveAt(oldIndex);
        _tabs.Insert(index, tab);
        var order = new List<Element>(Children);
        var owned = new HashSet<Element>();
        foreach (var entry in _tabs) owned.Add(entry.Content);
        var next = 0;
        for (var i = 0; i < order.Count; i++)
            if (owned.Contains(order[i])) order[i] = _tabs[next++].Content;
        ReorderChildren(order);
        RestoreSelection(selected);
        if (_shapesCreated) RebuildHeaders();
        InvalidateMeasure();
        MarkDirty();
    }

    private void RestoreSelection(Element? selected)
    {
        var index = _tabs.FindIndex(t => ReferenceEquals(t.Content, selected));
        if (index < 0 || !Available(index))
        {
            var start = Math.Clamp(index >= 0 ? index : _activeTab, 0, Math.Max(0, _tabs.Count - 1));
            index = -1;
            if (!_explicitClear)
                for (var offset = 0; offset < _tabs.Count; offset++)
                {
                    var candidate = (start + offset) % _tabs.Count;
                    if (Available(candidate)) { index = candidate; break; }
                }
        }
        SetSelection(index);
    }

    private void SetSelection(int index)
    {
        var previous = _activeTab;
        _activeTab = index;
        for (var i = 0; i < _tabs.Count; i++) _tabs[i].Content.Visible = i == _activeTab;
        _activeTabShape?.Visible(_activeTab >= 0);
        UpdateActiveTabHighlight();
        InvalidateMeasure(); MarkDirty();
        if (previous != _activeTab) ActiveTabChanged?.Invoke(_activeTab);
    }

    protected override void OnAttached()
    {
        if (!_shapesCreated)
        {
            _shapesCreated = true;
            var theme = OwnerWindow?.Theme ?? Theme.Dark;
            var style = Style;

            _tabHeaderHeight = style.FontSize + 12;

            // Header background
            _headerBackground = Shape.Gen();
            _headerBackground!.SetFill(theme.ButtonGradientTopPushed.R8, theme.ButtonGradientTopPushed.G8,
                theme.ButtonGradientTopPushed.B8, theme.ButtonGradientTopPushed.A8);
            AddPaint(_headerBackground);

            _headerLayer = Scene.Gen()!; _headerClip = Shape.Gen()!;
            _headerLayer.Clip(_headerClip); AddPaint(_headerLayer);

            // Active tab highlight
            _activeTabShape = Shape.Gen();
            var bg = style.Background;
            _activeTabShape!.SetFill(bg.R8, bg.G8, bg.B8, bg.A8);
            _headerLayer.Add(_activeTabShape);

            // Content border
            _contentBorder = Shape.Gen();
            _contentBorder!.StrokeWidth(1f);
            _contentBorder.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, theme.BorderDark.A8);
            _contentBorder.SetFill(0, 0, 0, 0);
            AddPaint(_contentBorder);

            _focusShape = Shape.Gen()!;
            _focusShape.SetFill(0, 0, 0, 0); _focusShape.StrokeWidth(2);
            _headerLayer.Add(_focusShape);

            RebuildHeaders();
        }
    }

    private void RebuildHeaders()
    {
        // Remove old header scenes
        foreach (var scene in _headerScenes)
            _headerLayer!.Remove(scene);
        _headerScenes.Clear();
        _headerTexts.Clear();

        var style = Style;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        float fontSize = EffectiveFontSize(style.FontSize);

        for (int i = 0; i < _tabs.Count; i++)
        {
            var text = ThorVG.Text.Gen();
            text!.SetFont(style.FontName);
            text.SetFontSize(fontSize);
            text.SetText(_tabs[i].Label);
            text.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);

            var scene = Scene.Gen()!;
            scene.Add(text);
            _headerLayer!.Add(scene);

            _headerTexts.Add(text);
            _headerScenes.Add(scene);
        }

        PositionHeaders();
        UpdateActiveTabHighlight();
    }

    private void PositionHeaders()
    {
        if (_headerScenes.Count == 0) return;

        var style = Style;
        float fontSize = EffectiveFontSize(style.FontSize);
        UpdateHeaderOffset();
        float x = 4 - _headerOffset;

        for (int i = 0; i < _tabs.Count && i < _headerScenes.Count; i++)
        {
            if (!_tabs[i].Visible) { _headerScenes[i].Visible(false); continue; }
            _headerScenes[i].Visible(true);
            float tabW = MeasureTabWidth(i);

            // Center text in tab
            var m = ThorVG.Text.Gen();
            if (m != null)
            {
                try
                {
                    m.SetFont(style.FontName);
                    m.SetFontSize(fontSize);
                    m.SetText(_tabs[i].Label);
                    m.Bounds(out float bx, out float by, out float bw, out float bh);
                    if (bw > 0 && bh > 0)
                    {
                        float tx = x + (tabW - bw) / 2f - bx;
                        float ty = (_tabHeaderHeight - bh) / 2f - by;
                        _headerScenes[i].Translate(tx, ty);
                    }
                }
                finally { m.Unref(); }
            }

            x += tabW + 2;
        }
    }

    private float MeasureTabWidth(int index)
    {
        if (!_tabs[index].Visible) return -2;
        var style = Style;
        var m = ThorVG.Text.Gen();
        if (m == null) return 60;
        try
        {
            m.SetFont(style.FontName);
            m.SetFontSize(EffectiveFontSize(style.FontSize));
            m.SetText(_tabs[index].Label);
            m.Bounds(out _, out _, out float bw, out _);
            var desired = Math.Max(40, bw + 20);
            return Bounds.W > 0 ? Math.Min(desired, Math.Max(1, Bounds.W - 8)) : desired;
        }
        finally { m.Unref(); }
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        var style = Style;
        _tabHeaderHeight = style.FontSize + 12;

        float contentW = (float.IsFinite(availableWidth) ? availableWidth : Bounds.W) - 8;
        float chrome = _tabHeaderHeight + 8;
        float contentMaxH = float.IsFinite(availableHeight) && availableHeight > chrome
            ? availableHeight - chrome
            : float.PositiveInfinity;

        // Measure active tab content to determine desired height
        float contentH = 0;
        if (_activeTab >= 0 && _activeTab < _tabs.Count)
        {
            var sz = _tabs[_activeTab].Content.Measure(contentW, contentMaxH);
            contentH = sz.Y;
        }

        float w = float.IsFinite(availableWidth) ? availableWidth : Bounds.W;
        float h = _tabHeaderHeight + 8 + contentH;
        return new Vector2(w, h);
    }

    protected override void OnSizeChanged()
    {
        float w = Bounds.W, h = Bounds.H;
        var style = Style;
        _tabHeaderHeight = style.FontSize + 12;

        // Header background
        _headerBackground?.ResetShape();
        _headerBackground?.AppendRect(0, 0, w, _tabHeaderHeight, 3, 3);
        _headerClip?.ResetShape();
        _headerClip?.AppendRect(0, 0, w, _tabHeaderHeight, 0, 0);

        // Content border
        _contentBorder?.ResetShape();
        _contentBorder?.AppendRect(0.5f, _tabHeaderHeight - 0.5f, w - 1, h - _tabHeaderHeight, 0, 0);

        UpdateActiveTabHighlight();
        PositionHeaders();
        // ArrangeContent is called from ArrangeCore — not here — so that
        // tab content is re-arranged even when TabWidget.Bounds didn't change.
    }

    protected override void ArrangeCore(RectF finalBounds)
    {
        ArrangeContent();
    }

    private void ArrangeContent()
    {
        float contentY = _tabHeaderHeight + 4;
        float contentH = Math.Max(0, Bounds.H - contentY - 4);
        float contentW = Math.Max(0, Bounds.W - 8);
        for (int i = 0; i < _tabs.Count; i++)
        {
            // Measure children so their DesiredSize is set before Arrange
            _tabs[i].Content.Measure(contentW, contentH);
            _tabs[i].Content.Arrange(new RectF(4, contentY, contentW, contentH));
        }
    }

    private void UpdateActiveTabHighlight()
    {
        UpdateHeaderOffset();
        PositionHeaders();
        _focusShape?.Visible((State & ElementState.Focused) != 0);
        _focusShape?.ResetShape();
        var focus = (OwnerWindow?.Theme ?? Theme.Dark).Button.BorderFocused;
        _focusShape?.StrokeFill(focus.R8, focus.G8, focus.B8, focus.A8);
        if (_activeTab < 0)
        {
            _focusShape?.AppendRect(2, 2, Math.Max(0, Bounds.W - 4), Math.Max(0, _tabHeaderHeight - 4), 2, 2);
            return;
        }
        if (_activeTabShape == null || _tabs.Count == 0) return;

        float x = 4 - _headerOffset;
        for (int i = 0; i < _activeTab; i++)
            x += MeasureTabWidth(i) + 2;

        float tabW = MeasureTabWidth(_activeTab);
        _activeTabShape.ResetShape();
        _activeTabShape.AppendRect(x, 0, tabW, _tabHeaderHeight, 3, 3);
        _focusShape?.AppendRect(x + 2, 2, Math.Max(0, tabW - 4), Math.Max(0, _tabHeaderHeight - 4), 2, 2);

        // Update text colors — active tab brighter
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        for (int i = 0; i < _headerTexts.Count; i++)
        {
            if (i == _activeTab)
                _headerTexts[i].SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);
            else
                _headerTexts[i].SetFill(theme.DisabledTextColor.R8, theme.DisabledTextColor.G8, theme.DisabledTextColor.B8);
        }
    }

    // --- Hit testing for tab headers ---

    public override Element? HitTest(float x, float y)
    {
        if (!Visible || !Enabled) return null;

        float localX = x - Bounds.X;
        float localY = y - Bounds.Y;

        if (!new RectF(0, 0, Bounds.W, Bounds.H).Contains(localX, localY))
            return null;

        // If in header area, we handle it
        if (localY < _tabHeaderHeight)
            return this;

        // Otherwise check children
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            var hit = Children[i].HitTest(localX, localY);
            if (hit != null) return hit;
        }

        return this;
    }

    public override bool OnMouseDown(int button, float x, float y)
    {
        if (!Enabled || button != 0) return false;

        WindowToLocal(x, y, out float lx, out float ly);

        // Check if click is in header area
        if (ly < _tabHeaderHeight)
        {
            int tabIndex = GetTabAtX(lx);
            if (tabIndex >= 0 && tabIndex < _tabs.Count)
                SelectFromUser(tabIndex);
            return true;
        }

        return false;
    }

    private int GetTabAtX(float localX)
    {
        float x = 4 - _headerOffset;
        for (int i = 0; i < _tabs.Count; i++)
        {
            float tabW = MeasureTabWidth(i);
            if (localX >= x && localX < x + tabW)
                return i;
            x += tabW + 2;
        }
        return -1;
    }

    private void UpdateHeaderOffset()
    {
        float total = 0, selectedX = 0;
        for (var i = 0; i < _tabs.Count; i++)
        {
            if (i == _activeTab) selectedX = total;
            total += MeasureTabWidth(i) + 2;
        }
        var width = Math.Max(0, Bounds.W - 8);
        if (_activeTab >= 0)
        {
            if (selectedX < _headerOffset) _headerOffset = selectedX;
            var end = selectedX + MeasureTabWidth(_activeTab);
            if (end > _headerOffset + width) _headerOffset = end - width;
        }
        _headerOffset = Math.Clamp(_headerOffset, 0, Math.Max(0, total - width));
    }

    protected override void OnStateChanged(ElementState oldState, ElementState newState)
    {
        UpdateActiveTabHighlight(); MarkDirty();
    }

    public override void OnKeyDown(int key, int mods, bool repeat)
    {
        if (!IsEffectivelyEnabled || !IsEffectivelyVisible || IsDisposed) return;
        // Header navigation must not consume arrows bubbled from a content editor.
        if (OwnerWindow != null && !ReferenceEquals(OwnerWindow.FocusedElement, this))
        { base.OnKeyDown(key, mods, repeat); return; }
        if (key != GLFW_KEY_LEFT && key != GLFW_KEY_RIGHT && key != GLFW_KEY_HOME && key != GLFW_KEY_END)
        { base.OnKeyDown(key, mods, repeat); return; }
        var direction = key == GLFW_KEY_LEFT || key == GLFW_KEY_END ? -1 : 1;
        var start = key == GLFW_KEY_HOME ? 0 : key == GLFW_KEY_END ? _tabs.Count - 1 :
            _activeTab < 0 ? (direction > 0 ? 0 : _tabs.Count - 1) : _activeTab + direction;
        for (var i = 0; i < _tabs.Count; i++)
        {
            var candidate = ((start + i * direction) % _tabs.Count + _tabs.Count) % _tabs.Count;
            if (Available(candidate)) { SelectFromUser(candidate); break; }
        }
    }

    protected override Style GetDefaultStyle()
    {
        return OwnerWindow?.Theme.Panel ?? new Style();
    }

    protected override void OnThemeChanged()
    {
        if (!_shapesCreated) return;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        var style = Style;

        _headerBackground?.SetFill(theme.ButtonGradientTopPushed.R8, theme.ButtonGradientTopPushed.G8,
            theme.ButtonGradientTopPushed.B8, theme.ButtonGradientTopPushed.A8);
        var bg = style.Background;
        _activeTabShape?.SetFill(bg.R8, bg.G8, bg.B8, bg.A8);
        _contentBorder?.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, theme.BorderDark.A8);

        RebuildHeaders(); UpdateActiveTabHighlight();

        MarkDirty();
    }

}
