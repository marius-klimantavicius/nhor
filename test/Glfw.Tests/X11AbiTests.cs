using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using G = Glfw.Glfw;
using static Glfw.GLFW;

internal static unsafe partial class Program
{
    // This calculator reads the actual compiled types, including InlineArray
    // and FixedBuffer metadata. It is static evidence for other pointer widths;
    // the separate host checks use CLR storage and offsets directly.
    private static Layout X11Layout(Type type, int word)
    {
        if (type.IsEnum) return X11Layout(Enum.GetUnderlyingType(type), word);
        int primitive = type.IsPointer || type.IsFunctionPointer || type == typeof(nint) || type == typeof(nuint) ? word :
            type == typeof(byte) || type == typeof(sbyte) ? 1 : type == typeof(short) || type == typeof(ushort) ? 2 :
            type == typeof(int) || type == typeof(uint) || type == typeof(float) ? 4 :
            type == typeof(long) || type == typeof(ulong) || type == typeof(double) ? 8 : 0;
        if (primitive != 0) return new Layout(primitive, primitive, new());
        var attr = type.StructLayoutAttribute ?? throw new Exception("No layout: " + type);
        Check(attr.Value != LayoutKind.Auto, "explicit native layout " + type);
        int pack = attr.Pack == 0 ? 8 : attr.Pack;
        int size = 0, alignment = 1;
        var offsets = new Dictionary<string, int>();
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(f => f.MetadataToken))
        {
            var buffer = field.GetCustomAttribute<FixedBufferAttribute>();
            Layout item = X11Layout(buffer?.ElementType ?? field.FieldType, word);
            int fieldSize = item.Size * (buffer?.Length ?? 1);
            int fieldAlignment = Math.Min(pack, item.Alignment);
            int offset = attr.Value == LayoutKind.Explicit ? field.GetCustomAttribute<FieldOffsetAttribute>()!.Value : Align(size, fieldAlignment);
            offsets.Add(field.Name, offset);
            size = Math.Max(size, offset + fieldSize);
            alignment = Math.Max(alignment, fieldAlignment);
        }
        var inline = type.GetCustomAttribute<InlineArrayAttribute>();
        if (inline != null) size *= inline.Length;
        return new Layout(Math.Max(attr.Size, Align(size, alignment)), alignment, offsets);
    }

    private static int Align(int value, int alignment) => (value + alignment - 1) / alignment * alignment;

    [StructLayout(LayoutKind.Sequential)]
    private struct X11AlignmentProbe<T> where T : unmanaged
    {
        public byte prefix;
        public T value;
    }

    private static int X11StorageSize<T>() where T : unmanaged => sizeof(T);
    private static int X11StorageAlignment<T>() where T : unmanaged
    {
        X11AlignmentProbe<T> probe = default;
        return (int)((byte*)&probe.value - (byte*)&probe);
    }
    private static int X11RuntimeMetric(Type type, string method) => (int)typeof(Program).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(type).Invoke(null, null)!;

    private static Type X11ProductionType(string name) => name == "XIMStylesActive" ? typeof(G).GetNestedType("XIMStyles", BindingFlags.NonPublic)! :
        typeof(G).Assembly.GetType("Glfw." + name) ?? typeof(G).GetNestedType(name, BindingFlags.NonPublic) ?? throw new Exception("Missing production type " + name);

    private static void ValidateX11Abi()
    {
        Check(!native, "X11 ABI scenario requires compiled managed production types");
        string path = Environment.GetEnvironmentVariable("GLFW_X11_ABI_JSON") ?? throw new Exception("GLFW_X11_ABI_JSON required");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        int word = document.RootElement.GetProperty("atom_bytes").GetInt32();
        bool host = Environment.GetEnvironmentVariable("GLFW_X11_ABI_HOST") == "1";
        Check(word == 4 || word == 8, "native Atom width");
        if (host) Check(word == IntPtr.Size, "host native/managed widths agree");
        foreach (string alias in new[] { "atom_alignment", "keysym_bytes", "time_bytes", "xid_bytes", "long_bytes", "ximstyle_bytes" })
            Check(document.RootElement.GetProperty(alias).GetInt32() == word, "Linux native-long alias " + alias);

        // An incomplete fixture must fail instead of silently omitting fields.
        foreach (string record in new[] { "XkbNamesRec", "XkbDescRec", "XkbKeyNameRec", "XkbKeyAliasRec", "XkbStateRec", "XkbStateNotifyEvent" })
        {
            foreach (string suffix in new[] { "__sizeof", "__alignof" })
                Check(document.RootElement.TryGetProperty(record + "_" + suffix, out _), "required native record metric " + record + suffix);
            foreach (var field in X11ProductionType(record).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                Check(document.RootElement.TryGetProperty(record + "_" + field.Name, out _), "required native field metric " + record + "." + field.Name);
        }

        int metrics = 0;
        foreach (var metric in document.RootElement.EnumerateObject())
        {
            int split = metric.Name.IndexOf('_');
            if (split < 0 || !char.IsUpper(metric.Name[0])) continue;
            Type type = X11ProductionType(metric.Name[..split]);
            string member = metric.Name[(split + 1)..];
            Layout layout = X11Layout(type, word);
            int actual;
            if (member == "__sizeof") actual = host ? X11RuntimeMetric(type, nameof(X11StorageSize)) : layout.Size;
            else if (member == "__alignof") actual = host ? X11RuntimeMetric(type, nameof(X11StorageAlignment)) : layout.Alignment;
            else
            {
                string fieldName = member.Split("___", StringSplitOptions.None)[0];
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
                if (member.EndsWith("___sizeof", StringComparison.Ordinal))
                    actual = host ? X11RuntimeMetric(field.FieldType, nameof(X11StorageSize)) : X11Layout(field.FieldType, word).Size;
                else if (member.EndsWith("___alignof", StringComparison.Ordinal))
                    actual = host ? X11RuntimeMetric(field.FieldType, nameof(X11StorageAlignment)) : X11Layout(field.FieldType, word).Alignment;
                else if (member.EndsWith("___element", StringComparison.Ordinal))
                {
                    var buffer = field.GetCustomAttribute<FixedBufferAttribute>();
                    var element = buffer?.ElementType ?? field.FieldType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single().FieldType;
                    actual = host ? X11RuntimeMetric(element, nameof(X11StorageSize)) : X11Layout(element, word).Size;
                }
                else actual = host ? Marshal.OffsetOf(type, member).ToInt32() : layout.Offsets[member];
            }
            Check(actual == metric.Value.GetInt32(), $"{metric.Name}: native={metric.Value} managed={actual}");
            metrics++;
        }
        // Reject fixed-64 storage even when this executable runs on a 64-bit CLR.
        var names = X11ProductionType("XkbNamesRec");
        foreach (var entry in new[] { ("vmods", 16), ("indicators", 32), ("groups", 4) })
        {
            var field = names.GetField(entry.Item1)!;
            Check(field.GetCustomAttribute<FixedBufferAttribute>() == null, "Atom array cannot use a fixed primitive buffer");
            Check(field.FieldType.GetCustomAttribute<InlineArrayAttribute>()?.Length == entry.Item2, "Atom inline count " + entry.Item1);
            var cells = field.FieldType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Check(cells.Length == 1 && cells[0].FieldType == typeof(nuint), "Atom inline element must be UIntPtr " + entry.Item1);
        }
        Record("x11-abi-metrics", metrics);
        Record("x11-abi-word-bytes", word);
        Record("x11-process-architecture", RuntimeInformation.ProcessArchitecture.ToString());
        Record("x11-abi-evidence", host ? "actual host CLR layout against native client headers" : "compiled production metadata layout calculation against cross-target client headers; no target managed execution");
    }

    private static void ValidateX11KeyNames()
    {
        var output = new List<object>();
        for (int cycle = 0; cycle < 4; cycle++)
        {
            InitHint(GLFW_PLATFORM, GLFW_PLATFORM_X11);
            Check(Init() == GLFW_TRUE, "X11 key-name initialization");
            int first = 8, last = 255;
            nint display = native ? N.glfwGetX11Display() : G.glfwGetX11Display();
            Check(display != 0, "key-name real X11 display");
            Check(XDisplayKeycodes(display, &first, &last) != 0, "native keycode range");
            int opcode = 0, eventBase = 0, errorBase = 0, major = 1, minor = 0;
            Check(XkbQueryExtension(display, &opcode, &eventBase, &errorBase, &major, &minor) != 0 &&
                  (native || Glfw._glfw.X11!.xkb.available), "real XKB path is active");
            var keys = new List<object>();
            int named = 0;
            foreach (int key in Enumerable.Range(GLFW_KEY_SPACE, GLFW_KEY_LAST - GLFW_KEY_SPACE + 1))
            {
                int code = native ? N.glfwGetKeyScancode(key) : G.glfwGetKeyScancode(key);
                Check(code == -1 || code >= first && code <= last, "mapped keycode in native range");
                Check(Error() == 0, "scancode lookup error");
                bool printable = key == GLFW_KEY_KP_EQUAL || key >= GLFW_KEY_KP_0 && key <= GLFW_KEY_KP_ADD ||
                                 key >= GLFW_KEY_APOSTROPHE && key <= GLFW_KEY_WORLD_2;
                string? byKey = native ? Marshal.PtrToStringUTF8(N.glfwGetKeyName(key, 0)) : G.glfwGetKeyName(key, 0);
                Check(Error() == (printable && code < 0 ? GLFW_INVALID_VALUE : 0), "key-name error contract");
                string? byCode = code < 0 ? null : native ? Marshal.PtrToStringUTF8(N.glfwGetKeyName(GLFW_KEY_UNKNOWN, code)) : G.glfwGetKeyName(GLFW_KEY_UNKNOWN, code);
                Check(printable ? byKey == byCode : byKey == null, "printable key and scancode name contract");
                Check(Error() == 0, "key lookup error");
                if (byKey != null) named++;
                keys.Add(new { key, code, name = byKey, scancodeName = byCode });
            }
            Check(named >= 36, "letter and number names available");
            output.Add(keys);
            GC.Collect(); GC.WaitForPendingFinalizers();
            Terminate();
            Check(Error() == 0, "key-name termination");
        }
        Record("x11-key-name-cycles", output);
    }

    [DllImport("libX11.so.6", CallingConvention = CallingConvention.Cdecl)]
    private static extern int XDisplayKeycodes(nint display, int* first, int* last);

    [DllImport("libX11.so.6", CallingConvention = CallingConvention.Cdecl)]
    private static extern int XkbQueryExtension(nint display, int* opcode, int* eventBase, int* errorBase, int* major, int* minor);

    private static partial class N
    {
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        internal static extern nint glfwGetKeyName(int key, int scancode);
    }
}
