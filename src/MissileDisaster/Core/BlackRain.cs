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
        /// The stain is sized from the FIRES, not from the fallout.
        /// <para>
        /// What makes the rain black is soot, and the soot comes from the city burning: the
        /// third of the three clouds behind it is the one the fires' updraught carries to about
        /// 800 m. Sizing it from the fallout was the wrong quantity - and it also, wrongly, gave
        /// an airburst no stain at all, when Hiroshima was an airburst at 600 m and the black
        /// rain there is the case everything else here is modelled on.
        /// </para>
        /// </summary>
        public const float StainRadiusPerBurn = 1f;
        public const float StainRadiusMin = 80f;
        public const float StainRadiusMax = 6000f;

        // Note for anyone tempted: the real rain drifts. The Hiroshima rain fell to the north and
        // west of the hypocentre, not in a ring around it, so a wind-borne ellipse would be more
        // faithful than the disc drawn here. It was built once and taken back out again - it was
        // never asked for - so this is a deliberate simplification rather than an oversight.

        /// <summary>
        /// The mark outlasts the shower that left it, but only just. It is soot on wet ground,
        /// not a scar: the rain that laid it down washes it away almost as fast.
        /// </summary>
        public const float StainSecondsFactor = 0.6f;

        /// <summary>Below this yield there is not enough column to scavenge anything worth seeing.</summary>
        public const float MinimumKilotons = 0.5f;

        /// <summary>
        /// The chance, in percent, that a detonation large enough for it actually brings the rain
        /// down. It needs moisture in the air to scavenge, and it did not follow every historical
        /// shot - making it certain turned a striking detail into scenery, so it is a coin toss.
        /// </summary>
        public const int ChancePercent = 50;

        /// <summary>Whether a detonation of this size could bring black rain down at all.</summary>
        public static bool Falls(float kilotons)
        {
            return kilotons >= MinimumKilotons;
        }

        /// <summary>
        /// Whether this particular detonation brings it down. roll is 0-99, and the caller owns
        /// the randomness - the simulation thread's own randomizer, so a replay of the same save
        /// makes the same weather.
        /// </summary>
        public static bool FallsThisTime(float kilotons, int roll)
        {
            if (!Falls(kilotons)) return false;
            return roll >= 0 && roll < ChancePercent;
        }

        /// <summary>How long it rains, in simulation seconds.</summary>
        public static float RainSeconds(float kilotons)
        {
            if (!Falls(kilotons)) return 0f;
            float root = (float)Math.Pow(kilotons, 1.0 / 3.0);
            return Clamp(RainSecondsPerKilotonRoot * root, RainSecondsMin, RainSecondsMax);
        }

        /// <summary>
        /// How far the stain reaches, in metres, from the radius of the fires whose soot made it
        /// black. No fires, no soot, no black rain - which is why a nuclear test in a desert
        /// produced fallout and white coral ash but nothing anyone called black rain.
        /// </summary>
        public static float StainRadius(float burnRadius)
        {
            if (burnRadius <= 0f) return 0f;
            return Clamp(burnRadius * StainRadiusPerBurn, StainRadiusMin, StainRadiusMax);
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
