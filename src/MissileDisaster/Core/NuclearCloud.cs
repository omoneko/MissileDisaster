using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// The real dimensions and timings of a nuclear fireball and its cloud, as a function of the
    /// yield in kilotons. Pure, with no UnityEngine dependency, so the effect can be built to
    /// figures rather than to taste.
    ///
    /// The numbers come from Glasstone and Dolan, "The Effects of Nuclear Weapons" (1977),
    /// chapter II, and the standard fits to its cloud charts:
    ///   - a 1 Mt fireball reaches about 5,700 ft across - a radius of 869 m - about 10 s after
    ///     the burst, and the radius goes as W^0.4
    ///   - the stabilised cloud radius is  R = 0.6 km * 10^(0.0137 L^3 - 0.0358 L^2 + 0.37 L)
    ///   - the stabilised cloud top is     H = 3.0 km * 10^(0.006941 L^4 - 0.06216 L^3
    ///                                                      + 0.1526 L^2 + 0.1878 L)
    ///     with L = log10(W in kt) in both
    ///   - the cloud rises at nearly 300 mph (about 134 m/s) through its first minute at 1 Mt,
    ///     3 miles in 30 s and 5 miles in a minute, and stabilises after about 10 minutes
    ///   - the stem is about half the width of the cloud below 20 kt and only a fifth to a tenth
    ///     of it in the megaton range
    ///
    /// A pleasing coincidence falls out of this: at 150 kt the stabilised cloud radius is 3.59 km
    /// and the 5 psi destruction radius 3.72 km, so a cloud built to these figures covers very
    /// nearly the ground the blast wrecked without being made to.
    /// </summary>
    public static class NuclearCloud
    {
        public const float ReferenceKilotons = 1000f; // the 1 Mt point the timings are quoted at

        // Fireball. 869 m at 1 Mt with the radius going as W^0.4 gives the coefficient below.
        public const float FireballCoefficient = 55f;
        public const float FireballExponent = 0.4f;
        public const float FireballSecondsAt1Mt = 10f;

        // Cloud fit coefficients, kept named so the formulas above can be checked against them.
        public const float CloudRadiusBaseMetres = 600f;
        public const float CloudTopBaseMetres = 3000f;

        // Stem width against the cloud, interpolated on log10(W) between the two figures
        // Glasstone gives: half the cloud at 20 kt, a seventh of it at 1 Mt.
        public const float StemFractionAt20Kt = 0.5f;
        public const float StemFractionAt1Mt = 0.15f;

        public const float RiseSpeedAt1Mt = 134f;      // 440 ft/s through the first minute
        public const float StabiliseSecondsAt1Mt = 600f; // about ten minutes

        /// <summary>The fireball's maximum radius in metres. Zero or less gives 0.</summary>
        public static float FireballRadius(float kilotons)
        {
            if (kilotons <= 0f) return 0f;
            return FireballCoefficient * (float)Math.Pow(kilotons, FireballExponent);
        }

        /// <summary>Seconds the fireball takes to swell to that radius - 10 s at 1 Mt, on the same W^0.4.</summary>
        public static float FireballSeconds(float kilotons)
        {
            if (kilotons <= 0f) return 0f;
            return FireballSecondsAt1Mt * (float)Math.Pow(kilotons / ReferenceKilotons, FireballExponent);
        }

        /// <summary>The stabilised cloud's radius in metres.</summary>
        public static float CloudRadius(float kilotons)
        {
            if (kilotons <= 0f) return 0f;
            double l = Math.Log10(kilotons);
            double e = 0.0137 * l * l * l - 0.0358 * l * l + 0.37 * l;
            return (float)(CloudRadiusBaseMetres * Math.Pow(10.0, e));
        }

        /// <summary>The stabilised cloud top - the height of the very top of the cap - in metres.</summary>
        public static float CloudTop(float kilotons)
        {
            if (kilotons <= 0f) return 0f;
            double l = Math.Log10(kilotons);
            double l2 = l * l;
            double e = 0.006941 * l2 * l2 - 0.06216 * l2 * l + 0.1526 * l2 + 0.1878 * l;
            return (float)(CloudTopBaseMetres * Math.Pow(10.0, e));
        }

        /// <summary>How wide the stem is against the cloud: half of it at 20 kt, down to about a seventh in the megaton range.</summary>
        public static float StemFraction(float kilotons)
        {
            if (kilotons <= 0f) return StemFractionAt20Kt;
            double l = Math.Log10(kilotons);
            const double lLow = 1.301;  // log10(20)
            const double lHigh = 3.0;   // log10(1000)
            double t = (l - lLow) / (lHigh - lLow);
            double f = StemFractionAt20Kt + t * (StemFractionAt1Mt - StemFractionAt20Kt);
            if (f > StemFractionAt20Kt) f = StemFractionAt20Kt;
            if (f < 0.1) f = 0.1;
            return (float)f;
        }

        /// <summary>The cloud's rise rate through its first minute, in m/s.</summary>
        public static float RiseSpeed(float kilotons)
        {
            if (kilotons <= 0f) return 0f;
            return RiseSpeedAt1Mt * (float)Math.Pow(kilotons / ReferenceKilotons, 0.2);
        }

        /// <summary>Seconds the cloud really takes to stabilise - minutes, which is why the effect compresses it.</summary>
        public static float StabiliseSeconds(float kilotons)
        {
            if (kilotons <= 0f) return 0f;
            return StabiliseSecondsAt1Mt * (float)Math.Pow(kilotons / ReferenceKilotons, 0.25);
        }
    }
}
