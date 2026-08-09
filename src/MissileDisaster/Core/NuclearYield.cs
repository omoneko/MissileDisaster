using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// Turns a nuclear yield in kilotons into a scale factor for the effect radii. Pure, with
    /// no UnityEngine dependency.
    /// Following the blast radius going as the cube root of the yield, and taking the standard
    /// 150 kt as the baseline, the multiplier is cbrt(kt/150).
    /// Both the catalogue selection and a typed-in yield go through this one function.
    /// </summary>
    public static class NuclearYields
    {
        public const int StandardKilotons = 150;

        /// <summary>The scale factor - the blast radius relative to the baseline - for a yield in kilotons. Zero or less gives 0.</summary>
        public static float Multiplier(int kilotons)
        {
            if (kilotons <= 0) return 0f;
            return (float)Math.Pow(kilotons / (double)StandardKilotons, 1.0 / 3.0);
        }

        /// <summary>
        /// The inverse: the yield in kilotons a scale factor came from. The launch path only
        /// carries the multiplier, but the fireball and cloud are built to real figures that need
        /// the yield itself, so this recovers it exactly.
        /// </summary>
        public static float Kilotons(float multiplier)
        {
            if (multiplier <= 0f) return 0f;
            return StandardKilotons * multiplier * multiplier * multiplier;
        }
    }
}
