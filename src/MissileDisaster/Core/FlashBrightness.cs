using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// How bright a detonation lights the scene. Pure, with no UnityEngine dependency.
    ///
    /// <para>
    /// This lives in Core, and separately from the drawn fireball, because of a bug it took a
    /// player report and two rounds of guessing to find. The mod was asking Unity for point
    /// lights of 10 to 90 and a map-wide directional of 9, where a Unity light normally sits
    /// between 1 and 8 - up to eleven times over. The smoke went blue while the flash was lit,
    /// with conventional warheads as well as nuclear, which is what finally identified it: the
    /// cloud's material is shared, so a material fault would have been blue always, and only the
    /// flash is both common to every warhead and confined to that window.
    /// </para>
    ///
    /// <para>
    /// The figures were unreachable from a test because they were private constants inside a
    /// MonoBehaviour. They are here now so <c>Sane</c> can be asserted, which is the whole point:
    /// the mistake was not the number, it was that nothing could see the number.
    /// </para>
    ///
    /// What the player reads as the ball of light is the drawn glow sphere and the afterglow,
    /// neither of which this touches. This is the light the scene is lit by.
    /// </summary>
    public static class FlashBrightness
    {
        /// <summary>
        /// The range a Unity light is built for. Past this the renderer's grading stops behaving,
        /// which is where the blue came from - so nothing here may leave it.
        /// </summary>
        public const float SaneMin = 0.5f;
        public const float SaneMax = 16f;

        // Nuclear: the point light at the burst.
        public const float NuclearMin = 4f;
        public const float NuclearMax = 14f;
        public const float NuclearPerKilotonRoot = 2.2f;

        // Nuclear: the map-wide wash that turns night into day for an instant.
        public const float DaylightMin = 1.2f;
        public const float DaylightMax = 3f;
        public const float DaylightPerKilotonRoot = 0.55f;

        // Conventional: sized from the fireball, since there is no yield in kilotons to root.
        public const float ConventionalPerMetre = 0.18f;
        public const float ConventionalMin = 2f;
        public const float ConventionalMax = 8f;

        /// <summary>The point light at a nuclear burst.</summary>
        public static float Nuclear(float kilotons)
        {
            if (kilotons <= 0f) return NuclearMin;
            float i = NuclearPerKilotonRoot * (float)Math.Pow(kilotons, 1.0 / 3.0);
            return Clamp(i, NuclearMin, NuclearMax);
        }

        /// <summary>The daylight a nuclear flash washes over the whole map.</summary>
        public static float Daylight(float kilotons)
        {
            if (kilotons <= 0f) return DaylightMin;
            float i = DaylightPerKilotonRoot * (float)Math.Pow(kilotons, 1.0 / 3.0);
            return Clamp(i, DaylightMin, DaylightMax);
        }

        /// <summary>The point light at a conventional burst, from its fireball.</summary>
        public static float Conventional(float fireballRadius)
        {
            if (fireballRadius <= 0f) return ConventionalMin;
            return Clamp(fireballRadius * ConventionalPerMetre, ConventionalMin, ConventionalMax);
        }

        /// <summary>Whether an intensity is inside the range the renderer is built for.</summary>
        public static bool Sane(float intensity)
        {
            return intensity >= SaneMin && intensity <= SaneMax;
        }

        private static float Clamp(float v, float lo, float hi)
        {
            if (v < lo) return lo;
            return v > hi ? hi : v;
        }
    }
}
