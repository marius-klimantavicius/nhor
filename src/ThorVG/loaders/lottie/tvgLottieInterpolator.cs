// Ported from ThorVG/src/loaders/lottie/tvgLottieInterpolator.h and tvgLottieInterpolator.cpp

using System;

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

        private readonly float[] samples = new float[SPLINE_TABLE_SIZE];

        private static float ConstA(float aA1, float aA2) => 1.0f - 3.0f * aA2 + 3.0f * aA1;
        private static float ConstB(float aA1, float aA2) => 3.0f * aA2 - 6.0f * aA1;
        private static float ConstC(float aA1) => 3.0f * aA1;

        private static float GetSlope(float t, float aA1, float aA2)
        {
            return 3.0f * ConstA(aA1, aA2) * t * t + 2.0f * ConstB(aA1, aA2) * t + ConstC(aA1);
        }

        private static float CalcBezier(float t, float aA1, float aA2)
        {
            return ((ConstA(aA1, aA2) * t + ConstB(aA1, aA2)) * t + ConstC(aA1)) * t;
        }

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
            var guessForT = intervalStart + dist * SAMPLE_STEP_SIZE;

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
                t = aA + (aB - aA) / 2.0f;
                x = CalcBezier(t, outTangent.x, inTangent.x) - aX;
                if (x > 0.0f) aB = t;
                else aA = t;
            } while (MathF.Abs(x) > SUBDIVISION_PRECISION && ++i < SUBDIVISION_MAX_ITERATIONS);
            return t;
        }

        private float NewtonRaphsonIterate(float aX, float aGuessT)
        {
            for (int i = 0; i < NEWTON_ITERATIONS; ++i)
            {
                var currentX = CalcBezier(aGuessT, outTangent.x, inTangent.x) - aX;
                var currentSlope = GetSlope(aGuessT, outTangent.x, inTangent.x);
                if (currentSlope == 0.0f) return aGuessT;
                aGuessT -= currentX / currentSlope;
            }
            return aGuessT;
        }

        public float Progress(float t)
        {
            if (outTangent.x == outTangent.y && inTangent.x == inTangent.y) return t;
            return CalcBezier(GetTForX(t), outTangent.y, inTangent.y);
        }

        public void Set(string? key, Point inTangent, Point outTangent)
        {
            if (key != null) this.key = key;
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
