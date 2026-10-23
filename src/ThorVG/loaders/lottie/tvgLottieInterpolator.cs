// Ported from ThorVG/src/loaders/lottie/tvgLottieInterpolator.h and tvgLottieInterpolator.cpp
// The upstream interpolation implementation is also covered by MPL-2.0:
// https://mozilla.org/MPL/2.0/

using System;
using System.Text;

namespace ThorVG
{
    public class LottieInterpolator
    {
        private const int SPLINE_TABLE_SIZE = 11;
        private const float SAMPLE_STEP_SIZE = 1.0f / (SPLINE_TABLE_SIZE - 1);
        private const float NEWTON_MIN_SLOPE = 0.02f;
        private const int NEWTON_ITERATIONS = 4;
        private const float SUBDIVISION_PRECISION = 0.0000001f;
        private const int SUBDIVISION_MAX_ITERATIONS = 10;

        public string? key;
        public Point outTangent;
        public Point inTangent;
        private string? keySource;
        private byte[]? keyPrefix;

        private readonly float[] samples = new float[SPLINE_TABLE_SIZE];

        internal static byte[] KeyPrefix(string? value)
        {
            byte[] bytes = TvgUtf8.Encode(value ?? "");
            int nul = bytes.AsSpan().IndexOf((byte)0);
            int length = Math.Min(20, nul < 0 ? bytes.Length : nul);
            if (length != bytes.Length) System.Array.Resize(ref bytes, length);
            return bytes;
        }

        internal bool KeyEquals(ReadOnlySpan<byte> prefix)
        {
            if (keyPrefix == null || !ReferenceEquals(keySource, key))
            {
                keyPrefix = KeyPrefix(key);
                keySource = key;
            }
            return prefix.SequenceEqual(keyPrefix);
        }

        private static float ConstA(float aA1, float aA2) => TvgMath.MultiplyAdd(-3.0f, aA2, 1.0f) + 3.0f * aA1;
        private static float ConstB(float aA1, float aA2) => TvgMath.MultiplyAdd(3.0f, aA2, -6.0f * aA1);
        private static float ConstC(float aA1) => 3.0f * aA1;

        private static float GetSlope(float t, float aA1, float aA2)
        {
            return TvgMath.MultiplyAdd(3.0f * ConstA(aA1, aA2) * t, t, 2.0f * ConstB(aA1, aA2) * t) + ConstC(aA1);
        }

        private static float CalcBezier(float t, float aA1, float aA2)
        {
            return TvgMath.MultiplyAdd(TvgMath.MultiplyAdd(ConstA(aA1, aA2), t, ConstB(aA1, aA2)), t, ConstC(aA1)) * t;
        }

        private static float BezierResidual(float t, float aA1, float aA2, float target) =>
            TvgMath.MultiplyAdd(TvgMath.MultiplyAdd(TvgMath.MultiplyAdd(ConstA(aA1, aA2), t, ConstB(aA1, aA2)), t, ConstC(aA1)), t, -target);

        private float GetTForX(float aX)
        {
            // Find interval where t lies
            var intervalStart = 0.0f;
            int currentSampleIdx = 1;
            int lastSampleIdx = SPLINE_TABLE_SIZE - 1;

            for (; currentSampleIdx != lastSampleIdx && samples[currentSampleIdx] <= aX; ++currentSampleIdx)
            {
                intervalStart += SAMPLE_STEP_SIZE;
            }

            --currentSampleIdx;

            // Interpolate to provide an initial guess for t
            var dist = (aX - samples[currentSampleIdx]) / (samples[currentSampleIdx + 1] - samples[currentSampleIdx]);
            var guessForT = TvgMath.MultiplyAdd(dist, SAMPLE_STEP_SIZE, intervalStart);

            var initialSlope = GetSlope(guessForT, outTangent.x, inTangent.x);
            if (initialSlope >= NEWTON_MIN_SLOPE) return NewtonRaphsonIterate(aX, guessForT);
            else if (initialSlope == 0.0f) return guessForT;
            else return BinarySubdivide(aX, intervalStart, intervalStart + SAMPLE_STEP_SIZE);
        }

        private float BinarySubdivide(float aX, float aA, float aB)
        {
            float x, t;
            int i = 0;

            do
            {
                t = TvgMath.MultiplyAdd(aB - aA, 0.5f, aA);
                x = BezierResidual(t, outTangent.x, inTangent.x, aX);
                if (x > 0.0f) aB = t;
                else aA = t;
            } while (MathF.Abs(x) > SUBDIVISION_PRECISION && ++i < SUBDIVISION_MAX_ITERATIONS);
            return t;
        }

        private float NewtonRaphsonIterate(float aX, float aGuessT)
        {
            for (int i = 0; i < NEWTON_ITERATIONS; ++i)
            {
                var currentX = BezierResidual(aGuessT, outTangent.x, inTangent.x, aX);
                var currentSlope = GetSlope(aGuessT, outTangent.x, inTangent.x);
                if (currentSlope == 0.0f) return aGuessT;
                aGuessT -= currentX / currentSlope;
            }
            return aGuessT;
        }

        public float Progress(float t)
        {
            if (outTangent.x == outTangent.y && inTangent.x == inTangent.y) return t;
            t = GetTForX(t);
            // The native final evaluation has no reused rounded constC. GNU
            // ARM contracts both constA's last addition and constC's multiply
            // here; sample/Newton evaluation reuses their rounded products.
            float a = TvgMath.MultiplyAdd(3.0f, outTangent.y, TvgMath.MultiplyAdd(-3.0f, inTangent.y, 1.0f));
            float b = ConstB(outTangent.y, inTangent.y);
            float value = TvgMath.MultiplyAdd(a, t, b) * t;
            return TvgMath.MultiplyAdd(3.0f, outTangent.y, value) * t;
        }

        public void Set(string? key, Point inTangent, Point outTangent)
        {
            int nul = key?.IndexOf('\0') ?? -1;
            this.key = nul < 0 ? key : key![..nul];
            keyPrefix = null;
            this.inTangent = inTangent;
            this.outTangent = outTangent;

            if (outTangent.x == outTangent.y && inTangent.x == inTangent.y) return;

            // calculate sample values
            for (int i = 0; i < SPLINE_TABLE_SIZE; ++i)
            {
                samples[i] = CalcBezier(i * SAMPLE_STEP_SIZE, outTangent.x, inTangent.x);
            }
        }
    }
}
