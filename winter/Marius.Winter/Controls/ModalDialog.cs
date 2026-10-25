using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using static Glfw.GLFW;

namespace Marius.Winter;

public enum DialogCloseReason { Accepted, Cancel, Escape, Backdrop, CloseButton }

/// <summary>An immutable completed user close, identified by its opening session.</summary>
public sealed class DialogResult(long sessionId, DialogCloseReason reason) : EventArgs
{
    public long SessionId { get; } = sessionId;
    public DialogCloseReason Reason { get; } = reason;
    public bool Accepted => Reason == DialogCloseReason.Accepted;
}

/// <summary>One in-window modal. Body and Footer are borrowed Elements: replacement,
/// closing and disposal detach them without disposing them. Dispose this host even
/// if it was never attached. Programmatic IsOpen changes are silent.</summary>
public sealed class ModalDialog : Element
{
    private readonly Surface _surface;
    private readonly Card _card;
    private readonly Header _header;
    private readonly ClippedTitle _title;
    private readonly Button _close;
    private readonly ScrollPanel _bodyViewport, _footerViewport;
    private Element? _body, _footer;
    private Window? _window;
    private Element? _opener;
    private bool _isOpen, _presented;
    private long _generation, _sessionId;
    private CancellationTokenSource? _request;
    private string _caption = "Dialog";
    private Func<CancellationToken, ValueTask<bool>>? _defaultAction;
    private Func<DialogResult, CancellationToken, ValueTask<bool>>? _closeGuard;

    public ModalDialog()
    {
        _surface = new Surface(this) { Background = new Color4(0, 0, 0, .48f), Padding = new Thickness(0), Focusable = true };
        _card = new Card { Layout = new CardLayout(this), Padding = new Thickness(0) };
        _title = new ClippedTitle(_caption) { TextWrapping = ThorVG.TextWrap.Word, CenterText = true };
        _close = new FrameCloseButton { Clicked = () => _ = RequestCloseAsync(DialogCloseReason.CloseButton) };
        _header = new Header(_title, _close);
        _bodyViewport = new ScrollPanel { Layout = new FillLayout(), HorizontalScroll = ScrollMode.Never, Padding = new Thickness(0) };
        _footerViewport = new ScrollPanel { Layout = new FillLayout(), HorizontalScroll = ScrollMode.Never, Padding = new Thickness(0) };
        _card.AddChild(_header); _card.AddChild(_bodyViewport); _card.AddChild(_footerViewport);
        _surface.AddChild(_card);
    }

    public string Title { get => _caption; set { _caption = value ?? ""; _title.Text = _caption; _surface.InvalidateMeasure(); } }
    public float Width { get; set; } = 520;
    public float Height { get; set; } = 400;
    public bool CloseOnBackdrop { get; set; }
    public bool CloseOnEscape { get; set; } = true;
    public bool ShowCloseButton { get => _close.Visible; set => _close.Visible = value; }
    public Func<Element?>? InitialFocus { get; set; }
    /// <summary>Only for Accepted requests. False keeps the modal open, e.g. for validation.</summary>
    public Func<CancellationToken, ValueTask<bool>>? DefaultAction
    { get => _defaultAction; set { if (Equals(_defaultAction, value)) return; CancelRequest(); _defaultAction = value; } }
    /// <summary>Runs after the default action for every user close. False rejects it.
    /// Caller-owned delegates/services are never disposed.</summary>
    public Func<DialogResult, CancellationToken, ValueTask<bool>>? CloseGuard
    { get => _closeGuard; set { if (Equals(_closeGuard, value)) return; CancelRequest(); _closeGuard = value; } }
    public event Action<DialogResult>? CloseRequested;
    public event Action<DialogResult>? CloseRejected;
    public event Action<DialogResult>? Closed;
    public event Action<Exception>? CloseFailed;
    public bool IsClosing => _request != null;
    public Exception? LastCloseError { get; private set; }
    public long SessionId => _sessionId;
    public RectF DialogBounds => _card.Bounds;
    internal Element ModalRoot => _surface;
    internal Element FocusFallback => _close.Visible && _close.Enabled ? _close : _surface;

    public Element? Body { get => _body; set => Replace(_bodyViewport, ref _body, value); }
    public Element? Footer { get => _footer; set => Replace(_footerViewport, ref _footer, value); }
    private void Replace(Element owner, ref Element? field, Element? value)
    {
        ThrowIfDisposed();
        if (ReferenceEquals(field, value)) return;
        if (value != null && (value.IsDisposed || value.Parent != null || value._overlayOwner != null))
            throw new InvalidOperationException("Modal content must be a live detached Element.");
        if (field != null) owner.RemoveChild(field);
        field = value;
        if (value != null) owner.AddChild(value);
        _surface.InvalidateMeasure();
        if (_presented) _window!.EnsureModalFocus();
    }

    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            ThrowIfDisposed(); OwnerWindow?.Dispatcher.VerifyAccess();
            if (_isOpen == value) return;
            if (value)
            {
                _isOpen = true;
                try { if (IsActive) Present(); }
                catch { _isOpen = false; throw; }
            }
            else { _isOpen = false; Unpresent(); }
        }
    }

    /// <summary>Starts a fresh identity, invalidating pending validation even if open state is unchanged.</summary>
    public void ResetSession(bool isOpen)
    {
        ThrowIfDisposed(); OwnerWindow?.Dispatcher.VerifyAccess();
        _isOpen = false; Unpresent(); IsOpen = isOpen;
    }

    /// <summary>Invalidates an awaiting request without changing the opening session.</summary>
    public void CancelCloseRequest() { OwnerWindow?.Dispatcher.VerifyAccess(); CancelRequest(); }

    private void Present()
    {
        if (_presented || OwnerWindow is not { } window) return;
        window.Dispatcher.VerifyAccess();
        _opener = window.FocusedElement;
        window.BeginModal(this);
        _window = window; _presented = true; _generation++; _sessionId++;
        window.Closing += OwnerClosing;
        try
        {
            window.ShowOverlay(_surface);
            UpdatePlacement();
            window.EnsureModalFocus(InitialFocus?.Invoke());
        }
        catch { _isOpen = false; Unpresent(); throw; }
    }

    private void CancelRequest()
    {
        _generation++;
        var request = _request; _request = null;
        if (request != null) { try { request.Cancel(); } finally { request.Dispose(); } }
    }

    private void Unpresent()
    {
        CancelRequest();
        if (!_presented) return;
        var window = _window!;
        _presented = false; _window = null;
        window.Closing -= OwnerClosing;
        window.EndModal(this, _opener);
        _opener = null;
        window.RemoveOverlay(_surface);
    }

    private ValueTask OwnerClosing()
    {
        _isOpen = false; Unpresent(); return ValueTask.CompletedTask;
    }

    public Task<bool> RequestCloseAsync(DialogCloseReason reason = DialogCloseReason.Cancel)
    {
        var window = _window;
        if (window == null || IsDisposed || !_presented || !_isOpen || _request != null) return Task.FromResult(false);
        window.Dispatcher.VerifyAccess();
        return window.Dispatcher.InvokeAsync(() => CloseCore(reason, window));
    }

    private async Task<bool> CloseCore(DialogCloseReason reason, Window window)
    {
        if (_request != null || !_presented || !_isOpen) return false;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(window.ShutdownToken);
        _request = request;
        LastCloseError = null;
        long generation = _generation;
        var result = new DialogResult(_sessionId, reason);
        bool Current() => !IsDisposed && _presented && _isOpen && ReferenceEquals(_window, window)
            && generation == _generation && ReferenceEquals(_request, request) && !request.IsCancellationRequested;
        try
        {
            CloseRequested?.Invoke(result);
            if (!Current()) return false;
            var action = DefaultAction; var guard = CloseGuard;
            bool allowed = reason != DialogCloseReason.Accepted || action == null || await action(request.Token);
            if (!Current()) return false;
            if (allowed && guard != null) allowed = await guard(result, request.Token);
            if (!Current()) return false;
            if (!allowed) { _request = null; CloseRejected?.Invoke(result); return false; }
            _request = null;
            _isOpen = false; Unpresent();
            Closed?.Invoke(result);
            return true;
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { return false; }
        catch (Exception error)
        {
            if (!_presented && !IsDisposed) throw;
            if (Current()) { LastCloseError = error; _request = null; CloseFailed?.Invoke(error); CloseRejected?.Invoke(result); }
            return false;
        }
        finally { if (ReferenceEquals(_request, request)) _request = null; }
    }

    internal void Escape() { if (CloseOnEscape) _ = RequestCloseAsync(DialogCloseReason.Escape); }
    internal void RevealFocus(Element target)
    {
        foreach (var viewport in new[] { _bodyViewport, _footerViewport })
        {
            bool inside = false;
            for (var e = target.Parent; e != null; e = e.Parent) if (ReferenceEquals(e, viewport)) { inside = true; break; }
            if (!inside || viewport.Children.Count == 0) continue;
            var content = viewport.Children[0];
            var extent = Math.Max(0, content.DesiredSize.Y - viewport.Bounds.H);
            if (extent <= 0) continue;
            target.WindowToLocal(0, 0, out var nx, out var ny);
            viewport.WindowToLocal(-nx, -ny, out _, out var top);
            var delta = top < 0 ? top : top + target.Bounds.H > viewport.Bounds.H ? Math.Min(top, top + target.Bounds.H - viewport.Bounds.H) : 0;
            if (delta != 0) viewport.Scroll += delta / extent;
        }
    }
    internal void UpdatePlacement()
    {
        if (!_presented || _window == null) return;
        var w = Math.Max(0, _window.Bounds.W); var h = Math.Max(0, _window.Bounds.H);
        _surface.Arrange(new RectF(0, 0, w, h));
        var cw = Math.Min(Math.Max(0, Width), Math.Max(0, w - 16));
        var ch = Math.Min(Math.Max(0, Height), Math.Max(0, h - 16));
        _card.Measure(cw, ch);
        _card.Arrange(new RectF((w - cw) / 2, (h - ch) / 2, cw, ch));
    }
    protected override Vector2 MeasureCore(float availableWidth, float availableHeight) => Vector2.Zero;
    protected override void OnActivityChanged(bool active) { if (active && _isOpen) Present(); else if (!active) Unpresent(); }
    protected override void OnDetaching() { _isOpen = false; Unpresent(); }
    public override void Dispose()
    {
        if (IsDisposed) return;
        _isOpen = false; Unpresent();
        Body = null; Footer = null;
        _surface.Dispose();
        CloseRequested = null; CloseRejected = null; Closed = null; CloseFailed = null;
        InitialFocus = null; DefaultAction = null; CloseGuard = null;
        base.Dispose();
    }

    private sealed class Surface(ModalDialog owner) : Panel
    {
        public override void OnClick() { if (owner.CloseOnBackdrop) _ = owner.RequestCloseAsync(DialogCloseReason.Backdrop); }
        public override bool OnMouseDown(int button, float x, float y) => false;
        public override void OnKeyDown(int key, int mods, bool repeat)
        {
            if (OwnerWindow?.IsCurrentKeyScope == false) return;
            if (key == GLFW_KEY_ESCAPE) { if (!repeat) owner.Escape(); return; }
            if ((key == GLFW_KEY_ENTER || key == GLFW_KEY_KP_ENTER) && (mods & (GLFW_MOD_CONTROL | GLFW_MOD_ALT | GLFW_MOD_SUPER)) == 0)
            { if (!repeat) _ = owner.RequestCloseAsync(DialogCloseReason.Accepted); return; }
        }
    }

    private sealed class FillLayout : ILayout
    {
        public Vector2 Measure(Element container, float width, float height) => container.Children.Count == 0 ? Vector2.Zero : container.Children[0].Measure(width, height);
        public void Arrange(Element container, RectF bounds) { if (container.Children.Count != 0) container.Children[0].Arrange(bounds); }
    }

    private sealed class Card : Panel
    {
        private DialogFrame? _frame;
        private float _headerHeight = DialogWindow.HeaderHeight;
        private float _width = float.NaN, _height = float.NaN;
        internal void ArrangeFrame(float headerHeight)
        {
            if (_width == Bounds.W && _height == Bounds.H && _headerHeight == headerHeight) return;
            _width = Bounds.W; _height = Bounds.H; _headerHeight = headerHeight;
            _frame?.ApplyTheme(OwnerWindow?.Theme ?? Theme.Dark, _headerHeight);
            _frame?.Arrange(Bounds.W, Bounds.H, headerHeight);
        }
        protected override void OnAttached()
        {
            _frame ??= new DialogFrame(AddPaint);
            OnThemeChanged();
        }
        protected override void OnThemeChanged()
        {
            _frame?.ApplyTheme(OwnerWindow?.Theme ?? Theme.Dark, _headerHeight);
            MarkDirty();
        }
    }

    // This is only presentation: the title never handles drag or resize input.
    private sealed class Header : Panel
    {
        private const float CloseSize = 28, Edge = 2, Gap = 6;
        private readonly ClippedTitle _title;
        private readonly Button _close;
        internal override bool ManagesOwnChildLayout => true;
        internal Header(ClippedTitle title, Button close)
        {
            _title = title; _close = close;
            AddChild(title); AddChild(close);
        }
        private float TitleInset(float width) => Math.Min(width / 2, _close.Visible ? Edge + CloseSize + Gap : 12);
        protected override Vector2 MeasureCore(float width, float height)
        {
            float textWidth = Math.Max(0, width - 2 * TitleInset(width));
            float textHeight = _title.Measure(textWidth, float.PositiveInfinity).Y;
            // At most two lines; leave the remaining short-window space for content.
            return new Vector2(width, Math.Max(DialogWindow.HeaderHeight, Math.Min(textHeight, _title.TwoLineHeight) + 12));
        }
        protected override void ArrangeCore(RectF bounds)
        {
            float inset = TitleInset(bounds.W), width = Math.Max(0, bounds.W - 2 * inset);
            float height = Math.Min(Math.Max(0, bounds.H - 12), _title.Measure(width, float.PositiveInfinity).Y);
            _title.Arrange(new RectF(inset, (bounds.H - height) / 2, width, height));
            float size = Math.Min(CloseSize, Math.Min(bounds.W, bounds.H));
            _close.Arrange(new RectF(Math.Max(0, bounds.W - Edge - size), (bounds.H - size) / 2, size, size));
        }
    }

    private sealed class ClippedTitle(string text) : Label(text)
    {
        private ThorVG.Shape? _clip;
        internal float TwoLineHeight
        {
            get
            {
                var measure = ThorVG.Text.Gen()!;
                try
                {
                    measure.SetFont(Style.FontName);
                    measure.SetFontSize(EffectiveFontSize(FontSize));
                    measure.SetText("Mg\nMg");
                    measure.GetTextSize(out _, out float height);
                    return height;
                }
                finally { measure.Unref(); }
            }
        }
        protected override void OnAttached()
        {
            base.OnAttached();
            if (_clip != null) return;
            _clip = ThorVG.Shape.Gen()!;
            Scene.Clip(_clip);
        }
        protected override void OnSizeChanged()
        {
            base.OnSizeChanged();
            UpdateClip();
        }
        protected override void OnPositionChanged() => UpdateClip();
        private void UpdateClip()
        {
            _clip?.ResetShape();
            _clip?.AppendRect(Bounds.X, Bounds.Y, Bounds.W, Bounds.H, 0, 0);
            Scene.pImpl.Mark(ThorVG.RenderUpdateFlag.Clip | ThorVG.RenderUpdateFlag.Transform);
        }
    }

    private sealed class FrameCloseButton : Button
    {
        private ThorVG.Shape? _cross, _feedback;
        protected override void OnAttached()
        {
            if (_cross != null) return;
            _feedback = ThorVG.Shape.Gen()!; AddPaint(_feedback);
            _cross = ThorVG.Shape.Gen()!; AddPaint(_cross);
            _cross.StrokeWidth(1.5f);
            _cross.StrokeCap(ThorVG.StrokeCap.Round);
            _cross.SetFill(0, 0, 0, 0);
            OnStateChanged(State, State);
        }
        protected override Vector2 MeasureCore(float width, float height) => new(28, 28);
        protected override void OnSizeChanged()
        {
            if (_cross == null) return;
            DialogFrame.DrawClose(_cross, Bounds.W / 2, Bounds.H / 2);
            _feedback?.ResetShape();
            _feedback?.AppendRect(1.5f, 1.5f, Math.Max(0, Bounds.W - 3), Math.Max(0, Bounds.H - 3), 4, 4);
        }
        protected override void OnThemeChanged() => OnStateChanged(State, State);
        protected override void OnStateChanged(ElementState oldState, ElementState state)
        {
            var theme = OwnerWindow?.Theme ?? Theme.Dark;
            var text = theme.TextColor;
            _cross?.StrokeFill(text.R8, text.G8, text.B8, text.A8);
            bool hover = (state & (ElementState.Hovered | ElementState.Pressed)) != 0;
            _feedback?.SetFill(text.R8, text.G8, text.B8, (byte)(hover ? (state.HasFlag(ElementState.Pressed) ? 45 : 24) : 0));
            var focus = Style.BorderFocused;
            _feedback?.StrokeWidth(1);
            _feedback?.StrokeFill(focus.R8, focus.G8, focus.B8, (byte)(state.HasFlag(ElementState.Focused) ? focus.A8 : 0));
            MarkDirty();
        }
    }

    private sealed class CardLayout(ModalDialog owner) : ILayout
    {
        public Vector2 Measure(Element container, float width, float height)
        {
            owner._header.Measure(width, height);
            owner._bodyViewport.Measure(Math.Max(0, width - 24), height);
            owner._footerViewport.Measure(Math.Max(0, width - 24), height);
            return new Vector2(width, height);
        }
        public void Arrange(Element container, RectF bounds)
        {
            float w = Math.Max(0, bounds.W - 24), h = Math.Max(0, bounds.H - 24);
            float header = Math.Min(bounds.H / 3, owner._header.Measure(bounds.W, bounds.H).Y);
            float footer = owner._footer == null ? 0 : Math.Min(h / 3, Math.Max(36, owner._footer.Measure(w, h).Y));
            owner._header.Arrange(new RectF(0, 0, bounds.W, header));
            owner._card.ArrangeFrame(header);
            owner._bodyViewport.Arrange(new RectF(12, header + 12, w, Math.Max(0, h - header - footer - 8)));
            owner._footerViewport.Arrange(new RectF(12, 12 + h - footer, w, footer));
        }
    }
}
