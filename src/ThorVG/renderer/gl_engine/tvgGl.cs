// Ported from ThorVG/src/renderer/gl_engine/tvgGl.h and tvgGl.cpp
// OpenGL function loader using NativeLibrary. Defines GL constants
// and runtime function loading via unmanaged function pointers.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ThorVG
{
    /// <summary>
    /// Static class that loads OpenGL functions at runtime using
    /// NativeLibrary.Load / TryGetExport, mirroring the C++ tvgGl.h/cpp
    /// function-pointer loading approach. Uses unmanaged function pointers
    /// (delegate* unmanaged[Cdecl]) for efficient native interop.
    /// </summary>
    public static unsafe class GL
    {
        // ============================================================
        //  Required GL version
        // ============================================================
        public const int TVG_REQUIRE_GL_MAJOR_VER = 3;
        public const int TVG_REQUIRE_GL_MINOR_VER = 3;

        // ============================================================
        //  GL Constants (from tvgGl.h)
        // ============================================================
        #region GL_VERSION_1_0
        public const uint GL_DEPTH_BUFFER_BIT               = 0x00000100;
        public const uint GL_STENCIL_BUFFER_BIT             = 0x00000400;
        public const uint GL_COLOR_BUFFER_BIT               = 0x00004000;
        public const byte GL_FALSE                          = 0;
        public const byte GL_TRUE                           = 1;
        public const uint GL_POINTS                         = 0x0000;
        public const uint GL_LINES                          = 0x0001;
        public const uint GL_LINE_LOOP                      = 0x0002;
        public const uint GL_LINE_STRIP                     = 0x0003;
        public const uint GL_TRIANGLES                      = 0x0004;
        public const uint GL_TRIANGLE_STRIP                 = 0x0005;
        public const uint GL_TRIANGLE_FAN                   = 0x0006;
        public const uint GL_NEVER                          = 0x0200;
        public const uint GL_LESS                           = 0x0201;
        public const uint GL_EQUAL                          = 0x0202;
        public const uint GL_LEQUAL                         = 0x0203;
        public const uint GL_GREATER                        = 0x0204;
        public const uint GL_NOTEQUAL                       = 0x0205;
        public const uint GL_GEQUAL                         = 0x0206;
        public const uint GL_ALWAYS                         = 0x0207;
        public const uint GL_ZERO                           = 0;
        public const uint GL_ONE                            = 1;
        public const uint GL_SRC_COLOR                      = 0x0300;
        public const uint GL_ONE_MINUS_SRC_COLOR            = 0x0301;
        public const uint GL_SRC_ALPHA                      = 0x0302;
        public const uint GL_ONE_MINUS_SRC_ALPHA            = 0x0303;
        public const uint GL_DST_ALPHA                      = 0x0304;
        public const uint GL_ONE_MINUS_DST_ALPHA            = 0x0305;
        public const uint GL_DST_COLOR                      = 0x0306;
        public const uint GL_ONE_MINUS_DST_COLOR            = 0x0307;
        public const uint GL_NONE                           = 0;
        public const uint GL_FRONT                          = 0x0404;
        public const uint GL_BACK                           = 0x0405;
        public const uint GL_FRONT_AND_BACK                 = 0x0408;
        public const uint GL_NO_ERROR                       = 0;
        public const uint GL_INVALID_ENUM                   = 0x0500;
        public const uint GL_INVALID_VALUE                  = 0x0501;
        public const uint GL_INVALID_OPERATION              = 0x0502;
        public const uint GL_OUT_OF_MEMORY                  = 0x0505;
        public const uint GL_CW                             = 0x0900;
        public const uint GL_CCW                            = 0x0901;
        public const uint GL_CULL_FACE                      = 0x0B44;
        public const uint GL_DEPTH_TEST                     = 0x0B71;
        public const uint GL_STENCIL_TEST                   = 0x0B90;
        public const uint GL_DITHER                         = 0x0BD0;
        public const uint GL_BLEND                          = 0x0BE2;
        public const uint GL_SCISSOR_TEST                   = 0x0C11;
        public const uint GL_UNPACK_ALIGNMENT               = 0x0CF5;
        public const uint GL_TEXTURE_2D                     = 0x0DE1;
        public const uint GL_DONT_CARE                      = 0x1100;
        public const uint GL_BYTE                           = 0x1400;
        public const uint GL_UNSIGNED_BYTE                  = 0x1401;
        public const uint GL_SHORT                          = 0x1402;
        public const uint GL_UNSIGNED_SHORT                 = 0x1403;
        public const uint GL_INT                            = 0x1404;
        public const uint GL_UNSIGNED_INT                   = 0x1405;
        public const uint GL_FLOAT                          = 0x1406;
        public const uint GL_INVERT                         = 0x150A;
        public const uint GL_TEXTURE                        = 0x1702;
        public const uint GL_COLOR                          = 0x1800;
        public const uint GL_DEPTH                          = 0x1801;
        public const uint GL_STENCIL                        = 0x1802;
        public const uint GL_STENCIL_INDEX                  = 0x1901;
        public const uint GL_DEPTH_COMPONENT                = 0x1902;
        public const uint GL_RED                            = 0x1903;
        public const uint GL_GREEN                          = 0x1904;
        public const uint GL_BLUE                           = 0x1905;
        public const uint GL_ALPHA                          = 0x1906;
        public const uint GL_RGB                            = 0x1907;
        public const uint GL_RGBA                           = 0x1908;
        public const uint GL_KEEP                           = 0x1E00;
        public const uint GL_REPLACE                        = 0x1E01;
        public const uint GL_INCR                           = 0x1E02;
        public const uint GL_DECR                           = 0x1E03;
        public const uint GL_VENDOR                         = 0x1F00;
        public const uint GL_RENDERER                       = 0x1F01;
        public const uint GL_VERSION                        = 0x1F02;
        public const uint GL_EXTENSIONS                     = 0x1F03;
        public const uint GL_NEAREST                        = 0x2600;
        public const uint GL_LINEAR                         = 0x2601;
        public const uint GL_NEAREST_MIPMAP_NEAREST         = 0x2700;
        public const uint GL_LINEAR_MIPMAP_NEAREST          = 0x2701;
        public const uint GL_NEAREST_MIPMAP_LINEAR          = 0x2702;
        public const uint GL_LINEAR_MIPMAP_LINEAR           = 0x2703;
        public const uint GL_TEXTURE_MAG_FILTER             = 0x2800;
        public const uint GL_TEXTURE_MIN_FILTER             = 0x2801;
        public const uint GL_TEXTURE_WRAP_S                 = 0x2802;
        public const uint GL_TEXTURE_WRAP_T                 = 0x2803;
        public const uint GL_REPEAT                         = 0x2901;
        #endregion

        #region GL_VERSION_1_1
        public const uint GL_POLYGON_OFFSET_FILL            = 0x8037;
        public const uint GL_RGB8                           = 0x8051;
        public const uint GL_RGBA8                          = 0x8058;
        public const uint GL_VERTEX_ARRAY                   = 0x8074;
        #endregion

        #region GL_VERSION_1_2
        public const uint GL_CLAMP_TO_EDGE                  = 0x812F;
        public const uint GL_TEXTURE_WRAP_R                 = 0x8072;
        #endregion

        #region GL_VERSION_1_3
        public const uint GL_TEXTURE0                       = 0x84C0;
        public const uint GL_TEXTURE1                       = 0x84C1;
        public const uint GL_TEXTURE2                       = 0x84C2;
        public const uint GL_TEXTURE3                       = 0x84C3;
        public const uint GL_ACTIVE_TEXTURE                 = 0x84E0;
        public const uint GL_MULTISAMPLE                    = 0x809D;
        #endregion

        #region GL_VERSION_1_4
        public const uint GL_DEPTH_COMPONENT16              = 0x81A5;
        public const uint GL_DEPTH_COMPONENT24              = 0x81A6;
        public const uint GL_INCR_WRAP                      = 0x8507;
        public const uint GL_DECR_WRAP                      = 0x8508;
        public const uint GL_FUNC_ADD                       = 0x8006;
        public const uint GL_FUNC_REVERSE_SUBTRACT          = 0x800B;
        public const uint GL_FUNC_SUBTRACT                  = 0x800A;
        public const uint GL_MIN                            = 0x8007;
        public const uint GL_MAX                            = 0x8008;
        #endregion

        #region GL_VERSION_1_5
        public const uint GL_ARRAY_BUFFER_BINDING           = 0x8894;
        public const uint GL_ARRAY_BUFFER                   = 0x8892;
        public const uint GL_ELEMENT_ARRAY_BUFFER           = 0x8893;
        public const uint GL_STREAM_DRAW                    = 0x88E0;
        public const uint GL_STATIC_DRAW                    = 0x88E4;
        public const uint GL_DYNAMIC_DRAW                   = 0x88E8;
        #endregion

        #region GL_VERSION_2_0
        public const uint GL_FRAGMENT_SHADER                = 0x8B30;
        public const uint GL_VERTEX_SHADER                  = 0x8B31;
        public const uint GL_MAX_VERTEX_ATTRIBS             = 0x8869;
        public const uint GL_MAX_TEXTURE_IMAGE_UNITS        = 0x8872;
        public const uint GL_COMPILE_STATUS                 = 0x8B81;
        public const uint GL_LINK_STATUS                    = 0x8B82;
        public const uint GL_VALIDATE_STATUS                = 0x8B83;
        public const uint GL_INFO_LOG_LENGTH                = 0x8B84;
        public const uint GL_ACTIVE_UNIFORMS                = 0x8B86;
        public const uint GL_SHADING_LANGUAGE_VERSION       = 0x8B8C;
        public const uint GL_CURRENT_PROGRAM                = 0x8B8D;
        #endregion

        #region GL_VERSION_3_0
        public const uint GL_MAJOR_VERSION                  = 0x821B;
        public const uint GL_MINOR_VERSION                  = 0x821C;
        public const uint GL_NUM_EXTENSIONS                 = 0x821D;
        public const uint GL_RGBA32F                        = 0x8814;
        public const uint GL_RGBA16F                        = 0x881A;
        public const uint GL_R8                             = 0x8229;
        public const uint GL_RG8                            = 0x822B;
        public const uint GL_HALF_FLOAT                     = 0x140B;
        public const uint GL_VERTEX_ARRAY_BINDING           = 0x85B5;
        public const uint GL_INVALID_FRAMEBUFFER_OPERATION  = 0x0506;
        public const uint GL_DEPTH_STENCIL_ATTACHMENT       = 0x821A;
        public const uint GL_DEPTH_STENCIL                  = 0x84F9;
        public const uint GL_UNSIGNED_INT_24_8              = 0x84FA;
        public const uint GL_DEPTH24_STENCIL8               = 0x88F0;
        public const uint GL_FRAMEBUFFER_BINDING            = 0x8CA6;
        public const uint GL_DRAW_FRAMEBUFFER_BINDING       = 0x8CA6;
        public const uint GL_READ_FRAMEBUFFER               = 0x8CA8;
        public const uint GL_DRAW_FRAMEBUFFER               = 0x8CA9;
        public const uint GL_FRAMEBUFFER_COMPLETE           = 0x8CD5;
        public const uint GL_MAX_COLOR_ATTACHMENTS          = 0x8CDF;
        public const uint GL_COLOR_ATTACHMENT0              = 0x8CE0;
        public const uint GL_COLOR_ATTACHMENT1              = 0x8CE1;
        public const uint GL_DEPTH_ATTACHMENT               = 0x8D00;
        public const uint GL_STENCIL_ATTACHMENT             = 0x8D20;
        public const uint GL_FRAMEBUFFER                    = 0x8D40;
        public const uint GL_RENDERBUFFER                   = 0x8D41;
        public const uint GL_STENCIL_INDEX8                 = 0x8D48;
        public const uint GL_MAX_SAMPLES                    = 0x8D57;
        #endregion

        #region GL_VERSION_3_1
        public const uint GL_UNIFORM_BUFFER                 = 0x8A11;
        public const uint GL_UNIFORM_BUFFER_BINDING         = 0x8A28;
        public const uint GL_UNIFORM_BUFFER_OFFSET_ALIGNMENT = 0x8A34;
        public const uint GL_INVALID_INDEX                  = 0xFFFFFFFF;
        #endregion

        // ============================================================
        //  Function pointers (unmanaged, like the C++ extern globals)
        // ============================================================
        #region Function Pointers
        // GL_VERSION_1_0
        public static delegate* unmanaged[Cdecl]<uint, void> glCullFace;
        public static delegate* unmanaged[Cdecl]<uint, void> glFrontFace;
        public static delegate* unmanaged[Cdecl]<int, int, int, int, void> glScissor;
        public static delegate* unmanaged[Cdecl]<uint, uint, int, void> glTexParameteri;
        public static delegate* unmanaged[Cdecl]<uint, int, int, int, int, int, uint, uint, void*, void> glTexImage2D;
        public static delegate* unmanaged[Cdecl]<uint, void> glDrawBuffer;
        public static delegate* unmanaged[Cdecl]<uint, void> glClear;
        public static delegate* unmanaged[Cdecl]<float, float, float, float, void> glClearColor;
        public static delegate* unmanaged[Cdecl]<int, void> glClearStencil;
        public static delegate* unmanaged[Cdecl]<double, void> glClearDepth;
        public static delegate* unmanaged[Cdecl]<byte, byte, byte, byte, void> glColorMask;
        public static delegate* unmanaged[Cdecl]<byte, void> glDepthMask;
        public static delegate* unmanaged[Cdecl]<uint, void> glDisable;
        public static delegate* unmanaged[Cdecl]<uint, void> glEnable;
        public static delegate* unmanaged[Cdecl]<uint, uint, void> glBlendFunc;
        public static delegate* unmanaged[Cdecl]<uint, int, uint, void> glStencilFunc;
        public static delegate* unmanaged[Cdecl]<uint, uint, uint, void> glStencilOp;
        public static delegate* unmanaged[Cdecl]<uint, void> glDepthFunc;
        public static delegate* unmanaged[Cdecl]<uint> glGetError;
        public static delegate* unmanaged[Cdecl]<uint, int*, void> glGetIntegerv;
        public static delegate* unmanaged[Cdecl]<uint, byte*> glGetString;
        public static delegate* unmanaged[Cdecl]<int, int, int, int, void> glViewport;

        // GL_VERSION_1_1
        public static delegate* unmanaged[Cdecl]<uint, int, uint, void*, void> glDrawElements;
        public static delegate* unmanaged[Cdecl]<uint, uint, void> glBindTexture;
        public static delegate* unmanaged[Cdecl]<int, uint*, void> glDeleteTextures;
        public static delegate* unmanaged[Cdecl]<int, uint*, void> glGenTextures;

        // GL_VERSION_1_3
        public static delegate* unmanaged[Cdecl]<uint, void> glActiveTexture;

        // GL_VERSION_1_4
        public static delegate* unmanaged[Cdecl]<uint, void> glBlendEquation;

        // GL_VERSION_1_5
        public static delegate* unmanaged[Cdecl]<uint, uint, void> glBindBuffer;
        public static delegate* unmanaged[Cdecl]<int, uint*, void> glDeleteBuffers;
        public static delegate* unmanaged[Cdecl]<int, uint*, void> glGenBuffers;
        public static delegate* unmanaged[Cdecl]<uint, nint, void*, uint, void> glBufferData;

        // GL_VERSION_2_0
        public static delegate* unmanaged[Cdecl]<int, uint*, void> glDrawBuffers;
        public static delegate* unmanaged[Cdecl]<uint, uint, uint, uint, void> glStencilOpSeparate;
        public static delegate* unmanaged[Cdecl]<uint, uint, int, uint, void> glStencilFuncSeparate;
        public static delegate* unmanaged[Cdecl]<uint, uint, void> glAttachShader;
        public static delegate* unmanaged[Cdecl]<uint, void> glCompileShader;
        public static delegate* unmanaged[Cdecl]<uint> glCreateProgram;
        public static delegate* unmanaged[Cdecl]<uint, uint> glCreateShader;
        public static delegate* unmanaged[Cdecl]<uint, void> glDeleteProgram;
        public static delegate* unmanaged[Cdecl]<uint, void> glDeleteShader;
        public static delegate* unmanaged[Cdecl]<uint, void> glDisableVertexAttribArray;
        public static delegate* unmanaged[Cdecl]<uint, void> glEnableVertexAttribArray;
        public static delegate* unmanaged[Cdecl]<uint, byte*, int> glGetAttribLocation;
        public static delegate* unmanaged[Cdecl]<uint, uint, int*, void> glGetProgramiv;
        public static delegate* unmanaged[Cdecl]<uint, int, int*, byte*, void> glGetProgramInfoLog;
        public static delegate* unmanaged[Cdecl]<uint, uint, int*, void> glGetShaderiv;
        public static delegate* unmanaged[Cdecl]<uint, int, int*, byte*, void> glGetShaderInfoLog;
        public static delegate* unmanaged[Cdecl]<uint, byte*, int> glGetUniformLocation;
        public static delegate* unmanaged[Cdecl]<uint, void> glLinkProgram;
        public static delegate* unmanaged[Cdecl]<uint, int, byte**, int*, void> glShaderSource;
        public static delegate* unmanaged[Cdecl]<uint, void> glUseProgram;
        public static delegate* unmanaged[Cdecl]<int, float, void> glUniform1f;
        public static delegate* unmanaged[Cdecl]<int, int, float*, void> glUniform1fv;
        public static delegate* unmanaged[Cdecl]<int, int, float*, void> glUniform2fv;
        public static delegate* unmanaged[Cdecl]<int, int, float*, void> glUniform3fv;
        public static delegate* unmanaged[Cdecl]<int, int, float*, void> glUniform4fv;
        public static delegate* unmanaged[Cdecl]<int, int, int*, void> glUniform1iv;
        public static delegate* unmanaged[Cdecl]<int, int, int*, void> glUniform2iv;
        public static delegate* unmanaged[Cdecl]<int, int, int*, void> glUniform3iv;
        public static delegate* unmanaged[Cdecl]<int, int, int*, void> glUniform4iv;
        public static delegate* unmanaged[Cdecl]<int, int, byte, float*, void> glUniformMatrix3fv;
        public static delegate* unmanaged[Cdecl]<uint, int, uint, byte, int, void*, void> glVertexAttribPointer;
        public static delegate* unmanaged[Cdecl]<uint, float, float, float, float, void> glVertexAttrib4f;

        // GL_VERSION_3_0
        public static delegate* unmanaged[Cdecl]<uint, uint, uint, nint, nint, void> glBindBufferRange;
        public static delegate* unmanaged[Cdecl]<uint, uint, void> glBindRenderbuffer;
        public static delegate* unmanaged[Cdecl]<int, uint*, void> glDeleteRenderbuffers;
        public static delegate* unmanaged[Cdecl]<int, uint*, void> glGenRenderbuffers;
        public static delegate* unmanaged[Cdecl]<uint, int, uint*, void> glInvalidateFramebuffer;
        public static delegate* unmanaged[Cdecl]<uint, uint, void> glBindFramebuffer;
        public static delegate* unmanaged[Cdecl]<int, uint*, void> glDeleteFramebuffers;
        public static delegate* unmanaged[Cdecl]<int, uint*, void> glGenFramebuffers;
        public static delegate* unmanaged[Cdecl]<uint, uint, uint, uint, int, void> glFramebufferTexture2D;
        public static delegate* unmanaged[Cdecl]<uint, uint, uint, uint, void> glFramebufferRenderbuffer;
        public static delegate* unmanaged[Cdecl]<int, int, int, int, int, int, int, int, uint, uint, void> glBlitFramebuffer;
        public static delegate* unmanaged[Cdecl]<uint, int, uint, int, int, void> glRenderbufferStorageMultisample;
        public static delegate* unmanaged[Cdecl]<uint, void> glBindVertexArray;
        public static delegate* unmanaged[Cdecl]<int, uint*, void> glDeleteVertexArrays;
        public static delegate* unmanaged[Cdecl]<int, uint*, void> glGenVertexArrays;

        // GL_VERSION_3_1
        public static delegate* unmanaged[Cdecl]<uint, byte*, uint> glGetUniformBlockIndex;
        public static delegate* unmanaged[Cdecl]<uint, uint, uint, void> glUniformBlockBinding;
        #endregion

        // ============================================================
        //  GL_CHECK debug helper (mirrors C++ GL_CHECK macro)
        // ============================================================
        [Conditional("DEBUG")]
        public static void GL_CHECK()
        {
            var err = glGetError();
            Debug.Assert(err == GL_NO_ERROR, $"GL error: 0x{err:X}");
        }

        // ============================================================
        //  Library handle
        // ============================================================
        private static nint _libGL;
        private static bool _loaded;

        // ============================================================
        //  LoadFunction helper — returns raw pointer as nint
        // ============================================================
        private static nint LoadFunction(string name)
        {
            // Try the lib first
            if (NativeLibrary.TryGetExport(_libGL, name, out var ptr))
            {
                return ptr;
            }

            // On Linux/GL, try glXGetProcAddress via the lib itself
            if (NativeLibrary.TryGetExport(_libGL, "glXGetProcAddress", out var glxGetProc))
            {
                var getProc = Marshal.GetDelegateForFunctionPointer<GlXGetProcAddressDelegate>(glxGetProc);
                var fnPtr = getProc(name);
                if (fnPtr != nint.Zero)
                {
                    return fnPtr;
                }
            }

            // Try glXGetProcAddressARB
            if (NativeLibrary.TryGetExport(_libGL, "glXGetProcAddressARB", out var glxGetProcArb))
            {
                var getProc = Marshal.GetDelegateForFunctionPointer<GlXGetProcAddressDelegate>(glxGetProcArb);
                var fnPtr = getProc(name);
                if (fnPtr != nint.Zero)
                {
                    return fnPtr;
                }
            }

            // On Windows, try wglGetProcAddress
            if (NativeLibrary.TryGetExport(_libGL, "wglGetProcAddress", out var wglGetProc))
            {
                var getProc = Marshal.GetDelegateForFunctionPointer<WglGetProcAddressDelegate>(wglGetProc);
                var fnPtr = getProc(name);
                if (fnPtr != nint.Zero)
                {
                    return fnPtr;
                }
            }

            return nint.Zero;
        }

        /// <summary>
        /// Loads a GL function by name, logs an error and returns false if not found.
        /// </summary>
        private static bool RequireFunction(string name, out nint ptr)
        {
            ptr = LoadFunction(name);
            if (ptr != nint.Zero) return true;

            TvgCommon.TVGERR("GL_ENGINE", "{0} is not supported.", name);
            return false;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate nint GlXGetProcAddressDelegate([MarshalAs(UnmanagedType.LPStr)] string procName);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate nint WglGetProcAddressDelegate([MarshalAs(UnmanagedType.LPStr)] string procName);

        // ============================================================
        //  glInit — loads the GL library and all function pointers
        // ============================================================
        public static bool glInit()
        {
            if (_loaded) return true;

            // Platform-specific library loading
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!NativeLibrary.TryLoad("opengl32.dll", out _libGL))
                {
                    TvgCommon.TVGERR("GL_ENGINE", "Cannot find the gl library.");
                    return false;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (!NativeLibrary.TryLoad("/Library/Frameworks/OpenGL.framework/OpenGL", out _libGL) &&
                    !NativeLibrary.TryLoad("/System/Library/Frameworks/OpenGL.framework/OpenGL", out _libGL))
                {
                    TvgCommon.TVGERR("GL_ENGINE", "Cannot find the gl library.");
                    return false;
                }
            }
            else // Linux
            {
                if (!NativeLibrary.TryLoad("libGL.so", out _libGL) &&
                    !NativeLibrary.TryLoad("libGL.so.4", out _libGL) &&
                    !NativeLibrary.TryLoad("libGL.so.3", out _libGL) &&
                    !NativeLibrary.TryLoad("libGL.so.1", out _libGL))
                {
                    TvgCommon.TVGERR("GL_ENGINE", "Cannot find the gl library.");
                    return false;
                }
            }

            nint p;

            // GL_VERSION_1_0
            if (!RequireFunction("glCullFace", out p)) return false;
            glCullFace = (delegate* unmanaged[Cdecl]<uint, void>)p;
            if (!RequireFunction("glFrontFace", out p)) return false;
            glFrontFace = (delegate* unmanaged[Cdecl]<uint, void>)p;
            if (!RequireFunction("glScissor", out p)) return false;
            glScissor = (delegate* unmanaged[Cdecl]<int, int, int, int, void>)p;
            if (!RequireFunction("glTexParameteri", out p)) return false;
            glTexParameteri = (delegate* unmanaged[Cdecl]<uint, uint, int, void>)p;
            if (!RequireFunction("glTexImage2D", out p)) return false;
            glTexImage2D = (delegate* unmanaged[Cdecl]<uint, int, int, int, int, int, uint, uint, void*, void>)p;
            // glDrawBuffer may not exist on GLES; load optionally
            p = LoadFunction("glDrawBuffer");
            glDrawBuffer = (delegate* unmanaged[Cdecl]<uint, void>)p;
            if (!RequireFunction("glClear", out p)) return false;
            glClear = (delegate* unmanaged[Cdecl]<uint, void>)p;
            if (!RequireFunction("glClearColor", out p)) return false;
            glClearColor = (delegate* unmanaged[Cdecl]<float, float, float, float, void>)p;
            if (!RequireFunction("glClearStencil", out p)) return false;
            glClearStencil = (delegate* unmanaged[Cdecl]<int, void>)p;
            if (!RequireFunction("glClearDepth", out p)) return false;
            glClearDepth = (delegate* unmanaged[Cdecl]<double, void>)p;
            if (!RequireFunction("glColorMask", out p)) return false;
            glColorMask = (delegate* unmanaged[Cdecl]<byte, byte, byte, byte, void>)p;
            if (!RequireFunction("glDepthMask", out p)) return false;
            glDepthMask = (delegate* unmanaged[Cdecl]<byte, void>)p;
            if (!RequireFunction("glDisable", out p)) return false;
            glDisable = (delegate* unmanaged[Cdecl]<uint, void>)p;
            if (!RequireFunction("glEnable", out p)) return false;
            glEnable = (delegate* unmanaged[Cdecl]<uint, void>)p;
            if (!RequireFunction("glBlendFunc", out p)) return false;
            glBlendFunc = (delegate* unmanaged[Cdecl]<uint, uint, void>)p;
            if (!RequireFunction("glStencilFunc", out p)) return false;
            glStencilFunc = (delegate* unmanaged[Cdecl]<uint, int, uint, void>)p;
            if (!RequireFunction("glStencilOp", out p)) return false;
            glStencilOp = (delegate* unmanaged[Cdecl]<uint, uint, uint, void>)p;
            if (!RequireFunction("glDepthFunc", out p)) return false;
            glDepthFunc = (delegate* unmanaged[Cdecl]<uint, void>)p;
            if (!RequireFunction("glGetError", out p)) return false;
            glGetError = (delegate* unmanaged[Cdecl]<uint>)p;
            if (!RequireFunction("glGetIntegerv", out p)) return false;
            glGetIntegerv = (delegate* unmanaged[Cdecl]<uint, int*, void>)p;
            if (!RequireFunction("glGetString", out p)) return false;
            glGetString = (delegate* unmanaged[Cdecl]<uint, byte*>)p;
            if (!RequireFunction("glViewport", out p)) return false;
            glViewport = (delegate* unmanaged[Cdecl]<int, int, int, int, void>)p;

            // GL_VERSION_1_1
            if (!RequireFunction("glDrawElements", out p)) return false;
            glDrawElements = (delegate* unmanaged[Cdecl]<uint, int, uint, void*, void>)p;
            if (!RequireFunction("glBindTexture", out p)) return false;
            glBindTexture = (delegate* unmanaged[Cdecl]<uint, uint, void>)p;
            if (!RequireFunction("glDeleteTextures", out p)) return false;
            glDeleteTextures = (delegate* unmanaged[Cdecl]<int, uint*, void>)p;
            if (!RequireFunction("glGenTextures", out p)) return false;
            glGenTextures = (delegate* unmanaged[Cdecl]<int, uint*, void>)p;

            // GL_VERSION_1_3
            if (!RequireFunction("glActiveTexture", out p)) return false;
            glActiveTexture = (delegate* unmanaged[Cdecl]<uint, void>)p;

            // GL_VERSION_1_4
            if (!RequireFunction("glBlendEquation", out p)) return false;
            glBlendEquation = (delegate* unmanaged[Cdecl]<uint, void>)p;

            // GL_VERSION_1_5
            if (!RequireFunction("glBindBuffer", out p)) return false;
            glBindBuffer = (delegate* unmanaged[Cdecl]<uint, uint, void>)p;
            if (!RequireFunction("glDeleteBuffers", out p)) return false;
            glDeleteBuffers = (delegate* unmanaged[Cdecl]<int, uint*, void>)p;
            if (!RequireFunction("glGenBuffers", out p)) return false;
            glGenBuffers = (delegate* unmanaged[Cdecl]<int, uint*, void>)p;
            if (!RequireFunction("glBufferData", out p)) return false;
            glBufferData = (delegate* unmanaged[Cdecl]<uint, nint, void*, uint, void>)p;

            // GL_VERSION_2_0
            p = LoadFunction("glDrawBuffers"); // optional
            glDrawBuffers = (delegate* unmanaged[Cdecl]<int, uint*, void>)p;
            if (!RequireFunction("glStencilOpSeparate", out p)) return false;
            glStencilOpSeparate = (delegate* unmanaged[Cdecl]<uint, uint, uint, uint, void>)p;
            if (!RequireFunction("glStencilFuncSeparate", out p)) return false;
            glStencilFuncSeparate = (delegate* unmanaged[Cdecl]<uint, uint, int, uint, void>)p;
            if (!RequireFunction("glAttachShader", out p)) return false;
            glAttachShader = (delegate* unmanaged[Cdecl]<uint, uint, void>)p;
            if (!RequireFunction("glCompileShader", out p)) return false;
            glCompileShader = (delegate* unmanaged[Cdecl]<uint, void>)p;
            if (!RequireFunction("glCreateProgram", out p)) return false;
            glCreateProgram = (delegate* unmanaged[Cdecl]<uint>)p;
            if (!RequireFunction("glCreateShader", out p)) return false;
            glCreateShader = (delegate* unmanaged[Cdecl]<uint, uint>)p;
            if (!RequireFunction("glDeleteProgram", out p)) return false;
            glDeleteProgram = (delegate* unmanaged[Cdecl]<uint, void>)p;
            if (!RequireFunction("glDeleteShader", out p)) return false;
            glDeleteShader = (delegate* unmanaged[Cdecl]<uint, void>)p;
            if (!RequireFunction("glDisableVertexAttribArray", out p)) return false;
            glDisableVertexAttribArray = (delegate* unmanaged[Cdecl]<uint, void>)p;
            if (!RequireFunction("glEnableVertexAttribArray", out p)) return false;
            glEnableVertexAttribArray = (delegate* unmanaged[Cdecl]<uint, void>)p;
            if (!RequireFunction("glGetAttribLocation", out p)) return false;
            glGetAttribLocation = (delegate* unmanaged[Cdecl]<uint, byte*, int>)p;
            if (!RequireFunction("glGetProgramiv", out p)) return false;
            glGetProgramiv = (delegate* unmanaged[Cdecl]<uint, uint, int*, void>)p;
            if (!RequireFunction("glGetProgramInfoLog", out p)) return false;
            glGetProgramInfoLog = (delegate* unmanaged[Cdecl]<uint, int, int*, byte*, void>)p;
            if (!RequireFunction("glGetShaderiv", out p)) return false;
            glGetShaderiv = (delegate* unmanaged[Cdecl]<uint, uint, int*, void>)p;
            if (!RequireFunction("glGetShaderInfoLog", out p)) return false;
            glGetShaderInfoLog = (delegate* unmanaged[Cdecl]<uint, int, int*, byte*, void>)p;
            if (!RequireFunction("glGetUniformLocation", out p)) return false;
            glGetUniformLocation = (delegate* unmanaged[Cdecl]<uint, byte*, int>)p;
            if (!RequireFunction("glLinkProgram", out p)) return false;
            glLinkProgram = (delegate* unmanaged[Cdecl]<uint, void>)p;
            if (!RequireFunction("glShaderSource", out p)) return false;
            glShaderSource = (delegate* unmanaged[Cdecl]<uint, int, byte**, int*, void>)p;
            if (!RequireFunction("glUseProgram", out p)) return false;
            glUseProgram = (delegate* unmanaged[Cdecl]<uint, void>)p;
            if (!RequireFunction("glUniform1f", out p)) return false;
            glUniform1f = (delegate* unmanaged[Cdecl]<int, float, void>)p;
            if (!RequireFunction("glUniform1fv", out p)) return false;
            glUniform1fv = (delegate* unmanaged[Cdecl]<int, int, float*, void>)p;
            if (!RequireFunction("glUniform2fv", out p)) return false;
            glUniform2fv = (delegate* unmanaged[Cdecl]<int, int, float*, void>)p;
            if (!RequireFunction("glUniform3fv", out p)) return false;
            glUniform3fv = (delegate* unmanaged[Cdecl]<int, int, float*, void>)p;
            if (!RequireFunction("glUniform4fv", out p)) return false;
            glUniform4fv = (delegate* unmanaged[Cdecl]<int, int, float*, void>)p;
            if (!RequireFunction("glUniform1iv", out p)) return false;
            glUniform1iv = (delegate* unmanaged[Cdecl]<int, int, int*, void>)p;
            if (!RequireFunction("glUniform2iv", out p)) return false;
            glUniform2iv = (delegate* unmanaged[Cdecl]<int, int, int*, void>)p;
            if (!RequireFunction("glUniform3iv", out p)) return false;
            glUniform3iv = (delegate* unmanaged[Cdecl]<int, int, int*, void>)p;
            if (!RequireFunction("glUniform4iv", out p)) return false;
            glUniform4iv = (delegate* unmanaged[Cdecl]<int, int, int*, void>)p;
            if (!RequireFunction("glUniformMatrix3fv", out p)) return false;
            glUniformMatrix3fv = (delegate* unmanaged[Cdecl]<int, int, byte, float*, void>)p;
            if (!RequireFunction("glVertexAttribPointer", out p)) return false;
            glVertexAttribPointer = (delegate* unmanaged[Cdecl]<uint, int, uint, byte, int, void*, void>)p;
            if (!RequireFunction("glVertexAttrib4f", out p)) return false;
            glVertexAttrib4f = (delegate* unmanaged[Cdecl]<uint, float, float, float, float, void>)p;

            // GL_VERSION_3_0
            if (!RequireFunction("glBindBufferRange", out p)) return false;
            glBindBufferRange = (delegate* unmanaged[Cdecl]<uint, uint, uint, nint, nint, void>)p;
            if (!RequireFunction("glBindRenderbuffer", out p)) return false;
            glBindRenderbuffer = (delegate* unmanaged[Cdecl]<uint, uint, void>)p;
            if (!RequireFunction("glDeleteRenderbuffers", out p)) return false;
            glDeleteRenderbuffers = (delegate* unmanaged[Cdecl]<int, uint*, void>)p;
            if (!RequireFunction("glGenRenderbuffers", out p)) return false;
            glGenRenderbuffers = (delegate* unmanaged[Cdecl]<int, uint*, void>)p;
            p = LoadFunction("glInvalidateFramebuffer"); // optional on desktop GL
            glInvalidateFramebuffer = (delegate* unmanaged[Cdecl]<uint, int, uint*, void>)p;
            if (!RequireFunction("glBindFramebuffer", out p)) return false;
            glBindFramebuffer = (delegate* unmanaged[Cdecl]<uint, uint, void>)p;
            if (!RequireFunction("glDeleteFramebuffers", out p)) return false;
            glDeleteFramebuffers = (delegate* unmanaged[Cdecl]<int, uint*, void>)p;
            if (!RequireFunction("glGenFramebuffers", out p)) return false;
            glGenFramebuffers = (delegate* unmanaged[Cdecl]<int, uint*, void>)p;
            if (!RequireFunction("glFramebufferTexture2D", out p)) return false;
            glFramebufferTexture2D = (delegate* unmanaged[Cdecl]<uint, uint, uint, uint, int, void>)p;
            if (!RequireFunction("glFramebufferRenderbuffer", out p)) return false;
            glFramebufferRenderbuffer = (delegate* unmanaged[Cdecl]<uint, uint, uint, uint, void>)p;
            if (!RequireFunction("glBlitFramebuffer", out p)) return false;
            glBlitFramebuffer = (delegate* unmanaged[Cdecl]<int, int, int, int, int, int, int, int, uint, uint, void>)p;
            if (!RequireFunction("glRenderbufferStorageMultisample", out p)) return false;
            glRenderbufferStorageMultisample = (delegate* unmanaged[Cdecl]<uint, int, uint, int, int, void>)p;
            if (!RequireFunction("glBindVertexArray", out p)) return false;
            glBindVertexArray = (delegate* unmanaged[Cdecl]<uint, void>)p;
            if (!RequireFunction("glDeleteVertexArrays", out p)) return false;
            glDeleteVertexArrays = (delegate* unmanaged[Cdecl]<int, uint*, void>)p;
            if (!RequireFunction("glGenVertexArrays", out p)) return false;
            glGenVertexArrays = (delegate* unmanaged[Cdecl]<int, uint*, void>)p;

            // GL_VERSION_3_1
            if (!RequireFunction("glGetUniformBlockIndex", out p)) return false;
            glGetUniformBlockIndex = (delegate* unmanaged[Cdecl]<uint, byte*, uint>)p;
            if (!RequireFunction("glUniformBlockBinding", out p)) return false;
            glUniformBlockBinding = (delegate* unmanaged[Cdecl]<uint, uint, uint, void>)p;

            // Verify version
            int vMajor, vMinor;
            glGetIntegerv((uint)GL_MAJOR_VERSION, &vMajor);
            glGetIntegerv((uint)GL_MINOR_VERSION, &vMinor);
            if (vMajor < TVG_REQUIRE_GL_MAJOR_VER || (vMajor == TVG_REQUIRE_GL_MAJOR_VER && vMinor < TVG_REQUIRE_GL_MINOR_VER))
            {
                TvgCommon.TVGERR("GL_ENGINE",
                    "OpenGL version is not satisfied. Current: v{0}.{1}, Required: v{2}.{3}",
                    vMajor, vMinor, TVG_REQUIRE_GL_MAJOR_VER, TVG_REQUIRE_GL_MINOR_VER);
                return false;
            }

            TvgCommon.TVGLOG("GL_ENGINE", "OpenGL version = v{0}.{1}", vMajor, vMinor);

            _loaded = true;
            return true;
        }

        // ============================================================
        //  glTerm — unloads the GL library
        // ============================================================
        public static bool glTerm()
        {
            if (_libGL != nint.Zero)
            {
                NativeLibrary.Free(_libGL);
                _libGL = nint.Zero;
            }
            _loaded = false;
            return true;
        }
    }
}
