using System;
using System.Collections.Generic;
using System.Numerics;
using ThorVG;

namespace Marius.Winter;

public class TreeNode
{
    public string Text { get; set; }
    public string? IconSvg { get; set; }
    public List<TreeNode> Children { get; } = new();
    public bool IsExpanded { get; set; }
    public object? Tag { get; set; }
    public TreeNode? Parent { get; internal set; }

    public TreeNode(string text) => Text = text;

    public bool HasChildren => Children.Count > 0;
}

/// <summary>
/// Hierarchical tree control with expand/collapse, selection, and scrolling.
/// Manages all rendering internally — each visible row is a set of ThorVG shapes.
/// </summary>
public class TreeView : Element
{
    private readonly List<TreeNode> _roots = new();
    private readonly List<VisibleRow> _visibleRows = new();
    private bool _shapesCreated;
    private Shape? _backgroundShape;
    private Shape? _clipShape;
    private Shape? _selectionHighlight;
    private Shape? _hoverHighlight;
    private Shape? _scrollTrack;
    private Shape? _scrollThumb;

    private int _selectedIndex = -1;
    private int _hoveredIndex = -1;
    private float _scroll;
    private float _contentHeight;
    private bool _draggingThumb;
    private float _dragStartScroll;
    private float _dragStartY;


    private const float RowHeight = 24f;
    private const float IndentWidth = 20f;
    private const float ArrowAreaWidth = 16f;
    private const float ArrowSize = 4.5f;
    private const float ScrollBarWidth = 8f;
    private const float ScrollBarPadding = 4f;
    private const float IconSize = 16f;
    private const float IconGap = 4f;

    private struct VisibleRow
    {
        public TreeNode Node;
        public int Depth;
        public Shape? Arrow;
        public Scene? IconScene;
        public Picture? IconPaint;
        public Scene TextScene;
        public Text TextObj;
    }

    // --- Events ---

    public Action<TreeNode?>? SelectionChanged;
    public Action<TreeNode>? NodeExpanded;
    public Action<TreeNode>? NodeCollapsed;

    // --- Public API ---

    public TreeNode AddNode(string text, TreeNode? parent = null, string? iconSvg = null)
    {
        var node = new TreeNode(text) { Parent = parent, IconSvg = iconSvg };
        if (parent != null)
            parent.Children.Add(node);
        else
            _roots.Add(node);

        if (_shapesCreated)
            RebuildVisibleRows();

        return node;
    }

    public void RemoveNode(TreeNode node)
    {
        if (node.Parent != null)
            node.Parent.Children.Remove(node);
        else
            _roots.Remove(node);
        node.Parent = null;

        if (_shapesCreated)
            RebuildVisibleRows();
    }

    public TreeNode? SelectedNode
    {
        get => _selectedIndex >= 0 && _selectedIndex < _visibleRows.Count
            ? _visibleRows[_selectedIndex].Node : null;
        set
        {
            if (value == null) { SelectRow(-1); return; }
            for (int i = 0; i < _visibleRows.Count; i++)
            {
                if (_visibleRows[i].Node == value) { SelectRow(i); return; }
            }
        }
    }

    public void ExpandAll()
    {
        foreach (var root in _roots) ExpandAllRecursive(root);
        if (_shapesCreated) RebuildVisibleRows();
    }

    public void CollapseAll()
    {
        foreach (var root in _roots) CollapseAllRecursive(root);
        if (_shapesCreated) RebuildVisibleRows();
    }

    private static void ExpandAllRecursive(TreeNode node)
    {
        if (node.HasChildren) node.IsExpanded = true;
        foreach (var c in node.Children) ExpandAllRecursive(c);
    }

    private static void CollapseAllRecursive(TreeNode node)
    {
        node.IsExpanded = false;
        foreach (var c in node.Children) CollapseAllRecursive(c);
    }

    // --- Element lifecycle ---

    protected override void OnAttached()
    {
        if (!_shapesCreated)
        {
            _shapesCreated = true;
            var style = Style;
            var theme = OwnerWindow?.Theme ?? Theme.Dark;

            // Background
            _backgroundShape = Shape.Gen();
            var bg = style.Background;
            _backgroundShape!.SetFill(bg.R8, bg.G8, bg.B8, bg.A8);
            AddPaint(_backgroundShape);

            // Selection highlight — rendered between background and row content
            _selectionHighlight = Shape.Gen();
            _selectionHighlight!.Visible(false);
            AddPaint(_selectionHighlight);

            // Hover highlight
            _hoverHighlight = Shape.Gen();
            _hoverHighlight!.Visible(false);
            AddPaint(_hoverHighlight);

            // Clip
            _clipShape = Shape.Gen();
            _clipShape!.AppendRect(Bounds.X, Bounds.Y, Bounds.W, Bounds.H, 0, 0);
            Scene.Clip(_clipShape);

            // Scrollbar (on Scene directly, not paintScene, so it renders on top)
            _scrollTrack = Shape.Gen();
            var trackColor = new Color4(0f, 0f, 0f, 32 / 255f);
            _scrollTrack!.SetFill(trackColor.R8, trackColor.G8, trackColor.B8, trackColor.A8);
            Scene.Add(_scrollTrack);

            _scrollThumb = Shape.Gen();
            var thumbColor = new Color4(theme.BorderLight.R, theme.BorderLight.G, theme.BorderLight.B, 0.5f);
            _scrollThumb!.SetFill(thumbColor.R8, thumbColor.G8, thumbColor.B8, thumbColor.A8);
            Scene.Add(_scrollThumb);

            ApplyHighlightColors();
            RebuildVisibleRows();
        }
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        // TreeView fills available space (it scrolls internally)
        float w = float.IsFinite(availableWidth) ? availableWidth : Bounds.W;
        float h = float.IsFinite(availableHeight) ? availableHeight : _contentHeight;
        return new Vector2(w, h);
    }

    protected override void OnSizeChanged()
    {
        float w = Bounds.W, h = Bounds.H;

        _backgroundShape?.ResetShape();
        _backgroundShape?.AppendRect(0, 0, w, h, Style.CornerRadius, Style.CornerRadius);

        _clipShape?.ResetShape();
        _clipShape?.AppendRect(Bounds.X, Bounds.Y, w, h, 0, 0);

        // Re-order scrollbar on top
        if (_scrollTrack != null) { Scene.Remove(_scrollTrack); Scene.Add(_scrollTrack); }
        if (_scrollThumb != null) { Scene.Remove(_scrollThumb); Scene.Add(_scrollThumb); }

        UpdateRowPositions();
        UpdateHighlights();
        UpdateScrollbar();
    }

    // --- Visible row management ---

    private void RebuildVisibleRows()
    {
        // Remove old row visuals
        foreach (var row in _visibleRows)
        {
            if (row.Arrow != null) RemovePaint(row.Arrow);
            if (row.IconScene != null) RemovePaint(row.IconScene);
            RemovePaint(row.TextScene);
        }
        _visibleRows.Clear();

        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        var style = Style;
        float fontSize = EffectiveFontSize(style.FontSize);

        // Walk tree depth-first, build visible rows
        foreach (var root in _roots)
            CollectVisibleRows(root, 0, theme, style, fontSize);

        _contentHeight = _visibleRows.Count * RowHeight;

        // Clamp selection/hover
        if (_selectedIndex >= _visibleRows.Count) _selectedIndex = -1;
        if (_hoveredIndex >= _visibleRows.Count) _hoveredIndex = -1;

        // Re-order scrollbar on top of new rows
        if (_scrollTrack != null) { Scene.Remove(_scrollTrack); Scene.Add(_scrollTrack); }
        if (_scrollThumb != null) { Scene.Remove(_scrollThumb); Scene.Add(_scrollThumb); }

        UpdateRowPositions();
        UpdateHighlights();
        UpdateScrollbar();
        MarkDirty();
    }

    private void CollectVisibleRows(TreeNode node, int depth, Theme theme, Style style, float fontSize)
    {
        // Arrow shape (if has children)
        Shape? arrow = null;
        if (node.HasChildren)
        {
            arrow = Shape.Gen()!;
            arrow.StrokeWidth(1.5f);
            arrow.StrokeFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8, theme.TextColor.A8);
            arrow.StrokeCap(StrokeCap.Round);
            arrow.StrokeJoin(StrokeJoin.Round);
            arrow.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8, theme.TextColor.A8);
            AddPaint(arrow);
        }

        // Icon (optional SVG)
        Scene? iconScene = null;
        Picture? iconPaint = null;
        if (!string.IsNullOrEmpty(node.IconSvg))
        {
            var picture = Picture.Gen();
            if (picture != null)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(node.IconSvg);
                if (picture.Load(bytes, (uint)bytes.Length, "svg", null, true) == Result.Success)
                {
                    picture.GetSize(out float iw, out float ih);
                    if (iw > 0 && ih > 0)
                    {
                        float scale = MathF.Min(IconSize / iw, IconSize / ih);
                        picture.SetSize(iw * scale, ih * scale);
                    }
                    iconPaint = picture;
                    iconScene = Scene.Gen()!;
                    iconScene.Add(iconPaint);
                    AddPaint(iconScene);
                }
            }
        }

        // Text
        var textObj = ThorVG.Text.Gen()!;
        textObj.SetFont(style.FontName);
        textObj.SetFontSize(fontSize);
        textObj.SetText(node.Text);
        textObj.SetFill(theme.TextColor.R8, theme.TextColor.G8, theme.TextColor.B8);

        var textScene = Scene.Gen()!;
        textScene.Add(textObj);
        AddPaint(textScene);

        _visibleRows.Add(new VisibleRow
        {
            Node = node,
            Depth = depth,
            Arrow = arrow,
            IconScene = iconScene,
            IconPaint = iconPaint,
            TextScene = textScene,
            TextObj = textObj,
        });

        if (node.IsExpanded)
        {
            foreach (var child in node.Children)
                CollectVisibleRows(child, depth + 1, theme, style, fontSize);
        }
    }

    private void UpdateRowPositions()
    {
        float w = Bounds.W;
        float scrollOffset = GetScrollOffset();
        var style = Style;
        float fontSize = EffectiveFontSize(style.FontSize);

        for (int i = 0; i < _visibleRows.Count; i++)
        {
            var row = _visibleRows[i];
            float rowY = i * RowHeight - scrollOffset;
            float indent = row.Depth * IndentWidth + 4;

            // Arrow
            if (row.Arrow != null)
            {
                float arrowX = indent + ArrowAreaWidth / 2f;
                float arrowY = rowY + RowHeight / 2f;
                row.Arrow.ResetShape();

                if (row.Node.IsExpanded)
                {
                    // Down-pointing triangle
                    row.Arrow.MoveTo(arrowX - ArrowSize, arrowY - ArrowSize * 0.4f);
                    row.Arrow.LineTo(arrowX + ArrowSize, arrowY - ArrowSize * 0.4f);
                    row.Arrow.LineTo(arrowX, arrowY + ArrowSize * 0.6f);
                    row.Arrow.Close();
                }
                else
                {
                    // Right-pointing triangle
                    row.Arrow.MoveTo(arrowX - ArrowSize * 0.4f, arrowY - ArrowSize);
                    row.Arrow.LineTo(arrowX + ArrowSize * 0.6f, arrowY);
                    row.Arrow.LineTo(arrowX - ArrowSize * 0.4f, arrowY + ArrowSize);
                    row.Arrow.Close();
                }
            }

            // Icon positioning
            float afterArrow = indent + ArrowAreaWidth;
            float textX = afterArrow;
            if (row.IconScene != null && row.IconPaint != null)
            {
                row.IconPaint.GetSize(out float iconW, out float iconH);
                float iconX = afterArrow;
                float iconY = rowY + (RowHeight - iconH) / 2f;
                ref var iconMat = ref row.IconScene.Transform();
                iconMat = new Matrix(1, 0, 0, 0, 1, 0, 0, 0, 1);
                row.IconScene.Translate(iconX, iconY);
                textX = afterArrow + IconSize + IconGap;
            }

            // Text positioning — reset to identity then translate (avoids accumulation)
            var tm = ThorVG.Text.Gen();
            if (tm != null)
            {
                tm.SetFont(style.FontName);
                tm.SetFontSize(fontSize);
                tm.SetText(row.Node.Text);
                tm.Bounds(out float bx, out float by, out float bw, out float bh);
                if (bh > 0)
                {
                    float tx = textX - bx;
                    float ty = rowY + (RowHeight - bh) / 2f - by;
                    ref var mat = ref row.TextScene.Transform();
                    mat = new Matrix(1, 0, 0, 0, 1, 0, 0, 0, 1);
                    row.TextScene.Translate(tx, ty);
                }
            }
        }
    }

    private void UpdateHighlights()
    {
        float w = Bounds.W;
        float scrollOffset = GetScrollOffset();

        // Selection highlight
        if (_selectedIndex >= 0 && _selectedIndex < _visibleRows.Count)
        {
            float rowY = _selectedIndex * RowHeight - scrollOffset;
            _selectionHighlight?.ResetShape();
            _selectionHighlight?.AppendRect(0, rowY, w, RowHeight, 0, 0);
            _selectionHighlight?.Visible(true);
        }
        else
        {
            _selectionHighlight?.Visible(false);
        }

        // Hover highlight (don't show on selected row — selection is already visible)
        if (_hoveredIndex >= 0 && _hoveredIndex < _visibleRows.Count && _hoveredIndex != _selectedIndex)
        {
            float rowY = _hoveredIndex * RowHeight - scrollOffset;
            _hoverHighlight?.ResetShape();
            _hoverHighlight?.AppendRect(0, rowY, w, RowHeight, 0, 0);
            _hoverHighlight?.Visible(true);
        }
        else
        {
            _hoverHighlight?.Visible(false);
        }
    }

    private void ApplyHighlightColors()
    {
        var theme = OwnerWindow?.Theme ?? Theme.Dark;

        // Selection: strong visible highlight
        var sel = new Color4(theme.SelectionColor.R, theme.SelectionColor.G,
            theme.SelectionColor.B, MathF.Max(theme.SelectionColor.A, 0.5f));
        _selectionHighlight?.SetFill(sel.R8, sel.G8, sel.B8, sel.A8);

        // Hover: lighter but still clearly visible
        var hov = new Color4(theme.SelectionColor.R, theme.SelectionColor.G,
            theme.SelectionColor.B, MathF.Max(theme.SelectionColor.A * 0.6f, 0.25f));
        _hoverHighlight?.SetFill(hov.R8, hov.G8, hov.B8, hov.A8);
    }

    // --- Scrolling ---

    private float GetScrollOffset()
    {
        float visibleH = Bounds.H;
        if (_contentHeight <= visibleH) return 0;
        return _scroll * (_contentHeight - visibleH);
    }

    private bool NeedsScrollbar() => _contentHeight > Bounds.H;

    private void UpdateScrollbar()
    {
        float w = Bounds.W, h = Bounds.H;

        if (!NeedsScrollbar())
        {
            _scrollTrack?.Visible(false);
            _scrollThumb?.Visible(false);
            return;
        }

        _scrollTrack?.Visible(true);
        _scrollThumb?.Visible(true);

        float trackX = w - ScrollBarWidth - 2;
        float trackY = ScrollBarPadding;
        float trackH = h - 2 * ScrollBarPadding;

        _scrollTrack?.ResetShape();
        _scrollTrack?.AppendRect(trackX, trackY, ScrollBarWidth, trackH, 3, 3);

        float ratio = Math.Min(1f, h / _contentHeight);
        float thumbH = Math.Max(20, trackH * ratio);
        float thumbY = trackY + _scroll * (trackH - thumbH);

        _scrollThumb?.ResetShape();
        _scrollThumb?.AppendRect(trackX + 1, thumbY + 1, ScrollBarWidth - 2, thumbH - 2, 2, 2);
    }

    // --- Input handling ---

    public override Element? HitTest(float x, float y)
    {
        if (!Visible || !Enabled) return null;
        float localX = x - Bounds.X;
        float localY = y - Bounds.Y;
        if (!new RectF(0, 0, Bounds.W, Bounds.H).Contains(localX, localY))
            return null;
        return this;
    }

    public override bool OnMouseDown(int button, float x, float y)
    {
        if (!Enabled || button != 0) return false;
        WindowToLocal(x, y, out float lx, out float ly);

        // Scrollbar drag
        if (NeedsScrollbar() && lx >= Bounds.W - ScrollBarWidth - ScrollBarPadding)
        {
            _draggingThumb = true;
            _dragStartScroll = _scroll;
            _dragStartY = ly;
            return true;
        }

        float scrollOffset = GetScrollOffset();
        int rowIndex = (int)((ly + scrollOffset) / RowHeight);
        if (rowIndex < 0 || rowIndex >= _visibleRows.Count) return true;

        var row = _visibleRows[rowIndex];
        float indent = row.Depth * IndentWidth + 4;

        // Click on arrow area → toggle expand
        if (row.Node.HasChildren && lx < indent + ArrowAreaWidth)
        {
            row.Node.IsExpanded = !row.Node.IsExpanded;
            if (row.Node.IsExpanded)
                NodeExpanded?.Invoke(row.Node);
            else
                NodeCollapsed?.Invoke(row.Node);
            RebuildVisibleRows();
        }
        else
        {
            SelectRow(rowIndex);
        }

        return true;
    }

    public override void OnDoubleClick()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _visibleRows.Count)
        {
            var node = _visibleRows[_selectedIndex].Node;
            if (node.HasChildren)
            {
                node.IsExpanded = !node.IsExpanded;
                if (node.IsExpanded)
                    NodeExpanded?.Invoke(node);
                else
                    NodeCollapsed?.Invoke(node);

                RebuildVisibleRows();
            }
        }

        base.OnDoubleClick();
    }

    public override bool OnMouseUp(int button, float x, float y)
    {
        if (button != 0) return false;
        _draggingThumb = false;
        return true;
    }

    public override void OnMouseMove(float x, float y)
    {
        if (_draggingThumb && NeedsScrollbar())
        {
            WindowToLocal(x, y, out _, out float ly);
            float trackH = Bounds.H - 2 * ScrollBarPadding;
            float ratio = Math.Min(1f, Bounds.H / _contentHeight);
            float thumbH = Math.Max(20, trackH * ratio);
            float scrollableTrack = trackH - thumbH;
            if (scrollableTrack > 0)
            {
                float delta = (ly - _dragStartY) / scrollableTrack;
                SetScroll(_dragStartScroll + delta);
            }
            return;
        }

        WindowToLocal(x, y, out float lxm, out float lym);
        float offset = GetScrollOffset();
        int newHover = (int)((lym + offset) / RowHeight);
        if (newHover < 0 || newHover >= _visibleRows.Count) newHover = -1;

        if (newHover != _hoveredIndex)
        {
            _hoveredIndex = newHover;
            UpdateHighlights();
            MarkDirty();
        }
    }

    public override void OnMouseLeave()
    {
        if (_hoveredIndex >= 0)
        {
            _hoveredIndex = -1;
            UpdateHighlights();
            MarkDirty();
        }
    }

    public override void OnScroll(float dx, float dy)
    {
        if (!NeedsScrollbar()) { base.OnScroll(dx, dy); return; }
        float step = Bounds.H * 0.25f / _contentHeight;
        SetScroll(_scroll - dy * step);
    }

    public override void OnKeyDown(int key, int mods, bool repeat)
    {
        // Up/Down arrow navigation
        if (key == 265) // GLFW_KEY_UP
        {
            if (_selectedIndex > 0) SelectRow(_selectedIndex - 1);
            return;
        }
        if (key == 264) // GLFW_KEY_DOWN
        {
            if (_selectedIndex < _visibleRows.Count - 1) SelectRow(_selectedIndex + 1);
            return;
        }
        // Left = collapse, Right = expand
        if (key == 263 && _selectedIndex >= 0) // GLFW_KEY_LEFT
        {
            var node = _visibleRows[_selectedIndex].Node;
            if (node.IsExpanded && node.HasChildren)
            {
                node.IsExpanded = false;
                NodeCollapsed?.Invoke(node);
                RebuildVisibleRows();
            }
            else if (node.Parent != null)
            {
                SelectedNode = node.Parent;
            }
            return;
        }
        if (key == 262 && _selectedIndex >= 0) // GLFW_KEY_RIGHT
        {
            var node = _visibleRows[_selectedIndex].Node;
            if (node.HasChildren && !node.IsExpanded)
            {
                node.IsExpanded = true;
                NodeExpanded?.Invoke(node);
                RebuildVisibleRows();
            }
            return;
        }

        base.OnKeyDown(key, mods, repeat);
    }

    private void SelectRow(int index)
    {
        if (index == _selectedIndex) return;
        _selectedIndex = index;
        UpdateHighlights();
        EnsureRowVisible(index);
        SelectionChanged?.Invoke(SelectedNode);
        MarkDirty();
    }

    private void SetScroll(float value)
    {
        float clamped = Math.Clamp(value, 0f, 1f);
        if (_scroll == clamped) return;
        _scroll = clamped;
        UpdateRowPositions();
        UpdateHighlights();
        UpdateScrollbar();
        MarkDirty();
    }

    private void EnsureRowVisible(int index)
    {
        if (index < 0 || !NeedsScrollbar()) return;
        float rowTop = index * RowHeight;
        float rowBottom = rowTop + RowHeight;
        float scrollOffset = GetScrollOffset();
        float visibleH = Bounds.H;

        if (rowTop < scrollOffset)
            SetScroll(rowTop / (_contentHeight - visibleH));
        else if (rowBottom > scrollOffset + visibleH)
            SetScroll((rowBottom - visibleH) / (_contentHeight - visibleH));
    }

    // --- Theme ---

    protected override Style GetDefaultStyle()
    {
        return OwnerWindow?.Theme.Panel ?? new Style();
    }

    protected override void OnThemeChanged()
    {
        if (!_shapesCreated) return;
        var theme = OwnerWindow?.Theme ?? Theme.Dark;
        var style = Style;
        var bg = style.Background;
        _backgroundShape?.SetFill(bg.R8, bg.G8, bg.B8, bg.A8);

        var thumbColor = new Color4(theme.BorderLight.R, theme.BorderLight.G, theme.BorderLight.B, 0.5f);
        _scrollThumb?.SetFill(thumbColor.R8, thumbColor.G8, thumbColor.B8, thumbColor.A8);

        ApplyHighlightColors();

        // Rebuild rows to pick up new text/arrow colors
        RebuildVisibleRows();
    }
}
