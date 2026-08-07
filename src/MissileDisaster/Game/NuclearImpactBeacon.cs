namespace MissileDisaster.Game
{
    /// <summary>
    /// Loosely coupled beacon publishing where nuclear warheads land, so that other mods can
    /// read it by reflection. The Alien Invasion mod, for instance, reads this to topple any
    /// tripod caught in a direct nuclear hit.
    /// Neither mod references the other's DLL: the reader looks up the type name
    /// "MissileDisaster.Game.NuclearImpactBeacon" in the AppDomain and calls the two members
    /// below by reflection. With only one of the mods installed, it is safely ignored.
    ///
    /// The reflection contract, which other mods depend on - do not break these signatures:
    ///   public static long CurrentId { get; }  // the last impact ID issued, 0 if none. A cheap check for anything new.
    ///   public static float[] Snapshot();      // recent nuclear impacts as {id, x, z} triples, newest first, up to Capacity of them.
    ///
    /// The last Capacity impacts are kept in a ring buffer, each with a monotonically rising ID
    /// starting at 1. A reader treats only the entries above the last ID it saw as new. Only the
    /// horizontal position (x, z) is published; deciding what counts as a hit is left to the
    /// reader and its own settings.
    ///
    /// Threading: Publish is called on the main thread, from MissileManager.UpdateVisual, when
    /// an impact is confirmed. Readers are also on the main thread today, but everything is
    /// lock-protected against future misuse.
    /// </summary>
    public static class NuclearImpactBeacon
    {
        private const int Capacity = 16;

        private struct Entry { public long Id; public float X; public float Z; }

        private static readonly Entry[] _ring = new Entry[Capacity];
        private static int _count;   // how many are held, at most Capacity
        private static int _head;    // the ring position to write next
        private static long _lastId; // the last ID issued
        private static readonly object _lock = new object();

        /// <summary>The last impact ID issued, or 0 if none. A cheap check a reader can make before calling Snapshot.</summary>
        public static long CurrentId
        {
            get { lock (_lock) { return _lastId; } }
        }

        /// <summary>Publishes one nuclear impact. Called on the main thread as the impact is confirmed.</summary>
        public static void Publish(float x, float z)
        {
            lock (_lock)
            {
                _lastId++;
                _ring[_head] = new Entry { Id = _lastId, X = x, Z = z };
                _head = (_head + 1) % Capacity;
                if (_count < Capacity) _count++;
            }
        }

        /// <summary>Recent nuclear impacts as {id, x, z} triples, newest first, up to Capacity of them. Empty if there are none.</summary>
        public static float[] Snapshot()
        {
            lock (_lock)
            {
                float[] result = new float[_count * 3];
                int idx = _head - 1; // the newest entry is one before _head
                for (int i = 0; i < _count; i++)
                {
                    if (idx < 0) idx += Capacity;
                    Entry e = _ring[idx];
                    result[i * 3] = e.Id;
                    result[i * 3 + 1] = e.X;
                    result[i * 3 + 2] = e.Z;
                    idx--;
                }
                return result;
            }
        }

        /// <summary>Reset on a level change, so old impacts are not carried into the new level. Main thread.</summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _count = 0;
                _head = 0;
                _lastId = 0;
            }
        }
    }
}
