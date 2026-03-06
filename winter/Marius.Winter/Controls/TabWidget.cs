using System;
using System.Collections.Generic;
using System.Numerics;
using ThorVG;

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
    private readonly List<TabInfo> _tabs = new();

    internal override bool ManagesOwnChildLayout => true;
    private readonly List<Scene> _headerScenes = new();
    private readonly List<Text> _headerTexts = new();
    private int _activeTab;
    private bool _shapesCreated;
    private float _tabHeaderHeight;

    private class TabInfo
    {
        public string Label = "";
        public Element Content = null!;
    }

    public TabWidget()
    {
    }

    public int ActiveTab
    {
        get => _activeTab;
        set
        {
            if (value < 0 || value >= _tabs.Count || value == _activeTab) return;
            _tabs[_activeTab].Content.Visible = false;
            _activeTab = value;
            _tabs[_activeTab].Content.Visible = true;
            UpdateActiveTabHighlight();
            MarkDirty();
        }
    }

    public int TabCount => _tabs.Count;

    public void AddTab(string label, Element content)
    {
        var tab = new TabInfo { Label = label, Content = content };
        _tabs.Add(tab);

        content.Visible = _tabs.Count == 1; // first tab visible, rest hidden

        if (_shapesCreated)
            RebuildHeaders();

        AddChild(content);

        InvalidateMeasure();
        MarkDirty();
    }

    public void RemoveTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        var tab = _tabs[index];
        RemoveChild(tab.Content);
        _tabs.RemoveAt(index);

        if (_activeTab >= _tabs.Count) _activeTab = Math.Max(0, _tabs.Count - 1);
        if (_tabs.Count > 0) _tabs[_activeTab].Content.Visible = true;

        if (_shapesCreated)
            RebuildHeaders();

        InvalidateMeasure();
        MarkDirty();
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

            // Active tab highlight
            _activeTabShape = Shape.Gen();
            var bg = style.Background;
            _activeTabShape!.SetFill(bg.R8, bg.G8, bg.B8, bg.A8);
            AddPaint(_activeTabShape);

            // Content border
            _contentBorder = Shape.Gen();
            _contentBorder!.StrokeWidth(1f);
            _contentBorder.StrokeFill(theme.BorderDark.R8, theme.BorderDark.G8, theme.BorderDark.B8, theme.BorderDark.A8);
            _contentBorder.SetFill(0, 0, 0, 0);
            AddPaint(_contentBorder);

            RebuildHeaders();
        }
    }

    private void RebuildHeaders()
    {
        // Remove old header scenes
        foreach (var scene in _headerScenes)
            RemovePaint(scene);
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
            AddPaint(scene);

            _headerTexts.Add(text);
            _headerScenes.Add(scene);
        }

        PositionHeaders();
    }

    private void PositionHeaders()
    {
        if (_headerScenes.Count == 0) return;

        var style = Style;
        float fontSize = EffectiveFontSize(style.FontSize);
        float x = 4;

        for (int i = 0; i < _tabs.Count; i++)
        {
            float tabW = MeasureTabWidth(i);

            // Center text in tab
            var m = ThorVG.Text.Gen();
            if (m != null)
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

            x += tabW + 2;
        }
    }

    private float MeasureTabWidth(int index)
    {
        var style = Style;
        var m = ThorVG.Text.Gen();
        if (m == null) return 60;
        m.SetFont(style.FontName);
        m.SetFontSize(EffectiveFontSize(style.FontSize));
        m.SetText(_tabs[index].Label);
        m.Bounds(out _, out _, out float bw, out _);
        return Math.Max(40, bw + 20); // minimum tab width with padding
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
        float contentH = Bounds.H - contentY - 4;
        float contentW = Bounds.W - 8;
        for (int i = 0; i < _tabs.Count; i++)
        {
            // Measure children so their DesiredSize is set before Arrange
            _tabs[i].Content.Measure(contentW, contentH);
            _tabs[i].Content.Arrange(new RectF(4, contentY, contentW, contentH));
        }
    }

    private void UpdateActiveTabHighlight()
    {
        if (_activeTabShape == null || _tabs.Count == 0) return;

        float x = 4;
        for (int i = 0; i < _activeTab; i++)
            x += MeasureTabWidth(i) + 2;

        float tabW = MeasureTabWidth(_activeTab);
        _activeTabShape.ResetShape();
        _activeTabShape.AppendRect(x, 0, tabW, _tabHeaderHeight, 3, 3);

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
                ActiveTab = tabIndex;
            return true;
        }

        return false;
    }

    private int GetTabAtX(float localX)
    {
        float x = 4;
        for (int i = 0; i < _tabs.Count; i++)
        {
            float tabW = MeasureTabWidth(i);
            if (localX >= x && localX < x + tabW)
                return i;
            x += tabW + 2;
        }
        return -1;
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

        for (int i = 0; i < _headerTexts.Count; i++)
        {
            if (i == _activeTab)
                _headerTexts[i].SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);
            else
                _headerTexts[i].SetFill(theme.DisabledTextColor.R8, theme.DisabledTextColor.G8, theme.DisabledTextColor.B8);
        }

        MarkDirty();
    }

}
