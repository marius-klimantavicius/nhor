using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Glfw;
using static Glfw.GLFW;

namespace ThorVG.Examples;

/************************************************************************/
/* Common Window Code                                                   */
/************************************************************************/

public abstract unsafe class ExampleWindow : IDisposable
{
    protected GlfwWindow? window;
    protected Canvas? canvas;
    protected uint width, height;
    protected ExampleBase example;
    protected uint stime;
    protected double mfps;

    protected bool needResize;
    protected bool needDraw;
    protected bool initialized;
    public bool ClearBuffer;
    public bool Print;

    // FPS tracking
    private Stopwatch? fpsWatch;
    private double emaDt = 1.0 / 60;
    private const double FpsHalfLife = 0.25;

    // Timing (replaces SDL GetTicks)
    private Stopwatch tickWatch = null!;

    protected ExampleWindow(ExampleBase example, uint width, uint height, uint threadsCnt)
    {
        this.example = example;
        this.width = width;
        this.height = height;

        if (!ExampleBase.Verify(Initializer.Init(threadsCnt), "Failed to init ThorVG engine!"))
            return;

        if (Glfw.Glfw.glfwInit() != GLFW_TRUE)
        {
            Console.WriteLine("Failed to init GLFW!");
            return;
        }

        tickWatch = Stopwatch.StartNew();
        stime = GetTicks();
        initialized = true;
    }

    protected uint GetTicks() => (uint)tickWatch.ElapsedMilliseconds;

    protected bool Draw()
    {
        if (canvas == null) return false;
        if (ExampleBase.Verify(canvas.Draw(ClearBuffer)))
        {
            ExampleBase.Verify(canvas.Sync());
            return true;
        }
        return false;
    }

    public bool Ready()
    {
        if (canvas == null) return false;
        if (!example.Content(canvas, width, height)) return false;
        if (!ExampleBase.Verify(canvas.Draw())) return false;
        if (!ExampleBase.Verify(canvas.Sync())) return false;
        return true;
    }

    protected void Fps(uint tickCnt, uint ctime)
    {
        if (tickCnt == 1)
        {
            Console.WriteLine($"[  Boot]: {ctime - stime}(ms)");
            fpsWatch = Stopwatch.StartNew();
            return;
        }

        if (fpsWatch == null) { fpsWatch = Stopwatch.StartNew(); return; }

        var dt = fpsWatch.Elapsed.TotalSeconds;
        fpsWatch.Restart();

        // Clamp abnormally large dt (e.g., during tab switching or pausing in debugger)
        if (dt > 0.25) dt = 0.25;

        // Continuous time-based alpha: maintains responsiveness regardless of framerate
        var alpha = 1 - Math.Exp(-Math.Log(2) * dt / FpsHalfLife);
        emaDt += alpha * (dt - emaDt);

        // Skip the unstable first 60 frames, also no need to print every frame.
        if (tickCnt > 59)
        {
            var result = 1.0 / emaDt;
            mfps += result;
            if (tickCnt % 10 == 0)
                Console.WriteLine($"[{tickCnt,5}]: {result:F2} / {mfps / (tickCnt - 59):F2} fps");
        }
    }

    public void Show()
    {
        if (window == null) return;

        Glfw.Glfw.glfwShowWindow(window);

        // Set up callbacks
        Glfw.Glfw.glfwSetKeyCallback(window, (w, key, scancode, action, mods) =>
        {
            if (action == GLFW_PRESS || action == GLFW_RELEASE)
            {
                bool pressed = action == GLFW_PRESS;
                if (key == GLFW_KEY_ESCAPE && pressed)
                {
                    Glfw.Glfw.glfwSetWindowShouldClose(w, GLFW_TRUE);
                    return;
                }
                if (key == GLFW_KEY_LEFT_SHIFT)
                {
                    example.LShift = pressed;
                    return;
                }
                if (pressed)
                    needDraw |= example.KeyDown(canvas!, key);
            }
        });

        Glfw.Glfw.glfwSetMouseButtonCallback(window, (w, button, action, mods) =>
        {
            Glfw.Glfw.glfwGetCursorPos(w, out double mx, out double my);
            if (action == GLFW_PRESS)
                needDraw |= example.ClickDown(canvas!, (int)mx, (int)my);
            else if (action == GLFW_RELEASE)
                needDraw |= example.ClickUp(canvas!, (int)mx, (int)my);
        });

        Glfw.Glfw.glfwSetCursorPosCallback(window, (w, xpos, ypos) =>
        {
            needDraw |= example.Motion(canvas!, (int)xpos, (int)ypos);
        });

        Glfw.Glfw.glfwSetFramebufferSizeCallback(window, (w, fbW, fbH) =>
        {
            if (fbW > 0 && fbH > 0)
            {
                width = (uint)fbW;
                height = (uint)fbH;
                needResize = true;
                needDraw = true;
            }
        });

        // Mainloop
        var ptime = GetTicks();
        example.Elapsed = 0;
        uint tickCnt = 0;

        while (Glfw.Glfw.glfwWindowShouldClose(window) == 0)
        {
            Glfw.Glfw.glfwPollEvents();

            if (needResize)
            {
                Resize();
                needResize = false;
            }

            if (tickCnt > 0)
            {
                needDraw |= example.Update(canvas!, example.Elapsed);
            }

            if (needDraw)
            {
                Draw();
                needDraw = false;
            }

            Refresh();

            var ctime = GetTicks();
            example.Elapsed += (ctime - ptime);
            ptime = ctime;
            ++tickCnt;

            if (Print) Fps(tickCnt, ctime);
        }
    }

    protected abstract void Resize();
    protected abstract void Refresh();

    public virtual void Dispose()
    {
        canvas = null;
        if (window != null)
        {
            Glfw.Glfw.glfwDestroyWindow(window);
            window = null;
        }
        Glfw.Glfw.glfwTerminate();
        Initializer.Term();
    }
}


/************************************************************************/
/* SwCanvas Window Code                                                 */
/************************************************************************/

public unsafe class SwWindow : ExampleWindow
{
    private uint[]? buffer;

    // Minimal GL function pointers for blitting SW buffer
    private delegate* unmanaged[Cdecl]<uint, uint*, void> _glGenTextures;
    private delegate* unmanaged[Cdecl]<uint, uint, void> _glBindTexture;
    private delegate* unmanaged[Cdecl]<uint, int, int, int, int, int, uint, uint, void*, void> _glTexImage2D;
    private delegate* unmanaged[Cdecl]<uint, int, int, int, int, int, uint, uint, void*, void> _glTexSubImage2D;
    private delegate* unmanaged[Cdecl]<uint, uint, int, void> _glTexParameteri;
    private delegate* unmanaged[Cdecl]<int, int, int, int, void> _glViewport;
    private delegate* unmanaged[Cdecl]<float, float, float, float, void> _glClearColor;
    private delegate* unmanaged[Cdecl]<uint, void> _glClear;
    private delegate* unmanaged[Cdecl]<uint, int, int, void> _glDrawArrays;

    // Shader functions
    private delegate* unmanaged[Cdecl]<uint, uint> _glCreateShader;
    private delegate* unmanaged[Cdecl]<uint, int, byte**, int*, void> _glShaderSource;
    private delegate* unmanaged[Cdecl]<uint, void> _glCompileShader;
    private delegate* unmanaged[Cdecl]<uint> _glCreateProgram;
    private delegate* unmanaged[Cdecl]<uint, uint, void> _glAttachShader;
    private delegate* unmanaged[Cdecl]<uint, void> _glLinkProgram;
    private delegate* unmanaged[Cdecl]<uint, void> _glUseProgram;
    private delegate* unmanaged[Cdecl]<uint, void> _glDeleteShader;

    // VAO/VBO
    private delegate* unmanaged[Cdecl]<int, uint*, void> _glGenVertexArrays;
    private delegate* unmanaged[Cdecl]<uint, void> _glBindVertexArray;
    private delegate* unmanaged[Cdecl]<int, uint*, void> _glGenBuffers;
    private delegate* unmanaged[Cdecl]<uint, uint, void> _glBindBuffer;
    private delegate* unmanaged[Cdecl]<uint, nint, void*, uint, void> _glBufferData;
    private delegate* unmanaged[Cdecl]<uint, int, uint, byte, int, nint, void> _glVertexAttribPointer;
    private delegate* unmanaged[Cdecl]<uint, void> _glEnableVertexAttribArray;

    // GL constants
    private const uint GL_TEXTURE_2D = 0x0DE1;
    private const uint GL_TEXTURE_MIN_FILTER = 0x2801;
    private const uint GL_TEXTURE_MAG_FILTER = 0x2800;
    private const int GL_LINEAR = 0x2601;
    private const int GL_RGBA = 0x1908;
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

    private uint texId;
    private uint vao, vbo;
    private uint shaderProgram;
    private bool glReady;

    public SwWindow(ExampleBase example, uint width, uint height, uint threadsCnt)
        : base(example, width, height, threadsCnt)
    {
        if (!initialized) return;

        Glfw.Glfw.glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 3);
        Glfw.Glfw.glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 3);
        Glfw.Glfw.glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Glfw.Glfw.glfwWindowHint(GLFW_OPENGL_FORWARD_COMPAT, GLFW_TRUE);

        Glfw.Glfw.glfwWindowHint(GLFW_VISIBLE, GLFW_FALSE);
        Glfw.Glfw.glfwWindowHint(GLFW_RESIZABLE, GLFW_TRUE);

        window = Glfw.Glfw.glfwCreateWindow((int)width, (int)height,
            "ThorVG Example (Software)", null, null);

        if (window == null)
        {
            Console.WriteLine("Failed to create GLFW window!");
            return;
        }

        Glfw.Glfw.glfwMakeContextCurrent(window);

        // Load GL functions for blitting
        if (!LoadGlFunctions())
        {
            Console.WriteLine("Failed to load GL functions for SW blitting!");
            return;
        }

        InitBlitPipeline();

        //Create a Canvas
        canvas = SwCanvas.Gen();
        if (canvas == null)
        {
            Console.WriteLine("SwCanvas is not supported. Did you enable the SwEngine?");
            return;
        }

        Resize();
    }

    private nint GetProc(string name)
    {
        return Glfw.Glfw.glfwGetProcAddress(name);
    }

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
        // Fullscreen triangle shader
        ReadOnlySpan<byte> vertSrc = "#version 330 core\nlayout(location=0) in vec2 aPos;\nlayout(location=1) in vec2 aUV;\nout vec2 vUV;\nvoid main() { gl_Position = vec4(aPos, 0.0, 1.0); vUV = aUV; }\0"u8;

        ReadOnlySpan<byte> fragSrc = "#version 330 core\nin vec2 vUV;\nout vec4 FragColor;\nuniform sampler2D uTex;\nvoid main() { FragColor = texture(uTex, vUV); }\0"u8;

        // Compile shaders
        var vs = _glCreateShader(GL_VERTEX_SHADER);
        fixed (byte* p = vertSrc) { byte* pp = p; _glShaderSource(vs, 1, &pp, null); }
        _glCompileShader(vs);

        var fs = _glCreateShader(GL_FRAGMENT_SHADER);
        fixed (byte* p = fragSrc) { byte* pp = p; _glShaderSource(fs, 1, &pp, null); }
        _glCompileShader(fs);

        shaderProgram = _glCreateProgram();
        _glAttachShader(shaderProgram, vs);
        _glAttachShader(shaderProgram, fs);
        _glLinkProgram(shaderProgram);

        _glDeleteShader(vs);
        _glDeleteShader(fs);

        // Fullscreen quad: 2 triangles, pos(xy) + uv(xy)
        Span<float> verts = stackalloc float[24]
        {
            -1f, -1f,  0f, 1f,  // bottom-left  (UV flipped Y for top-down buffer)
             1f, -1f,  1f, 1f,  // bottom-right
             1f,  1f,  1f, 0f,  // top-right
            -1f, -1f,  0f, 1f,  // bottom-left
             1f,  1f,  1f, 0f,  // top-right
            -1f,  1f,  0f, 0f,  // top-left
        };

        uint localVao, localVbo;
        _glGenVertexArrays(1, &localVao);
        vao = localVao;
        _glBindVertexArray(vao);
        _glGenBuffers(1, &localVbo);
        vbo = localVbo;
        _glBindBuffer(GL_ARRAY_BUFFER, vbo);
        fixed (float* vp = verts)
            _glBufferData(GL_ARRAY_BUFFER, (nint)(24 * sizeof(float)), vp, GL_STATIC_DRAW);
        _glVertexAttribPointer(0, 2, GL_FLOAT, 0, 4 * sizeof(float), (nint)0);
        _glEnableVertexAttribArray(0);
        _glVertexAttribPointer(1, 2, GL_FLOAT, 0, 4 * sizeof(float), (nint)(2 * sizeof(float)));
        _glEnableVertexAttribArray(1);

        // Create texture
        uint localTex;
        _glGenTextures(1, &localTex);
        texId = localTex;
        _glBindTexture(GL_TEXTURE_2D, texId);
        _glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
        _glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);

        glReady = true;
    }

    protected override void Resize()
    {
        buffer = new uint[width * height];
        ((SwCanvas)canvas!).Target(buffer, width, width, height, ColorSpace.ARGB8888);

        if (glReady)
        {
            _glBindTexture(GL_TEXTURE_2D, texId);
            _glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA8, (int)width, (int)height, 0,
                (uint)GL_BGRA, GL_UNSIGNED_BYTE, null);
        }
    }

    protected override void Refresh()
    {
        if (buffer == null || !glReady) return;

        Glfw.Glfw.glfwGetFramebufferSize(window!, out int fbW, out int fbH);
        _glViewport(0, 0, fbW, fbH);

        _glBindTexture(GL_TEXTURE_2D, texId);
        fixed (uint* ptr = buffer)
        {
            _glTexSubImage2D(GL_TEXTURE_2D, 0, 0, 0, (int)width, (int)height,
                (uint)GL_BGRA, GL_UNSIGNED_BYTE, ptr);
        }

        _glClearColor(0, 0, 0, 1);
        _glClear(GL_COLOR_BUFFER_BIT);
        _glUseProgram(shaderProgram);
        _glBindVertexArray(vao);
        _glDrawArrays(GL_TRIANGLES, 0, 6);

        Glfw.Glfw.glfwSwapBuffers(window!);
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}


/************************************************************************/
/* GlCanvas Window Code                                                 */
/************************************************************************/

public unsafe class GlWindow : ExampleWindow
{
    private nint contextId;

    public GlWindow(ExampleBase example, uint width, uint height, uint threadsCnt)
        : base(example, width, height, threadsCnt)
    {
        if (!initialized) return;

        Glfw.Glfw.glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 3);
        Glfw.Glfw.glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 3);
        Glfw.Glfw.glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Glfw.Glfw.glfwWindowHint(GLFW_OPENGL_FORWARD_COMPAT, GLFW_TRUE);

        Glfw.Glfw.glfwWindowHint(GLFW_VISIBLE, GLFW_FALSE);
        Glfw.Glfw.glfwWindowHint(GLFW_RESIZABLE, GLFW_TRUE);

        window = Glfw.Glfw.glfwCreateWindow((int)width, (int)height,
            "ThorVG Example (OpenGL)", null, null);

        if (window == null)
        {
            Console.WriteLine("Failed to create GLFW window!");
            return;
        }

        Glfw.Glfw.glfwMakeContextCurrent(window);

        //Create a Canvas
        canvas = GlCanvas.Gen();
        if (canvas == null)
        {
            Console.WriteLine("GlCanvas is not supported. Did you enable the GlEngine?");
            return;
        }

        contextId = 1;
        Resize();
    }

    protected override void Resize()
    {
        ((GlCanvas)canvas!).Target(nint.Zero, nint.Zero, contextId, 0,
            width, height, ColorSpace.ABGR8888S);
    }

    protected override void Refresh()
    {
        if (window != null)
            Glfw.Glfw.glfwSwapBuffers(window);
    }

    public override void Dispose()
    {
        canvas = null;
        base.Dispose();
    }
}
