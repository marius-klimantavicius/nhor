// Ported from ThorVG/src/loaders/svg/tvgSvgPath.h and tvgSvgPath.cpp
// SVG path "d" attribute parser.

using System;

namespace ThorVG
{
    public static class SvgPath
    {
        private static int SkipComma(string content, int pos)
        {
            pos = SvgUtil.SkipWhiteSpace(content, pos, -1);
            if (pos < content.Length && content[pos] == ',') return pos + 1;
            return pos;
        }

        private static bool ParseNumber(string content, ref int pos, out float number)
        {
            number = TvgStr.ToFloat(content, ref pos);
            // The index is updated by ToFloat; if it did not move, no number was parsed
            return true; // ToFloat returns 0 if no number; we check differently
        }

        private static bool TryParseNumber(string content, ref int pos, out float number)
        {
            int startPos = pos;
            number = TvgStr.ToFloat(content, ref pos);
            if (pos == startPos) return false;
            pos = SkipComma(content, pos);
            return true;
        }

        private static bool TryParseFlag(string content, ref int pos, out int flag)
        {
            flag = 0;
            if (pos >= content.Length) return false;
            if (content[pos] != '0' && content[pos] != '1') return false;
            flag = content[pos] - '0';
            pos++;
            pos = SkipComma(content, pos);
            return true;
        }

        private static void PathAppendArcTo(RenderPath path, ref Point cur, ref Point curCtl,
            in Point next, Point radius, float angle, bool largeArc, bool sweep)
        {
            var start = cur;
            var cosPhi = MathF.Cos(angle);
            var sinPhi = MathF.Sin(angle);
            var dx = (start.x - next.x) * 0.5f;
            var dy = (start.y - next.y) * 0.5f;
            var x1p = cosPhi * dx + sinPhi * dy;
            var y1p = cosPhi * dy - sinPhi * dx;
            var x1p2 = x1p * x1p;
            var y1p2 = y1p * y1p;
            var rx = radius.x;
            var ry = radius.y;
            var rx2 = rx * rx;
            var ry2 = ry * ry;
            var lambda = (x1p2 / rx2) + (y1p2 / ry2);

            if (lambda > 1.0f)
            {
                var sqrtLambda = MathF.Sqrt(lambda);
                rx *= sqrtLambda;
                ry *= sqrtLambda;
                rx2 = rx * rx;
                ry2 = ry * ry;
            }

            var c = (rx2 * ry2) - (rx2 * y1p2) - (ry2 * x1p2);
            float cpx, cpy, centerX, centerY;

            if (c < 0.0f)
            {
                rx *= MathF.Sqrt(1.0f - c / (rx2 * ry2));
                ry *= MathF.Sqrt(1.0f - c / (rx2 * ry2));
                rx2 = rx * rx;
                ry2 = ry * ry;
                cpx = 0.0f; cpy = 0.0f;
                centerX = 0.0f; centerY = 0.0f;
            }
            else
            {
                c = MathF.Sqrt(c / ((rx2 * y1p2) + (ry2 * x1p2)));
                if (largeArc == sweep) c = -c;
                cpx = c * (rx * y1p / ry);
                cpy = c * (-ry * x1p / rx);
                centerX = cosPhi * cpx - sinPhi * cpy;
                centerY = sinPhi * cpx + cosPhi * cpy;
            }

            centerX += (start.x + next.x) * 0.5f;
            centerY += (start.y + next.y) * 0.5f;

            var at = TvgMath.Atan2(((y1p - cpy) / ry), ((x1p - cpx) / rx));
            var theta1 = (at < 0.0f) ? 2.0f * MathConstants.MATH_PI + at : at;
            var nat = TvgMath.Atan2(((-y1p - cpy) / ry), ((-x1p - cpx) / rx));
            var deltaTheta = (nat < at) ? 2.0f * MathConstants.MATH_PI - at + nat : nat - at;

            if (sweep)
            {
                if (deltaTheta < 0.0f) deltaTheta += 2.0f * MathConstants.MATH_PI;
            }
            else
            {
                if (deltaTheta > 0.0f) deltaTheta -= 2.0f * MathConstants.MATH_PI;
            }

            var segments = (int)(MathF.Abs(deltaTheta / MathConstants.MATH_PI2) + 1.0f);
            var delta = deltaTheta / segments;
            var bcp = 4.0f / 3.0f * (1.0f - MathF.Cos(delta / 2.0f)) / MathF.Sin(delta / 2.0f);
            var cosPhiRx = cosPhi * rx;
            var cosPhiRy = cosPhi * ry;
            var sinPhiRx = sinPhi * rx;
            var sinPhiRy = sinPhi * ry;
            var cosTheta1 = MathF.Cos(theta1);
            var sinTheta1 = MathF.Sin(theta1);

            for (int i = 0; i < segments; ++i)
            {
                var theta2 = theta1 + delta;
                var cosTheta2 = MathF.Cos(theta2);
                var sinTheta2 = MathF.Sin(theta2);

                var c1 = new Point(
                    start.x + (-bcp * (cosPhiRx * sinTheta1 + sinPhiRy * cosTheta1)),
                    start.y + (bcp * (cosPhiRy * cosTheta1 - sinPhiRx * sinTheta1)));

                var e = new Point(
                    centerX + cosPhiRx * cosTheta2 - sinPhiRy * sinTheta2,
                    centerY + sinPhiRx * cosTheta2 + cosPhiRy * sinTheta2);

                curCtl = new Point(
                    e.x + bcp * (cosPhiRx * sinTheta2 + sinPhiRy * cosTheta2),
                    e.y + bcp * (sinPhiRx * sinTheta2 - cosPhiRy * cosTheta2));

                cur = e;
                path.CubicTo(c1, curCtl, cur);

                start = e;
                theta1 = theta2;
                cosTheta1 = cosTheta2;
                sinTheta1 = sinTheta2;
            }
        }

        private static int NumberCount(char cmd)
        {
            switch (cmd)
            {
                case 'M': case 'm':
                case 'L': case 'l':
                case 'T': case 't':
                    return 2;
                case 'C': case 'c':
                case 'E': case 'e':
                    return 6;
                case 'H': case 'h':
                case 'V': case 'v':
                    return 1;
                case 'S': case 's':
                case 'Q': case 'q':
                    return 4;
                case 'A': case 'a':
                    return 7;
                default:
                    return 0;
            }
        }

        private static bool ProcessCommand(RenderPath path, char cmd, float[] arr, int count,
            ref Point cur, ref Point curCtl, ref Point start, ref bool quadratic, ref bool closed)
        {
            switch (cmd)
            {
                case 'm': case 'l': case 'c': case 's': case 'q': case 't':
                    for (int i = 0; i < count - 1; i += 2) { arr[i] += cur.x; arr[i + 1] += cur.y; }
                    break;
                case 'h': arr[0] += cur.x; break;
                case 'v': arr[0] += cur.y; break;
                case 'a': arr[5] += cur.x; arr[6] += cur.y; break;
            }

            switch (cmd)
            {
                case 'm': case 'M':
                    start = cur = new Point(arr[0], arr[1]);
                    path.MoveTo(cur);
                    break;
                case 'l': case 'L':
                    cur = new Point(arr[0], arr[1]);
                    path.LineTo(cur);
                    break;
                case 'c': case 'C':
                    curCtl = new Point(arr[2], arr[3]);
                    cur = new Point(arr[4], arr[5]);
                    path.CubicTo(new Point(arr[0], arr[1]), curCtl, cur);
                    quadratic = false;
                    break;
                case 's': case 'S':
                {
                    Point ctrl;
                    if (path.cmds.count > 1 && path.cmds.Last() == PathCommand.CubicTo && !quadratic)
                        ctrl = new Point(2 * cur.x - curCtl.x, 2 * cur.y - curCtl.y);
                    else
                        ctrl = cur;
                    curCtl = new Point(arr[0], arr[1]);
                    cur = new Point(arr[2], arr[3]);
                    path.CubicTo(ctrl, curCtl, cur);
                    quadratic = false;
                    break;
                }
                case 'q': case 'Q':
                {
                    var qx = arr[0]; var qy = arr[1];
                    var ctrl1 = new Point((cur.x + 2 * qx) / 3.0f, (cur.y + 2 * qy) / 3.0f);
                    var ctrl2 = new Point((arr[2] + 2 * qx) / 3.0f, (arr[3] + 2 * qy) / 3.0f);
                    curCtl = new Point(qx, qy);
                    cur = new Point(arr[2], arr[3]);
                    path.CubicTo(ctrl1, ctrl2, cur);
                    quadratic = true;
                    break;
                }
                case 't': case 'T':
                {
                    Point ctrl;
                    if (path.cmds.count > 1 && path.cmds.Last() == PathCommand.CubicTo && quadratic)
                        ctrl = new Point(2 * cur.x - curCtl.x, 2 * cur.y - curCtl.y);
                    else
                        ctrl = cur;
                    var ctrl1 = new Point((cur.x + 2 * ctrl.x) / 3.0f, (cur.y + 2 * ctrl.y) / 3.0f);
                    var ctrl2 = new Point((arr[0] + 2 * ctrl.x) / 3.0f, (arr[1] + 2 * ctrl.y) / 3.0f);
                    curCtl = ctrl;
                    cur = new Point(arr[0], arr[1]);
                    path.CubicTo(ctrl1, ctrl2, cur);
                    quadratic = true;
                    break;
                }
                case 'h': case 'H':
                    path.LineTo(new Point(arr[0], cur.y));
                    cur.x = arr[0];
                    break;
                case 'v': case 'V':
                    path.LineTo(new Point(cur.x, arr[0]));
                    cur.y = arr[0];
                    break;
                case 'z': case 'Z':
                    path.Close();
                    cur = start;
                    closed = true;
                    break;
                case 'a': case 'A':
                    if (TvgMath.Zero(arr[0]) || TvgMath.Zero(arr[1]))
                    {
                        cur = new Point(arr[5], arr[6]);
                        path.LineTo(cur);
                    }
                    else if (!TvgMath.Equal(cur.x, arr[5]) || !TvgMath.Equal(cur.y, arr[6]))
                    {
                        PathAppendArcTo(path, ref cur, ref curCtl,
                            new Point(arr[5], arr[6]),
                            new Point(MathF.Abs(arr[0]), MathF.Abs(arr[1])),
                            TvgMath.Deg2Rad(arr[2]),
                            arr[3] != 0.0f, arr[4] != 0.0f);
                        cur = curCtl = new Point(arr[5], arr[6]);
                        quadratic = false;
                    }
                    break;
                default: return false;
            }
            return true;
        }

        private static bool NextCommand(string path, ref int pos, ref char cmd, float[] arr, ref int count, ref bool closed)
        {
            pos = SkipComma(path, pos);
            if (pos >= path.Length) return false;

            if (char.IsLetter(path[pos]))
            {
                cmd = path[pos];
                pos++;
                count = NumberCount(cmd);
            }
            else
            {
                if (cmd == 'm') cmd = 'l';
                else if (cmd == 'M') cmd = 'L';
                else if (closed) return false;
            }

            if (count == 7)
            {
                // Special case for arc command
                if (TryParseNumber(path, ref pos, out arr[0]) &&
                    TryParseNumber(path, ref pos, out arr[1]) &&
                    TryParseNumber(path, ref pos, out arr[2]) &&
                    TryParseFlag(path, ref pos, out int large) &&
                    TryParseFlag(path, ref pos, out int sweep) &&
                    TryParseNumber(path, ref pos, out arr[5]) &&
                    TryParseNumber(path, ref pos, out arr[6]))
                {
                    arr[3] = large;
                    arr[4] = sweep;
                    return true;
                }
                count = 0;
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                if (!TryParseNumber(path, ref pos, out arr[i]))
                {
                    count = 0;
                    return false;
                }
                pos = SkipComma(path, pos);
            }
            return true;
        }

        public static bool ToShape(string svgPath, RenderPath output)
        {
            var arr = new float[7];
            int count = 0;
            var cur = new Point(0, 0);
            var curCtl = new Point(0, 0);
            var start = new Point(0, 0);
            char cmd = '\0';
            int pos = 0;
            var lastCmds = output.cmds.count;
            var isQuadratic = false;
            var closed = false;

            while (pos < svgPath.Length)
            {
                if (!NextCommand(svgPath, ref pos, ref cmd, arr, ref count, ref closed)) break;
                closed = false;
                if (!ProcessCommand(output, cmd, arr, count, ref cur, ref curCtl, ref start, ref isQuadratic, ref closed)) break;
            }

            if (output.cmds.count > lastCmds)
            {
                unsafe
                {
                    if (output.cmds.data[lastCmds] != PathCommand.MoveTo) return false;
                }
            }
            return true;
        }
    }
}
