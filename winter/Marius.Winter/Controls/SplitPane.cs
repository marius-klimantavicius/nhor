using System;
using System.Numerics;
using ThorVG;

namespace Marius.Winter;

/// <summary>Two clipped panes separated by a focusable, draggable divider. All sizes are logical units.</summary>
public class SplitPane : Element
{
    private readonly Pane _first = new();
    private readonly Pane _second = new();
    private readonly Divider _divider;
    private float _position = 220, _firstMinimum, _secondMinimum, _resetPosition = 220, _keyboardStep = 10;
    private Orientation _orientation;
    private bool _userUpdating;

    public SplitPane()
    {
        _divider = new Divider(this);
        AddChild(_first);
        AddChild(_divider);
        AddChild(_second);
    }

    internal override bool ManagesOwnChildLayout => true;

    /// <summary>Replacing a pane detaches the old content without disposing it.</summary>
    public Element? First { get => _first.Content; set => _first.Content = value; }
    public Element? Second { get => _second.Content; set => _second.Content = value; }

    /// <summary>Requested first-pane extent; preserved when the container temporarily shrinks. Setting it is silent.</summary>
    public float Position
    {
        get => _position;
        set
        {
            ValidateExtent(value, nameof(Position));
            if (_position == value) return;
            _divider.CancelDrag();
            _position = value;
            Relayout();
        }
    }

    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (value is not (Orientation.Horizontal or Orientation.Vertical)) throw new ArgumentOutOfRangeException(nameof(value));
            if (_orientation == value) return;
            _orientation = value;
            _divider.CancelDrag();
            _divider.Cursor = value == Orientation.Horizontal ? CursorType.HResize : CursorType.VResize;
            Relayout();
        }
    }

    public float FirstMinimum { get => _firstMinimum; set => SetMinimum(ref _firstMinimum, value); }
    public float SecondMinimum { get => _secondMinimum; set => SetMinimum(ref _secondMinimum, value); }
    public float ResetPosition { get => _resetPosition; set { ValidateExtent(value, nameof(ResetPosition)); _resetPosition = value; } }
    public float KeyboardStep
    {
        get => _keyboardStep;
        set { ValidateExtent(value, nameof(KeyboardStep)); if (value == 0) throw new ArgumentOutOfRangeException(nameof(value)); _keyboardStep = value; }
    }

    public float DividerSize => 8;
    /// <summary>Actual first-pane extent after layout constraints. Layout alone never emits PositionChanged.</summary>
    public float VisiblePosition { get; private set; }
    /// <summary>User drag, keyboard or Reset only; programmatic Position assignment is silent.</summary>
    public Action<float>? PositionChanged;
    /// <summary>Geometry observation, including resize and programmatic updates; distinct from a user edit.</summary>
    public Action<float>? VisiblePositionChanged;

    public void Reset() { _divider.CancelDrag(); SetUserPosition(_resetPosition); }
    public void FocusDivider() => OwnerWindow?.SetFocus(_divider);

    private static void ValidateExtent(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name, "Use a finite, nonnegative logical extent.");
    }

    private void SetMinimum(ref float field, float value)
    {
        ValidateExtent(value, nameof(value));
        if (field == value) return;
        field = value;
        _divider.CancelDrag();
        Relayout();
    }

    private static float FiniteSize(float value) => float.IsFinite(value) ? MathF.Max(0, value) : 0;

    private void Range(float extent, out float available, out float minimum, out float maximum)
    {
        available = MathF.Max(0, extent - DividerSize);
        // Double avoids overflow when two individually valid finite minima are added.
        double minima = (double)_firstMinimum + _secondMinimum;
        if (minima > available)
            minimum = maximum = (float)(available * (_firstMinimum / minima));
        else
        {
            minimum = _firstMinimum;
            maximum = available - _secondMinimum;
        }
    }

    private void SetUserPosition(float value)
    {
        if (!IsEffectivelyVisible || !IsEffectivelyEnabled || IsDisposed) return;
        Range(FiniteSize(_orientation == Orientation.Horizontal ? Bounds.W : Bounds.H), out _, out float min, out float max);
        value = Math.Clamp(value, min, max);
        if (_position == value) return;
        float previous = VisiblePosition;
        _position = value;
        _userUpdating = true;
        try
        {
            Relayout();
            PositionChanged?.Invoke(value);
        }
        finally
        {
            _userUpdating = false;
            if (!IsDisposed && previous != VisiblePosition) VisiblePositionChanged?.Invoke(VisiblePosition);
        }
    }

    private void Relayout()
    {
        ArrangePanes(Bounds.W, Bounds.H);
        InvalidateMeasure();
    }

    protected override Vector2 MeasureCore(float availableWidth, float availableHeight)
    {
        float width = float.IsFinite(availableWidth) ? FiniteSize(availableWidth) : MathF.Max(_position, _firstMinimum) + _secondMinimum + DividerSize;
        float height = float.IsFinite(availableHeight) ? FiniteSize(availableHeight) : MathF.Max(_position, _firstMinimum) + _secondMinimum + DividerSize;
        Range(_orientation == Orientation.Horizontal ? width : height, out float available, out float min, out float max);
        float first = Math.Clamp(_position, min, max);
        if (_orientation == Orientation.Horizontal)
        {
            var a = _first.Measure(first, availableHeight);
            var b = _second.Measure(available - first, availableHeight);
            if (!float.IsFinite(availableHeight)) height = MathF.Max(a.Y, b.Y);
        }
        else
        {
            var a = _first.Measure(availableWidth, first);
            var b = _second.Measure(availableWidth, available - first);
            if (!float.IsFinite(availableWidth)) width = MathF.Max(a.X, b.X);
        }
        return new Vector2(width, height);
    }

    protected override void OnSizeChanged() => ArrangePanes(Bounds.W, Bounds.H);
    protected override void ArrangeCore(RectF finalBounds) => ArrangePanes(finalBounds.W, finalBounds.H);

    private void ArrangePanes(float width, float height)
    {
        width = FiniteSize(width); height = FiniteSize(height);
        bool horizontal = _orientation == Orientation.Horizontal;
        float extent = horizontal ? width : height;
        Range(extent, out float available, out float min, out float max);
        float previous = VisiblePosition;
        VisiblePosition = Math.Clamp(_position, min, max);
        float divider = MathF.Min(DividerSize, extent);
        if (horizontal)
        {
            _first.Arrange(new RectF(0, 0, VisiblePosition, height));
            _divider.Arrange(new RectF(VisiblePosition, 0, divider, height));
            _second.Arrange(new RectF(VisiblePosition + divider, 0, available - VisiblePosition, height));
        }
        else
        {
            _first.Arrange(new RectF(0, 0, width, VisiblePosition));
            _divider.Arrange(new RectF(0, VisiblePosition, width, divider));
            _second.Arrange(new RectF(0, VisiblePosition + divider, width, available - VisiblePosition));
        }
        if (!_userUpdating && previous != VisiblePosition) VisiblePositionChanged?.Invoke(VisiblePosition);
    }

    protected override void OnDisposed() { PositionChanged = null; VisiblePositionChanged = null; }

    private sealed class Pane : Element
    {
        private Shape? _clip;
        internal override bool ManagesOwnChildLayout => true;
        internal Element? Content
        {
            get => Children.Count == 0 ? null : Children[0];
            set
            {
                ThrowIfDisposed();
                if (ReferenceEquals(value, Content)) return;
                if (value != null)
                {
                    value.ThrowIfDisposed();
                    if (value.Parent != null || value._overlayOwner != null || value is Window)
                        throw new InvalidOperationException("Pane content already has an owner.");
                    for (Element? ancestor = this; ancestor != null; ancestor = ancestor.Parent)
                        if (ReferenceEquals(ancestor, value)) throw new InvalidOperationException("Pane content cannot be an ancestor.");
                }
                ClearChildren();
                if (value != null) AddChild(value);
                LayoutContent();
            }
        }
        protected override Vector2 MeasureCore(float availableWidth, float availableHeight) => Content?.Measure(availableWidth, availableHeight) ?? Vector2.Zero;
        protected override void OnAttached()
        {
            if (_clip != null) return;
            _clip = Shape.Gen();
            Scene.Clip(_clip);
            UpdateClip();
        }
        private void UpdateClip()
        {
            _clip?.ResetShape();
            _clip?.AppendRect(Bounds.X, Bounds.Y, Bounds.W, Bounds.H);
            Scene.pImpl.Mark(RenderUpdateFlag.Clip | RenderUpdateFlag.Transform);
        }
        private void LayoutContent()
        {
            Content?.Measure(Bounds.W, Bounds.H);
            Content?.Arrange(new RectF(0, 0, Bounds.W, Bounds.H));
        }
        protected override void OnSizeChanged() { UpdateClip(); LayoutContent(); }
        protected override void OnPositionChanged() => UpdateClip();
        protected override void ArrangeCore(RectF finalBounds) => LayoutContent();
    }

    private sealed class Divider : Element
    {
        private readonly SplitPane _owner;
        private bool _dragging;
        private float _offset;
        private Shape? _background, _grip;
        internal Divider(SplitPane owner) { _owner = owner; Focusable = true; Cursor = CursorType.HResize; }
        internal void CancelDrag() => _dragging = false;
        protected override void OnActivityChanged(bool active) { if (!active) CancelDrag(); }
        protected override void OnDetaching() => CancelDrag();
        public override void OnDragEnd() => CancelDrag();
        public override bool OnMouseDown(int button, float x, float y)
        {
            if (button != 0 || !IsEffectivelyVisible || !IsEffectivelyEnabled || IsDisposed) return false;
            WindowToLocal(x, y, out float lx, out float ly);
            if (!new RectF(0, 0, Bounds.W, Bounds.H).Contains(lx, ly)) return false;
            _dragging = true;
            _offset = _owner._orientation == Orientation.Horizontal ? lx : ly;
            return true;
        }
        public override void OnMouseMove(float x, float y)
        {
            if (!_dragging) return;
            _owner.WindowToLocal(x, y, out float lx, out float ly);
            _owner.SetUserPosition((_owner._orientation == Orientation.Horizontal ? lx : ly) - _offset);
        }
        public override bool OnMouseUp(int button, float x, float y)
        {
            if (button != 0) return false;
            CancelDrag();
            return true;
        }
        public override void OnKeyDown(int key, int mods, bool repeat)
        {
            if (!IsEffectivelyEnabled || !IsEffectivelyVisible || IsDisposed) return;
            bool horizontal = _owner._orientation == Orientation.Horizontal;
            float step = _owner._keyboardStep * ((mods & 1) != 0 ? 5 : 1);
            if (key == (horizontal ? 263 : 265)) _owner.SetUserPosition(_owner.VisiblePosition - step);
            else if (key == (horizontal ? 262 : 264)) _owner.SetUserPosition(_owner.VisiblePosition + step);
            else if (key == 268) _owner.SetUserPosition(0);
            else if (key == 269) _owner.SetUserPosition(float.MaxValue);
            else if (key is 257 or 335) _owner.Reset();
            else base.OnKeyDown(key, mods, repeat);
        }
        protected override void OnAttached()
        {
            if (_background != null) return;
            _background = Shape.Gen(); AddPaint(_background);
            _grip = Shape.Gen(); AddPaint(_grip);
            Paint();
        }
        protected override void OnSizeChanged() => Paint();
        protected override void OnThemeChanged() => Paint();
        protected override void OnStateChanged(ElementState oldState, ElementState newState) => Paint();
        private void Paint()
        {
            if (_background == null || _grip == null) return;
            var theme = OwnerWindow?.Theme ?? Theme.Dark;
            bool focused = (State & (ElementState.Focused | ElementState.Hovered)) != 0;
            var color = focused ? theme.TextColor : theme.BorderMedium;
            _background.ResetShape(); _background.AppendRect(0, 0, Bounds.W, Bounds.H);
            var bg = theme.WindowBackground;
            _background.SetFill(bg.R8, bg.G8, bg.B8, bg.A8);
            _grip.ResetShape();
            if (_owner._orientation == Orientation.Horizontal)
                _grip.AppendRect(MathF.Max(0, (Bounds.W - 2) / 2), 0, MathF.Min(2, Bounds.W), Bounds.H);
            else
                _grip.AppendRect(0, MathF.Max(0, (Bounds.H - 2) / 2), Bounds.W, MathF.Min(2, Bounds.H));
            _grip.SetFill(color.R8, color.G8, color.B8, IsEffectivelyEnabled ? color.A8 : (byte)80);
            MarkDirty();
        }
    }
}
