// Native char strings can contain bytes which are not well-formed UTF-8.
// Keep their original bytes at the managed string boundary. Ordinary strings
// use the runtime codec directly; metadata follows the string's GC lifetime.
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ThorVG;

internal static class TvgUtf8
{
    private sealed class RawString
    {
        internal readonly byte[] Bytes;
        internal RawString(ReadOnlySpan<byte> bytes) { Bytes = bytes.ToArray(); }
    }

    private static readonly UTF8Encoding Strict = new(false, true);
    private static readonly ConditionalWeakTable<string, RawString> Raw = new();

    internal static string Decode(ReadOnlySpan<byte> bytes)
    {
        try { return Strict.GetString(bytes); }
        catch (DecoderFallbackException)
        {
            // GetString allocates a distinct nonempty string for invalid input.
            string value = Encoding.UTF8.GetString(bytes);
            Raw.Add(value, new RawString(bytes));
            return value;
        }
    }

    internal static byte[] Encode(string value)
        => Raw.TryGetValue(value, out var raw) ? (byte[])raw.Bytes.Clone() : Encoding.UTF8.GetBytes(value);

    internal static int ByteCount(string value)
        => Raw.TryGetValue(value, out var raw) ? raw.Bytes.Length : Encoding.UTF8.GetByteCount(value);

    internal static bool HasRaw(string value) => Raw.TryGetValue(value, out _);

    internal static string Slice(string value, int start, int length)
    {
        string result = value.Substring(start, length);
        if (ReferenceEquals(result, value) || length == 0 || !Raw.TryGetValue(value, out var raw)) return result;
        int offset = 0, chars = 0, begin = -1, end = -1;
        while (true)
        {
            if (chars == start) begin = offset;
            if (chars == start + length) { end = offset; break; }
            if (offset == raw.Bytes.Length) break;
            var status = Rune.DecodeFromUtf8(raw.Bytes.AsSpan(offset), out var rune, out int consumed);
            chars += status == OperationStatus.Done ? rune.Utf16SequenceLength : 1;
            offset += consumed;
        }
        // Managed character limits may deliberately split a surrogate pair;
        // preserve that existing string API adaptation at such a boundary.
        if (begin >= 0 && end >= 0) return Decode(raw.Bytes.AsSpan(begin, end - begin));
        return result;
    }

    internal static string Slice(string value, int start) => Slice(value, start, value.Length - start);

    internal static string Concat(string left, string right)
    {
        if (!HasRaw(left) && !HasRaw(right)) return string.Concat(left, right);
        byte[] a = Encode(left), b = Encode(right);
        byte[] bytes = new byte[checked(a.Length + b.Length)];
        a.CopyTo(bytes, 0); b.CopyTo(bytes, a.Length);
        return Decode(bytes);
    }

    // OS file I/O retains native char path bytes; decoders remain managed.
    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int OpenUnix(byte[] path, int flags, uint mode);
    [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static extern int Fcntl(int descriptor, int command, int value);
    [DllImport("kernel32.dll", EntryPoint = "CreateFileA", SetLastError = true)]
    private static extern SafeFileHandle OpenWindows(byte[] path, uint access, uint sharing,
        IntPtr attributes, uint creation, uint flags, IntPtr template);

    private static FileStream OpenFile(string path, bool write)
    {
        path = TvgStr.Duplicate(path)!;
        if (!HasRaw(path)) return new FileStream(path, write ? FileMode.Create : FileMode.Open,
            write ? FileAccess.Write : FileAccess.Read, write ? FileShare.None : FileShare.Read);
        byte[] bytes = Encode(path);
        System.Array.Resize(ref bytes, checked(bytes.Length + 1));
        SafeFileHandle handle;
        if (OperatingSystem.IsWindows())
            handle = OpenWindows(bytes, write ? 0x40000000u : 0x80000000u, write ? 0u : 1u,
                IntPtr.Zero, write ? 2u : 3u, 0x80, IntPtr.Zero);
        else
        {
            bool linux = OperatingSystem.IsLinux() || OperatingSystem.IsAndroid();
            int descriptor = OpenUnix(bytes, write ? 1 | (linux ? 0x40 | 0x200 : 0x200 | 0x400) : 0, 438);
            if (descriptor < 0) throw new IOException("Native file open failed", Marshal.GetLastPInvokeError());
            handle = new SafeFileHandle((IntPtr)descriptor, true);
            Fcntl(descriptor, 2, 1); // F_SETFD, FD_CLOEXEC
        }
        try
        {
            if (handle.IsInvalid) throw new IOException("Native file open failed", Marshal.GetLastPInvokeError());
            return new FileStream(handle, write ? FileAccess.Write : FileAccess.Read);
        }
        catch { handle.Dispose(); throw; }
    }

    internal static FileStream OpenRead(string path) => OpenFile(path, false);
    internal static FileStream Create(string path) => OpenFile(path, true);

    internal static byte[] ReadAllBytes(string path)
    {
        if (!HasRaw(path)) return File.ReadAllBytes(TvgStr.Duplicate(path)!);
        using var stream = OpenRead(path);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
