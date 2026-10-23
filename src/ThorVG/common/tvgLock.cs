// Ported from ThorVG/src/common/tvgLock.h
//
// The C++ version has two modes: with and without THORVG_THREAD_SUPPORT.
// The C# port always compiles the locking primitives but honours a
// runtime flag (ThreadingEnabled) analogous to the C++ TaskScheduler
// thread-count check.

using System.Threading;

namespace ThorVG
{
    /// <summary>
    /// Mutual-exclusion key.  Mirrors C++ <c>tvg::Key</c>.
    /// </summary>
    public class Key
    {
        internal readonly object SyncRoot = new object();

        public void Lock() => Monitor.Enter(SyncRoot);
        public void Unlock() => Monitor.Exit(SyncRoot);
    }

    /// <summary>A lock that also guards global state without worker threads.</summary>
    public sealed class StrictKey : Key { }

    /// <summary>
    /// Scoped (RAII-style) lock.  Mirrors C++ <c>tvg::ScopedLock</c>.
    /// Use with a <c>using</c> statement.
    /// </summary>
    public struct ScopedLock : System.IDisposable
    {
        private readonly object? _syncRoot;

        /// <summary>
        /// Set to <c>true</c> to enable locking.
        /// When <c>false</c> (the default), <see cref="ScopedLock"/> is a
        /// no-op, matching the C++ single-threaded (no THORVG_THREAD_SUPPORT)
        /// behaviour.
        /// </summary>
        public static volatile bool ThreadingEnabled;

        public ScopedLock(Key key)
        {
            // Decide once, before acquiring, so a scheduler transition cannot
            // cause the acquired monitor to be forgotten during disposal.
            if (ThreadingEnabled || key is StrictKey)
            {
                Monitor.Enter(key.SyncRoot);
                _syncRoot = key.SyncRoot;
            }
            else
            {
                _syncRoot = null;
            }
        }

        public void Dispose()
        {
            if (_syncRoot != null)
            {
                Monitor.Exit(_syncRoot);
            }
        }
    }
}
