using System;
using System.Collections.Generic;
using System.Numerics;
using Marius.Winter.Taffy;
using ThorVG;

namespace Marius.Winter;

public abstract class Element : IDisposable
{
    private readonly Scene _scene;
    private readonly Scene _paintScene; // own paints — always renders below children
    private Element? _parent;
    private readonly List<Element> _children = new();
    private RectF _bounds;
    private RectF _layoutBounds; // requested rectangle, before visibility/constraints
    private bool _visible = true;
    private bool _enabled = true;
    private float _opacity = 1f;
    private ElementState _state;
    private CursorType _cursor = CursorType.Arrow;
    private Style? _style;
    private Style? _detachedDefaultStyle;
    private bool _measureDirty = true;
    private Vector2 _desiredSize;
    private float _lastMeasureWidth = float.NaN;
    private float _lastMeasureHeight = float.NaN;
    private bool _disposed;
    private bool _active;
    private bool _detaching;
    private Style? _observedStyle;
    private Style? _observedEffectiveStyle;
    private float? _minWidth, _minHeight, _maxWidth, _maxHeight;
    internal bool HasLayoutBounds;
    internal bool AutoArrangeRoot;
    internal RectF LayoutBounds => _layoutBounds;

    // --- Taffy integration ---
    internal NodeId _taffyNode;
    internal bool _taffyNodeValid; // false until registered with a TaffyTree
    internal bool _taffyStyleDirty = true; // needs SyncTaffyStyle before next layout
    internal TaffyLayoutEngine? _taffyOwner;

    protected Element()
    {
        _scene = Scene.Gen()!;
        // The Element owns its scene independently of its visual parent. Detach
        // releases the parent's reference; Dispose releases this reference.
        _scene.Ref();
        _paintScene = Scene.Gen()!;
        _scene.Add(_paintScene);
    }

    // --- Properties ---

    public Scene Scene => _scene;
    public Element? Parent => _parent;
    public IReadOnlyList<Element> Children => _children;
    internal List<Element> ChildrenMutable => _children;
    internal Window? _overlayOwner; // set when this element is used as an overlay
    public Window? OwnerWindow => _overlayOwner ?? (this is Window self ? self : _parent?.OwnerWindow);
    public bool IsDisposed => _disposed;
    public bool IsEffectivelyVisible => !_disposed && _visible && ((_parent ?? _overlayOwner)?.IsEffectivelyVisible ?? true);
    public bool IsEffectivelyEnabled => !_disposed && !_detaching && _enabled && ((_parent ?? _overlayOwner)?.IsEffectivelyEnabled ?? true);
    public bool IsActive => OwnerWindow != null && IsEffectivelyVisible && IsEffectivelyEnabled;
    // Queued menu commands must not revive after an owner detaches/reinserts.
    internal long MenuInputVersion;

    public RectF Bounds
    {
        get => _bounds;
        set
        {
            HasLayoutBounds = true;
            AutoArrangeRoot = false;
            _layoutBounds = value;
            SetArrangedBounds(value);
        }
    }

    private void SetArrangedBounds(RectF value)
    {
        if (_bounds.X == value.X && _bounds.Y == value.Y &&
            _bounds.W == value.W && _bounds.H == value.H)
            return;

        var sizeChanged = _bounds.W != value.W || _bounds.H != value.H;
        var positionChanged = _bounds.X != value.X || _bounds.Y != value.Y;
        _bounds = value;
        _scene.Translate(value.X, value.Y);

        if (sizeChanged)
            OnSizeChanged();
        if (positionChanged)
            OnPositionChanged();

        MarkDirty();
    }

    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value) return;
            _visible = value;
            _scene.Visible(value);
            if (!value) OwnerWindow?.ReleaseSubtreeReferences(this);
            NotifyActivityChanged();
            InvalidateMeasure();
        }
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (!value) OwnerWindow?.ReleaseSubtreeReferences(this);
            NotifyActivityChanged();
            if (!value)
                SetState(ElementState.Disabled);
            else
                SetState(ElementState.Normal);
        }
    }

    public float Opacity
    {
        get => _opacity;
        set
        {
            if (_opacity == value) return;
            _opacity = value;
            _scene.Opacity((byte)(value * 255f + 0.5f));
            MarkDirty();
        }
    }

    public ElementState State
    {
        get => _state;
        internal set => SetState(value);
    }

    public CursorType Cursor
    {
        get => _cursor;
        set => _cursor = value;
    }

    /// <summary>
    /// Whether this element can receive keyboard focus via Tab navigation.
    /// Controls like TextBox set this to true.
    /// </summary>
    public bool Focusable { get; set; }

    /// <summary>Minimum width constraint. null = no explicit minimum (0).</summary>
    public float? MinWidth { get => _minWidth; set => SetConstraint(ref _minWidth, value); }

    /// <summary>Minimum height constraint. null = no explicit minimum (0).</summary>
    public float? MinHeight { get => _minHeight; set => SetConstraint(ref _minHeight, value); }

    /// <summary>Maximum width constraint. null = no maximum.</summary>
    public float? MaxWidth { get => _maxWidth; set => SetConstraint(ref _maxWidth, value); }

    /// <summary>Maximum height constraint. null = no maximum.</summary>
    public float? MaxHeight { get => _maxHeight; set => SetConstraint(ref _maxHeight, value); }

    private void SetConstraint(ref float? field, float? value)
    {
        if (field == value) return;
        field = value;
        InvalidateMeasure();
    }

    /// <summary>
    /// Per-child layout hints. Set to a FlexItem or GridItem to control
    /// how this element is sized/placed by its parent's layout.
    /// </summary>
    private object? _layoutData;
    public object? LayoutData
    {
        get => _layoutData;
        set
        {
            if (Equals(_layoutData, value)) return;
            _layoutData = value;
            _taffyStyleDirty = true;
            _parent?.InvalidateMeasure();
        }
    }

    private object? _tooltip;

    /// <summary>
    /// Tooltip content shown on hover. Set to a string for text, or a
    /// <see cref="TooltipContent"/> for text+SVG combinations.
    /// </summary>
    public object? Tooltip
    {
        get => _tooltip;
        set
        {
            if (ReferenceEquals(_tooltip, value)) return;
            _tooltip = value;
            OwnerWindow?.RefreshTooltip(this);
        }
    }

    public Style Style
    {
        get
        {
            var style = _style ?? (OwnerWindow == null ? _detachedDefaultStyle ??= GetDefaultStyle() : GetDefaultStyle());
            var effective = ApplyStyleOverrides(style);
            if (!ReferenceEquals(style, _observedStyle) || !ReferenceEquals(effective, _observedEffectiveStyle))
            {
                _observedStyle?.Unsubscribe(this);
                _observedEffectiveStyle?.Unsubscribe(this);
                _observedStyle = style;
                _observedEffectiveStyle = effective;
                if (!_disposed)
                {
                    style.Subscribe(this);
                    if (!ReferenceEquals(style, effective)) effective.Subscribe(this);
                }
            }
            return effective;
        }
        set
        {
            if (ReferenceEquals(_style, value)) return;
            _style = value;
            _ = Style;
            RefreshStyle();
        }
    }

    protected virtual Style ApplyStyleOverrides(Style style) => style;

    internal void StyleMutated(Style style)
    {
        if (!_disposed && (ReferenceEquals(style, _observedStyle) || ReferenceEquals(style, _observedEffectiveStyle)))
            RefreshStyle();
    }

    private void RefreshStyle()
    {
        InvalidateMeasure();
        OnStyleChanged();
    }

    public Vector2 DesiredSize => _desiredSize;

    // --- Child management ---

    public void AddChild(Element child)
    {
        ValidateNewChild(child);

        _children.Add(child);
        child._parent = this;
        _scene.Add(child._scene);
        child.NotifyAttached();

        // If child subtree was built before joining the widget tree,
        // propagate the correct theme to all descendants
        if (OwnerWindow != null)
            child.NotifyThemeChanged();

        // If child already has bounds (from a prior Arrange), re-run its size logic
        // so shapes created in OnAttached get their geometry
        if (child._bounds.W > 0 || child._bounds.H > 0)
            child.OnSizeChanged();

        // If parent already has bounds, re-run layout so new child gets arranged
        if (_bounds.W > 0 || _bounds.H > 0)
            OnSizeChanged();

        InvalidateMeasure();
        MarkDirty();
    }

    public void InsertChild(Element child, int index)
    {
        ValidateNewChild(child);

        _children.Insert(index, child);
        child._parent = this;

        // Insert the new child's scene at the correct z-order position.
        // Scene.Add(target, at) inserts target before at in the paints list,
        // avoiding Remove/re-Add which would destroy existing scenes via Unref.
        if (index + 1 < _children.Count)
            _scene.Add(child._scene, _children[index + 1]._scene);
        else
            _scene.Add(child._scene);

        child.NotifyAttached();

        if (OwnerWindow != null)
            child.NotifyThemeChanged();

        if (child._bounds.W > 0 || child._bounds.H > 0)
            child.OnSizeChanged();

        if (_bounds.W > 0 || _bounds.H > 0)
            OnSizeChanged();

        InvalidateMeasure();
        MarkDirty();
    }

    public void RemoveChild(Element child)
    {
        if (child._parent != this || child._detaching) return;
        child.DetachSubtree();
        _children.Remove(child);
        _scene.Remove(child._scene);
        child._parent = null;

        // Re-run layout so remaining children are rearranged
        if (_bounds.W > 0 || _bounds.H > 0)
            OnSizeChanged();

        InvalidateMeasure();
        MarkDirty();
    }

    /// <summary>
    /// Reorder children to match the given list. Does not call OnDetaching/OnAttached
    /// or Scene.Remove/Add (which would break ref counts and parent pointers).
    /// The newOrder list must contain exactly the same elements as the current children.
    /// </summary>
    public void ReorderChildren(IReadOnlyList<Element> newOrder)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(newOrder);
        // Validate the entire input before touching either tree. Snapshotting also
        // permits ReorderChildren(Children) without clearing its own input list.
        if (newOrder.Count != _children.Count)
            throw new ArgumentException("Order must contain every immediate child exactly once", nameof(newOrder));
        var ordered = new Element[newOrder.Count];
        var remaining = new HashSet<Element>(_children, ReferenceEqualityComparer.Instance);
        for (int i = 0; i < ordered.Length; i++)
        {
            var child = newOrder[i];
            if (child == null || !remaining.Remove(child))
                throw new ArgumentException("Order must contain every immediate child exactly once", nameof(newOrder));
            ordered[i] = child;
        }

        // Build a set of child scenes for fast lookup.
        var childScenes = new HashSet<Paint>(ordered.Length);
        for (int i = 0; i < ordered.Length; i++)
            childScenes.Add(ordered[i]._scene);

        // Find which positions in the scene's paints list hold child scenes.
        var paintPositions = new List<int>();
        var presentScenes = new HashSet<Paint>();
        for (int i = 0; i < _scene.paints.Count; i++)
        {
            if (childScenes.Contains(_scene.paints[i]))
            {
                paintPositions.Add(i);
                presentScenes.Add(_scene.paints[i]);
            }
        }

        // Place newOrder scenes at those positions, preserving the relative
        // slot assignments for any non-child paints (background, scrollbars, etc.).
        int position = 0;
        for (int i = 0; i < ordered.Length; i++)
        {
            // Scene is publicly mutable. Preserve an explicitly absent scene
            // while still ordering all present child scenes and managed children.
            if (!presentScenes.Contains(ordered[i]._scene)) continue;
            // A pure z-order mutation has no geometry/color changes for the
            // partial renderer to notice. Damage each moved scene's old region.
            if (!ReferenceEquals(_scene.paints[paintPositions[position]], ordered[i]._scene))
                ordered[i]._scene.pImpl.Damage();
            _scene.paints[paintPositions[position++]] = ordered[i]._scene;
        }

        // Reorder the children list.
        _children.Clear();
        _children.AddRange(ordered);

        InvalidateMeasure();
        if (_bounds.W > 0 || _bounds.H > 0)
            OnSizeChanged();
        MarkDirty();
    }

    public void ClearChildren()
    {
        for (int i = _children.Count - 1; i >= 0; i--)
        {
            var child = _children[i];
            child.DetachSubtree();
            _scene.Remove(child._scene);
            child._parent = null;
        }
        _children.Clear();

        if (_bounds.W > 0 || _bounds.H > 0)
            OnSizeChanged();

        InvalidateMeasure();
        MarkDirty();
    }

    // --- Layout ---

    /// <summary>Whether this element's measure cache is dirty and needs re-measurement.</summary>
    public bool IsMeasureDirty => _measureDirty;

    public void InvalidateMeasure()
    {
        _measureDirty = true;
        _taffyStyleDirty = true;
        _parent?.InvalidateMeasure();
        MarkDirty();
    }

    public Vector2 Measure(float availableWidth, float availableHeight)
    {
        if (!_measureDirty && availableWidth == _lastMeasureWidth && availableHeight == _lastMeasureHeight)
            return _desiredSize;
        _measureDirty = false;
        _lastMeasureWidth = availableWidth;
        _lastMeasureHeight = availableHeight;
        _desiredSize = _visible ? ClampSize(MeasureCore(availableWidth, availableHeight)) : Vector2.Zero;
        return _desiredSize;
    }

    public void Arrange(RectF finalBounds)
    {
        AutoArrangeRoot = false;
        HasLayoutBounds = true;
        if (IgnoresParentArrange)
        {
            // Keep our own bounds; only re-arrange internal content.
            ArrangeCore(Bounds);
            return;
        }
        // Keep the target independently of collapsed arranged geometry. Manual
        // roots/children have no parent layout to reconstruct it on show.
        _layoutBounds = finalBounds;
        float w = _visible ? ClampWidth(finalBounds.W) : 0;
        float h = _visible ? ClampHeight(finalBounds.H) : 0;
        var clamped = new RectF(finalBounds.X, finalBounds.Y, w, h);
        SetArrangedBounds(clamped);
        ArrangeCore(clamped);
    }

    private Vector2 ClampSize(Vector2 size)
    {
        return new Vector2(ClampWidth(size.X), ClampHeight(size.Y));
    }

    private float ClampWidth(float w)
    {
        if (MinWidth.HasValue && w < MinWidth.Value) w = MinWidth.Value;
        if (MaxWidth.HasValue && w > MaxWidth.Value) w = MaxWidth.Value;
        return w;
    }

    private float ClampHeight(float h)
    {
        if (MinHeight.HasValue && h < MinHeight.Value) h = MinHeight.Value;
        if (MaxHeight.HasValue && h > MaxHeight.Value) h = MaxHeight.Value;
        return h;
    }

    /// <summary>
    /// If true, this element manages its own children's layout (e.g. ScrollPanel, DialogWindow).
    /// Taffy will treat this as a leaf node and call MeasureCore for sizing.
    /// Children will NOT be added to the Taffy tree.
    /// </summary>
    internal virtual bool ManagesOwnChildLayout => false;

    /// <summary>
    /// If true, Arrange() preserves the element's current Bounds instead of applying the parent's
    /// finalBounds. Used by DialogWindow which manages its own position via dragging/resizing.
    /// </summary>
    internal virtual bool IgnoresParentArrange => false;

    protected virtual Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        return new Vector2(_bounds.W, _bounds.H);
    }

    protected virtual void ArrangeCore(RectF finalBounds)
    {
        // Default: arrange children at their current positions
    }

    // --- State ---

    private void SetState(ElementState newState)
    {
        if (_state == newState) return;
        var old = _state;
        _state = newState;
        OnStateChanged(old, newState);
    }

    // --- Overridable callbacks ---

    protected virtual Style GetDefaultStyle() => OwnerWindow?.Theme.Label ?? new Style();
    protected virtual void OnAttached() { }
    protected virtual void OnDetaching() { }
    protected virtual void OnSizeChanged() { }
    /// <summary>Called after a parent-space position change, including Arrange.</summary>
    protected virtual void OnPositionChanged() { }
    protected virtual void OnStyleChanged()
    {
        OnThemeChanged();
        OnSizeChanged();
        OnStateChanged(_state, _state);
    }
    /// <summary>Called when effective window visibility/enabled activity changes.</summary>
    protected virtual void OnActivityChanged(bool active) { }
    protected virtual void OnDisposed() { }
    protected virtual void OnStateChanged(ElementState oldState, ElementState newState) { }
    protected virtual void OnThemeChanged() { }

    internal void NotifyThemeChanged()
    {
        _ = Style;
        RefreshStyle();
        foreach (var child in _children)
            child.NotifyThemeChanged();
    }

    private void ValidateNewChild(Element child)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(child);
        child.ThrowIfDisposed();
        if (child._parent != null || child._overlayOwner != null || child is Window)
            throw new InvalidOperationException("Element already has an owner");
        for (Element? ancestor = this; ancestor != null; ancestor = ancestor._parent)
            if (ReferenceEquals(ancestor, child)) throw new InvalidOperationException("An Element cannot contain itself");
    }

    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    internal void NotifyAttached()
    {
        _detaching = false;
        OnAttached();
        InvalidateMeasure();
        // Descendants built before entering the Window also need their owner-
        // dependent setup. Shape creation in OnAttached is idempotent.
        if (OwnerWindow != null)
            foreach (var child in _children) child.NotifyAttached();
        NotifyActivityChanged();
        if (_bounds.W > 0 || _bounds.H > 0) OnSizeChanged();
    }

    internal void NotifyActivityChanged()
    {
        bool active = IsActive;
        if (_active != active)
        {
            _active = active;
            if (!active) OwnerWindow?.Animator.Cancel(this);
            OnActivityChanged(active);
        }
        foreach (var child in _children) child.NotifyActivityChanged();
    }

    internal void DetachSubtree()
    {
        _detaching = true;
        OwnerWindow?.ReleaseSubtreeReferences(this);
        NotifyDetaching();
        ReleaseLayoutNodes();
    }

    private void NotifyDetaching()
    {
        foreach (var child in _children) child.NotifyDetaching();
        if (_active)
        {
            _active = false;
            OnActivityChanged(false);
        }
        OwnerWindow?.Animator.Cancel(this);
        OnDetaching();
    }

    internal void ReleaseLayoutNodes()
    {
        // Walk the managed tree, including manually laid-out roots which are
        // deliberately absent from their parent's Taffy child list.
        foreach (var child in _children) child.ReleaseLayoutNodes();
        _taffyOwner?.RemoveOwnedNode(this);
    }

    /// <summary>
    /// Permanently releases this subtree. RemoveChild/ClearChildren only detach
    /// reusable Elements; call Dispose when their retained resources are no longer needed.
    /// </summary>
    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_parent != null) _parent.RemoveChild(this);
        else if (_overlayOwner != null) _overlayOwner.RemoveOverlay(this);
        else DetachSubtree();
        for (int i = _children.Count - 1; i >= 0; i--) _children[i].Dispose();
        _observedStyle?.Unsubscribe(this);
        _observedEffectiveStyle?.Unsubscribe(this);
        OnDisposed();
        // Canvas/overlay scenes can own a root without an Element parent.
        if (_scene.Parent() is Scene parentScene) parentScene.Remove(_scene);
        _scene.Unref();
        GC.SuppressFinalize(this);
    }

    // --- Input events (overridden by controls) ---

    public virtual void OnMouseEnter() { }
    public virtual void OnMouseLeave() { }
    public virtual void OnMouseMove(float x, float y) { }
    public virtual bool OnMouseDown(int button, float x, float y) => false;
    public virtual bool OnMouseUp(int button, float x, float y) => false;
    public virtual void OnClick() { }
    public Action? DoubleClicked;
    public virtual void OnDoubleClick() { DoubleClicked?.Invoke(); }
    public virtual void OnScroll(float dx, float dy) { _parent?.OnScroll(dx, dy); }
    public virtual void OnDragStart(float x, float y) { }
    public virtual void OnDrag(float dx, float dy) { }
    public virtual void OnDragEnd() { }
    public virtual void OnKeyDown(int key, int mods, bool repeat) => OnKeyDownBubble(key, mods, repeat);
    public virtual void OnKeyUp(int key, int mods) { }
    public virtual void OnTextInput(string text) { }
    public virtual void OnFocus() { }
    public virtual void OnBlur() { }

    // --- Context menu ---

    /// <summary>
    /// Static context menu for this element. Set to a <see cref="Menu"/> instance
    /// to show it on right-click. For dynamic menus, override
    /// <see cref="OnBuildContextMenu"/> instead.
    /// </summary>
    private Menu? _contextMenu;
    internal long ContextMenuVersion { get; private set; }
    public Menu? ContextMenu
    {
        get => _contextMenu;
        set
        {
            if (ReferenceEquals(_contextMenu, value)) return;
            ContextMenuVersion++;
            OwnerWindow?.ContextMenuChanged(this);
            _contextMenu = value;
        }
    }

    /// <summary>
    /// Called when a context menu is about to be shown. Override to populate
    /// items dynamically. The base implementation copies items from
    /// <see cref="ContextMenu"/> if set.
    /// </summary>
    public virtual void OnBuildContextMenu(Menu menu)
    {
        if (ContextMenu != null)
        {
            foreach (var item in ContextMenu.Items)
                menu.Items.Add(item);
        }
    }

    protected void OnKeyDownBubble(int key, int mods, bool repeat)
    {
        if (OwnerWindow?.NoteKeyBubble() == false) return;
        _parent?.OnKeyDown(key, mods, repeat);
    }

    // --- Hit testing ---

    public virtual Element? HitTest(float x, float y)
    {
        if (!_visible || !_enabled) return null;

        // Local coordinates (x,y are already relative to parent)
        float localX = x - _bounds.X;
        float localY = y - _bounds.Y;

        if (!new RectF(0, 0, _bounds.W, _bounds.H).Contains(localX, localY))
            return null;

        // Check children in reverse order (front-to-back)
        for (int i = _children.Count - 1; i >= 0; i--)
        {
            var hit = _children[i].HitTest(localX, localY);
            if (hit != null) return hit;
        }

        return this;
    }

    // --- Coordinate conversion ---

    /// <summary>Convert window-space coordinates to this element's local space.</summary>
    public void WindowToLocal(float wx, float wy, out float lx, out float ly)
    {
        if (_parent != null)
        {
            _parent.WindowToLocal(wx, wy, out float px, out float py);
            lx = px - _bounds.X;
            ly = py - _bounds.Y;
        }
        else
        {
            lx = wx - _bounds.X;
            ly = wy - _bounds.Y;
        }
    }

    // --- Helpers ---

    protected void AddPaint(Paint paint) => _paintScene.Add(paint);
    protected void RemovePaint(Paint paint) => _paintScene.Remove(paint);

    // Scene.Remove releases its owner. Keep the paint alive while moving it
    // above child scenes; the scene is its only permanent owner.
    protected void BringPaintToFront(Paint paint)
    {
        paint.Ref();
        try
        {
            _scene.Remove(paint);
            _scene.Add(paint);
        }
        finally { paint.Unref(); }
    }

    protected void MarkDirty()
    {
        var w = OwnerWindow;
        if (w != null) w.Dirty = true;
    }

    /// <summary>
    /// Compensate for ThorVG font DPI difference (96/72 ≈ 1.333).
    /// Returns the font size that produces text matching the requested pixel height.
    /// </summary>
    protected float EffectiveFontSize(float requestedSize)
    {
        var t = ThorVG.Text.Gen();
        if (t == null) return requestedSize;
        try
        {
            t.SetFont(Style.FontName);
            t.SetFontSize(requestedSize);
            t.SetText("M");
            t.GetMetrics(out var m);
            float measuredHeight = m.ascent - m.descent;
            if (measuredHeight > 0)
                return requestedSize * requestedSize / measuredHeight;
            return requestedSize;
        }
        finally { t.Unref(); }
    }
}
