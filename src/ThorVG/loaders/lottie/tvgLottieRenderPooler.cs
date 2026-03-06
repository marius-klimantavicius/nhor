// Ported from ThorVG/src/loaders/lottie/tvgLottieRenderPooler.h

using System.Collections.Generic;

namespace ThorVG
{
    /// <summary>
    /// Object pool for reusing Paint objects during rendering.
    /// Mirrors C++ LottieRenderPooler.
    /// </summary>
    public class LottieRenderPooler<T> where T : Paint
    {
        public List<T> pooler = new();

        public T Pooling(bool copy = false)
        {
            // return available one
            foreach (var p in pooler)
            {
                if (p.RefCnt() == 1) return p;
            }

            // no empty, generate a new one
            T p2;
            if (copy && pooler.Count > 0)
            {
                p2 = (T)pooler[0].Duplicate();
            }
            else
            {
                // Use reflection-free approach: we know T is either Shape or Scene
                if (typeof(T) == typeof(Shape))
                    p2 = (T)(Paint)Shape.Gen();
                else if (typeof(T) == typeof(Scene))
                    p2 = (T)(Paint)Scene.Gen();
                else
                    p2 = (T)(Paint)Shape.Gen(); // fallback
            }
            p2.Ref();
            pooler.Add(p2);
            return p2;
        }
    }
}
