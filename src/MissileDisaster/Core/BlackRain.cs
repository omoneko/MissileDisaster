using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// The black rain that follows a nuclear detonation: how long it falls and how far and how
    /// long it stains the ground (no UnityEngine dependency).
    ///
    /// <para>
    /// The real thing is soot and fallout scavenged out of the rising column by the water the
    /// fireball condensed. It fell on Hiroshima within half an hour of the burst, heavy, dark,
    /// and dirty enough to mark what it landed on. It is one of the details of a nuclear strike
    /// that people actually remember, and none of it happens in the game on its own - the
    /// vanilla weather simulation never reads pollution, so the rain a player sees after a strike
    /// is the ordinary weather cycle and a coincidence.
    /// </para>
    ///
    /// So the mod causes it deliberately. The figures below are drawn to be legible rather than
    /// exact: the rain is minutes rather than the half hour it took to arrive, and the stain is
    /// hours rather than the weeks the real marks lasted, because a game is watched for minutes.
    /// </summary>
    public static class BlackRain
    {
        // How long it rains, in simulation seconds, against the cube root of the yield - the same
        // law every other radius and time in this mod follows.
        public const float RainSecondsPerKilotonRoot = 22f;
        public const float RainSecondsMin = 45f;
        public const float RainSecondsMax = 240f;

        /// <summary>
        /// The stain spreads wider than the fallout that carries it: rain drifts on the wind and
        /// lands beyond where the column stood.
        /// </summary>
        public const float StainRadiusPerFallout = 1.35f;
        public const float StainRadiusMin = 80f;
        public const float StainRadiusMax = 4000f;

        /// <summary>The mark outlasts the shower that left it, but not by a great deal.</summary>
        public const float StainSecondsFactor = 2.5f;

        /// <summary>Below this yield there is not enough column to scavenge anything worth seeing.</summary>
        public const float MinimumKilotons = 0.5f;

        /// <summary>Whether a detonation of this size brings black rain down at all.</summary>
        public static bool Falls(float kilotons)
        {
            return kilotons >= MinimumKilotons;
        }

        /// <summary>How long it rains, in simulation seconds.</summary>
        public static float RainSeconds(float kilotons)
        {
            if (!Falls(kilotons)) return 0f;
            float root = (float)Math.Pow(kilotons, 1.0 / 3.0);
            return Clamp(RainSecondsPerKilotonRoot * root, RainSecondsMin, RainSecondsMax);
        }

        /// <summary>How far the stain reaches, in metres, from the fallout radius it rode down on.</summary>
        public static float StainRadius(float falloutRadius)
        {
            if (falloutRadius <= 0f) return 0f;
            return Clamp(falloutRadius * StainRadiusPerFallout, StainRadiusMin, StainRadiusMax);
        }

        /// <summary>How long the ground stays marked, in simulation seconds.</summary>
        public static float StainSeconds(float rainSeconds)
        {
            if (rainSeconds <= 0f) return 0f;
            return rainSeconds * StainSecondsFactor;
        }

        private static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
