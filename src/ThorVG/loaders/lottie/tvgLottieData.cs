// Ported from ThorVG/src/loaders/lottie/tvgLottieData.h

using System;
using System.Runtime.CompilerServices;

namespace ThorVG
{
    public struct PathSet
    {
        public Point[] pts;
        public PathCommand[] cmds;
        public ushort ptsCnt;
        public ushort cmdsCnt;
    }

    public struct RGB32
    {
        public int r, g, b;

        public RGB32(int r, int g, int b)
        {
            this.r = r; this.g = g; this.b = b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RGB32 operator -(RGB32 lhs, RGB32 rhs)
        {
            return new RGB32(lhs.r - rhs.r, lhs.g - rhs.g, lhs.b - rhs.b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RGB32 operator +(RGB32 lhs, RGB32 rhs)
        {
            return new RGB32(lhs.r + rhs.r, lhs.g + rhs.g, lhs.b + rhs.b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RGB32 operator *(RGB32 lhs, float rhs)
        {
            return new RGB32(
                (int)MathF.Round(lhs.r * rhs),
                (int)MathF.Round(lhs.g * rhs),
                (int)MathF.Round(lhs.b * rhs));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RGB32 Lerp(in RGB32 s, in RGB32 e, float t)
        {
            return new RGB32(
                Math.Clamp((int)TvgMath.MultiplyAdd(e.r - s.r, t, s.r), 0, 255),
                Math.Clamp((int)TvgMath.MultiplyAdd(e.g - s.g, t, s.g), 0, 255),
                Math.Clamp((int)TvgMath.MultiplyAdd(e.b - s.b, t, s.b), 0, 255));
        }
    }

    public class ColorStop
    {
        public Fill.ColorStop[]? data;
        public Array<float>? input;

        public void Copy(ColorStop rhs, uint cnt)
        {
            if (rhs.data != null)
            {
                data = new Fill.ColorStop[cnt];
                System.Array.Copy(rhs.data, data, cnt);
            }
            if (rhs.input != null)
            {
                TvgCommon.TVGERR("LOTTIE", "Must be populated!");
            }
        }
    }

    public class TextDocument
    {
        public string? text;
        private string? cachedText;
        private byte[] cachedUtf8 = System.Array.Empty<byte>();
        internal byte[] Utf8
        {
            get
            {
                if (!ReferenceEquals(cachedText, text))
                {
                    cachedUtf8 = LottieDataHelper.Utf8CString(text);
                    cachedText = text;
                }
                return cachedUtf8;
            }
        }
        public float height;
        public float shift;
        public RGB32 color;
        public Point bboxPos;
        public Point bboxSize;
        public RGB32 strokeColor;
        public float strokeWidth;
        public bool strokeBelow;
        public string? name;
        public float size;
        public float tracking;
        public float justify;    // horizontal alignment
        public byte caps;        // 0: Regular, 1: AllCaps, 2: SmallCaps

        public TextDocument Clone() => (TextDocument)MemberwiseClone();

        public void Copy(TextDocument rhs)
        {
            text = rhs.text;
            name = rhs.name;
        }
    }

    public static class LottieDataHelper
    {
        internal static byte[] Utf8CString(string? value)
        {
            if (string.IsNullOrEmpty(value)) return System.Array.Empty<byte>();
            int end = value.IndexOf('\0');
            return TvgUtf8.Encode(end < 0 ? value : TvgUtf8.Slice(value, 0, end));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int REMAP255(float val)
        {
            return (int)MathF.Round(val * 255.0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte LerpByte(byte start, byte end, float t)
        {
            return TvgMath.Lerp(start, end, t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LerpInt(int start, int end, float t)
        {
            return (int)MathF.Round(start + (end - start) * t);
        }
    }
}
