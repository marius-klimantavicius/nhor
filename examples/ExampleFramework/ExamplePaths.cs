using System;
using System.IO;
using System.Reflection;

namespace ThorVG.Examples;

public static class ExamplePaths
{
    private static string? _exampleDir;

    /// <summary>
    /// Path to thorvg.example/res/ relative to the repository root.
    /// Walks up from the executing assembly location until it finds ThorVG.slnx.
    /// </summary>
    public static string ExampleDir
    {
        get
        {
            if (_exampleDir != null) return _exampleDir;
            _exampleDir = FindExampleDir();
            return _exampleDir;
        }
    }

    /// <summary>
    /// Override the resource directory (e.g., from --res CLI argument).
    /// </summary>
    public static void SetResDir(string path)
    {
        _exampleDir = Path.GetFullPath(path);
    }

    private static string FindExampleDir()
    {
        // Walk up from executing assembly location
        var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "Nhor.slnx")))
            {
                var resDir = Path.Combine(dir, "ref", "thorvg.example", "res");
                if (Directory.Exists(resDir))
                    return resDir;
            }
            dir = Path.GetDirectoryName(dir);
        }

        // Fallback: try current directory
        var cwd = Directory.GetCurrentDirectory();
        var dir2 = cwd;
        while (dir2 != null)
        {
            if (File.Exists(Path.Combine(dir2, "Nhor.slnx")))
            {
                var resDir = Path.Combine(dir2, "ref", "thorvg.example", "res");
                if (Directory.Exists(resDir))
                    return resDir;
            }
            dir2 = Path.GetDirectoryName(dir2);
        }

        Console.WriteLine("Warning: Could not find thorvg.example/res/ directory.");
        return Path.Combine(cwd, "ref", "thorvg.example", "res");
    }
}
