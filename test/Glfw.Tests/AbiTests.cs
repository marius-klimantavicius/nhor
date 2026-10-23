using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using G = Glfw.Glfw;

internal static unsafe partial class Program
{
    private static void ValidateAbi(bool calculate32 = false)
    {
        string path = Environment.GetEnvironmentVariable("GLFW_ABI_JSON") ?? throw new Exception("GLFW_ABI_JSON required");
        using var nativeAbi = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var property in nativeAbi.RootElement.EnumerateObject())
        {
            if (property.Name == "end") continue;
            var parts = property.Name.Split('.');
            Type type = (typeof(G).Assembly.GetType("Glfw." + parts[0]) ?? typeof(G).GetNestedType(parts[0], BindingFlags.NonPublic))!;
            int expected = property.Value.GetInt32();
            int actual = calculate32 ? (parts[1] == "__sizeof" ? Layout32(type).Size : Layout32(type).Offsets[parts[1]]) : parts[1] == "__sizeof" ? Marshal.SizeOf(type) : (int)Marshal.OffsetOf(type, parts[1]);
            Check(expected == actual, $"{property.Name}: native={expected} managed={actual}");
        }
        Record("abi-layout-assertions", assertions);
        if (calculate32) Record("abi-evidence", "static managed 32-bit layout calculation against cross-compiled Windows headers; no Windows execution");
    }
    private sealed record Layout(int Size, int Alignment, Dictionary<string, int> Offsets);
    private static readonly Dictionary<Type, Layout> LayoutCache = new();
    private static Layout Layout32(Type type)
    {
        if (type.IsEnum) return Layout32(Enum.GetUnderlyingType(type));
        int primitive = type.IsPointer || type.IsFunctionPointer || type == typeof(nint) || type == typeof(nuint) ? 4 :
            type == typeof(byte) || type == typeof(sbyte) ? 1 : type == typeof(char) || type == typeof(short) || type == typeof(ushort) ? 2 :
            type == typeof(int) || type == typeof(uint) || type == typeof(float) || type == typeof(bool) ? 4 : type == typeof(long) || type == typeof(ulong) || type == typeof(double) ? 8 : 0;
        if (primitive != 0) return new Layout(primitive, primitive, new());
        if (LayoutCache.TryGetValue(type, out var cached)) return cached;
        var attribute = type.StructLayoutAttribute!;
        int pack = attribute.Pack == 0 ? 8 : attribute.Pack;
        int size = 0, alignment = 1;
        var offsets = new Dictionary<string, int>();
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var fieldLayout = Layout32(field.FieldType);
            int fieldAlign = Math.Min(pack, fieldLayout.Alignment);
            int offset = attribute.Value == LayoutKind.Explicit ? field.GetCustomAttribute<FieldOffsetAttribute>()!.Value : (size + fieldAlign - 1) / fieldAlign * fieldAlign;
            offsets[field.Name] = offset; size = Math.Max(size, offset + fieldLayout.Size); alignment = Math.Max(alignment, fieldAlign);
        }
        var inline = type.GetCustomAttribute<InlineArrayAttribute>();
        if (inline != null) size *= inline.Length;
        size = Math.Max(attribute.Size, (size + alignment - 1) / alignment * alignment);
        return LayoutCache[type] = new Layout(size, alignment, offsets);
    }
}
