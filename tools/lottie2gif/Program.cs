// Ported from ThorVG/tools/lottie2gif/lottie2gif.cpp

using System;
using System.IO;

namespace ThorVG.Tools
{
    class App
    {
        private uint fps = 30;
        private uint width = 600;
        private uint height = 600;
        private byte r, g, b;
        private bool background = false;

        private static void HelpMsg()
        {
            Console.WriteLine(
                "Usage: \n" +
                "   lottie2gif [Lottie file] or [Lottie folder] [-r resolution] [-f fps] [-b background color]\n\n" +
                "Examples: \n" +
                "    $ lottie2gif input.json\n" +
                "    $ lottie2gif input.json -r 600x600\n" +
                "    $ lottie2gif input.json -f 30\n" +
                "    $ lottie2gif input.json -r 600x600 -f 30\n" +
                "    $ lottie2gif input.lot\n" +
                "    $ lottie2gif lottiefolder\n" +
                "    $ lottie2gif lottiefolder -r 600x600 -f 30 -b fa7410\n");
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
            if (Initializer.Init() != Result.Success) return false;

            var animation = Animation.Gen();
            var picture = animation.GetPicture();
            if (picture.Load(input) != Result.Success)
            {
                Initializer.Term();
                return false;
            }

            picture.GetSize(out float w, out float h);
            var scale = (float)width / w;
            picture.SetSize(w * scale, h * scale);

            var saver = Saver.Gen();

            if (background)
            {
                var bg = Shape.Gen();
                bg.SetFill(r, g, b);
                bg.AppendRect(0, 0, w * scale, h * scale);
                saver.Background(bg);
            }

            if (saver.Save(animation, output, 100, fps) != Result.Success)
            {
                Initializer.Term();
                return false;
            }

            if (saver.Sync() != Result.Success)
            {
                Initializer.Term();
                return false;
            }

            return Initializer.Term() == Result.Success;
        }

        private void ConvertFile(string lottieName)
        {
            var gifName = Path.ChangeExtension(lottieName, ".gif");

            if (Convert(lottieName, gifName))
                Console.WriteLine($"Generated Gif file : {gifName}");
            else
                Console.WriteLine($"Failed Converting Gif file : {lottieName}");
        }

        private void HandleDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Couldn't open directory \"{path}\".");
                return;
            }

            foreach (var entry in Directory.EnumerateFileSystemEntries(path))
            {
                var name = Path.GetFileName(entry);
                if (name.StartsWith('.') || name.StartsWith('$')) continue;

                if (Directory.Exists(entry))
                {
                    HandleDirectory(entry);
                }
                else
                {
                    if (!Validate(entry)) continue;
                    ConvertFile(entry);
                }
            }
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
                            Console.WriteLine("Error: Missing resolution attribute. Expected eg. -r 600x600.");
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
                            Console.WriteLine($"Error: Resolution ({pArg}) is corrupted. Expected eg. -r 600x600.");
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
                            Console.WriteLine("Error: Missing background color attribute. Expected eg. -b fa7410.");
                            return 1;
                        }
                        var bgColor = System.Convert.ToUInt32(pArg, 16);
                        r = (byte)((bgColor & 0xff0000) >> 16);
                        g = (byte)((bgColor & 0x00ff00) >> 8);
                        b = (byte)(bgColor & 0x0000ff);
                        background = true;
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

            foreach (var input in inputs)
            {
                var path = Path.GetFullPath(input);
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    Console.WriteLine($"Invalid file or path name: \"{input}\"");
                    continue;
                }

                if (Directory.Exists(path))
                {
                    Console.WriteLine($"Directory: \"{path}\"");
                    HandleDirectory(path);
                }
                else
                {
                    if (!Validate(path)) continue;
                    ConvertFile(path);
                }
            }

            return 0;
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
