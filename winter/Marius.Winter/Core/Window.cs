using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Glfw;
using ThorVG;
using static Glfw.GLFW;

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

    private Theme _theme;
    private readonly AnimationManager _animator = new();
    internal readonly TaffyLayoutEngine _taffyLayout = new();
    public readonly ConcurrentQueue<Action> DispatcherQueue = new();
    public Action? Resized;
    private Shape? _backgroundShape;
    private Stopwatch _clock = null!;
    private double _lastFrameTime;

    // Input state
    private Element? _hoveredElement;
    private Element? _focusedElement;
    private Element? _capturedElement;
    private float _mouseX, _mouseY;

    // Overlay layer — rendered on top of all children, hit-tested first
    private Scene? _overlayScene;
    private readonly List<Element> _overlays = new();

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
    private GlfwCursor?[] _cursors = new GlfwCursor?[6];

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
        set => _dirty = value;
    }

    public Window(int width, int height, string title, Theme? theme = null,
        RenderBackend backend = RenderBackend.SW)
    {
        _width = (uint)width;
        _height = (uint)height;
        _theme = theme ?? Theme.Dark;
        _backend = backend;

        if (Initializer.Init(0) != Result.Success)
        {
            Console.Error.WriteLine("Failed to init ThorVG engine");
            return;
        }

        if (Glfw.Glfw.glfwInit() != GLFW_TRUE)
        {
            Console.Error.WriteLine("Failed to init GLFW");
            return;
        }

        Glfw.Glfw.glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 3);
        Glfw.Glfw.glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 3);
        Glfw.Glfw.glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Glfw.Glfw.glfwWindowHint(GLFW_OPENGL_FORWARD_COMPAT, GLFW_TRUE);

        Glfw.Glfw.glfwWindowHint(GLFW_VISIBLE, GLFW_FALSE);
        Glfw.Glfw.glfwWindowHint(GLFW_RESIZABLE, GLFW_TRUE);

        _window = Glfw.Glfw.glfwCreateWindow(width, height, title, null, null);
        if (_window == null)
        {
            Console.Error.WriteLine("Failed to create GLFW window");
            return;
        }

        Glfw.Glfw.glfwMakeContextCurrent(_window);
        InitCursors();

        if (_backend == RenderBackend.GL)
        {
            var glCanvas = GlCanvas.Gen();
            if (glCanvas == null)
            {
                Console.Error.WriteLine("GlCanvas not available");
                return;
            }
            _canvas = glCanvas;
            RetargetGlCanvas();
        }
        else
        {
            if (!LoadGlFunctions())
            {
                Console.Error.WriteLine("Failed to load GL functions");
                return;
            }
            InitBlitPipeline();

            var swCanvas = SwCanvas.Gen();
            if (swCanvas == null)
            {
                Console.Error.WriteLine("SwCanvas not available");
                return;
            }
            _canvas = swCanvas;
            RetargetSwCanvas();
        }

        LoadDefaultFont();

        // Background shape
        _backgroundShape = Shape.Gen();
        _backgroundShape!.AppendRect(0, 0, _width, _height, 0, 0);
        var bg = _theme.WindowBackground;
        _backgroundShape.SetFill(bg.R8, bg.G8, bg.B8, bg.A8);
        AddPaint(_backgroundShape);

        // Add our scene as canvas root
        _canvas.Add(Scene);

        // Overlay scene — added directly to canvas so it always renders on top of everything
        _overlayScene = Scene.Gen()!;
        _canvas.Add(_overlayScene);

        _clock = Stopwatch.StartNew();
        _initialized = true;
    }

    public void Run()
    {
        if (!_initialized || _window == null) return;

        Glfw.Glfw.glfwShowWindow(_window);
        SetupCallbacks();

        // Initial layout
        Bounds = new RectF(0, 0, _width, _height);
        _dirty = true;

        while (Glfw.Glfw.glfwWindowShouldClose(_window) == 0)
        {
            Glfw.Glfw.glfwPollEvents();

            // Drain dispatcher queue (Blazor integration)
            while (DispatcherQueue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex) { Console.Error.WriteLine($"Dispatcher error: {ex}"); }
                _dirty = true;
            }

            var now = _clock.Elapsed.TotalSeconds;
            float dt = (float)(now - _lastFrameTime);
            _lastFrameTime = now;

            if (dt > 0.25f) dt = 0.25f; // clamp after pauses

            _animator.Tick(dt);
            if (_animator.HasActiveAnimations) _dirty = true;

            // Tooltip delay timer
            if (_tooltipElement != null && _tooltipOverlay == null)
            {
                _tooltipTimer += dt;
                if (_tooltipTimer >= TooltipDelay)
                    ShowTooltip();
            }

            if (_dirty)
            {
                _dirty = false;
                _canvas!.Update();
                _canvas.Draw(true);
                _canvas.Sync();
                if (_backend == RenderBackend.SW)
                    BlitToScreen();
                else
                    Glfw.Glfw.glfwSwapBuffers(_window);
            }
        }

        Dispose();
    }

    public void Dispose()
    {
        _canvas = null;

        for (int i = 0; i < _cursors.Length; i++)
        {
            if (_cursors[i] != null)
            {
                Glfw.Glfw.glfwDestroyCursor(_cursors[i]!);
                _cursors[i] = null;
            }
        }

        if (_window != null)
        {
            Glfw.Glfw.glfwDestroyWindow(_window);
            _window = null;
        }

        Glfw.Glfw.glfwTerminate();
        Initializer.Term();
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
        }
    }

    // --- Overlay management ---

    public void ShowOverlay(Element overlay)
    {
        if (_overlays.Contains(overlay)) return;
        _overlays.Add(overlay);
        overlay._overlayOwner = this;
        _overlayScene?.Add(overlay.Scene);
        overlay.NotifyThemeChanged();
        _dirty = true;
    }

    public void RemoveOverlay(Element overlay)
    {
        if (!_overlays.Remove(overlay)) return;
        overlay._overlayOwner = null;
        _overlayScene?.Remove(overlay.Scene);
        _dirty = true;
    }

    public void DismissAllOverlays()
    {
        for (int i = _overlays.Count - 1; i >= 0; i--)
        {
            var overlay = _overlays[i];
            overlay._overlayOwner = null;
            _overlayScene?.Remove(overlay.Scene);
            overlay.OnBlur();
        }
        _overlays.Clear();
        _dirty = true;
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
        CollectFocusable(this, focusable);
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
        {
            Console.Error.WriteLine($"Font resource not found: {resName}");
            return;
        }

        var data = new byte[stream.Length];
        stream.ReadExactly(data);
        Text.LoadFont("default", data, (uint)data.Length, "font/ttf");

        var boldResName = "Marius.Winter.Resources.PublicSans-Bold.ttf";
        using var boldStream = asm.GetManifestResourceStream(boldResName);
        if (boldStream != null)
        {
            var boldData = new byte[boldStream.Length];
            boldStream.ReadExactly(boldData);
            Text.LoadFont("default-bold", boldData, (uint)boldData.Length, "font/ttf");
        }

        var monoResName = "Marius.Winter.Resources.JetBrainsMono-Regular.ttf";
        using var monoStream = asm.GetManifestResourceStream(monoResName);
        if (monoStream != null)
        {
            var monoData = new byte[monoStream.Length];
            monoStream.ReadExactly(monoData);
            Text.LoadFont("monospace", monoData, (uint)monoData.Length, "font/ttf");
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

                _focusedElement?.OnKeyDown(key, mods, action == GLFW_REPEAT);
                _dirty = true;
            }
            else if (action == GLFW_RELEASE)
            {
                _focusedElement?.OnKeyUp(key, mods);
            }
        });

        Glfw.Glfw.glfwSetCharCallback(_window!, (w, codepoint) =>
        {
            _focusedElement?.OnTextInput(char.ConvertFromUtf32((int)codepoint));
            _dirty = true;
        });

        Glfw.Glfw.glfwSetMouseButtonCallback(_window!, (w, button, action, mods) =>
        {
            // Hide tooltip on any mouse button press
            if (action == GLFW_PRESS && _tooltipOverlay != null)
                HideTooltip();

            if (action == GLFW_PRESS)
            {
                // Check overlays first
                var overlayHit = HitTestOverlays(_mouseX, _mouseY);
                if (overlayHit != null)
                {
                    _capturedElement = overlayHit;
                    overlayHit.OnMouseDown(button, _mouseX, _mouseY);
                    _dirty = true;
                    return;
                }

                // Click outside overlays dismisses them
                if (_overlays.Count > 0)
                {
                    DismissAllOverlays();
                    _dirty = true;
                }

                var target = _capturedElement ?? HitTest(_mouseX, _mouseY);
                if (target != null && target != this)
                {
                    // Bubble OnMouseDown up from hit target through ancestors
                    // until one handles it (returns true)
                    var handler = BubbleMouseDown(target, button, _mouseX, _mouseY);
                    _capturedElement = handler ?? target;
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
                        target.OnClick();

                    _capturedElement = null;
                    _dirty = true;

                    // Update hover state
                    UpdateHover(_mouseX, _mouseY);
                }
            }
        });

        Glfw.Glfw.glfwSetCursorPosCallback(_window!, (w, xpos, ypos) =>
        {
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
            var target = _hoveredElement ?? HitTest(_mouseX, _mouseY);
            if (target != null && target != this)
            {
                target.OnScroll((float)xoffset, (float)yoffset);
                _dirty = true;
                // Re-run hover detection — scrolling moves content under the cursor
                UpdateHover(_mouseX, _mouseY);
            }
        });

        Glfw.Glfw.glfwSetFramebufferSizeCallback(_window!, (w, fbW, fbH) =>
        {
            if (fbW > 0 && fbH > 0)
            {
                _width = (uint)fbW;
                _height = (uint)fbH;
                if (_backend == RenderBackend.GL)
                    RetargetGlCanvas();
                else
                    RetargetSwCanvas();
                Bounds = new RectF(0, 0, _width, _height);
                _dirty = true;
            }
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

    private void ShowTooltip()
    {
        if (_tooltipElement == null || _tooltipElement.Tooltip == null) return;

        _tooltipOverlay = new TooltipOverlay(_tooltipElement.Tooltip);
        ShowOverlay(_tooltipOverlay);
        _tooltipOverlay.PositionNear(_tooltipMouseX, _tooltipMouseY, _width, _height, _theme);

        // Fade-in animation
        _tooltipOverlay.Opacity = 0;
        _animator.Cancel("tooltip");
        _animator.Start(new Animation
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
            RemoveOverlay(_tooltipOverlay);
            _tooltipOverlay = null;
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
        if (context == 0) context = 1; // fallback — CurrentContext() is a no-op in C# port
        ((GlCanvas)_canvas!).Target(display, surface, context, 0, _width, _height, ColorSpace.ABGR8888S);
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

        return _glGenTextures != null && _glCreateProgram != null && _glGenVertexArrays != null;
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
        ((SwCanvas)_canvas!).Target(_buffer, _width, _width, _height, ColorSpace.ARGB8888);

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
