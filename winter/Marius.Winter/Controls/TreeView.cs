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

    /// <summary>
    /// When true, the expand arrow is shown even if Children is empty.
    /// Set to false after loading reveals no children.
    /// </summary>
    public bool MayHaveChildren { get; set; }

    public TreeNode(string text) => Text = text;

    public bool HasChildren => Children.Count > 0 || MayHaveChildren;
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
    private int _updateDepth; // > 0 means batching — skip rebuilds
    private Shape? _backgroundShape;
    private Shape? _clipShape;
    private Shape? _selectionHighlight;
    private Shape? _hoverHighlight;
    private Shape? _scrollTrack;
    private Shape? _scrollThumb;
    private Shape? _scrollTrackH;
    private Shape? _scrollThumbH;

    private int _selectedIndex = -1;
    private int _hoveredIndex = -1;
    private TreeNode? _pendingSelectedNode;
    private float _scroll;
    private float _scrollH;
    private float _contentHeight;
    private float _contentWidth;
    private bool _draggingThumb;
    private float _dragStartScroll;
    private float _dragStartY;
    private bool _draggingThumbH;
    private float _dragStartScrollH;
    private float _dragStartX;


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
        public float TextBoundsX, TextBoundsY, TextBoundsW, TextBoundsH;
    }

    // --- Events ---

    public Action<TreeNode?>? SelectionChanged;
    public Action<TreeNode>? NodeExpanded;
    public Action<TreeNode>? NodeCollapsed;
    public Action<TreeNode>? NodeDoubleClicked;

    // --- Public API ---

    public List<TreeNode> Nodes => _roots;

    /// <summary>Suppress tree rebuilds until EndUpdate is called. Nestable.</summary>
    public void BeginUpdate() => _updateDepth++;

    /// <summary>Re-enable tree rebuilds and apply pending changes.</summary>
    public void EndUpdate()
    {
        if (_updateDepth > 0) _updateDepth--;
        if (_updateDepth == 0 && _shapesCreated)
            RebuildVisibleRows();
    }

    public TreeNode AddNode(string text, TreeNode? parent = null, string? iconSvg = null)
    {
        var node = new TreeNode(text) { Parent = parent, IconSvg = iconSvg };
        if (parent != null)
            parent.Children.Add(node);
        else
            _roots.Add(node);

        if (_shapesCreated && _updateDepth == 0)
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

        if (_shapesCreated && _updateDepth == 0)
            RebuildVisibleRows();
    }

    public TreeNode? SelectedNode
    {
        get => _selectedIndex >= 0 && _selectedIndex < _visibleRows.Count
            ? _visibleRows[_selectedIndex].Node : null;
        set
        {
            _pendingSelectedNode = null;
            if (value == null) { SelectRow(-1); return; }
            for (int i = 0; i < _visibleRows.Count; i++)
            {
                if (_visibleRows[i].Node == value) { SelectRow(i); return; }
            }
            // Rows not built yet — defer until next rebuild
            _pendingSelectedNode = value;
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

            // Horizontal scrollbar
            _scrollTrackH = Shape.Gen();
            _scrollTrackH!.SetFill(trackColor.R8, trackColor.G8, trackColor.B8, trackColor.A8);
            Scene.Add(_scrollTrackH);

            _scrollThumbH = Shape.Gen();
            _scrollThumbH!.SetFill(thumbColor.R8, thumbColor.G8, thumbColor.B8, thumbColor.A8);
            Scene.Add(_scrollThumbH);

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

        // Re-order scrollbars on top
        if (_scrollTrack != null) { Scene.Remove(_scrollTrack); Scene.Add(_scrollTrack); }
        if (_scrollThumb != null) { Scene.Remove(_scrollThumb); Scene.Add(_scrollThumb); }
        if (_scrollTrackH != null) { Scene.Remove(_scrollTrackH); Scene.Add(_scrollTrackH); }
        if (_scrollThumbH != null) { Scene.Remove(_scrollThumbH); Scene.Add(_scrollThumbH); }

        UpdateRowPositions();
        UpdateHighlights();
        UpdateScrollbar();
    }

    // --- Visible row management ---

    /// <summary>Rebuild with scroll anchored to the given node (stays at same pixel position).</summary>
    private void RebuildVisibleRows(TreeNode? anchor)
    {
        // Find the anchor row's pixel offset before rebuild
        int anchorOldIndex = -1;
        if (anchor != null)
        {
            for (int i = 0; i < _visibleRows.Count; i++)
                if (_visibleRows[i].Node == anchor) { anchorOldIndex = i; break; }
        }
        float anchorScreenY = 0;
        if (anchorOldIndex >= 0)
            anchorScreenY = anchorOldIndex * RowHeight - GetScrollOffset();

        // Track selected node to restore after rebuild
        var selectedNode = (_selectedIndex >= 0 && _selectedIndex < _visibleRows.Count)
            ? _visibleRows[_selectedIndex].Node : null;

        RebuildVisibleRowsCore();

        // Apply pending selection from before rows existed
        bool wasPending = false;
        if (_pendingSelectedNode != null)
        {
            selectedNode = _pendingSelectedNode;
            _pendingSelectedNode = null;
            wasPending = true;
        }

        // Restore selection
        if (selectedNode != null)
        {
            _selectedIndex = -1;
            for (int i = 0; i < _visibleRows.Count; i++)
                if (_visibleRows[i].Node == selectedNode) { _selectedIndex = i; break; }
        }

        // Pre-scroll to pending selection (Bounds not available yet, set ratio directly)
        if (wasPending && _selectedIndex >= 0 && _contentHeight > 0)
        {
            _scroll = Math.Clamp(_selectedIndex * RowHeight / _contentHeight, 0f, 1f);

            // Also pre-scroll horizontally so the selected row's text is visible
            var row = _visibleRows[_selectedIndex];
            float indent = row.Depth * IndentWidth + 4 + ArrowAreaWidth;
            if (_contentWidth > 0)
                _scrollH = Math.Clamp(indent / _contentWidth, 0f, 1f);
        }

        // Restore scroll so anchor node stays at the same screen position
        if (anchor != null && anchorOldIndex >= 0)
        {
            int anchorNewIndex = -1;
            for (int i = 0; i < _visibleRows.Count; i++)
                if (_visibleRows[i].Node == anchor) { anchorNewIndex = i; break; }
            if (anchorNewIndex >= 0)
            {
                float visibleH = Bounds.H;
                if (NeedsScrollbarH()) visibleH -= ScrollBarWidth + ScrollBarPadding;
                float scrollRange = _contentHeight - visibleH;
                if (scrollRange > 0)
                {
                    float desiredOffset = anchorNewIndex * RowHeight - anchorScreenY;
                    _scroll = Math.Clamp(desiredOffset / scrollRange, 0f, 1f);
                }
            }
        }

        UpdateRowPositions();
        UpdateHighlights();
        UpdateScrollbar();
        MarkDirty();
    }

    private void RebuildVisibleRowsCore()
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

        // Calculate content width from cached text bounds
        float maxW = 0;
        for (int i = 0; i < _visibleRows.Count; i++)
        {
            var row = _visibleRows[i];
            float indent = row.Depth * IndentWidth + 4;
            float afterArrow = indent + ArrowAreaWidth;
            float textX = afterArrow;
            if (row.IconScene != null) textX = afterArrow + IconSize + IconGap;

            float rowW = textX + row.TextBoundsW + 8;
            if (rowW > maxW) maxW = rowW;
        }
        _contentWidth = maxW;

        // Clamp scroll positions if content no longer overflows
        if (!NeedsScrollbar()) _scroll = 0;
        if (!NeedsScrollbarH()) _scrollH = 0;

        // Clamp selection/hover
        if (_selectedIndex >= _visibleRows.Count) _selectedIndex = -1;
        if (_hoveredIndex >= _visibleRows.Count) _hoveredIndex = -1;

        // Re-order scrollbars on top of new rows
        if (_scrollTrack != null) { Scene.Remove(_scrollTrack); Scene.Add(_scrollTrack); }
        if (_scrollThumb != null) { Scene.Remove(_scrollThumb); Scene.Add(_scrollThumb); }
        if (_scrollTrackH != null) { Scene.Remove(_scrollTrackH); Scene.Add(_scrollTrackH); }
        if (_scrollThumbH != null) { Scene.Remove(_scrollThumbH); Scene.Add(_scrollThumbH); }
    }

    private void RebuildVisibleRows()
    {
        // Anchor to the first visible row to preserve scroll position
        TreeNode? anchor = null;
        if (_visibleRows.Count > 0 && Bounds.H > 0)
        {
            float scrollOffset = GetScrollOffset();
            int firstVisibleIdx = Math.Clamp((int)(scrollOffset / RowHeight), 0, _visibleRows.Count - 1);
            anchor = _visibleRows[firstVisibleIdx].Node;
        }
        RebuildVisibleRows(anchor);
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

        // Measure bounds before adding to scene — Bounds() includes parent transforms
        textObj.Bounds(out float tbx, out float tby, out float tbw, out float tbh);

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
            TextBoundsX = tbx, TextBoundsY = tby,
            TextBoundsW = tbw, TextBoundsH = tbh,
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
        float scrollOffsetH = GetScrollOffsetH();

        for (int i = 0; i < _visibleRows.Count; i++)
        {
            var row = _visibleRows[i];
            float rowY = i * RowHeight - scrollOffset;
            float indent = row.Depth * IndentWidth + 4 - scrollOffsetH;

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

            // Text positioning — use cached bounds, reset to identity then translate
            if (row.TextBoundsH > 0)
            {
                float tx = textX - row.TextBoundsX;
                float ty = rowY + (RowHeight - row.TextBoundsH) / 2f - row.TextBoundsY;
                ref var mat = ref row.TextScene.Transform();
                mat = new Matrix(1, 0, 0, 0, 1, 0, 0, 0, 1);
                row.TextScene.Translate(tx, ty);
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
        if (NeedsScrollbarH()) visibleH -= ScrollBarWidth + ScrollBarPadding;
        if (_contentHeight <= visibleH) return 0;
        return _scroll * (_contentHeight - visibleH);
    }

    private bool NeedsScrollbar() => _contentHeight > Bounds.H;

    private float GetScrollOffsetH()
    {
        float visibleW = Bounds.W;
        if (NeedsScrollbar()) visibleW -= ScrollBarWidth + 2; // account for vertical scrollbar
        if (_contentWidth <= visibleW) return 0;
        return _scrollH * (_contentWidth - visibleW);
    }

    private bool NeedsScrollbarH()
    {
        float visibleW = Bounds.W;
        if (NeedsScrollbar()) visibleW -= ScrollBarWidth + 2;
        return _contentWidth > visibleW;
    }

    private void UpdateScrollbar()
    {
        float w = Bounds.W, h = Bounds.H;
        bool needsV = NeedsScrollbar();
        bool needsH = NeedsScrollbarH();

        // --- Vertical scrollbar ---
        if (!needsV)
        {
            _scrollTrack?.Visible(false);
            _scrollThumb?.Visible(false);
        }
        else
        {
            _scrollTrack?.Visible(true);
            _scrollThumb?.Visible(true);

            float trackX = w - ScrollBarWidth - 2;
            float trackY = ScrollBarPadding;
            float trackH = h - 2 * ScrollBarPadding;
            if (needsH) trackH -= ScrollBarWidth + 2; // shrink to leave room for horizontal bar

            _scrollTrack?.ResetShape();
            _scrollTrack?.AppendRect(trackX, trackY, ScrollBarWidth, trackH, 3, 3);

            float ratio = Math.Min(1f, h / _contentHeight);
            float thumbH = Math.Max(20, trackH * ratio);
            float thumbY = trackY + _scroll * (trackH - thumbH);

            _scrollThumb?.ResetShape();
            _scrollThumb?.AppendRect(trackX + 1, thumbY + 1, ScrollBarWidth - 2, thumbH - 2, 2, 2);
        }

        // --- Horizontal scrollbar ---
        if (!needsH)
        {
            _scrollTrackH?.Visible(false);
            _scrollThumbH?.Visible(false);
        }
        else
        {
            _scrollTrackH?.Visible(true);
            _scrollThumbH?.Visible(true);

            float trackY = h - ScrollBarWidth - 2;
            float trackX = ScrollBarPadding;
            float trackW = w - 2 * ScrollBarPadding;
            if (needsV) trackW -= ScrollBarWidth + 2; // shrink to leave room for vertical bar

            _scrollTrackH?.ResetShape();
            _scrollTrackH?.AppendRect(trackX, trackY, trackW, ScrollBarWidth, 3, 3);

            float visibleW = w;
            if (needsV) visibleW -= ScrollBarWidth + 2;
            float ratioH = Math.Min(1f, visibleW / _contentWidth);
            float thumbW = Math.Max(20, trackW * ratioH);
            float thumbX = trackX + _scrollH * (trackW - thumbW);

            _scrollThumbH?.ResetShape();
            _scrollThumbH?.AppendRect(thumbX + 1, trackY + 1, thumbW - 2, ScrollBarWidth - 2, 2, 2);
        }
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

        // Horizontal scrollbar drag
        if (NeedsScrollbarH() && ly >= Bounds.H - ScrollBarWidth - ScrollBarPadding)
        {
            _draggingThumbH = true;
            _dragStartScrollH = _scrollH;
            _dragStartX = lx;
            return true;
        }

        // Vertical scrollbar drag
        if (NeedsScrollbar() && lx >= Bounds.W - ScrollBarWidth - ScrollBarPadding)
        {
            _draggingThumb = true;
            _dragStartScroll = _scroll;
            _dragStartY = ly;
            return true;
        }

        float scrollOffset = GetScrollOffset();
        float scrollOffsetH = GetScrollOffsetH();
        int rowIndex = (int)((ly + scrollOffset) / RowHeight);
        if (rowIndex < 0 || rowIndex >= _visibleRows.Count) return true;

        // Check if click is on the arrow area
        var row = _visibleRows[rowIndex];
        if (row.Node.HasChildren)
        {
            float indent = row.Depth * IndentWidth + 4 - GetScrollOffsetH();
            if (lx >= indent && lx < indent + ArrowAreaWidth)
            {
                SelectRow(rowIndex);
                row.Node.IsExpanded = !row.Node.IsExpanded;
                if (row.Node.IsExpanded)
                    NodeExpanded?.Invoke(row.Node);
                else
                    NodeCollapsed?.Invoke(row.Node);
                RebuildVisibleRows(row.Node);
                return true;
            }
        }

        SelectRow(rowIndex);

        return true;
    }

    public override void OnDoubleClick()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _visibleRows.Count)
        {
            var node = _visibleRows[_selectedIndex].Node;
            NodeDoubleClicked?.Invoke(node);
            if (node.HasChildren)
            {
                node.IsExpanded = !node.IsExpanded;
                if (node.IsExpanded)
                    NodeExpanded?.Invoke(node);
                else
                    NodeCollapsed?.Invoke(node);

                RebuildVisibleRows(node);
            }
        }

        base.OnDoubleClick();
    }

    public override bool OnMouseUp(int button, float x, float y)
    {
        if (button != 0) return false;
        _draggingThumb = false;
        _draggingThumbH = false;
        return true;
    }

    public override void OnMouseMove(float x, float y)
    {
        if (_draggingThumbH && NeedsScrollbarH())
        {
            WindowToLocal(x, y, out float lxh, out _);
            float trackW = Bounds.W - 2 * ScrollBarPadding;
            if (NeedsScrollbar()) trackW -= ScrollBarWidth + 2;
            float visibleW = Bounds.W;
            if (NeedsScrollbar()) visibleW -= ScrollBarWidth + 2;
            float ratioH = Math.Min(1f, visibleW / _contentWidth);
            float thumbW = Math.Max(20, trackW * ratioH);
            float scrollableTrack = trackW - thumbW;
            if (scrollableTrack > 0)
            {
                float delta = (lxh - _dragStartX) / scrollableTrack;
                SetScrollH(_dragStartScrollH + delta);
            }
            return;
        }

        if (_draggingThumb && NeedsScrollbar())
        {
            WindowToLocal(x, y, out _, out float ly);
            float trackH = Bounds.H - 2 * ScrollBarPadding;
            if (NeedsScrollbarH()) trackH -= ScrollBarWidth + 2;
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
        bool handledV = false;
        bool handledH = false;

        if (NeedsScrollbar() && dy != 0)
        {
            float step = Bounds.H * 0.25f / _contentHeight;
            SetScroll(_scroll - dy * step);
            handledV = true;
        }

        if (NeedsScrollbarH() && dx != 0)
        {
            float visibleW = Bounds.W;
            if (NeedsScrollbar()) visibleW -= ScrollBarWidth + 2;
            float step = visibleW * 0.25f / _contentWidth;
            SetScrollH(_scrollH + dx * step);
            handledH = true;
        }

        if (!handledV && !handledH) base.OnScroll(dx, dy);
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
                RebuildVisibleRows(node);
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
                RebuildVisibleRows(node);
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

    private void SetScrollH(float value)
    {
        float clamped = Math.Clamp(value, 0f, 1f);
        if (_scrollH == clamped) return;
        _scrollH = clamped;
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
        if (NeedsScrollbarH()) visibleH -= ScrollBarWidth + ScrollBarPadding;
        if (visibleH <= 0) return;

        float scrollRange = _contentHeight - visibleH;
        if (scrollRange <= 0) return;

        if (rowTop < scrollOffset)
            SetScroll(rowTop / scrollRange);
        else if (rowBottom > scrollOffset + visibleH)
            SetScroll((rowBottom - visibleH) / scrollRange);
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
        _scrollThumbH?.SetFill(thumbColor.R8, thumbColor.G8, thumbColor.B8, thumbColor.A8);

        ApplyHighlightColors();

        // Rebuild rows to pick up new text/arrow colors
        RebuildVisibleRows();
    }
}
