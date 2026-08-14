using System.Collections.Generic;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// Carries a ground stain from the simulation thread, where the impact is resolved, to the
    /// main thread, where it can be drawn.
    ///
    /// <para>
    /// The same shape MissileManager already uses for impacts, and for the same reason: the
    /// stain is a GameObject with a ParticleSystem, and Unity objects are main-thread only.
    /// What crosses the boundary is a small lock-protected queue of plain values.
    /// </para>
    /// </summary>
    public static class BlackRainQueue
    {
        private struct Stain
        {
            public Vector3 GroundZero;
            public float Radius;
            public float Seconds;
        }

        private static readonly List<Stain> _queue = new List<Stain>();
        private static readonly object _lock = new object();

        /// <summary>Simulation thread. Asks for a stain to be drawn on the next main-thread frame.</summary>
        public static void Enqueue(Vector3 groundZero, float radius, float seconds)
        {
            lock (_lock)
            {
                _queue.Add(new Stain { GroundZero = groundZero, Radius = radius, Seconds = seconds });
            }
        }

        /// <summary>Main thread. Draws everything asked for since the last call.</summary>
        public static void DrainAndDraw()
        {
            List<Stain> pending = null;
            lock (_lock)
            {
                if (_queue.Count > 0)
                {
                    pending = new List<Stain>(_queue);
                    _queue.Clear();
                }
            }
            if (pending == null) return;

            for (int i = 0; i < pending.Count; i++)
            {
                Effects.BlackRainFx.Play(pending[i].GroundZero, pending[i].Radius, pending[i].Seconds);
            }
        }

        /// <summary>Called on a level change, so one city's stains are not drawn onto the next.</summary>
        public static void Reset()
        {
            lock (_lock) { _queue.Clear(); }
        }
    }
}
