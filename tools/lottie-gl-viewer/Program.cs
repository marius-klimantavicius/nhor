// Lottie GL Viewer — renders Lottie animations using ThorVG GL renderer

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static Glfw.GLFW;

namespace ThorVG.Tools
{
    unsafe class App
    {
        private uint width = 800;
        private uint height = 800;
        private byte bgR, bgG, bgB;
        private bool hasBackground;
        private bool nextRequested;

        private static void HelpMsg()
        {
            Console.WriteLine(
                "Usage: \n" +
                "   lottie-gl-viewer [Lottie file] [-r resolution] [-b background color]\n\n" +
                "Examples: \n" +
                "    $ lottie-gl-viewer input.json\n" +
                "    $ lottie-gl-viewer input.lot -r 1024x768\n" +
                "    $ lottie-gl-viewer input.json -r 800x800 -b ffffff\n\n" +
                "Controls:\n" +
                "    Space — next file in directory (circular)\n");
        }

        private static string[] FindLottieFiles(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath)!;
            var files = Directory.GetFiles(dir)
                .Where(f => f.EndsWith(".lot", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return files;
        }

        private int Run(string filePath)
        {
            // 1. Initialize GLFW
            if (Glfw.Glfw.glfwInit() != GLFW_TRUE)
            {
                Console.WriteLine("Error: Failed to initialize GLFW.");
                return 1;
            }

            // 2. GL 3.3 core profile
            Glfw.Glfw.glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 3);
            Glfw.Glfw.glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 3);
            Glfw.Glfw.glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Glfw.Glfw.glfwWindowHint(GLFW_OPENGL_FORWARD_COMPAT, GLFW_TRUE);

            // 3. Create window
            var window = Glfw.Glfw.glfwCreateWindow((int)width, (int)height, "ThorVG Lottie GL Viewer", null, null);
            if (window == null)
            {
                Console.WriteLine("Error: Failed to create GLFW window.");
                Glfw.Glfw.glfwTerminate();
                return 1;
            }

            Glfw.Glfw.glfwMakeContextCurrent(window);
            Glfw.Glfw.glfwSwapInterval(1);

            // Key callback for space → next file
            Glfw.Glfw.glfwSetKeyCallback(window, (w, key, scanCode, action, mods) =>
            {
                if (key == GLFW_KEY_SPACE && action == GLFW_PRESS)
                    nextRequested = true;
            });

            // 4. Initialize ThorVG (must happen after GL context is current)
            if (Initializer.Init() != Result.Success)
            {
                Console.WriteLine("Error: Failed to initialize ThorVG.");
                Glfw.Glfw.glfwDestroyWindow(window);
                Glfw.Glfw.glfwTerminate();
                return 1;
            }

            // 5. Create GL canvas
            var canvas = GlCanvas.Gen();
            if (canvas == null)
            {
                Console.WriteLine("Error: Failed to create GL canvas. Is OpenGL 3.3+ available?");
                Initializer.Term();
                Glfw.Glfw.glfwDestroyWindow(window);
                Glfw.Glfw.glfwTerminate();
                return 1;
            }

            nint contextId = 1;
            if (canvas.Target(nint.Zero, nint.Zero, contextId, 0, width, height, ColorSpace.ABGR8888S) != Result.Success)
            {
                Console.WriteLine("Error: Failed to set GL canvas target.");
                Initializer.Term();
                Glfw.Glfw.glfwDestroyWindow(window);
                Glfw.Glfw.glfwTerminate();
                return 1;
            }

            // Build file list for cycling
            var files = FindLottieFiles(filePath);
            var fileIndex = Array.IndexOf(files, filePath);
            if (fileIndex < 0) fileIndex = 0;

            // 6. Load animation
            var animation = Animation.Gen();
            var picture = animation.GetPicture();
            if (picture.Load(filePath) != Result.Success)
            {
                Console.WriteLine($"Error: Failed to load \"{filePath}\".");
                Initializer.Term();
                Glfw.Glfw.glfwDestroyWindow(window);
                Glfw.Glfw.glfwTerminate();
                return 1;
            }

            picture.GetSize(out float origW, out float origH);
            var scale = Math.Min((float)width / origW, (float)height / origH);
            picture.SetSize(origW * scale, origH * scale);

            // Background (default wheat, override with -b)
            var bg = Shape.Gen();
            bg.SetFill(hasBackground ? bgR : (byte)0xF5, hasBackground ? bgG : (byte)0xDE, hasBackground ? bgB : (byte)0xB3);
            bg.AppendRect(0, 0, width, height);
            canvas.Add(bg);

            canvas.Add(picture);

            // 7. Render loop
            var totalFrames = animation.TotalFrame();
            var duration = animation.Duration();
            var sw = Stopwatch.StartNew();

            Console.WriteLine($"Playing: {Path.GetFileName(filePath)} ({origW}x{origH}, {totalFrames:F0} frames, {duration:F2}s)");

            while (Glfw.Glfw.glfwWindowShouldClose(window) == 0)
            {
                Glfw.Glfw.glfwPollEvents();

                // Switch to next file
                if (nextRequested && files.Length > 0)
                {
                    nextRequested = false;
                    fileIndex = (fileIndex + 1) % files.Length;

                    canvas.Remove(picture);
                    animation = Animation.Gen();
                    picture = animation.GetPicture();
                    if (picture.Load(files[fileIndex]) == Result.Success)
                    {
                        picture.GetSize(out origW, out origH);
                        scale = Math.Min((float)width / origW, (float)height / origH);
                        picture.SetSize(origW * scale, origH * scale);
                        totalFrames = animation.TotalFrame();
                        duration = animation.Duration();
                        sw.Restart();
                        canvas.Add(picture);
                        Console.WriteLine($"Playing: {Path.GetFileName(files[fileIndex])} ({origW}x{origH}, {totalFrames:F0} frames, {duration:F2}s)");
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Failed to load \"{files[fileIndex]}\", skipping.");
                        canvas.Add(picture);
                    }
                }

                // Handle resize
                Glfw.Glfw.glfwGetFramebufferSize(window, out int fbW, out int fbH);
                if (fbW > 0 && fbH > 0 && ((uint)fbW != width || (uint)fbH != height))
                {
                    width = (uint)fbW;
                    height = (uint)fbH;
                    canvas.Target(nint.Zero, nint.Zero, contextId, 0, width, height, ColorSpace.ABGR8888S);
                    scale = Math.Min((float)width / origW, (float)height / origH);
                    picture.SetSize(origW * scale, origH * scale);
                    bg.ResetShape();
                    bg.AppendRect(0, 0, width, height);
                }

                // Advance frame
                var elapsed = sw.Elapsed.TotalSeconds;
                var frame = (float)(elapsed / duration % 1.0 * totalFrames);
                animation.Frame(frame);

                canvas.Update();
                canvas.Draw(true);
                canvas.Sync();

                Glfw.Glfw.glfwSwapBuffers(window);
            }

            // 8. Cleanup
            Initializer.Term();
            Glfw.Glfw.glfwDestroyWindow(window);
            Glfw.Glfw.glfwTerminate();
            return 0;
        }

        public int Setup(string[] args)
        {
            string? filePath = null;

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
                            Console.WriteLine("Error: Missing resolution. Expected eg. -r 800x800.");
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
                            Console.WriteLine($"Error: Invalid resolution ({pArg}). Expected eg. -r 800x800.");
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
                        var bgColor = Convert.ToUInt32(pArg, 16);
                        bgR = (byte)((bgColor & 0xff0000) >> 16);
                        bgG = (byte)((bgColor & 0x00ff00) >> 8);
                        bgB = (byte)(bgColor & 0x0000ff);
                        hasBackground = true;
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Unknown flag ({p}).");
                    }
                }
                else
                {
                    filePath = p;
                }
            }

            if (filePath == null)
            {
                HelpMsg();
                return 0;
            }

            filePath = Path.GetFullPath(filePath);
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: File not found: \"{filePath}\"");
                return 1;
            }

            return Run(filePath);
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
