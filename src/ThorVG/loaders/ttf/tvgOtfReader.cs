// Ported from ThorVG/src/loaders/sfnt/tvgOtfReader.{h,cpp} at
// 64d2d589d909f48b6d70b341ce4fe34130e26d68.
// Copyright (c) 2026 ThorVG project. MIT license (ref/ThorVG/LICENSE).

using System;
using System.Collections.Generic;

namespace ThorVG
{
    /// <summary>CFF1 / Type 2 outline reader, sharing the SFNT tables with TtfReader.</summary>
    public sealed class OtfReader : TtfReader
    {
        private const int SUBROUTINE_MAX = 10;
        private uint cff, toCharStrings, gsubrs, subrs, fdArray, fdSelect;

        private struct SubRoutine { public uint p, end; }
        private readonly struct Index
        {
            public readonly uint count, offsets, data, end;
            public readonly byte offSize;
            public Index(uint count, uint offsets, uint data, uint end, byte offSize)
            {
                this.count = count; this.offsets = offsets; this.data = data; this.end = end; this.offSize = offSize;
            }
        }

        // Bounds failures are input errors, rather than managed indexing exceptions.
        private sealed class InvalidFont : FormatException { }
        private uint At(uint origin, long offset)
        {
            long value = origin + offset;
            if (value < 0 || value > size) throw new InvalidFont();
            return (uint)value;
        }
        private byte B8(uint at) { if (!Validate(at, 1)) throw new InvalidFont(); return data![at]; }
        private ushort B16(uint at) { if (!Validate(at, 2)) throw new InvalidFont(); return U16(data!, at); }
        private uint B32(uint at) { if (!Validate(at, 4)) throw new InvalidFont(); return U32(data!, at); }
        private static T Pick<T>(List<T> args)
        {
            if (args.Count == 0) throw new InvalidFont();
            T value = args[^1]; args.RemoveAt(args.Count - 1); return value;
        }
        private static int Last(List<int> args)
        {
            if (args.Count == 0) throw new InvalidFont();
            return args[^1];
        }

        private int Offset(uint origin, byte bytes)
        {
            if (bytes < 1 || bytes > 4 || !Validate(origin, bytes)) throw new InvalidFont();
            uint value = 0;
            for (byte i = 0; i < bytes; ++i) value = (value << 8) | B8(origin++);
            if (value > int.MaxValue) throw new InvalidFont();
            return (int)value;
        }

        private Index ReadIndex(uint idx)
        {
            uint count = B16(idx);
            if (count == 0) return new Index(0, 0, 0, At(idx, 2), 0);
            byte bytes = B8(At(idx, 2));
            uint offsets = At(idx, 3);
            if (bytes < 1 || bytes > 4 || !Validate(offsets, (count + 1) * bytes)) throw new InvalidFont();
            uint start = At(offsets, (count + 1) * bytes);
            int last = Offset(At(offsets, count * bytes), bytes);
            if (last < 1) throw new InvalidFont();
            return new Index(count, offsets, start, At(start, last - 1), bytes);
        }

        private void ObjectRange(in Index index, uint number, out uint start, out uint end)
        {
            if (number >= index.count) throw new InvalidFont();
            int off1 = Offset(At(index.offsets, number * index.offSize), index.offSize);
            int off2 = Offset(At(index.offsets, (number + 1) * index.offSize), index.offSize);
            if (off1 < 1 || off2 < off1) throw new InvalidFont();
            start = At(index.data, off1 - 1);
            end = At(index.data, off2 - 1);
            if (end > index.end) throw new InvalidFont();
        }

        private uint Skip(uint index) => ReadIndex(index).end;

        // Upstream's diagnostic Name INDEX helper; not used during loading.
        private bool Name(uint idx)
        {
            var index = ReadIndex(idx);
            if (index.count == 0) return false;
            for (uint i = 0; i < index.count; ++i)
            {
                ObjectRange(index, i, out uint start, out uint end);
                Console.Write("Name[{0}]: ", i);
                while (start < end) Console.Write((char)B8(start++));
                Console.WriteLine();
            }
            return true;
        }

        private bool DICT(List<int> args, byte b, ref uint ptr)
        {
            if (b >= 32)
            {
                int value;
                if (b <= 246) { value = b - 139; ptr = At(ptr, 1); }
                else if (b <= 250) { value = (b - 247) * 256 + B8(At(ptr, 1)) + 108; ptr = At(ptr, 2); }
                else if (b <= 254) { value = -(b - 251) * 256 - B8(At(ptr, 1)) - 108; ptr = At(ptr, 2); }
                else { value = unchecked((int)B32(At(ptr, 1))); ptr = At(ptr, 5); }
                args.Add(value);
                return true;
            }
            if (b == 28) { args.Add(unchecked((short)B16(At(ptr, 1)))); ptr = At(ptr, 3); return true; }
            if (b == 29) { args.Add(unchecked((int)B32(At(ptr, 1)))); ptr = At(ptr, 5); return true; }
            if (b == 12) { ptr = At(ptr, 2); return true; }
            return false;
        }

        private uint LocalSubrs(List<int> args, int offset, int length)
        {
            args.Clear();
            uint origin = At(cff, offset);
            uint end = At(origin, length);
            for (uint ptr = origin; ptr < end;)
            {
                byte b = B8(ptr);
                if (DICT(args, b, ref ptr)) continue;
                if (b == 19 && args.Count > 0) return At(origin, Last(args));
                ++ptr;
                args.Clear();
            }
            return 0;
        }

        private uint FdSubrs(uint glyph)
        {
            int fd = -1;
            byte format = B8(fdSelect);
            if (format == 0) fd = B8(At(fdSelect, 1L + glyph));
            else if (format == 3)
            {
                int count = B16(At(fdSelect, 1));
                uint ranges = At(fdSelect, 3);
                for (int i = 0; i < count; ++i)
                {
                    uint range = At(ranges, i * 3L);
                    ushort first = B16(range), next = B16(At(range, 3));
                    byte rangeFd = B8(At(range, 2));
                    if (glyph >= first && glyph < next) { fd = rangeFd; break; }
                }
            }
            else return 0;

            var index = ReadIndex(fdArray);
            if (fd < 0 || fd >= index.count) return 0;
            ObjectRange(index, (uint)fd, out uint ptr, out uint end);
            var args = new List<int>(32);
            while (ptr < end)
            {
                byte b = B8(ptr);
                if (b == 12) { ptr = At(ptr, 2); args.Clear(); continue; }
                if (DICT(args, b, ref ptr)) continue;
                if (b == 18 && args.Count >= 2)
                {
                    int privateSize = args[^2], privateOffset = args[^1];
                    return LocalSubrs(args, privateOffset, privateSize);
                }
                ++ptr;
                args.Clear();
            }
            return 0;
        }

        private Result CFF()
        {
            cff = Table("CFF ");
            if (cff == 0 || !Validate(cff, 4))
                return Table("CFF2") != 0 ? Result.NonSupport : Result.InvalidArguments;
            uint p = At(cff, B8(At(cff, 2)));
            p = Skip(p); // Name INDEX
            var top = ReadIndex(p);
            if (top.count == 0) return Result.InvalidArguments;
            ObjectRange(top, 0, out uint ptr, out uint end);
            var args = new List<int>(32);
            while (ptr < end)
            {
                byte b = B8(ptr);
                if (b == 12)
                {
                    byte op = B8(At(ptr, 1));
                    if (op == 36) fdArray = At(cff, Last(args));
                    else if (op == 37) fdSelect = At(cff, Last(args));
                    ptr = At(ptr, 2); args.Clear(); continue;
                }
                if (DICT(args, b, ref ptr)) continue;
                if (b == 17) toCharStrings = (uint)Last(args);
                else if (b == 18 && args.Count >= 2)
                    subrs = LocalSubrs(args, args[^1], args[^2]);
                ++ptr; args.Clear();
            }
            if (toCharStrings == 0 || toCharStrings > size) return Result.InvalidArguments;
            toCharStrings = At(cff, toCharStrings);
            p = Skip(p); // end of Top DICT INDEX
            p = Skip(p); // end of String INDEX
            gsubrs = p;
            return Result.Success;
        }

        private bool SubRoutineCall(float operand, uint indexOffset, ref uint p, ref uint end, Span<SubRoutine> stack, ref int srp)
        {
            if (srp >= SUBROUTINE_MAX) return false;
            var index = ReadIndex(indexOffset);
            if (index.count == 0) return false;
            int bias = index.count < 1240 ? 107 : index.count < 33900 ? 1131 : 32768;
            int number = (int)MathF.Round(operand, MidpointRounding.AwayFromZero) + bias;
            if (number < 0 || number >= index.count) return false;
            ObjectRange(index, (uint)number, out uint next, out uint nextEnd);
            if (nextEnd <= next) return false;
            stack[srp++] = new SubRoutine { p = p, end = end };
            p = next; end = nextEnd;
            return true;
        }

        private static Point Add(Point a, float x, float y) => new Point(a.x + x, a.y + y);

        private static void AlternatingCurve(List<float> v, ref Point pos, RenderPath path, bool horizontal)
        {
            bool last = false;
            for (int i = 0; i + 3 < v.Count; i += last ? 5 : 4)
            {
                last = v.Count - i == 5;
                Point cp1, cp2;
                if (horizontal)
                {
                    cp1 = Add(pos, v[i], 0); cp2 = Add(cp1, v[i + 1], -v[i + 2]);
                    pos = Add(cp2, last ? v[i + 4] : 0, -v[i + 3]);
                }
                else
                {
                    cp1 = Add(pos, 0, -v[i]); cp2 = Add(cp1, v[i + 1], -v[i + 2]);
                    pos = Add(cp2, v[i + 3], last ? -v[i + 4] : 0);
                }
                path.CubicTo(cp1, cp2, pos);
                horizontal = !horizontal;
            }
        }

        private static void AlignedCurve(List<float> v, ref Point pos, RenderPath path, bool horizontal)
        {
            int i = 0;
            float d = v.Count % 4 == 1 ? v[i++] : 0;
            for (; i + 3 < v.Count; i += 4)
            {
                Point cp1 = horizontal ? Add(pos, v[i], -d) : Add(pos, d, -v[i]);
                Point cp2 = Add(cp1, v[i + 1], -v[i + 2]);
                pos = horizontal ? Add(cp2, v[i + 3], 0) : Add(cp2, 0, -v[i + 3]);
                path.CubicTo(cp1, cp2, pos);
                d = 0;
            }
        }

        private static void RrCurveTo(List<float> v, ref Point pos, RenderPath path)
        {
            for (int i = 0; i + 5 < v.Count; i += 6)
            {
                Point cp1 = Add(pos, v[i], -v[i + 1]), cp2 = Add(cp1, v[i + 2], -v[i + 3]);
                pos = Add(cp2, v[i + 4], -v[i + 5]);
                path.CubicTo(cp1, cp2, pos);
            }
        }

        private static void RCurveLine(List<float> v, ref Point pos, RenderPath path)
        {
            if (v.Count < 8) return;
            RrCurveTo(v, ref pos, path);
            pos = Add(pos, v[^2], -v[^1]); path.LineTo(pos);
        }

        private static void RLineCurve(List<float> v, ref Point pos, RenderPath path)
        {
            if (v.Count < 6) throw new InvalidFont();
            for (int i = 0; i + 6 < v.Count; i += 2) { pos = Add(pos, v[i], -v[i + 1]); path.LineTo(pos); }
            int start = v.Count - 6;
            Point cp1 = Add(pos, v[start], -v[start + 1]), cp2 = Add(cp1, v[start + 2], -v[start + 3]);
            pos = Add(cp2, v[start + 4], -v[start + 5]); path.CubicTo(cp1, cp2, pos);
        }

        private bool Operand(List<float> values, byte b, ref uint p)
        {
            if (b >= 32)
            {
                if (b <= 246) { values.Add(b - 139); p = At(p, 1); }
                else if (b <= 250) { values.Add((b - 247) * 256 + B8(At(p, 1)) + 108); p = At(p, 2); }
                else if (b <= 254) { values.Add(-(b - 251) * 256 - B8(At(p, 1)) - 108); p = At(p, 2); }
                else { values.Add(unchecked((int)B32(At(p, 1))) / 65536.0f); p = At(p, 5); }
            }
            else if (b == 28) { values.Add(unchecked((short)B16(At(p, 1)))); p = At(p, 3); }
            else return false; // pinned upstream rejects other CharString opcodes
            return true;
        }

        private bool CharStrings(RenderPath path, uint glyph)
        {
            var index = ReadIndex(toCharStrings);
            if (glyph >= index.count) return false;
            if (fdSelect != 0 && fdArray != 0) subrs = FdSubrs(glyph);
            ObjectRange(index, glyph, out uint p, out uint end);
            if (end <= p) return false;
            Point pos = default;
            Span<SubRoutine> stack = stackalloc SubRoutine[SUBROUTINE_MAX];
            int srp = 0, stems = 0;
            var values = new List<float>(40);
            while (p < end)
            {
                byte b = B8(p);
                switch (b)
                {
                    case 1: case 3: case 18: case 23:
                        // Upstream counts CFF stems to size masks; it does not hint CFF outlines.
                        stems += values.Count / 2; break;
                    case 4: pos.y -= Pick(values); path.MoveTo(pos); break;
                    case 5:
                        for (int i = 0; i + 1 < values.Count; i += 2) { pos = Add(pos, values[i], -values[i + 1]); path.LineTo(pos); }
                        break;
                    case 6: case 7:
                        for (int i = 0; i < values.Count; ++i)
                        {
                            if ((i % 2 == 0) == (b == 6)) pos.x += values[i]; else pos.y -= values[i];
                            path.LineTo(pos);
                        }
                        break;
                    case 8: RrCurveTo(values, ref pos, path); break;
                    case 10: case 29:
                        if (SubRoutineCall(Pick(values), b == 10 ? subrs : gsubrs, ref p, ref end, stack, ref srp)) continue;
                        return false;
                    case 11:
                        if (srp <= 0) return false;
                        var state = stack[--srp]; p = At(state.p, 1); end = state.end; continue;
                    case 12:
                        // Exact upstream limitation: escaped CharString operators are skipped.
                        // tvgOtfReader.cpp: charStrings(), case 12. No flex/arithmetic support is claimed.
                        values.Clear(); p = At(p, 2); continue;
                    case 14: path.Close(); break;
                    case 19: case 20:
                        stems += values.Count / 2;
                        p = At(p, 1L + (stems + 7) / 8); values.Clear(); continue;
                    case 21: pos.y -= Pick(values); pos.x += Pick(values); path.MoveTo(pos); break;
                    case 22: pos.x += Pick(values); path.MoveTo(pos); break;
                    case 24: RCurveLine(values, ref pos, path); break;
                    case 25: RLineCurve(values, ref pos, path); break;
                    case 26: case 27: AlignedCurve(values, ref pos, path, b == 27); break;
                    case 30: case 31: AlternatingCurve(values, ref pos, path, b == 31); break;
                    default: if (Operand(values, b, ref p)) continue; return false;
                }
                values.Clear(); ++p;
            }
            return true;
        }

        public override Result HeaderResult()
        {
            try { return HeaderSfnt() ? CFF() : Result.InvalidArguments; }
            catch (InvalidFont) { return Result.InvalidArguments; }
        }
        public override bool Header() => HeaderResult() == Result.Success;

        public override uint Glyph(uint codepoint, TtfGlyphMetrics glyph)
        {
            glyph.idx = Glyph(codepoint);
            if (glyph.idx == INVALID_GLYPH) return 0;
            return GlyphMetrics(glyph);
        }

        public override uint GlyphMetrics(TtfGlyphMetrics glyph) => ReadHorizontalMetrics(glyph) ? 1u : 0u;

        public override bool Convert(RenderPath path, TtfGlyphMetrics glyph, uint glyphOffset, in Point offset, ushort depth)
        {
            if (glyph.idx == INVALID_GLYPH || glyphOffset == 0) return false;
            try { return CharStrings(path, glyph.idx); }
            catch (InvalidFont) { return false; }
        }

        // Source-backed unsupported path: pinned OtfReader::positioning() returns false.
        public override bool Kerning(uint lglyph, uint rglyph, ref Point output) => false;
    }
}
