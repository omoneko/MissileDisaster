namespace MissileDisaster.Game
{
    /// <summary>
    /// Loosely coupled beacon publishing every impact, of any warhead, so that other mods can
    /// read it by reflection. This is the general form of NuclearImpactBeacon; CS:WARFRONT uses
    /// it to damage military units, which is what fixed the reported problem of units surviving
    /// a missile strike.
    /// Neither mod references the other's DLL: the reader looks up the type name
    /// "MissileDisaster.Game.ImpactBeacon" in the AppDomain and calls the two members below by
    /// reflection. With only one of the mods installed, it is safely ignored.
    ///
    /// The reflection contract, which other mods depend on - do not break these signatures:
    ///   public static long CurrentId();   // the last impact ID issued, 0 if none. A cheap check for anything new.
    ///   public static float[] Snapshot(); // recent impacts, newest first, as
    ///                                     // {id, x, z, destructionRadius, burnRadius, isNuclear(0/1)}
    ///                                     // six-element records, up to Capacity of them.
    /// The ID rises monotonically for the life of the process and is not rewound even by Reset,
    /// so a reader tracking the last ID it saw never breaks.
    ///
    /// Threading: Publish is called on the main thread, from MissileManager.UpdateVisual, when
    /// an impact is confirmed. The reader (CS:WARFRONT) calls in from the simulation thread, so
    /// every public member is lock-protected.
    /// </summary>
    public static class ImpactBeacon
    {
        private const int Capacity = 16;
        private const int Stride = 6;

        private struct Entry
        {
            public long Id;
            public float X, Z, DestructionRadius, BurnRadius;
            public bool IsNuclear;
        }

        private static readonly Entry[] _ring = new Entry[Capacity];
        private static int _count;
        private static int _head;
        private static long _lastId;
        private static readonly object _lock = new object();

        /// <summary>The last impact ID issued, or 0 if none.</summary>
        public static long CurrentId()
        {
            lock (_lock) { return _lastId; }
        }

        /// <summary>Publishes one impact. Main thread, as the impact is confirmed.</summary>
        public static void Publish(float x, float z, float destructionRadius, float burnRadius, bool isNuclear)
        {
            lock (_lock)
            {
                _lastId++;
                _ring[_head] = new Entry
                {
                    Id = _lastId, X = x, Z = z,
                    DestructionRadius = destructionRadius, BurnRadius = burnRadius, IsNuclear = isNuclear
                };
                _head = (_head + 1) % Capacity;
                if (_count < Capacity) _count++;
            }
        }

        /// <summary>Recent impacts, newest first, as {id, x, z, destructionRadius, burnRadius, isNuclear}.</summary>
        public static float[] Snapshot()
        {
            lock (_lock)
            {
                float[] result = new float[_count * Stride];
                int idx = _head - 1;
                for (int i = 0; i < _count; i++)
                {
                    if (idx < 0) idx += Capacity;
                    Entry e = _ring[idx];
                    int o = i * Stride;
                    result[o] = e.Id;
                    result[o + 1] = e.X;
                    result[o + 2] = e.Z;
                    result[o + 3] = e.DestructionRadius;
                    result[o + 4] = e.BurnRadius;
                    result[o + 5] = e.IsNuclear ? 1f : 0f;
                    idx--;
                }
                return result;
            }
        }

        /// <summary>Reset on a level change. It clears the entries but never rewinds the ID, which readers rely on.</summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _count = 0;
                _head = 0;
            }
        }
    }
}
