using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// The dimensions of the smoke column a non-nuclear detonation throws up, in metres and
    /// seconds. Pure, with no UnityEngine dependency, so the shape can be tested.
    ///
    /// <para>
    /// This produces a <see cref="NuclearCloudDimensions"/>, and that is deliberate rather than
    /// lazy: the struct is the geometry contract <see cref="CloudPuffs"/> places puffs against -
    /// cap, stem, base, depth and the three phase lengths - and none of it is nuclear in any way
    /// but its name. Reusing it means a bomb's column is drawn by exactly the same vortex-ring
    /// flow as a warhead's, which is the whole point: the earlier conventional mushroom was
    /// free-running particles, the technique the nuclear cloud was rebuilt to get away from
    /// because a crowd of drifting particles cannot hold a silhouette. It never grew a stem.
    /// </para>
    ///
    /// <para>
    /// Everything is a factor of the fireball radius, which already follows the cube root of the
    /// charge, so the cloud scales with the yield for free and a bomb's is never mistakable for a
    /// warhead's. At the 1 t baseline - a 15 m fireball - the column stands 135 m with a cap 60 m
    /// across, against the 150 kt cloud's 795 m and 430 m.
    /// </para>
    ///
    /// The proportions are not the nuclear ones scaled down. A nuclear cap is flattened by the
    /// tropopause, which is what makes it spread far wider than it is deep; a tonne of high
    /// explosive lifts its smoke a few hundred metres at most, nowhere near any such lid, so its
    /// head stays a roughly round ball of smoke on a narrow dirty stem. That is what the
    /// photographs show and what the factors below give.
    /// </summary>
    public static class ConventionalCloudDisplay
    {
        /// <summary>Below this fireball there is no column worth drawing - a bomblet lifts dust, not a cloud.</summary>
        public const float MinimumFireballRadius = 3f;

        /// <summary>
        /// The ceiling, applied to the fireball radius the whole cloud is then built from.
        ///
        /// <para>
        /// It exists because the two clouds are drawn at different scales and would otherwise
        /// meet. A nuclear cloud is brought down to 6% of its real size - see
        /// NuclearCloudDisplay.CloudScale - because a real one is 13 km tall and does not fit on
        /// a screen; a conventional column is a couple of hundred metres and is drawn at its
        /// true size. Left unbounded, a thermobaric warhead's 40 m fireball gives a 360 m column
        /// with a 63 m cap, against the 360 m and 59 m a 1 kt nuclear cloud is drawn at - the two
        /// meet exactly - and a hand-typed 50 t charge beats it outright. A bomb must never be
        /// mistakable for a warhead.
        /// </para>
        ///
        /// It is applied to the fireball radius rather than to the height, so cap, stem and
        /// column are all held back together and every proportion below survives it - the same
        /// argument CloudScale makes on the nuclear side, and for the same reason: squashing the
        /// height alone turns a mushroom into a pancake on a lump.
        ///
        /// The knee sits above the conventional and white phosphorus warheads, which are drawn to
        /// their true figures. Thermobaric is the one that has to be held back, and that is the
        /// honest answer rather than a fudge: a fuel-air cloud really is the largest non-nuclear
        /// thing here, so it is the one that runs into the smallest warhead from below.
        /// </summary>
        /// It is the cap that binds rather than the height: a 1 kt cloud is drawn as a tall thin
        /// spike, 360 m over a cap only 59 m in radius, so a conventional cloud runs into its
        /// width well before its height.
        public const float FireballKnee = 18f;
        public const float FireballCeiling = 24f;

        // The structure, against the fireball radius.
        public const float CloudTopFactor = 9f;        // how high the smoke reaches
        public const float CapRadiusFactor = 2f;       // the head, a round ball rather than a canopy
        public const float CapBaseFraction = 0.58f;    // of the cloud top: the head takes the upper 42%

        /// <summary>
        /// The stem, against the fireball. Narrower than the cap - that contrast is the whole
        /// silhouette - but nothing like as narrow as a nuclear one, whose cap is spread out
        /// along the tropopause and so towers over a stem of its own true width. Nothing spreads
        /// a bomb's head, so the two are only about three to one, which is what the photographs
        /// show and what stops the column reading as a thread with a ball on it.
        /// </summary>
        public const float StemRadiusFactor = 0.7f;

        /// <summary>
        /// How far out the fires feed smoke into the column, against the cap.
        ///
        /// <para>
        /// Far tighter than the nuclear figure, and for a real reason rather than a cosmetic one:
        /// that one stands for a burning city kilometres across, and a tonne of high explosive
        /// sets fire to a street. Carried over unchanged it put a scatter of smoke puffs across
        /// a 66 m field with nothing joining them up, which read as a handful of blobs lying
        /// around the blast rather than as smoke. Pulled in, the same puffs are the skirt of dust
        /// around the foot of the column, which is what a bomb actually throws up.
        /// </para>
        /// </summary>
        public const float FireFieldFactor = 1.2f;

        // How long it takes to stand up. A real column of this size is up in a couple of seconds,
        // so unlike the nuclear cloud there is nothing to compress - the figures are already
        // watchable. The square root is what keeps a big charge's column from taking
        // proportionally longer: it climbs faster as well as higher.
        public const float RiseSecondsPerRootMetre = 0.55f;
        public const float RiseSecondsMin = 1.5f;
        public const float RiseSecondsKnee = 4f;
        public const float RiseSecondsCeiling = 6f;

        // It stands briefly and shreds. Held far shorter than the nuclear cloud's on purpose:
        // this is a moment in a strike, not the event itself, and a bomb's smoke is torn apart by
        // the wind almost as soon as it stops rising.
        public const float HoldFactor = 0.9f;
        public const float HoldSecondsMin = 1.5f;
        public const float HoldSecondsMax = 5f;

        // Longer than the rise, like the nuclear one: a column that takes two seconds to form and
        // vanishes in a blink reads as a deletion rather than as smoke dispersing.
        public const float FadeFactor = 1.6f;
        public const float FadeSecondsMin = 3f;
        public const float FadeSecondsMax = 8f;

        /// <summary>
        /// The condensation cloud - the thin white shell that flashes into being just behind the
        /// shock front and is gone again within a second. It is not a nuclear phenomenon: any
        /// detonation in damp air drops the pressure behind its front far enough to condense the
        /// water in it, which is why it is on film behind so many ordinary charges.
        /// </summary>
        public const float CondensationRadiusFactor = 2.4f;
        public const float CondensationSeconds = 0.65f;
        public const float CondensationDelaySeconds = 0.08f;

        /// <summary>Whether a detonation with this fireball throws up a column at all.</summary>
        public static bool Draws(float fireballRadius)
        {
            return fireballRadius >= MinimumFireballRadius;
        }

        /// <summary>
        /// The cloud a detonation with this fireball radius is drawn at. A radius below the
        /// minimum is raised to it, so a caller that skipped <see cref="Draws"/> still gets
        /// something sane rather than a cloud of zero size.
        /// </summary>
        public static NuclearCloudDimensions For(float fireballRadius)
        {
            // One ceiling, applied here and nowhere else, so every factor below is taken from a
            // fireball the cloud is allowed to be built from and the proportions never move.
            float fb = EffectCeiling.Soft(fireballRadius,
                MinimumFireballRadius, FireballKnee, FireballCeiling);

            var d = new NuclearCloudDimensions();
            d.FireballRadius = fb;
            // The fireball belongs to ExplosionFallback here, not to this cloud - unlike the
            // nuclear effect, which draws its own. Left at zero so nothing reads it by accident.
            d.FireballSeconds = 0f;

            d.CloudTop = fb * CloudTopFactor;
            d.CapBase = d.CloudTop * CapBaseFraction;
            d.CapDepth = d.CloudTop - d.CapBase;
            d.CapRadius = fb * CapRadiusFactor;
            d.StemRadius = fb * StemRadiusFactor;
            d.FireFieldRadius = d.CapRadius * FireFieldFactor;

            d.RiseSeconds = EffectCeiling.Soft(
                RiseSecondsPerRootMetre * (float)Math.Sqrt(fb),
                RiseSecondsMin, RiseSecondsKnee, RiseSecondsCeiling);
            d.HoldSeconds = Clamp(d.RiseSeconds * HoldFactor, HoldSecondsMin, HoldSecondsMax);
            d.FadeSeconds = Clamp(d.RiseSeconds * FadeFactor, FadeSecondsMin, FadeSecondsMax);
            return d;
        }

        /// <summary>The whole thing's length, in seconds - rise, stand and fade.</summary>
        public static float ShowSeconds(NuclearCloudDimensions d)
        {
            return d.RiseSeconds + d.HoldSeconds + d.FadeSeconds;
        }

        private static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            return v > max ? max : v;
        }
    }
}
