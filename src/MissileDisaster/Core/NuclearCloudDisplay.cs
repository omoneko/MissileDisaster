namespace MissileDisaster.Core
{
    /// <summary>
    /// The dimensions a nuclear detonation is actually drawn at, in metres and seconds. Pure,
    /// with no UnityEngine dependency, so that what ends up on screen can be tested rather than
    /// only looked at.
    /// </summary>
    public struct NuclearCloudDimensions
    {
        public float FireballRadius;   // metres
        public float FireballSeconds;  // how long the fireball takes to swell to that radius
        public float CapRadius;        // the stabilised canopy's radius
        public float StemRadius;       // the column, narrow against the cap
        public float CloudTop;         // the height of the top of the cap above the ground
        public float RiseSeconds;      // how long the column takes to climb, after time compression
    }

    /// <summary>
    /// Turns a yield in kilotons into the dimensions the effect is drawn at: the real figures
    /// from NuclearCloud, held under the engineering ceilings by EffectCeiling.
    ///
    /// Two separate things are going on here, and they are worth keeping apart:
    ///
    ///   - the figures, which are real up to about 15 Mt. Beyond that the Glasstone cap-radius
    ///     fit is a cubic in log10(W) extrapolated past the charts it was fitted to, and it runs
    ///     away: it asks for a 140 km cap at 50 Mt where Castle Bravo's, at 15 Mt, was measured
    ///     at about 50 km and Tsar Bomba's at about the same. The fit is left alone - it is
    ///     physics, and correcting it by eye would be worse - and the ceiling absorbs it
    ///   - the ceilings, which are engineering limits: how much cloud a 17 km map and a particle
    ///     budget can carry. They are soft, so that the yield is never thrown away. A 1.2 Mt
    ///     cloud, a 10 Mt one and a 50 Mt one are all visibly different sizes, which under the
    ///     old hard clamps they were not - everything from about 950 kt upwards came out at
    ///     exactly the same 8 km cap.
    ///
    /// The knees are set at the old hard clamps, so every yield that was already drawn to its
    /// real figures still is, to the metre.
    /// </summary>
    public static class NuclearCloudDisplay
    {
        // The fireball. The knee sits above a 25 Mt fireball, so the whole catalogue up to Tsar
        // Bomba is drawn at its real radius.
        public const float FireballRadiusMin = 25f;
        public const float FireballRadiusKnee = 3000f;
        public const float FireballRadiusCeiling = 7000f;

        // How long it swells for. A 50 Mt fireball really burns for the better part of a minute;
        // the ceiling is what keeps that watchable.
        public const float FireballSecondsMin = 0.8f;
        public const float FireballSecondsKnee = 12f;
        public const float FireballSecondsCeiling = 20f;

        // The canopy. The playable map is about 17 km across, so the knee - the old hard clamp -
        // is already a cap that spans half of it, and the ceiling is a cloud that covers the map
        // one and a half times over.
        public const float CapRadiusMin = 200f;
        public const float CapRadiusKnee = 8000f;
        public const float CapRadiusCeiling = 26000f;

        // The column's height. The old hard clamp of 12 km cut into even the 150 kt baseline,
        // whose real top is 13.3 km; the knee is left there so nothing below it moves, and the
        // ceiling is about the top of a 50 Mt cloud.
        public const float CloudTopMin = 800f;
        public const float CloudTopKnee = 12000f;
        public const float CloudTopCeiling = 30000f;

        // Time. The real rise is minutes, so it is compressed by twenty-five to one and then
        // held between a device that is not over in a blink and one that is not still climbing
        // when the player has stopped watching.
        public const float RiseCompression = 25f;
        public const float RiseSecondsMin = 8f;
        public const float RiseSecondsKnee = 26f;
        public const float RiseSecondsCeiling = 40f;

        /// <summary>
        /// The dimensions for a yield in kilotons. Zero or less falls back to the 150 kt
        /// baseline, so a warhead that somehow arrives without a yield still draws something.
        /// </summary>
        public static NuclearCloudDimensions For(float kilotons)
        {
            float kt = kilotons > 0f ? kilotons : NuclearYields.StandardKilotons;

            var d = new NuclearCloudDimensions();
            d.FireballRadius = EffectCeiling.Soft(NuclearCloud.FireballRadius(kt),
                FireballRadiusMin, FireballRadiusKnee, FireballRadiusCeiling);
            d.FireballSeconds = EffectCeiling.Soft(NuclearCloud.FireballSeconds(kt),
                FireballSecondsMin, FireballSecondsKnee, FireballSecondsCeiling);
            d.CapRadius = EffectCeiling.Soft(NuclearCloud.CloudRadius(kt),
                CapRadiusMin, CapRadiusKnee, CapRadiusCeiling);
            d.StemRadius = d.CapRadius * NuclearCloud.StemFraction(kt);
            d.CloudTop = EffectCeiling.Soft(NuclearCloud.CloudTop(kt),
                CloudTopMin, CloudTopKnee, CloudTopCeiling);
            d.RiseSeconds = EffectCeiling.Soft(NuclearCloud.StabiliseSeconds(kt) / RiseCompression,
                RiseSecondsMin, RiseSecondsKnee, RiseSecondsCeiling);
            return d;
        }
    }
}
