using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Marius.Winter.Blazor;

public static class AttributeHelper
{
    public static bool GetBool(object? value, bool defaultValue = false)
    {
        return value switch
        {
            null => defaultValue,
            bool b => b,
            int i => i != 0,
            string s when s == "0" => false,
            string s when s == "1" => true,
            string s => bool.TryParse(s, out var r) ? r : defaultValue,
            _ => defaultValue
        };
    }

    public static int GetInt(object? value, int defaultValue = 0)
    {
        return value switch
        {
            null => defaultValue,
            int i => i,
            float f => (int)f,
            double d => (int)d,
            string s => int.TryParse(s, CultureInfo.InvariantCulture, out var r) ? r : defaultValue,
            _ => defaultValue
        };
    }

    public static float GetFloat(object? value, float defaultValue = 0f)
    {
        return value switch
        {
            null => defaultValue,
            float f => f,
            double d => (float)d,
            int i => i,
            string s => float.TryParse(s, CultureInfo.InvariantCulture, out var r) ? r : defaultValue,
            _ => defaultValue
        };
    }

    public static float? GetNullableFloat(object? value)
    {
        return value switch
        {
            null => null,
            float f => f,
            double d => (float)d,
            int i => i,
            string s => float.TryParse(s, CultureInfo.InvariantCulture, out var r) ? r : null,
            _ => null
        };
    }

    public static T GetEnum<T>(object? value, T defaultValue = default) where T : struct, Enum
    {
        return value switch
        {
            null => defaultValue,
            T e => e,
            int i => Unsafe.As<int, T>(ref i),
            string s => Enum.TryParse<T>(s, out var r) ? r : defaultValue,
            _ => defaultValue
        };
    }

    public static string? GetString(object? value)
    {
        return value switch
        {
            null => null,
            string s => s,
            _ => value.ToString()
        };
    }

    public static T? GetObject<T>(object? value) where T : class
    {
        if (value is T direct) return direct;

        return WeakObjectStore.Get<T>(value);
    }

    public static Color4 GetColor4(object? value, Color4 defaultValue = default)
    {
        if (value is Color4 c) return c;
        if (value is string s && TryParseColor4(s, out var parsed)) return parsed;

        return defaultValue;
    }

    public static Thickness GetThickness(object? value, Thickness defaultValue = default)
    {
        if (value is Thickness t) return t;
        if (value is string s && TryParseThickness(s, out var parsed)) return parsed;

        return defaultValue;
    }

    public static string Color4ToString(Color4 c)
    {
        return FormattableString.Invariant($"{c.R},{c.G},{c.B},{c.A}");
    }

    public static string ThicknessToString(Thickness t)
    {
        return FormattableString.Invariant($"{t.Left},{t.Top},{t.Right},{t.Bottom}");
    }

    public static CornerRadius GetCornerRadius(object? value, CornerRadius defaultValue = default)
    {
        if (value is CornerRadius cr) return cr;
        if (value is string s && TryParseCornerRadius(s, out var parsed)) return parsed;

        return defaultValue;
    }

    public static string CornerRadiusToString(CornerRadius cr)
    {
        return FormattableString.Invariant($"cr:{cr.TopLeft},{cr.TopRight},{cr.BottomRight},{cr.BottomLeft}");
    }

    private static bool TryParseColor4(string s, out Color4 result)
    {
        result = default;
        var parts = s.Split(',');
        if (parts.Length != 4) return false;
        if (!float.TryParse(parts[0], CultureInfo.InvariantCulture, out var r)) return false;
        if (!float.TryParse(parts[1], CultureInfo.InvariantCulture, out var g)) return false;
        if (!float.TryParse(parts[2], CultureInfo.InvariantCulture, out var b)) return false;
        if (!float.TryParse(parts[3], CultureInfo.InvariantCulture, out var a)) return false;

        result = new Color4(r, g, b, a);
        return true;
    }

    private static bool TryParseThickness(string s, out Thickness result)
    {
        result = default;
        var parts = s.Split(',');
        if (parts.Length != 4) return false;
        if (!float.TryParse(parts[0], CultureInfo.InvariantCulture, out var l)) return false;
        if (!float.TryParse(parts[1], CultureInfo.InvariantCulture, out var t)) return false;
        if (!float.TryParse(parts[2], CultureInfo.InvariantCulture, out var r)) return false;
        if (!float.TryParse(parts[3], CultureInfo.InvariantCulture, out var b)) return false;

        result = new Thickness(l, t, r, b);
        return true;
    }

    private static bool TryParseCornerRadius(string s, out CornerRadius result)
    {
        result = default;
        if (!s.StartsWith("cr:")) return false;

        var parts = s.AsSpan(3).ToString().Split(',');
        if (parts.Length != 4) return false;
        if (!float.TryParse(parts[0], CultureInfo.InvariantCulture, out var tl)) return false;
        if (!float.TryParse(parts[1], CultureInfo.InvariantCulture, out var tr)) return false;
        if (!float.TryParse(parts[2], CultureInfo.InvariantCulture, out var br)) return false;
        if (!float.TryParse(parts[3], CultureInfo.InvariantCulture, out var bl)) return false;

        result = new CornerRadius(tl, tr, br, bl);
        return true;
    }

    // --- byte[] ---

    private const string Base64Prefix = "b64:";

    public static string ByteArrayToString(byte[] bytes)
    {
        return Base64Prefix + Convert.ToBase64String(bytes);
    }

    public static byte[]? GetByteArray(object? value)
    {
        if (value is byte[] direct) return direct;
        if (value is string s && s.StartsWith(Base64Prefix))
            return Convert.FromBase64String(s.Substring(Base64Prefix.Length));

        return null;
    }

    // --- TrackSize[] ---

    public static string TrackSizeArrayToString(TrackSize[] tracks)
    {
        var parts = new string[tracks.Length];
        for (int i = 0; i < tracks.Length; i++)
        {
            parts[i] = tracks[i].Type switch
            {
                TrackSize.TrackType.Fixed => FormattableString.Invariant($"{tracks[i].Value}px"),
                TrackSize.TrackType.Auto => "auto",
                TrackSize.TrackType.Fraction => FormattableString.Invariant($"{tracks[i].Value}fr"),
                _ => "auto"
            };
        }

        return string.Join(",", parts);
    }

    public static TrackSize[]? GetTrackSizeArray(object? value)
    {
        if (value is TrackSize[] direct) return direct;
        if (value is not string s || s.Length == 0) return null;

        var parts = s.Split(',');
        var result = new TrackSize[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i].Trim();
            if (p == "auto")
                result[i] = TrackSize.Auto();
            else if (p.EndsWith("fr") && float.TryParse(p.AsSpan(0, p.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out var fr))
                result[i] = TrackSize.Fr(fr);
            else if (p.EndsWith("px") && float.TryParse(p.AsSpan(0, p.Length - 2), NumberStyles.Float, CultureInfo.InvariantCulture, out var px))
                result[i] = TrackSize.Px(px);
            else
                result[i] = TrackSize.Auto();
        }

        return result;
    }

    // --- string[] ---

    private const char StringArraySep = '\x1F'; // Unit separator

    public static string StringArrayToString(string[] strings)
    {
        return string.Join(StringArraySep, strings);
    }

    public static string[]? GetStringArray(object? value)
    {
        if (value is string[] direct) return direct;
        if (value is string s)
            return s.Split(StringArraySep);

        return null;
    }
}