namespace MissileDisaster.Core
{
    /// <summary>
    /// The dimensions a nuclear detonation is actually drawn at, in metres and seconds. Pure,
    /// with no UnityEngine dependency, so that what ends up on screen can be tested rather than
    /// only looked at.
    /// </summary>
    public struct NuclearCloudDimensions
    {
        public float FireballRadius;   // metres - see FireballScale
        public float FireballSeconds;  // how long the fireball takes to swell to that radius
        public float CapRadius;        // the stabilised canopy's radius
        public float StemRadius;       // the column, narrow against the cap
        public float CloudTop;         // the height of the top of the cap above the ground
        public float CapBase;          // the canopy's underside - where the cloud stopped rising
        public float CapDepth;         // CloudTop - CapBase
        public float RiseSeconds;      // how long the column takes to climb, after time compression
    }

    /// <summary>
    /// Turns a yield in kilotons into the dimensions the effect is drawn at: the real figures
    /// from NuclearCloud, held under the engineering ceilings by EffectCeiling, and then brought
    /// down to a size a player can actually watch.
    ///
    /// Three separate things are going on here, and they are worth keeping apart:
    ///
    ///   - the figures, which are real up to about 15 Mt. Beyond that the Glasstone cap-radius
    ///     fit is a cubic in log10(W) extrapolated past the charts it was fitted to, and it runs
    ///     away: it asks for a 140 km cap at 50 Mt where Castle Bravo's, at 15 Mt, was measured
    ///     at about 50 km and Tsar Bomba's at about the same. The fit is left alone - it is
    ///     physics, and correcting it by eye would be worse - and the ceiling absorbs it
    ///   - the ceilings, which are engineering limits: how much cloud a 17 km map and a particle
    ///     budget can carry. They are soft, so that the yield is never thrown away. A 1.2 Mt
    ///     cloud, a 10 Mt one and a 50 Mt one are all visibly different sizes, which under the
    ///     old hard clamps they were not
    ///   - the scale, which is a straight admission that a real cloud does not fit on a screen
    ///
    /// The knees are set at the old hard clamps, so every yield that was already drawn to its
    /// real figures still is, to the metre, before the scale is applied.
    /// </summary>
    public static class NuclearCloudDisplay
    {
        /// <summary>
        /// How large the cloud is drawn against its real size. This is the one deliberate
        /// departure from the figures in the whole model, and it is a playability decision, not
        /// a physical one: a real 150 kt cloud stands 13 km tall, and at the zoom Cities:
        /// Skylines is played at the player sees a column disappearing off the top of the screen
        /// rather than a mushroom.
        ///
        /// It is applied to the cloud - cap, stem and height alike - so every proportion the
        /// model was checked against the photographs for survives it unchanged.
        ///
        /// This is the number to change to make clouds larger or smaller across the board. At
        /// 0.20 a 150 kt cloud stands 2.6 km, which fits in frame from a few kilometres back at
        /// the height the game is normally played at; 0.30 is a taller cloud that starts to run
        /// off the top of the screen.
        /// </summary>
        public const float CloudScale = 0.20f;

        /// <summary>
        /// The fireball gets its own, larger scale - two and a half times the cloud's.
        ///
        /// It is the smallest part of the effect and the only part judged against the buildings
        /// around it rather than against the cloud, so shrinking it in step with a cloud brought
        /// down to a fifth leaves a spark under a mushroom. Leaving it at full size does not work
        /// either: a real 50 Mt fireball is 8 km across, which against a canopy the ceiling has
        /// already compressed would be nearly as wide as the cloud itself. Two and a half times
        /// its share puts it at about a third of the canopy's width at every yield, against the
        /// tenth to a seventh it really is.
        /// </summary>
        public const float FireballScale = 0.50f;

        // The tropopause: the lid the canopy spreads out under, in real metres, before the
        // scale. Through the troposphere the air gets colder with height, so a fireball that
        // cools as it expands stays warmer than what surrounds it and keeps climbing; at the
        // tropopause the temperature stops falling and begins to rise, the cloud's buoyancy is
        // gone within a kilometre or two, and it has nowhere left to go but sideways. That is
        // what a cap is, and where its underside is.
        // 11 km is the mid-latitude value, which is where most of the charts the model is fitted
        // to were measured. It is nearer 17 km over the tropics, which is part of why the
        // Pacific tests spread wider than the fit expects.
        public const float TropopauseAltitude = 11000f;

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

        // The canopy, in real metres. The playable map is about 17 km across, so the knee - the
        // old hard clamp - is already a cap that spans half of it, and the ceiling is a cloud
        // that covers the map one and a half times over.
        public const float CapRadiusMin = 200f;
        public const float CapRadiusKnee = 8000f;
        public const float CapRadiusCeiling = 26000f;

        // The column's height, in real metres. The old hard clamp of 12 km cut into even the
        // 150 kt baseline, whose real top is 13.3 km; the knee is left there so nothing below it
        // moves, and the ceiling is about the top of a 50 Mt cloud.
        public const float CloudTopMin = 800f;
        public const float CloudTopKnee = 12000f;
        public const float CloudTopCeiling = 30000f;

        // Time. A real cloud takes ten minutes to stabilise and then stands for an hour. This is
        // what the rise is divided by, and the bounds it is then held inside: long enough that
        // the dust visibly wells up and the column visibly climbs, and bounded so that a
        // strategic warhead is not still rising two minutes later.
        public const float RiseCompression = 12f;
        public const float RiseSecondsMin = 12f;
        public const float RiseSecondsKnee = 40f;
        public const float RiseSecondsCeiling = 60f;

        /// <summary>
        /// The dimensions for a yield in kilotons. Zero or less falls back to the 150 kt
        /// baseline, so a warhead that somehow arrives without a yield still draws something.
        /// </summary>
        public static NuclearCloudDimensions For(float kilotons)
        {
            float kt = kilotons > 0f ? kilotons : NuclearYields.StandardKilotons;

            // Real metres first, so the tropopause is compared against a real altitude.
            float capRadius = EffectCeiling.Soft(NuclearCloud.CloudRadius(kt),
                CapRadiusMin, CapRadiusKnee, CapRadiusCeiling);
            float cloudTop = EffectCeiling.Soft(NuclearCloud.CloudTop(kt),
                CloudTopMin, CloudTopKnee, CloudTopCeiling);
            float capBase = cloudTop * 0.5f;
            if (capBase > TropopauseAltitude) capBase = TropopauseAltitude;

            var d = new NuclearCloudDimensions();
            // The fireball is brought down less far than the cloud around it.
            d.FireballRadius = EffectCeiling.Soft(NuclearCloud.FireballRadius(kt),
                FireballRadiusMin, FireballRadiusKnee, FireballRadiusCeiling) * FireballScale;
            d.FireballSeconds = EffectCeiling.Soft(NuclearCloud.FireballSeconds(kt),
                FireballSecondsMin, FireballSecondsKnee, FireballSecondsCeiling);
            d.CapRadius = capRadius * CloudScale;
            d.CloudTop = cloudTop * CloudScale;
            d.CapBase = capBase * CloudScale;
            d.CapDepth = d.CloudTop - d.CapBase;
            d.StemRadius = d.CapRadius * NuclearCloud.StemFraction(kt);
            d.RiseSeconds = EffectCeiling.Soft(NuclearCloud.StabiliseSeconds(kt) / RiseCompression,
                RiseSecondsMin, RiseSecondsKnee, RiseSecondsCeiling);
            return d;
        }
    }
}
