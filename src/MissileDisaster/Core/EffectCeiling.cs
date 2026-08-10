using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// The ceiling an effect dimension is held under. Pure, with no UnityEngine dependency.
    ///
    /// Every effect in this mod is built to real figures, and the real figures run away from the
    /// map: a 50 Mt cloud is 140 km across on the Glasstone fit, on a map 17 km wide. Something
    /// has to bound them. A hard clamp does bound them, but it also throws the yield away above
    /// the limit - every warhead from a megaton upwards came out of the old clamp identical,
    /// which is the one thing a mod about yields must not do.
    ///
    /// A soft ceiling bounds the value without ever flattening it:
    ///
    ///     f(v) = knee + span * (1 - e^(-(v-knee)/span)),  span = ceiling - knee
    ///
    /// Below the knee it is the identity, so everything inside the range the figures were
    /// verified over is untouched. At the knee it is continuous and its slope is exactly 1, so
    /// there is no visible kink where it takes over. Above it, it is strictly increasing and
    /// approaches the ceiling without ever passing it, so a larger yield is always a larger
    /// explosion, however far past the fit's range it is asked for.
    /// </summary>
    public static class EffectCeiling
    {
        /// <summary>
        /// The value held under the ceiling: itself up to the knee, then compressed smoothly
        /// into what is left before the ceiling. A ceiling at or below the knee degenerates to a
        /// hard clamp at the knee, and a negative value is returned unchanged.
        /// </summary>
        public static float Soft(float value, float knee, float ceiling)
        {
            if (value <= knee) return value;
            if (ceiling <= knee) return knee;
            float span = ceiling - knee;
            return knee + span * (1f - (float)Math.Exp(-(value - knee) / span));
        }

        /// <summary>The value held between a floor and a soft ceiling - the usual case for an effect dimension.</summary>
        public static float Soft(float value, float floor, float knee, float ceiling)
        {
            if (value < floor) return floor;
            return Soft(value, knee, ceiling);
        }
    }
}
