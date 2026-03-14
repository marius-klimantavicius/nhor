// Ported from ThorVG/src/renderer/sw_engine/tvgSwMemPool.cpp

using System.Collections.Generic;

namespace ThorVG
{
    public static class SwMemPool
    {
        [System.ThreadStatic]
        private static SwMpool? _pool;

        private static readonly List<SwMpool> _pools = new List<SwMpool>();
        private static readonly object _lock = new object();
        private static uint _threads;

        public static SwMpool mpoolReq()
        {
            if (_pool == null)
            {
                _pool = new SwMpool(_threads);
                lock (_lock)
                {
                    _pools.Add(_pool);
                }
            }
            return _pool;
        }

        public static void mpoolInit(uint threads)
        {
            _threads = threads;
        }

        public static void mpoolTerm()
        {
            lock (_lock)
            {
                foreach (var p in _pools)
                {
                    // Allow GC to collect
                }
                _pool = null;
                _pools.Clear();
            }
        }
    }
}
