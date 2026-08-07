using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// Turns the charge of a non-nuclear warhead, in kg of TNT, into a scale factor for its
    /// effect radii. Pure, with no UnityEngine dependency.
    /// The blast radius goes as the cube root of the charge, so against the 1000 kg baseline -
    /// the default spec of every non-nuclear warhead - the multiplier is cbrt(kg/1000).
    /// The defaults for the high-explosive, cluster, white phosphorus and thermobaric warheads
    /// are all scaled relative to the chosen yield this way.
    /// </summary>
    public static class ConventionalYields
    {
        public const int ReferenceKilograms = 1000; // the 1 t TNT baseline, equal to the default spec

        /// <summary>The scale factor - the blast radius relative to the baseline - for a charge in kg of TNT. Zero or less gives 0.</summary>
        public static float Multiplier(int kilograms)
        {
            if (kilograms <= 0) return 0f;
            return (float)Math.Pow(kilograms / (double)ReferenceKilograms, 1.0 / 3.0);
        }
    }
}
