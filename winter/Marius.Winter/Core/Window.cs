using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Glfw;
using ThorVG;
using static Glfw.GLFW;
using Task = System.Threading.Tasks.Task;

namespace Marius.Winter;

public enum RenderBackend { GL, SW }

public unsafe class Window : Element
{
    private GlfwWindow? _window;
    internal GlfwWindow? GlfwWindow => _window;
    private Canvas? _canvas;
    private readonly RenderBackend _backend;
    private uint _width, _height;
    private bool _dirty = true;
    private bool _initialized;
    private bool _ownsSession, _graphicsInitialized, _glfwInitialized, _disposing, _running;
    private bool _drawable = true;
    private volatile bool _closeRequested;
    private int _logicalWidth, _logicalHeight;
    private readonly object _runtimeGate = new();
    private static readonly object SessionGate = new();
    private static bool _sessionActive;
    private readonly CancellationTokenSource _shutdown = new();

    private Theme _theme;
    private readonly AnimationManager _animator = new();
    internal readonly TaffyLayoutEngine _taffyLayout = new();
    public WindowDispatcherQueue DispatcherQueue { get; }
    public WindowDispatcher Dispatcher { get; }
    public CancellationToken ShutdownToken { get; }
    /// <summary>UI-thread async teardown, while the native context is alive.
    /// Only disposal continuations are pumped once shutdown begins.</summary>
    public event Func<ValueTask>? Closing;
    public uint FramebufferWidth => _drawable ? _width : 0;
    public uint FramebufferHeight => _drawable ? _height : 0;
    public float RenderScaleX => _logicalWidth > 0 ? (float)_width / _logicalWidth : 1;
    public float RenderScaleY => _logicalHeight > 0 ? (float)_height / _logicalHeight : 1;
    public float ContentScaleX { get; private set; } = 1;
    public float ContentScaleY { get; private set; } = 1;
    public Action? Resized;
    private Shape? _backgroundShape;
    private Stopwatch _clock = null!;
    private double _lastFrameTime;

    // Input state
    private Element? _hoveredElement;
    private Element? _focusedElement;
    private Element? _capturedElement;
    private float _mouseX, _mouseY;

    // Double-click detection
    private long _lastClickTimestamp;
    private Element? _lastClickElement;
    private int _lastClickButton = -1;
    private static readonly long DoubleClickInterval = Stopwatch.Frequency * 400 / 1000; // 400ms

    // Overlay layer — rendered on top of all children, hit-tested first
    private Scene? _overlayScene;
    private readonly List<Element> _overlays = new();
    private readonly Dictionary<Element, Element> _overlayInputOwners = new();
    private ModalDialog? _modal;
    private bool _keyBubbled;
    private bool _routingKey;
    private ModalDialog? _keyModal;
    private long _keyModalSession;

    // Context menu state
    private MenuPopup? _contextMenuPopup;
    private Element? _contextMenuTarget;

    // Tooltip state
    private const float TooltipDelay = 0.5f; // seconds before tooltip appears
    private float _tooltipTimer;
    private Element? _tooltipElement;
    private TooltipOverlay? _tooltipOverlay;
    private float _tooltipMouseX, _tooltipMouseY;

    // SW blit pipeline (only used when _backend == SW)
    private uint[]? _buffer;
    private delegate* unmanaged[Cdecl]<uint, uint*, void> _glGenTextures;
    private delegate* unmanaged[Cdecl]<uint, uint, void> _glBindTexture;
    private delegate* unmanaged[Cdecl]<uint, int, int, int, int, int, uint, uint, void*, void> _glTexImage2D;
    private delegate* unmanaged[Cdecl]<uint, int, int, int, int, int, uint, uint, void*, void> _glTexSubImage2D;
    private delegate* unmanaged[Cdecl]<uint, uint, int, void> _glTexParameteri;
    private delegate* unmanaged[Cdecl]<int, int, int, int, void> _glViewport;
    private delegate* unmanaged[Cdecl]<float, float, float, float, void> _glClearColor;
    private delegate* unmanaged[Cdecl]<uint, void> _glClear;
    private delegate* unmanaged[Cdecl]<uint, int, int, void> _glDrawArrays;
    private delegate* unmanaged[Cdecl]<uint, uint> _glCreateShader;
    private delegate* unmanaged[Cdecl]<uint, int, byte**, int*, void> _glShaderSource;
    private delegate* unmanaged[Cdecl]<uint, void> _glCompileShader;
    private delegate* unmanaged[Cdecl]<uint> _glCreateProgram;
    private delegate* unmanaged[Cdecl]<uint, uint, void> _glAttachShader;
    private delegate* unmanaged[Cdecl]<uint, void> _glLinkProgram;
    private delegate* unmanaged[Cdecl]<uint, void> _glUseProgram;
    private delegate* unmanaged[Cdecl]<uint, void> _glDeleteShader;
    private delegate* unmanaged[Cdecl]<int, uint*, void> _glGenVertexArrays;
    private delegate* unmanaged[Cdecl]<uint, void> _glBindVertexArray;
    private delegate* unmanaged[Cdecl]<int, uint*, void> _glGenBuffers;
    private delegate* unmanaged[Cdecl]<uint, uint, void> _glBindBuffer;
    private delegate* unmanaged[Cdecl]<uint, nint, void*, uint, void> _glBufferData;
    private delegate* unmanaged[Cdecl]<uint, int, uint, byte, int, nint, void> _glVertexAttribPointer;
    private delegate* unmanaged[Cdecl]<uint, void> _glEnableVertexAttribArray;

    private const uint GL_TEXTURE_2D = 0x0DE1;
    private const uint GL_TEXTURE_MIN_FILTER = 0x2801;
    private const uint GL_TEXTURE_MAG_FILTER = 0x2800;
    private const int GL_LINEAR = 0x2601;
    private const int GL_BGRA = 0x80E1;
    private const uint GL_UNSIGNED_BYTE = 0x1401;
    private const uint GL_COLOR_BUFFER_BIT = 0x00004000;
    private const uint GL_FRAGMENT_SHADER = 0x8B30;
    private const uint GL_VERTEX_SHADER = 0x8B31;
    private const uint GL_ARRAY_BUFFER = 0x8892;
    private const uint GL_STATIC_DRAW = 0x88E4;
    private const uint GL_FLOAT = 0x1406;
    private const uint GL_TRIANGLES = 0x0004;
    private const int GL_RGBA8 = 0x8058;

    private uint _texId, _vao, _vbo, _shaderProgram;
    private bool _glReady;

    // GLFW cursors
    private GlfwCursor?[] _cursors = new GlfwCursor?[8];

    // --- Public API ---

    public Theme Theme
    {
        get => _theme;
        set
        {
            _theme = value;
            var bg = _theme.WindowBackground;
            _backgroundShape?.SetFill(bg.R8, bg.G8, bg.B8, bg.A8);
            NotifyThemeChanged();
            _dirty = true;
        }
    }
    public AnimationManager Animator => _animator;

    public bool Dirty
    {
        get => _dirty;
        set { bool wake = value && !_dirty; _dirty = value; if (wake) Wake(); }
    }

    public Window(int width, int height, string title, Theme? theme = null,
        RenderBackend backend = RenderBackend.SW)
    {
        DispatcherQueue = new WindowDispatcherQueue(Wake);
        Dispatcher = new WindowDispatcher(DispatcherQueue);
        ShutdownToken = _shutdown.Token;
        _width = (uint)width;
        _height = (uint)height;
        _theme = theme ?? Theme.Dark;
        _backend = backend;
        _animator.Changed = () => Dirty = true;
        try
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Window dimensions must be positive.");
            lock (SessionGate)
            {
                if (_sessionActive) throw new InvalidOperationException("Winter supports one live Window session per process. Dispose the current Window before constructing another.");
                _sessionActive = _ownsSession = true;
            }
            Check(Initializer.Init(0), "initialize ThorVG");
            _graphicsInitialized = true;

            if (Glfw.Glfw.glfwInit() != GLFW_TRUE)
                throw NativeFailure("initialize GLFW");
            _glfwInitialized = true;

            Glfw.Glfw.glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 3);
            Glfw.Glfw.glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 3);
            Glfw.Glfw.glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Glfw.Glfw.glfwWindowHint(GLFW_OPENGL_FORWARD_COMPAT, GLFW_TRUE);

            Glfw.Glfw.glfwWindowHint(GLFW_VISIBLE, GLFW_FALSE);
            Glfw.Glfw.glfwWindowHint(GLFW_RESIZABLE, GLFW_TRUE);

            _window = Glfw.Glfw.glfwCreateWindow(width, height, title, null, null);
            if (_window == null)
                throw NativeFailure("create GLFW OpenGL 3.3 window");

            Glfw.Glfw.glfwMakeContextCurrent(_window);
            OpenGlLibraryLifetime.Retain();
            Glfw.Glfw.glfwSwapInterval(0);
            Glfw.Glfw.glfwGetWindowSize(_window, out _logicalWidth, out _logicalHeight);
            Glfw.Glfw.glfwGetFramebufferSize(_window, out int initialW, out int initialH);
            _drawable = initialW > 0 && initialH > 0;
            Glfw.Glfw.glfwGetWindowContentScale(_window, out float contentX, out float contentY);
            ContentScaleX = contentX; ContentScaleY = contentY;
            _width = (uint)Math.Max(1, initialW); _height = (uint)Math.Max(1, initialH);
            InitCursors();

            if (_backend == RenderBackend.GL)
            {
                var glCanvas = GlCanvas.Gen();
                if (glCanvas == null)
                    throw new InvalidOperationException("Winter initialization failed: ThorVG GL canvas is unavailable.");
                _canvas = glCanvas;
                RetargetGlCanvas();
            }
            else
            {
                if (!LoadGlFunctions())
                    throw new InvalidOperationException("Winter initialization failed: required OpenGL 3.3 SW upload functions are unavailable.");
                InitBlitPipeline();

                var swCanvas = SwCanvas.Gen();
                if (swCanvas == null)
                    throw new InvalidOperationException("Winter initialization failed: ThorVG SW canvas is unavailable.");
                _canvas = swCanvas;
                RetargetSwCanvas();
            }

            LoadDefaultFont();

            // Background shape
            _backgroundShape = Shape.Gen();
            _backgroundShape!.AppendRect(0, 0, _logicalWidth, _logicalHeight, 0, 0);
            var bg = _theme.WindowBackground;
            _backgroundShape.SetFill(bg.R8, bg.G8, bg.B8, bg.A8);
            AddPaint(_backgroundShape);

            // Add our scene as canvas root
            Check(_canvas.Add(Scene), "attach Window scene");

            // Overlay scene — added directly to canvas so it always renders on top of everything
            _overlayScene = Scene.Gen()!;
            Check(_canvas.Add(_overlayScene), "attach overlay scene");

            Bounds = new RectF(0, 0, _logicalWidth, _logicalHeight);
            ApplyRenderScale();
            SetupCallbacks();

            _clock = Stopwatch.StartNew();
            _initialized = true;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Run()
    {
        Dispatcher.VerifyAccess();
        ThrowIfDisposed();
        if (!_initialized || _window == null) throw new InvalidOperationException("Window initialization did not complete.");
        if (_running) throw new InvalidOperationException("Window.Run is already active.");
        _running = true;
        try
        {
            Glfw.Glfw.glfwMakeContextCurrent(_window);
            Glfw.Glfw.glfwShowWindow(_window);
            _lastFrameTime = _clock.Elapsed.TotalSeconds;
            while (!_closeRequested && Glfw.Glfw.glfwWindowShouldClose(_window) == 0)
            {
                using (Dispatcher.EnterScope())
                {
                    // Advance the previous frame's deadlines before callbacks
                    // can start a new animation/tooltip after a long native wait.
                    var now = _clock.Elapsed.TotalSeconds;
                    float elapsed = (float)(now - _lastFrameTime);
                    _lastFrameTime = now;
                    if (_animator.Tick(Math.Min(elapsed, 0.25f))) _dirty = true;
                    if (_tooltipElement != null && _tooltipOverlay == null)
                    {
                        _tooltipTimer += elapsed;
                        if (_tooltipTimer >= TooltipDelay) ShowTooltip();
                    }
                    Glfw.Glfw.glfwPollEvents();
                    DrainDispatcher();
                    if (_closeRequested || IsDisposed) break;
                    FlushLayout();
                    if (_dirty && _drawable)
                    {
                        _dirty = false;
                        Check(_canvas!.Update(), "update canvas");
                        Check(_canvas.Draw(true), "draw canvas");
                        Check(_canvas.Sync(), "synchronize canvas");
                        if (_backend == RenderBackend.SW) BlitToScreen();
                        else Glfw.Glfw.glfwSwapBuffers(_window);
                    }
                }
                if (!_closeRequested && !IsDisposed && Glfw.Glfw.glfwWindowShouldClose(_window) == 0) WaitForWork();
            }
        }
        finally { _running = false; Dispose(); }
    }

    public override void Dispose()
    {
        Dispatcher.VerifyAccess();
        if (IsDisposed || _disposing) return;
        _disposing = true;
        var failures = new List<Exception>();
        void Cleanup(Action action) { try { action(); } catch (Exception ex) { failures.Add(ex); } }
        Dispatcher.BeginShutdown();
        if (_window != null) Glfw.Glfw.glfwMakeContextCurrent(_window);
        using (Dispatcher.EnterScope(cleanup: true))
        {
            Cleanup(() => _shutdown.Cancel());
            var closing = Closing;
            Closing = null;
            if (closing != null)
                foreach (Func<ValueTask> handler in closing.GetInvocationList())
                    Cleanup(() => CompleteTeardown(handler()));
            Cleanup(DismissAllOverlays);
            _animator.CancelAll();
            if (_canvas != null) Cleanup(() => Check(_canvas.Sync(), "synchronize canvas before disposal"));
            Cleanup(() => base.Dispose());
            if (_canvas != null) Cleanup(_canvas.Dispose);
            _canvas = null;
            _overlayScene = null;
        }
        Dispatcher.Close();

        lock (_runtimeGate)
        {
            for (int i = 0; i < _cursors.Length; i++)
            {
                if (_cursors[i] != null)
                {
                    Cleanup(() => Glfw.Glfw.glfwDestroyCursor(_cursors[i]!));
                    _cursors[i] = null;
                }
            }

            if (_window != null)
            {
                Cleanup(DeleteBlitPipeline);
                Cleanup(() => Glfw.Glfw.glfwDestroyWindow(_window));
                _window = null;
            }
            if (_glfwInitialized) { Cleanup(Glfw.Glfw.glfwTerminate); _glfwInitialized = false; }
        }
        if (_graphicsInitialized) { Cleanup(() => Check(Initializer.Term(), "release ThorVG runtime")); _graphicsInitialized = false; }
        if (_ownsSession) { lock (SessionGate) { _sessionActive = false; _ownsSession = false; } }
        _initialized = false;
        _shutdown.Dispose();
        if (failures.Count != 0) throw new AggregateException("Winter shutdown failed; all cleanup stages were attempted.", failures);
    }

    /// <summary>May be called from any thread; wakes a waiting Run loop.</summary>
    public void Close() { _closeRequested = true; Wake(); }

    private void Wake()
    {
        lock (_runtimeGate)
            if (_glfwInitialized && _window != null) Glfw.Glfw.glfwPostEmptyEvent();
    }

    private void DrainDispatcher()
    {
        long started = Stopwatch.GetTimestamp();
        for (int i = 0; i < 64 && DispatcherQueue.TryDequeue(out var action); i++)
        {
            action();
            _dirty = true;
            if (_closeRequested || IsDisposed || Stopwatch.GetElapsedTime(started).TotalMilliseconds >= 4) break;
        }
    }

    /// <summary>Opt an immediate child into filling the logical client area.
    /// A later explicit Bounds/Arrange assignment returns it to manual sizing.</summary>
    public void SetAutoLayout(Element root)
    {
        Dispatcher.VerifyAccess();
        if (root.Parent != this) throw new ArgumentException("Automatic roots must be immediate Window children.", nameof(root));
        root.AutoArrangeRoot = true;
        root.InvalidateMeasure();
    }

    private void FlushLayout()
    {
        _modal?.UpdatePlacement();
        EnsureModalFocus();
        Measure(Bounds.W, Bounds.H);
        foreach (var root in Children)
        {
            if (!root.HasLayoutBounds && root is ILayoutContainer { Layout: not null }) root.AutoArrangeRoot = true;
            FlushElement(root, root.AutoArrangeRoot);
        }
        foreach (var overlay in _overlays.ToArray())
        {
            if (overlay is ComboBoxPopup choices) choices.UpdatePlacement();
            FlushElement(overlay, false);
        }
    }

    private void FlushElement(Element element, bool autoRoot)
    {
        if (element.IsMeasureDirty || (autoRoot && (element.Bounds.W != Bounds.W || element.Bounds.H != Bounds.H)))
        {
            var bounds = autoRoot ? new RectF(0, 0, Bounds.W, Bounds.H) : element.LayoutBounds;
            element.Measure(bounds.W, bounds.H);
            element.Arrange(bounds);
            element.AutoArrangeRoot = autoRoot;
        }
        // Manual-layout leaves and containers without a layout can own dirty
        // descendants absent from their parent's Taffy traversal.
        foreach (var child in element.Children) FlushElement(child, false);
    }

    private void ApplyRenderScale()
    {
        var matrix = new Matrix { e11 = RenderScaleX, e22 = RenderScaleY, e33 = 1 };
        Check(Scene.Transform(matrix), "scale Window scene");
        if (_overlayScene != null) Check(_overlayScene.Transform(matrix), "scale overlay scene");
    }

    private void ResizeTarget(int framebufferWidth, int framebufferHeight)
    {
        _drawable = framebufferWidth > 0 && framebufferHeight > 0;
        if (!_drawable) return;
        uint width = (uint)framebufferWidth, height = (uint)framebufferHeight;
        if (width != _width || height != _height)
        {
            Check(_canvas!.Sync(), "synchronize canvas before retarget");
            _width = width; _height = height;
            if (_backend == RenderBackend.GL) RetargetGlCanvas();
            else RetargetSwCanvas();
        }
        ApplyRenderScale();
        _dirty = true;
    }

    private void RefreshNativeSize()
    {
        Glfw.Glfw.glfwGetWindowSize(_window!, out int width, out int height);
        if (width > 0 && height > 0)
        {
            _logicalWidth = width; _logicalHeight = height;
            Bounds = new RectF(0, 0, width, height);
        }
        Glfw.Glfw.glfwGetFramebufferSize(_window!, out int fbWidth, out int fbHeight);
        ResizeTarget(fbWidth, fbHeight);
    }

    private void WaitForWork()
    {
        if (!DispatcherQueue.IsEmpty || (_dirty && _drawable)) return;
        double timeout = double.PositiveInfinity;
        if (_animator.HasActiveAnimations) timeout = Math.Max(0, 1.0 / 60 - (_clock.Elapsed.TotalSeconds - _lastFrameTime));
        if (_tooltipElement != null && _tooltipOverlay == null) timeout = Math.Min(timeout, Math.Max(0, TooltipDelay - _tooltipTimer));
        if (timeout <= 0) return;
        // GLFW waits also poll and dispatch native callbacks before returning.
        // Async input handlers must capture the same context as PollEvents.
        using var scope = Dispatcher.EnterScope();
        if (double.IsPositiveInfinity(timeout)) Glfw.Glfw.glfwWaitEvents();
        else Glfw.Glfw.glfwWaitEventsTimeout(timeout);
    }

    private void CompleteTeardown(ValueTask operation)
    {
        var task = operation.AsTask();
        // Unlike a blocking GetResult, this services async disposal continuations.
        // External completion wakes the native wait even without a UI continuation.
        _ = task.ContinueWith(_ => Wake(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, System.Threading.Tasks.TaskScheduler.Default);
        while (!task.IsCompleted)
        {
            DrainDispatcher();
            if (!task.IsCompleted && DispatcherQueue.IsEmpty) Glfw.Glfw.glfwWaitEvents();
        }
        task.GetAwaiter().GetResult();
    }

    private static void Check(Result result, string operation)
    {
        if (result != Result.Success) throw new InvalidOperationException($"Winter failed to {operation}: ThorVG returned {result}.");
    }

    private static Exception NativeFailure(string operation)
    {
        int code = Glfw.Glfw.glfwGetError(out string? detail);
        return new InvalidOperationException($"Winter failed to {operation}: GLFW error 0x{code:X}: {detail ?? "no platform diagnostic"}.");
    }

    public void SetCursor(CursorType type)
    {
        if (_window == null) return;
        var cursor = _cursors[(int)type];
        Glfw.Glfw.glfwSetCursor(_window, cursor);
    }

    // --- Focus management ---

    public Element? FocusedElement => _focusedElement;

    public void SetFocus(Element? element)
    {
        if (element != null && !CanReceiveInput(element)) return;
        if (_focusedElement == element) return;
        var old = _focusedElement;
        _focusedElement = element;

        if (old != null)
        {
            old.State = old.State & ~ElementState.Focused;
            old.OnBlur();
        }
        if (element != null)
        {
            element.State = element.State | ElementState.Focused;
            element.OnFocus();
            _modal?.RevealFocus(element);
        }
    }

    private bool CanReceiveInput(Element element) => ReferenceEquals(element.OwnerWindow, this) && element.IsActive && InModalScope(element);

    private bool InModalScope(Element element)
    {
        if (_modal == null) return true;
        if (ReferenceEquals(element, _modal.ModalRoot) || IsDescendantOf(element, _modal.ModalRoot)) return true;
        for (Element? root = element; root != null; root = root.Parent)
            if (_overlayInputOwners.TryGetValue(root, out var owner)) return InModalScope(owner);
        return false;
    }

    internal bool IsCurrentKeyScope => !_routingKey || (ReferenceEquals(_keyModal, _modal) && (_modal == null || _modal.SessionId == _keyModalSession));
    internal bool NoteKeyBubble() { _keyBubbled = true; return IsCurrentKeyScope; }

    internal void BeginModal(ModalDialog modal)
    {
        Dispatcher.VerifyAccess();
        if (_modal != null && !ReferenceEquals(_modal, modal)) throw new InvalidOperationException("Only one in-window modal may be open.");
        DismissAllOverlays();
        ReleaseSubtreeReferences(this);
        _modal = modal;
    }

    internal void EndModal(ModalDialog modal, Element? opener)
    {
        if (!ReferenceEquals(_modal, modal)) return;
        DismissAllOverlays();
        ReleaseSubtreeReferences(modal.ModalRoot);
        _modal = null;
        if (ShutdownToken.IsCancellationRequested) return;
        if (opener is { Focusable: true } && CanReceiveInput(opener)) SetFocus(opener);
        else
        {
            var eligible = new List<Element>(); CollectFocusable(this, eligible);
            SetFocus(eligible.Count > 0 ? eligible[0] : null);
        }
    }

    internal void EnsureModalFocus(Element? requested = null)
    {
        if (_modal == null) return;
        if (requested is { Focusable: true } && CanReceiveInput(requested)) { SetFocus(requested); return; }
        if (_focusedElement != null && CanReceiveInput(_focusedElement) && _focusedElement.Focusable) return;
        // Prefer body editors/actions over the title close button.
        var eligible = new List<Element>();
        if (_modal.Body != null) CollectFocusable(_modal.Body, eligible);
        if (_modal.Footer != null) CollectFocusable(_modal.Footer, eligible);
        SetFocus(eligible.Count > 0 ? eligible[0] : _modal.FocusFallback);
    }

    private Element? FocusedInput()
    {
        if (_focusedElement != null && !CanReceiveInput(_focusedElement)) SetFocus(null);
        return _focusedElement;
    }

    // Called before severing parent/window ownership, so OnBlur and animation
    // cleanup still have access to the old window and its animator.
    internal void ReleaseSubtreeReferences(Element root)
    {
        bool Contains(Element? element) => ReferenceEquals(root, element) || IsDescendantOf(element, root);
        if (Contains(_focusedElement)) SetFocus(null);
        if (Contains(_capturedElement))
        {
            var captured = _capturedElement;
            _capturedElement = null;
            captured!.State &= ~ElementState.Pressed;
            captured.OnDragEnd();
        }
        if (Contains(_hoveredElement))
        {
            var hovered = _hoveredElement;
            _hoveredElement = null;
            for (var element = hovered; element != null && element != this; element = element.Parent)
            {
                element.State &= ~ElementState.Hovered;
                element.OnMouseLeave();
            }
            SetCursor(CursorType.Arrow);
        }
        if (Contains(_tooltipElement)) HideTooltip();
        if (Contains(_lastClickElement))
        {
            _lastClickElement = null;
            _lastClickTimestamp = 0;
        }
        if (Contains(_contextMenuTarget)) DismissContextMenu();
        ClearTransientState(root);
        _dirty = true;
    }

    private static void ClearTransientState(Element element)
    {
        element.State &= ~(ElementState.Focused | ElementState.Hovered | ElementState.Pressed);
        foreach (var child in element.Children) ClearTransientState(child);
    }

    private void ValidatePointerReferences()
    {
        if (_capturedElement != null && !CanReceiveInput(_capturedElement)) ReleaseSubtreeReferences(_capturedElement);
        if (_hoveredElement != null && !CanReceiveInput(_hoveredElement)) ReleaseSubtreeReferences(_hoveredElement);
    }

    // --- Overlay management ---

    public void ShowOverlay(Element overlay, Element? inputOwner = null)
    {
        ThrowIfDisposed();
        overlay.ThrowIfDisposed();
        if (_overlays.Contains(overlay)) return;
        if (overlay.Parent != null || overlay._overlayOwner != null)
            throw new InvalidOperationException("Overlay already has an owner");
        if (_modal != null && !ReferenceEquals(overlay, _modal.ModalRoot) && (inputOwner == null || !CanReceiveInput(inputOwner)))
            throw new InvalidOperationException("A modal owns input; overlays must belong to its active content.");
        _overlays.Add(overlay);
        if (inputOwner != null) _overlayInputOwners.Add(overlay, inputOwner);
        overlay._overlayOwner = this;
        _overlayScene?.Add(overlay.Scene);
        overlay.NotifyAttached();
        overlay.NotifyThemeChanged();
        _dirty = true;
    }

    public void RemoveOverlay(Element overlay)
    {
        if (!_overlays.Remove(overlay)) return;
        overlay.DetachSubtree();
        _overlayScene?.Remove(overlay.Scene);
        overlay._overlayOwner = null;
        _overlayInputOwners.Remove(overlay);
        _dirty = true;
    }

    public void DismissAllOverlays()
    {
        DismissContextMenu();
        HideTooltip();
        foreach (var overlay in _overlays.ToArray())
        {
            if (ReferenceEquals(overlay, _modal?.ModalRoot)) continue;
            RemoveOverlay(overlay);
            overlay.OnBlur();
        }
        _contextMenuPopup = null;
        _contextMenuTarget = null;
        _dirty = true;
    }

    // --- Context menu ---

    private void ShowContextMenu(Element target, float x, float y)
    {
        var menu = new Menu();
        target.OnBuildContextMenu(menu);
        if (menu.Items.Count == 0) return;

        DismissContextMenu();
        _contextMenuTarget = target;

        _contextMenuPopup = new MenuPopup(this, menu.Items,
            onItemSelected: item => { DismissContextMenu(); item.Action?.Invoke(); },
            onDismiss: DismissContextMenu);

        var popupSize = _contextMenuPopup.MeasurePopup();

        // Position at cursor, clamped to window bounds
        float px = x, py = y;
        if (px + popupSize.X > Bounds.W - 4)
            px = Bounds.W - 4 - popupSize.X;
        if (py + popupSize.Y > Bounds.H - 4)
            py = Bounds.H - 4 - popupSize.Y;
        if (px < 4) px = 4;
        if (py < 4) py = 4;

        _contextMenuPopup.Arrange(new RectF(px, py, popupSize.X, popupSize.Y));
        ShowOverlay(_contextMenuPopup, target);

        // Fade-in animation
        _contextMenuPopup.Opacity = 0;
        Animator.Cancel("context_menu");
        Animator.Start(new Animation
        {
            Duration = 0.10f,
            Easing = Easings.EaseOutCubic,
            Tag = "context_menu",
            Apply = t => { if (_contextMenuPopup != null) _contextMenuPopup.Opacity = t; }
        });
    }

    private void DismissContextMenu()
    {
        _contextMenuTarget = null;
        if (_contextMenuPopup != null)
        {
            var popup = _contextMenuPopup;
            _contextMenuPopup = null;
            popup.CloseSubPopup();
            Animator.Cancel("context_menu");
            RemoveOverlay(popup);
            popup.Dispose();
        }
    }

    private Element? HitTestOverlays(float x, float y)
    {
        for (int i = _overlays.Count - 1; i >= 0; i--)
        {
            var hit = _overlays[i].HitTest(x, y);
            if (hit != null) return hit;
        }
        return null;
    }

    // --- Tab navigation ---

    private void CycleFocus(bool forward)
    {
        var focusable = new List<Element>();
        if (_modal != null)
        {
            if (_modal.Body != null) CollectFocusable(_modal.Body, focusable);
            if (_modal.Footer != null) CollectFocusable(_modal.Footer, focusable);
            if (_modal.ShowCloseButton) focusable.Add(_modal.FocusFallback);
        }
        else CollectFocusable(this, focusable);
        if (focusable.Count == 0) return;

        int current = _focusedElement != null ? focusable.IndexOf(_focusedElement) : -1;
        int next;
        if (forward)
            next = current + 1 >= focusable.Count ? 0 : current + 1;
        else
            next = current - 1 < 0 ? focusable.Count - 1 : current - 1;

        SetFocus(focusable[next]);
    }

    private static void CollectFocusable(Element element, List<Element> result)
    {
        if (!element.Visible || !element.Enabled) return;
        if (element.Focusable)
            result.Add(element);
        foreach (var child in element.Children)
            CollectFocusable(child, result);
    }

    // --- Private implementation ---

    private void LoadDefaultFont()
    {
        var asm = Assembly.GetExecutingAssembly();
        var resName = "Marius.Winter.Resources.PublicSans-Regular.ttf";
        using var stream = asm.GetManifestResourceStream(resName);
        if (stream == null)
            throw new InvalidOperationException($"Winter initialization failed: embedded font resource was not found: {resName}.");

        var data = new byte[stream.Length];
        stream.ReadExactly(data);
        Check(Text.LoadFont("default", data, (uint)data.Length, "font/ttf"), "load default font");

        var boldResName = "Marius.Winter.Resources.PublicSans-Bold.ttf";
        using var boldStream = asm.GetManifestResourceStream(boldResName);
        if (boldStream != null)
        {
            var boldData = new byte[boldStream.Length];
            boldStream.ReadExactly(boldData);
            Check(Text.LoadFont("default-bold", boldData, (uint)boldData.Length, "font/ttf"), "load bold font");
        }

        var monoResName = "Marius.Winter.Resources.JetBrainsMono-Regular.ttf";
        using var monoStream = asm.GetManifestResourceStream(monoResName);
        if (monoStream != null)
        {
            var monoData = new byte[monoStream.Length];
            monoStream.ReadExactly(monoData);
            Check(Text.LoadFont("monospace", monoData, (uint)monoData.Length, "font/ttf"), "load monospace font");
        }
    }

    private void InitCursors()
    {
        _cursors[(int)CursorType.Arrow] = Glfw.Glfw.glfwCreateStandardCursor(GLFW_ARROW_CURSOR);
        _cursors[(int)CursorType.IBeam] = Glfw.Glfw.glfwCreateStandardCursor(GLFW_IBEAM_CURSOR);
        _cursors[(int)CursorType.Hand] = Glfw.Glfw.glfwCreateStandardCursor(GLFW_POINTING_HAND_CURSOR);
        _cursors[(int)CursorType.HResize] = Glfw.Glfw.glfwCreateStandardCursor(GLFW_RESIZE_EW_CURSOR);
        _cursors[(int)CursorType.VResize] = Glfw.Glfw.glfwCreateStandardCursor(GLFW_RESIZE_NS_CURSOR);
        _cursors[(int)CursorType.Crosshair] = Glfw.Glfw.glfwCreateStandardCursor(GLFW_CROSSHAIR_CURSOR);
        _cursors[(int)CursorType.ResizeNWSE] = Glfw.Glfw.glfwCreateStandardCursor(GLFW.GLFW_RESIZE_NWSE_CURSOR);
        _cursors[(int)CursorType.ResizeNESW] = Glfw.Glfw.glfwCreateStandardCursor(GLFW.GLFW_RESIZE_NESW_CURSOR);
    }

    private void SetupCallbacks()
    {
        Glfw.Glfw.glfwSetKeyCallback(_window!, (w, key, scancode, action, mods) =>
        {
            if (action == GLFW_PRESS || action == GLFW_REPEAT)
            {
                // Tab / Shift+Tab cycles focus
                if (key == GLFW_KEY_TAB)
                {
                    CycleFocus((mods & GLFW_MOD_SHIFT) == 0);
                    _dirty = true;
                    return;
                }

                EnsureModalFocus();
                var focused = FocusedInput();
                // Child popup dismissals consume Escape before the modal.
                if (_modal != null && key == GLFW_KEY_ESCAPE)
                {
                    if (focused is ComboBox { IsDropDownOpen: true }) focused.OnKeyDown(key, mods, action == GLFW_REPEAT);
                    else if (_overlays.Exists(o => !ReferenceEquals(o, _modal.ModalRoot) && o is not TooltipOverlay)) DismissAllOverlays();
                    else if (action != GLFW_REPEAT) _modal.Escape();
                }
                else
                {
                    _keyBubbled = false;
                    _keyModal = _modal; _keyModalSession = _modal?.SessionId ?? 0; _routingKey = true;
                    try
                    {
                        focused?.OnKeyDown(key, mods, action == GLFW_REPEAT);
                        // Legacy TextBox commits but does not bubble Enter. Newer
                        // form-aware inputs bubble themselves; never route twice.
                        if (_modal != null && IsCurrentKeyScope && !_keyBubbled && ReferenceEquals(focused, _focusedElement)
                            && focused is TextBox && (key == GLFW_KEY_ENTER || key == GLFW_KEY_KP_ENTER))
                            focused.Parent?.OnKeyDown(key, mods, action == GLFW_REPEAT);
                    }
                    finally { _routingKey = false; _keyModal = null; }
                }
                _dirty = true;
            }
            else if (action == GLFW_RELEASE)
            {
                FocusedInput()?.OnKeyUp(key, mods);
            }
        });

        Glfw.Glfw.glfwSetCharCallback(_window!, (w, codepoint) =>
        {
            FocusedInput()?.OnTextInput(char.ConvertFromUtf32((int)codepoint));
            _dirty = true;
        });

        Glfw.Glfw.glfwSetMouseButtonCallback(_window!, (w, button, action, mods) =>
        {
            ValidatePointerReferences();
            // Hide tooltip on any mouse button press
            if (action == GLFW_PRESS && _tooltipOverlay != null)
                HideTooltip();

            if (action == GLFW_PRESS)
            {
                // Check overlays first
                var overlayHit = HitTestOverlays(_mouseX, _mouseY);
                if (overlayHit != null)
                {
                    if (_modal != null)
                    {
                        // Outside a child popup dismisses the transient first.
                        Element root = overlayHit; while (root.Parent != null) root = root.Parent;
                        if (ReferenceEquals(root, _modal.ModalRoot)) DismissAllOverlays();
                        if (button == 1) { SetFocus(overlayHit); ShowContextMenu(overlayHit, _mouseX, _mouseY); _dirty = true; return; }
                        var handler = BubbleMouseDown(overlayHit, button, _mouseX, _mouseY);
                        var capture = handler ?? overlayHit;
                        _capturedElement = CanReceiveInput(capture) ? capture : null;
                        // Popup rows keep focus at their owning chooser.
                        if (!(_overlayInputOwners.TryGetValue(root, out var popupOwner) && popupOwner is ComboBox))
                        { if (capture.Focusable) SetFocus(capture); else EnsureModalFocus(); }
                    }
                    else
                    {
                        _capturedElement = overlayHit;
                        overlayHit.OnMouseDown(button, _mouseX, _mouseY);
                    }
                    _dirty = true;
                    return;
                }

                // Click outside overlays dismisses them
                if (_overlays.Count > 0)
                {
                    // A second press on the chooser closes its list. Do not send that same
                    // press through as a fresh click that would immediately reopen it.
                    var hit = button == 0 ? HitTest(_mouseX, _mouseY) : null;
                    var toggledOwner = false;
                    foreach (var overlay in _overlays)
                        if (overlay is ComboBoxPopup choices && choices.BelongsTo(hit)) toggledOwner = true;
                    DismissAllOverlays();
                    _dirty = true;
                    if (toggledOwner) return;
                }

                // Right-click: show context menu instead of normal capture
                if (button == 1)
                {
                    var target = HitTest(_mouseX, _mouseY);
                    if (target != null && target != this)
                    {
                        SetFocus(target);
                        ShowContextMenu(target, _mouseX, _mouseY);
                    }
                    _dirty = true;
                    return;
                }

                var target2 = _capturedElement ?? HitTest(_mouseX, _mouseY);
                if (target2 != null && target2 != this)
                {
                    // Bubble OnMouseDown up from hit target through ancestors
                    // until one handles it (returns true)
                    var handler = BubbleMouseDown(target2, button, _mouseX, _mouseY);
                    var capture = handler ?? target2;
                    _capturedElement = CanReceiveInput(capture) ? capture : null;
                    SetFocus(_capturedElement);
                    _dirty = true;
                }
                else
                {
                    SetFocus(null);
                }
            }
            else if (action == GLFW_RELEASE)
            {
                var target = _capturedElement;
                if (target != null)
                {
                    target.OnMouseUp(button, _mouseX, _mouseY);

                    // Check if still inside for click — hit can be the captured element
                    // or any descendant of it (e.g. Label inside Button)
                    var hitNow = HitTestOverlays(_mouseX, _mouseY) ?? HitTest(_mouseX, _mouseY);
                    if (hitNow == target || IsDescendantOf(hitNow, target))
                    {
                        long now = Stopwatch.GetTimestamp();
                        bool isDoubleClick = target == _lastClickElement
                            && button == _lastClickButton
                            && (now - _lastClickTimestamp) < DoubleClickInterval;

                        target.OnClick();

                        if (isDoubleClick)
                        {
                            target.OnDoubleClick();
                            _lastClickElement = null;
                            _lastClickTimestamp = 0;
                        }
                        else
                        {
                            _lastClickElement = target;
                            _lastClickButton = button;
                            _lastClickTimestamp = now;
                        }
                    }

                    _capturedElement = null;
                    _dirty = true;

                    // Update hover state
                    UpdateHover(_mouseX, _mouseY);
                }
            }
        });

        Glfw.Glfw.glfwSetCursorPosCallback(_window!, (w, xpos, ypos) =>
        {
            ValidatePointerReferences();
            _mouseX = (float)xpos;
            _mouseY = (float)ypos;

            if (_capturedElement != null)
            {
                _capturedElement.OnMouseMove(_mouseX, _mouseY);
                _dirty = true;
            }
            else
            {
                UpdateHover(_mouseX, _mouseY);
            }
        });

        Glfw.Glfw.glfwSetScrollCallback(_window!, (w, xoffset, yoffset) =>
        {
            ValidatePointerReferences();
            var target = _hoveredElement ?? HitTestOverlays(_mouseX, _mouseY) ?? (_modal == null ? HitTest(_mouseX, _mouseY) : null);
            if (target != null && target != this)
            {
                target.OnScroll((float)xoffset, (float)yoffset);
                _dirty = true;
                // Re-run hover detection — scrolling moves content under the cursor
                UpdateHover(_mouseX, _mouseY);
            }
        });

        Glfw.Glfw.glfwSetWindowSizeCallback(_window!, (w, width, height) => RefreshNativeSize());
        Glfw.Glfw.glfwSetFramebufferSizeCallback(_window!, (w, width, height) => ResizeTarget(width, height));
        Glfw.Glfw.glfwSetWindowContentScaleCallback(_window!, (w, x, y) =>
        {
            ContentScaleX = x; ContentScaleY = y;
            RefreshNativeSize();
        });
        Glfw.Glfw.glfwSetWindowRefreshCallback(_window!, _ => _dirty = true);
        Glfw.Glfw.glfwSetWindowCloseCallback(_window!, _ => Close());
        Glfw.Glfw.glfwSetWindowFocusCallback(_window!, (_, focused) =>
        {
            if (focused == 0) ReleaseSubtreeReferences(this);
        });
        Glfw.Glfw.glfwSetCursorEnterCallback(_window!, (_, entered) =>
        {
            if (entered == 0 && _capturedElement == null && _hoveredElement != null) ReleaseSubtreeReferences(_hoveredElement);
        });
    }

    private void UpdateHover(float x, float y)
    {
        var hit = HitTestOverlays(x, y) ?? HitTest(x, y);
        if (hit == this) hit = null; // don't hover the window itself
        // Don't treat the tooltip overlay itself as the hovered element
        if (hit is TooltipOverlay) hit = _hoveredElement;

        if (hit != _hoveredElement)
        {
            var old = _hoveredElement;
            _hoveredElement = hit;

            // Collect new ancestor chain as a set
            var newChain = new HashSet<Element>();
            for (var el = hit; el != null && el != this; el = el.Parent)
                newChain.Add(el);

            // Elements in old chain but NOT in new chain: mouse left them
            for (var el = old; el != null && el != this; el = el.Parent)
            {
                if (!newChain.Contains(el))
                {
                    el.State = el.State & ~ElementState.Hovered;
                    el.OnMouseLeave();
                }
            }

            // Collect old ancestor chain as a set
            var oldChain = new HashSet<Element>();
            for (var el = old; el != null && el != this; el = el.Parent)
                oldChain.Add(el);

            // Elements in new chain but NOT in old chain: mouse entered them
            for (var el = hit; el != null && el != this; el = el.Parent)
            {
                if (!oldChain.Contains(el))
                {
                    el.State = el.State | ElementState.Hovered;
                    el.OnMouseEnter();
                }
            }

            SetCursor(GetEffectiveCursor(hit));
            _dirty = true;

            // Tooltip: new element hovered — reset timer
            UpdateTooltipTarget(hit, x, y);
        }
        else if (hit != null)
        {
            hit.OnMouseMove(x, y);
            // Update cursor — element may dynamically change cursor based on mouse position
            SetCursor(GetEffectiveCursor(hit));
            // If mouse moved significantly while waiting, reset timer
            if (_tooltipOverlay == null && _tooltipElement != null)
            {
                float dx = x - _tooltipMouseX;
                float dy = y - _tooltipMouseY;
                if (dx * dx + dy * dy > 25) // >5px movement
                {
                    _tooltipTimer = 0;
                    _tooltipMouseX = x;
                    _tooltipMouseY = y;
                }
            }
        }
    }

    /// <summary>
    /// Bubble OnMouseDown from target up through ancestors until one handles it (returns true).
    /// Returns the handling element, or null if none handled it.
    /// </summary>
    private Element? BubbleMouseDown(Element target, int button, float x, float y)
    {
        var el = target;
        while (el != null && el != this)
        {
            if (el.OnMouseDown(button, x, y))
                return el;
            el = el.Parent;
        }
        return null;
    }

    /// <summary>Walk up the parent chain to find the nearest non-default cursor (like CSS cursor inheritance).</summary>
    private static CursorType GetEffectiveCursor(Element? element)
    {
        for (var el = element; el != null; el = el.Parent)
        {
            if (el.Cursor != CursorType.Arrow)
                return el.Cursor;
        }
        return CursorType.Arrow;
    }

    /// <summary>Returns true if 'element' is a descendant of 'ancestor'.</summary>
    private static bool IsDescendantOf(Element? element, Element ancestor)
    {
        var el = element?.Parent;
        while (el != null)
        {
            if (el == ancestor) return true;
            el = el.Parent;
        }
        return false;
    }

    private void UpdateTooltipTarget(Element? element, float x, float y)
    {
        // Hide existing tooltip
        HideTooltip();

        // Check if the new element has tooltip content
        if (element?.Tooltip != null)
        {
            _tooltipElement = element;
            _tooltipTimer = 0;
            _tooltipMouseX = x;
            _tooltipMouseY = y;
        }
        else
        {
            _tooltipElement = null;
        }
    }

    // Parameter/native updates affect only the help belonging to this hovered
    // owner. In particular, clearing must also cancel a delayed presentation.
    internal void RefreshTooltip(Element owner)
    {
        if (!ReferenceEquals(owner, _hoveredElement)) return;
        if (owner.Tooltip == null)
        {
            if (ReferenceEquals(owner, _tooltipElement)) HideTooltip();
            return;
        }

        bool visible = ReferenceEquals(owner, _tooltipElement) && _tooltipOverlay != null;
        if (!ReferenceEquals(owner, _tooltipElement))
        {
            _tooltipTimer = 0;
            _tooltipMouseX = _mouseX;
            _tooltipMouseY = _mouseY;
        }
        if (visible) HideTooltip();
        _tooltipElement = owner;
        if (visible) PresentTooltip(fadeIn: false);
        else _dirty = true;
    }

    private void ShowTooltip() => PresentTooltip(fadeIn: true);

    private void PresentTooltip(bool fadeIn)
    {
        if (_tooltipElement == null || _tooltipElement.Tooltip == null) return;

        _tooltipOverlay = new TooltipOverlay(_tooltipElement.Tooltip);
        ShowOverlay(_tooltipOverlay, _tooltipElement);
        _tooltipOverlay.PositionNear(_tooltipMouseX, _tooltipMouseY, (uint)_logicalWidth, (uint)_logicalHeight, _theme);

        // Fade-in animation
        _tooltipOverlay.Opacity = fadeIn ? 0 : 1;
        _animator.Cancel("tooltip");
        if (fadeIn) _animator.Start(new Animation
        {
            Duration = 0.15f,
            Easing = Easings.EaseOutCubic,
            Tag = "tooltip",
            Apply = t => { if (_tooltipOverlay != null) _tooltipOverlay.Opacity = t; }
        });

        _dirty = true;
    }

    private void HideTooltip()
    {
        _animator.Cancel("tooltip");
        if (_tooltipOverlay != null)
        {
            var overlay = _tooltipOverlay;
            _tooltipOverlay = null;
            RemoveOverlay(overlay);
            overlay.Dispose();
            _dirty = true;
        }
        _tooltipElement = null;
        _tooltipTimer = 0;
    }

    // --- GL backend ---

    private void RetargetGlCanvas()
    {
        nint display = 0, surface = 0, context = 0;
        if (_window!.context.glx != null)
        {
            display = _glfw.X11?.display ?? 0;
            surface = (nint)_window.context.glx.window;
            context = _window.context.glx.handle;
        }
        else if (_window.context.wgl != null)
        {
            context = _window.context.wgl.handle;
        }
        if (context == 0) throw new PlatformNotSupportedException("Winter GL requires a supported GLFW GLX or WGL context; no native context handle was available.");
        Check(((GlCanvas)_canvas!).Target(display, surface, context, 0, _width, _height, ColorSpace.ABGR8888S), "target GL canvas");
    }

    // --- SW backend ---

    private nint GetProc(string name) => Glfw.Glfw.glfwGetProcAddress(name);

    private bool LoadGlFunctions()
    {
        _glGenTextures = (delegate* unmanaged[Cdecl]<uint, uint*, void>)GetProc("glGenTextures");
        _glBindTexture = (delegate* unmanaged[Cdecl]<uint, uint, void>)GetProc("glBindTexture");
        _glTexImage2D = (delegate* unmanaged[Cdecl]<uint, int, int, int, int, int, uint, uint, void*, void>)GetProc("glTexImage2D");
        _glTexSubImage2D = (delegate* unmanaged[Cdecl]<uint, int, int, int, int, int, uint, uint, void*, void>)GetProc("glTexSubImage2D");
        _glTexParameteri = (delegate* unmanaged[Cdecl]<uint, uint, int, void>)GetProc("glTexParameteri");
        _glViewport = (delegate* unmanaged[Cdecl]<int, int, int, int, void>)GetProc("glViewport");
        _glClearColor = (delegate* unmanaged[Cdecl]<float, float, float, float, void>)GetProc("glClearColor");
        _glClear = (delegate* unmanaged[Cdecl]<uint, void>)GetProc("glClear");
        _glDrawArrays = (delegate* unmanaged[Cdecl]<uint, int, int, void>)GetProc("glDrawArrays");
        _glCreateShader = (delegate* unmanaged[Cdecl]<uint, uint>)GetProc("glCreateShader");
        _glShaderSource = (delegate* unmanaged[Cdecl]<uint, int, byte**, int*, void>)GetProc("glShaderSource");
        _glCompileShader = (delegate* unmanaged[Cdecl]<uint, void>)GetProc("glCompileShader");
        _glCreateProgram = (delegate* unmanaged[Cdecl]<uint>)GetProc("glCreateProgram");
        _glAttachShader = (delegate* unmanaged[Cdecl]<uint, uint, void>)GetProc("glAttachShader");
        _glLinkProgram = (delegate* unmanaged[Cdecl]<uint, void>)GetProc("glLinkProgram");
        _glUseProgram = (delegate* unmanaged[Cdecl]<uint, void>)GetProc("glUseProgram");
        _glDeleteShader = (delegate* unmanaged[Cdecl]<uint, void>)GetProc("glDeleteShader");
        _glGenVertexArrays = (delegate* unmanaged[Cdecl]<int, uint*, void>)GetProc("glGenVertexArrays");
        _glBindVertexArray = (delegate* unmanaged[Cdecl]<uint, void>)GetProc("glBindVertexArray");
        _glGenBuffers = (delegate* unmanaged[Cdecl]<int, uint*, void>)GetProc("glGenBuffers");
        _glBindBuffer = (delegate* unmanaged[Cdecl]<uint, uint, void>)GetProc("glBindBuffer");
        _glBufferData = (delegate* unmanaged[Cdecl]<uint, nint, void*, uint, void>)GetProc("glBufferData");
        _glVertexAttribPointer = (delegate* unmanaged[Cdecl]<uint, int, uint, byte, int, nint, void>)GetProc("glVertexAttribPointer");
        _glEnableVertexAttribArray = (delegate* unmanaged[Cdecl]<uint, void>)GetProc("glEnableVertexAttribArray");

        return _glGenTextures != null && _glBindTexture != null && _glTexImage2D != null &&
            _glTexSubImage2D != null && _glTexParameteri != null && _glViewport != null &&
            _glClearColor != null && _glClear != null && _glDrawArrays != null && _glCreateShader != null &&
            _glShaderSource != null && _glCompileShader != null && _glCreateProgram != null &&
            _glAttachShader != null && _glLinkProgram != null && _glUseProgram != null && _glDeleteShader != null &&
            _glGenVertexArrays != null && _glBindVertexArray != null && _glGenBuffers != null &&
            _glBindBuffer != null && _glBufferData != null && _glVertexAttribPointer != null && _glEnableVertexAttribArray != null;
    }

    private void DeleteBlitPipeline()
    {
        if (_texId != 0)
        {
            var delete = (delegate* unmanaged[Cdecl]<int, uint*, void>)GetProc("glDeleteTextures");
            uint id = _texId; if (delete != null) delete(1, &id); _texId = 0;
        }
        if (_vbo != 0)
        {
            var delete = (delegate* unmanaged[Cdecl]<int, uint*, void>)GetProc("glDeleteBuffers");
            uint id = _vbo; if (delete != null) delete(1, &id); _vbo = 0;
        }
        if (_vao != 0)
        {
            var delete = (delegate* unmanaged[Cdecl]<int, uint*, void>)GetProc("glDeleteVertexArrays");
            uint id = _vao; if (delete != null) delete(1, &id); _vao = 0;
        }
        if (_shaderProgram != 0)
        {
            var delete = (delegate* unmanaged[Cdecl]<uint, void>)GetProc("glDeleteProgram");
            if (delete != null) delete(_shaderProgram); _shaderProgram = 0;
        }
        _glReady = false;
    }

    private void InitBlitPipeline()
    {
        ReadOnlySpan<byte> vertSrc = "#version 330 core\nlayout(location=0) in vec2 aPos;\nlayout(location=1) in vec2 aUV;\nout vec2 vUV;\nvoid main() { gl_Position = vec4(aPos, 0.0, 1.0); vUV = aUV; }\0"u8;
        ReadOnlySpan<byte> fragSrc = "#version 330 core\nin vec2 vUV;\nout vec4 FragColor;\nuniform sampler2D uTex;\nvoid main() { FragColor = texture(uTex, vUV); }\0"u8;

        var vs = _glCreateShader(GL_VERTEX_SHADER);
        fixed (byte* p = vertSrc) { byte* pp = p; _glShaderSource(vs, 1, &pp, null); }
        _glCompileShader(vs);

        var fs = _glCreateShader(GL_FRAGMENT_SHADER);
        fixed (byte* p = fragSrc) { byte* pp = p; _glShaderSource(fs, 1, &pp, null); }
        _glCompileShader(fs);

        _shaderProgram = _glCreateProgram();
        _glAttachShader(_shaderProgram, vs);
        _glAttachShader(_shaderProgram, fs);
        _glLinkProgram(_shaderProgram);
        _glDeleteShader(vs);
        _glDeleteShader(fs);

        Span<float> verts = stackalloc float[24]
        {
            -1f, -1f,  0f, 1f,
             1f, -1f,  1f, 1f,
             1f,  1f,  1f, 0f,
            -1f, -1f,  0f, 1f,
             1f,  1f,  1f, 0f,
            -1f,  1f,  0f, 0f,
        };

        uint localVao, localVbo;
        _glGenVertexArrays(1, &localVao);
        _vao = localVao;
        _glBindVertexArray(_vao);
        _glGenBuffers(1, &localVbo);
        _vbo = localVbo;
        _glBindBuffer(GL_ARRAY_BUFFER, _vbo);
        fixed (float* vp = verts)
            _glBufferData(GL_ARRAY_BUFFER, (nint)(24 * sizeof(float)), vp, GL_STATIC_DRAW);
        _glVertexAttribPointer(0, 2, GL_FLOAT, 0, 4 * sizeof(float), (nint)0);
        _glEnableVertexAttribArray(0);
        _glVertexAttribPointer(1, 2, GL_FLOAT, 0, 4 * sizeof(float), (nint)(2 * sizeof(float)));
        _glEnableVertexAttribArray(1);

        uint localTex;
        _glGenTextures(1, &localTex);
        _texId = localTex;
        _glBindTexture(GL_TEXTURE_2D, _texId);
        _glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
        _glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);

        _glReady = true;
    }

    private void RetargetSwCanvas()
    {
        _buffer = new uint[_width * _height];
        Check(((SwCanvas)_canvas!).Target(_buffer, _width, _width, _height, ColorSpace.ARGB8888), "target SW canvas");

        if (_glReady)
        {
            _glBindTexture(GL_TEXTURE_2D, _texId);
            _glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA8, (int)_width, (int)_height, 0,
                (uint)GL_BGRA, GL_UNSIGNED_BYTE, null);
        }
    }

    private void BlitToScreen()
    {
        if (_buffer == null || !_glReady || _window == null) return;

        Glfw.Glfw.glfwGetFramebufferSize(_window, out int fbW, out int fbH);
        _glViewport(0, 0, fbW, fbH);

        _glBindTexture(GL_TEXTURE_2D, _texId);
        fixed (uint* ptr = _buffer)
        {
            _glTexSubImage2D(GL_TEXTURE_2D, 0, 0, 0, (int)_width, (int)_height,
                (uint)GL_BGRA, GL_UNSIGNED_BYTE, ptr);
        }

        _glClearColor(0, 0, 0, 1);
        _glClear(GL_COLOR_BUFFER_BIT);
        _glUseProgram(_shaderProgram);
        _glBindVertexArray(_vao);
        _glDrawArrays(GL_TRIANGLES, 0, 6);

        Glfw.Glfw.glfwSwapBuffers(_window);
    }

    // --- Override Element for Window-specific behavior ---

    // Style/theme refresh must not synthesize a native resize notification.
    protected override void OnStyleChanged() => OnThemeChanged();

    protected override void OnSizeChanged()
    {
        // Update background shape to match new size
        if (_backgroundShape != null)
        {
            _backgroundShape.ResetShape();
            _backgroundShape.AppendRect(0, 0, Bounds.W, Bounds.H, 0, 0);
        }

        Resized?.Invoke();
    }
}
