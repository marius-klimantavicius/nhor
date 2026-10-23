// lottie2gif-gl — converts Lottie animations to GIF using ThorVG GL renderer

using System;
using System.IO;
using System.Runtime.InteropServices;
using static Glfw.GLFW;

namespace ThorVG.Tools
{
    unsafe class App
    {
        private uint fps = 30;
        private uint width = 600;
        private uint height = 600;
        private byte r, g, b;
        private bool background = false;
        private uint? randomSeed;

        private static void HelpMsg()
        {
            Console.WriteLine(
                "Usage: \n" +
                "   lottie2gif-gl [Lottie file] or [Lottie folder] [-r resolution] [-f fps] [-b background color] [-s random seed]\n\n" +
                "Examples: \n" +
                "    $ lottie2gif-gl input.json\n" +
                "    $ lottie2gif-gl input.json -r 600x600\n" +
                "    $ lottie2gif-gl input.json -f 30\n" +
                "    $ lottie2gif-gl input.json -r 600x600 -f 30 -b fa7410\n" +
                "    $ lottie2gif-gl input.lot\n" +
                "    $ lottie2gif-gl lottiefolder\n");
        }

        private static bool Validate(string lottieName)
        {
            var ext = Path.GetExtension(lottieName);
            if (ext != ".json" && ext != ".lot")
            {
                Console.WriteLine($"Error: \"{lottieName}\" is invalid.");
                return false;
            }
            return true;
        }

        private bool Convert(string input, string output)
        {
            // 1. Initialize GLFW
            if (Glfw.Glfw.glfwInit() != GLFW_TRUE)
            {
                Console.WriteLine("Error: Failed to initialize GLFW.");
                return false;
            }

            // 2. GL 3.3 core profile
            Glfw.Glfw.glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 3);
            Glfw.Glfw.glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 3);
            Glfw.Glfw.glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Glfw.Glfw.glfwWindowHint(GLFW_OPENGL_FORWARD_COMPAT, GLFW_TRUE);

            Glfw.Glfw.glfwWindowHint(GLFW_FOCUSED, GLFW_FALSE);
            Glfw.Glfw.glfwWindowHint(GLFW_FOCUS_ON_SHOW, GLFW_FALSE);

            // 3. Create window
            var window = Glfw.Glfw.glfwCreateWindow((int)width, (int)height, "lottie2gif-gl", null, null);
            if (window == null)
            {
                Console.WriteLine("Error: Failed to create GLFW window.");
                Glfw.Glfw.glfwTerminate();
                return false;
            }

            Glfw.Glfw.glfwMakeContextCurrent(window);
            Glfw.Glfw.glfwSwapInterval(0); // No vsync for maximum speed

            GlCanvas? canvas = null;
            Animation? animation = null;
            bool initialized = false;
            GifWriter? writer = null;
            try
            {
                // Load glReadPixels via GLFW
                var readPixelsPtr = Glfw.Glfw.glfwGetProcAddress("glReadPixels");
                if (readPixelsPtr == nint.Zero)
                {
                    Console.WriteLine("Error: glReadPixels not available.");
                    return false;
                }
                var glReadPixels = (delegate* unmanaged[Cdecl]<int, int, int, int, uint, uint, void*, void>)readPixelsPtr;

                // 4. Initialize ThorVG (must happen after GL context is current)
                if (Initializer.Init() != Result.Success)
                {
                    Console.WriteLine("Error: Failed to initialize ThorVG.");
                    return false;
                }

                // 5. Create GL canvas
                initialized = true;
                canvas = GlCanvas.Gen();
                if (canvas == null)
                {
                    Console.WriteLine("Error: Failed to create GL canvas.");
                    return false;
                }

                if (canvas.Target(0, width, height, ColorSpace.ABGR8888S) != Result.Success)
                {
                    Console.WriteLine("Error: Failed to set GL canvas target.");
                    return false;
                }

                // 6. Load animation
                animation = Animation.Gen();
                var picture = animation.GetPicture();
                if (randomSeed.HasValue) LottieExpressions.SeedRandom(randomSeed.Value);
                if (picture.Load(input) != Result.Success)
                {
                    Console.WriteLine($"Error: Failed to load \"{input}\".");
                    return false;
                }

                picture.GetSize(out float origW, out float origH);
                var scale = (float)width / origW;
                picture.SetSize(origW * scale, origH * scale);

                // Use picture bounds for GIF dimensions (matches GifSaver behavior)
                picture.Bounds(out float bx, out float by, out float bw, out float bh);
                if (bx < 0) bw += bx;
                if (by < 0) bh += by;
                var w = (uint)bw;
                var h = (uint)bh;

                // Resize window and re-target canvas to match picture bounds
                if (w != width || h != height)
                {
                    Glfw.Glfw.glfwSetWindowSize(window, (int)w, (int)h);
                    canvas.Target(0, w, h, ColorSpace.ABGR8888S);
                }

                // Background
                if (background)
                {
                    var bg = Shape.Gen();
                    bg.SetFill(r, g, b);
                    bg.AppendRect(0, 0, bw, bh);
                    canvas.Add(bg);
                }

                canvas.Add(picture);
                var totalFrames = animation.TotalFrame();
                var duration = animation.Duration();
                if (!(duration > 0) || !float.IsFinite(duration) || !(totalFrames > 0) || w == 0 || h == 0)
                {
                    Console.WriteLine("Error: Animation has no renderable duration or bounds.");
                    return false;
                }

                var actualFps = (float)fps;
                if (actualFps > 60.0f) actualFps = 60.0f;
                else if (TvgMath.Zero(actualFps) || actualFps < 0.0f)
                    actualFps = totalFrames / duration;

                var delay = 1.0f / actualFps;
                var transparent = !background;

                var buffer = new uint[w * h];
                var tmpRow = new uint[w];

                writer = new GifWriter();
                if (!GifEncoder.GifBegin(writer, output, w, h, (uint)(delay * 100.0f)))
                {
                    Console.WriteLine("Error: Failed to begin GIF encoding.");
                    return false;
                }

                // 8. Frame loop
                for (var p = 0.0f; p < duration; p += delay)
                {
                    var frameNo = totalFrames * (p / duration);
                    animation.Frame(frameNo);
                    if (canvas.Update() != Result.Success || canvas.Draw(true) != Result.Success || canvas.Sync() != Result.Success)
                    {
                        Console.WriteLine("Error: Failed to render animation frame.");
                        return false;
                    }

                    // Read pixels from GL framebuffer
                    fixed (uint* bufPtr = buffer)
                    {
                        glReadPixels(0, 0, (int)w, (int)h, GL.GL_RGBA, GL.GL_UNSIGNED_BYTE, bufPtr);
                    }

                    // Flip vertically (GL has origin at bottom-left, GIF at top-left)
                    int rowWidth = (int)w;
                    for (int y = 0; y < (int)h / 2; y++)
                    {
                        int topOff = y * rowWidth;
                        int botOff = ((int)h - 1 - y) * rowWidth;
                        Array.Copy(buffer, topOff, tmpRow, 0, rowWidth);
                        Array.Copy(buffer, botOff, buffer, topOff, rowWidth);
                        Array.Copy(tmpRow, 0, buffer, botOff, rowWidth);
                    }

                    // Encode frame
                    fixed (uint* bufPtr = buffer)
                    {
                        if (!GifEncoder.GifWriteFrame(writer, (byte*)bufPtr, w, h, (uint)(delay * 100.0f), transparent))
                        {
                            Console.WriteLine("Error: Failed to encode GIF frame.");
                            return false;
                        }
                    }

                    Glfw.Glfw.glfwSwapBuffers(window);
                    Glfw.Glfw.glfwPollEvents();
                }

                if (!GifEncoder.GifEnd(writer)) return false;
                return true;
            }
            catch (Exception error)
            {
                Console.WriteLine($"Error: {error.Message}");
                return false;
            }
            finally
            {
                try { if (writer?.f != null) GifEncoder.GifEnd(writer); }
                finally
                {
                    try { canvas?.Dispose(); }
                    finally
                    {
                        try { animation?.Dispose(); }
                        finally
                        {
                            if (initialized) Initializer.Term();
                            Glfw.Glfw.glfwDestroyWindow(window);
                            Glfw.Glfw.glfwTerminate();
                        }
                    }
                }
            }
        }

        private bool ConvertFile(string lottieName)
        {
            var gifName = Path.ChangeExtension(lottieName, ".gif");

            bool converted = Convert(lottieName, gifName);
            if (converted)
                Console.WriteLine($"Generated Gif file : {gifName}");
            else
                Console.WriteLine($"Failed Converting Gif file : {lottieName}");
            return converted;
        }

        private bool HandleDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Couldn't open directory \"{path}\".");
                return false;
            }

            bool succeeded = true;
            foreach (var entry in Directory.EnumerateFileSystemEntries(path))
            {
                var name = Path.GetFileName(entry);
                if (name.StartsWith('.') || name.StartsWith('$')) continue;

                if (Directory.Exists(entry))
                {
                    succeeded &= HandleDirectory(entry);
                }
                else
                {
                    if (!Validate(entry)) continue;
                    succeeded &= ConvertFile(entry);
                }
            }
            return succeeded;
        }

        public int Setup(string[] args)
        {
            var inputs = new System.Collections.Generic.List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                var p = args[i];
                if (p.StartsWith('-'))
                {
                    var pArg = (i + 1 < args.Length) ? args[++i] : null;

                    if (p == "-r")
                    {
                        if (pArg == null)
                        {
                            Console.WriteLine("Error: Missing resolution. Expected eg. -r 600x600.");
                            return 1;
                        }
                        var parts = pArg.Split('x');
                        if (parts.Length == 2 &&
                            uint.TryParse(parts[0], out var pw) &&
                            uint.TryParse(parts[1], out var ph) &&
                            pw > 0 && ph > 0)
                        {
                            width = pw;
                            height = ph;
                        }
                        else
                        {
                            Console.WriteLine($"Error: Invalid resolution ({pArg}). Expected eg. -r 600x600.");
                            return 1;
                        }
                    }
                    else if (p == "-f")
                    {
                        if (pArg == null)
                        {
                            Console.WriteLine("Error: Missing fps value. Expected eg. -f 30.");
                            return 1;
                        }
                        if (!uint.TryParse(pArg, out fps) || fps == 0)
                        {
                            Console.WriteLine($"Error: Invalid fps value ({pArg}).");
                            return 1;
                        }
                    }
                    else if (p == "-b")
                    {
                        if (pArg == null)
                        {
                            Console.WriteLine("Error: Missing background color. Expected eg. -b fa7410.");
                            return 1;
                        }
                        var bgColor = System.Convert.ToUInt32(pArg, 16);
                        r = (byte)((bgColor & 0xff0000) >> 16);
                        g = (byte)((bgColor & 0x00ff00) >> 8);
                        b = (byte)(bgColor & 0x0000ff);
                        background = true;
                    }
                    else if (p == "-s")
                    {
                        if (!uint.TryParse(pArg, out var seed))
                        {
                            Console.WriteLine($"Error: Invalid random seed ({pArg}). Expected a nonnegative integer.");
                            return 1;
                        }
                        randomSeed = seed;
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Unknown flag ({p}).");
                    }
                }
                else
                {
                    inputs.Add(p);
                }
            }

            if (inputs.Count == 0)
            {
                HelpMsg();
                return 0;
            }

            bool succeeded = true;
            foreach (var input in inputs)
            {
                var path = Path.GetFullPath(input);
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    Console.WriteLine($"Invalid file or path name: \"{input}\"");
                    succeeded = false;
                    continue;
                }

                if (Directory.Exists(path))
                {
                    Console.WriteLine($"Directory: \"{path}\"");
                    succeeded &= HandleDirectory(path);
                }
                else
                {
                    if (!Validate(path)) { succeeded = false; continue; }
                    succeeded &= ConvertFile(path);
                }
            }

            return succeeded ? 0 : 1;
        }
    }

    class Program
    {
        static int Main(string[] args)
        {
            var app = new App();
            return app.Setup(args);
        }
    }
}
