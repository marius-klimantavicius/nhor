using System;
using System.IO;

namespace ThorVG.Examples;

public abstract class ExampleBase
{
    public uint Elapsed;
    public bool LShift;

    public abstract bool Content(Canvas canvas, uint w, uint h);
    public virtual bool Update(Canvas canvas, uint elapsed) => false;
    public virtual bool ClickDown(Canvas canvas, int x, int y) => false;
    public virtual bool ClickUp(Canvas canvas, int x, int y) => false;
    public virtual bool KeyDown(Canvas canvas, int key) => false;
    public virtual bool Motion(Canvas canvas, int x, int y) => false;
    public virtual void Populate(string path) { }

    public float Timestamp()
    {
        return Environment.TickCount * 0.001f;
    }

    public void ScanDir(string path)
    {
        var rpath = Path.GetFullPath(path);
        if (!Directory.Exists(rpath))
        {
            Console.WriteLine($"Couldn't open directory \"{rpath}\".");
            return;
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(rpath))
        {
            var name = Path.GetFileName(entry);
            if (name.Length > 0 && (name[0] == '.' || name[0] == '$')) continue;
            if (!Directory.Exists(entry))
            {
                Populate(entry);
            }
        }
    }

    public static bool Verify(Result result, string failMsg = "")
    {
        switch (result)
        {
            case Result.FailedAllocation:
                Console.WriteLine($"FailedAllocation! {failMsg}");
                return false;
            case Result.InsufficientCondition:
                Console.WriteLine($"InsufficientCondition! {failMsg}");
                return false;
            case Result.InvalidArguments:
                Console.WriteLine($"InvalidArguments! {failMsg}");
                return false;
            case Result.MemoryCorruption:
                Console.WriteLine($"MemoryCorruption! {failMsg}");
                return false;
            case Result.NonSupport:
                Console.WriteLine($"NonSupport! {failMsg}");
                return false;
            case Result.Unknown:
                Console.WriteLine($"Unknown! {failMsg}");
                return false;
            default:
                return true;
        }
    }

    public static float Progress(uint elapsed, float durationInSec, bool rewind = false)
    {
        var duration = (uint)(durationInSec * 1000.0f); //sec -> millisec.
        if (elapsed == 0 || duration == 0) return 0.0f;
        var forward = ((elapsed / duration) % 2 == 0);
        if (elapsed % duration == 0) return forward ? 0.0f : 1.0f;
        var progress = (float)(elapsed % duration) / (float)duration;
        if (rewind) return forward ? progress : (1 - progress);
        return progress;
    }
}
