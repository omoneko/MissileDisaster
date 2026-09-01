using System;
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
        public float RiseSeconds;      // how long the cloud takes to form, after time compression
        public float HoldSeconds;      // how long it then stands at full size
        public float FadeSeconds;      // how long the staggered thinning takes at the end
        public float FireFieldRadius;  // how far out the burning city feeds smoke into the cloud
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
        /// It is applied to the cloud - cap, stem and height alike, one number for all three - so
        /// every proportion the model was checked against the photographs for survives it. That
        /// is not a nicety. Height is what runs off the top of a screen, so the temptation is to
        /// squash the height alone; doing exactly that is what turned the effect into a pancake
        /// on a lump, with a 150 kt column 459 m across and 375 m tall. A stem wider than it is
        /// tall is not a stem, and no arrangement of particles reads as a mushroom from there.
        /// If a cloud has to be shorter, this is the number to lower - it takes the width with
        /// it, which is the point.
        ///
        /// At 0.06 a 150 kt cloud stands 783 m over a 431 m canopy, which is the shape the
        /// figures give it and fits in frame at the zoom the game is played at.
        /// </summary>
        public const float CloudScale = 0.06f;

        /// <summary>
        /// Extra height, on top of CloudScale, applied to the column alone.
        ///
        /// <para>
        /// A playtest asked for twice the height because the large yields read as squat, and
        /// raising the drawn ceiling did almost nothing - at 150 kt the cloud is drawn 795 m
        /// tall against a ceiling of 2000 m, so the ceiling was never what held it down.
        /// CloudScale was, and lowering or raising that takes the width with it, which is
        /// exactly what must not happen here: the complaint is the proportion, not the size.
        /// </para>
        ///
        /// So the height gets its own multiplier. The clouds come out taller than the figures
        /// strictly give them - a deliberate departure, in the same spirit as FireballScale
        /// being nearly three times CloudScale, and for the same reason: a game is watched from
        /// low angles that the photographs were not taken at.
        /// </summary>
        public const float CloudHeightScale = 2f;

        /// <summary>
        /// The fireball gets its own, larger scale - nearly three times the cloud's.
        ///
        /// It is the smallest part of the effect and the only part judged against the buildings
        /// around it rather than against the cloud, so shrinking it in step with a cloud brought
        /// down to a fifth leaves a spark under a mushroom. Leaving it at full size does not work
        /// either: a real 50 Mt fireball is 8 km across, which against a canopy the ceiling has
        /// already compressed would be nearly as wide as the cloud itself. Two and a half times
        /// its share puts it at about a third of the canopy's width at every yield, against the
        /// tenth to a seventh it really is.
        ///
        /// Raised twice on playtest - 0.16 to 0.26 to 0.38 - because the fireball kept being
        /// upstaged by the cloud that follows it. The flash is the moment of the strike, and at
        /// 0.38 a 150 kt fireball is 310 m across: bigger than a city block, which is the read
        /// it needs against the buildings around it.
        /// </summary>
        public const float FireballScale = 0.38f;

        /// <summary>
        /// The ceiling on the drawn cloud height. It began life equal to the airburst ceiling
        /// (ModConfig.MaxBurstAltitude, 1000 m), on the argument that nothing should be drawn
        /// above the highest thing the mod will put in the sky; the playtest overruled that -
        /// the clouds read better standing taller than the camera's usual frame, which pans up
        /// a mushroom naturally - so it is now double, and deliberately decoupled from the
        /// burst ceiling, which has its own flight-path reasons to stay where it is.
        /// </summary>
        /// Doubled again after a second playtest: at 2000 m the largest yields still read as
        /// squat, because the cap's width goes on growing past the knee while the height is
        /// compressed, so the very clouds that should tower were the flattest on screen.
        public const float ScreenTopAltitude = 4000f;

        /// <summary>
        /// Where the soft ceiling on the drawn height starts to bite. It sits high enough that
        /// everything up to about a megaton is drawn in proportion and the ceiling is only a
        /// safety net - a guarantee that no yield, however absurd, can put its canopy where the
        /// player cannot see it. Above it the height is compressed while the width is not, so
        /// the very largest clouds do come out wider than their share; that is the price of the
        /// guarantee, and it is paid only by weapons nobody has ever built but Tsar Bomba.
        /// </summary>
        /// Doubled with ScreenTopAltitude, so everything that was drawn in proportion still is
        /// and the compression starts twice as high - which is where the extra height goes.
        public const float CloudTopDrawnKnee = 2800f;

        /// <summary>
        /// How much further the cap spreads sideways than the true proportion, on top of
        /// CloudScale. The playtest verdict on the honest shape was that the cap wanted to be
        /// broader - a game is watched from low angles the photographs were not taken at - so
        /// the cap alone is widened; the column keeps its true width against the unwidened cap,
        /// or the mushroom loses its stem-to-cap contrast.
        /// </summary>
        public const float CapWidthScale = 1.3f;

        /// <summary>
        /// Extra width for the cap at low yields, on top of CapWidthScale.
        ///
        /// <para>
        /// The figures are right and the small clouds still read wrong: at 15 kt the canopy is
        /// only 120 m of radius against a column drawn 795 m tall, so the mushroom comes out as
        /// a thin spike with a knob on it. The proportion is honest - a small cloud really is
        /// narrow for its height - but it is not what a 15 kt shot looks like in the photographs
        /// people remember, because those were taken from far enough away to foreshorten it.
        /// </para>
        ///
        /// So the boost is full at and below LowYieldKilotons and gone by LowYieldFadesByKilotons,
        /// interpolated on log10 between them - the same scale every other yield law here uses.
        /// The large yields, which already read correctly, are untouched.
        /// </summary>
        public const float LowYieldCapWidthBoost = 1.25f;
        public const float LowYieldKilotons = 30f;
        public const float LowYieldFadesByKilotons = 200f;

        /// <summary>
        /// The cap's total width multiplier at this yield: CapWidthScale, plus the low-yield
        /// boost where it applies.
        /// </summary>
        public static float CapWidthFactor(float kilotons)
        {
            if (kilotons <= 0f) return CapWidthScale * LowYieldCapWidthBoost;
            if (kilotons <= LowYieldKilotons) return CapWidthScale * LowYieldCapWidthBoost;
            if (kilotons >= LowYieldFadesByKilotons) return CapWidthScale;

            double from = Math.Log10(LowYieldKilotons);
            double to = Math.Log10(LowYieldFadesByKilotons);
            float t = (float)((Math.Log10(kilotons) - from) / (to - from));
            float boost = LowYieldCapWidthBoost + (1f - LowYieldCapWidthBoost) * t;
            return CapWidthScale * boost;
        }

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

        // Time. A real cloud takes ten minutes to stabilise and then stands for an hour; the
        // playtest verdict on a 31 s rise and a minute of lingering was "too slow to form and
        // stays too long". So the compression is steep: the cloud forms in seconds - fast enough
        // to be a spectacle rather than a wait - stands for a moment at full size, and thins
        // away. The whole 150 kt shot is about 25 s.
        // Eased from 45 with the growth curve - see CloudAnimation.RiseEasePower - because the
        // column read as being pushed up rather than climbing. The two go together: a softer
        // curve over a slightly longer rise, rather than either on its own.
        public const float RiseCompression = 38f;
        public const float RiseSecondsMin = 5f;
        public const float RiseSecondsKnee = 10f;
        public const float RiseSecondsCeiling = 16f;

        // How long it stands, against how long it rose, and how long the fade takes. The fade
        // is deliberately longer than the rise: a cloud that takes eight seconds to form and
        // vanishes in a blink reads as a deletion, and a real one takes far longer to shred
        // than to rise. The thinning is staggered per puff on top of this - see CloudPuffs.
        // Lengthened on a Workshop report that the smoke vanishes too quickly. The rise is
        // untouched - how fast the cloud goes UP was tuned against a playtest that found a
        // slow one a wait rather than a spectacle - so all of this buys time at the end,
        // where the complaint actually was: it stands longer and thins out far more slowly.
        public const float HoldFactor = 1.9f;
        public const float HoldSecondsMin = 13f;
        public const float HoldSecondsMax = 26f;
        public const float FadeFactor = 2.8f;
        public const float FadeSecondsMin = 20f;
        public const float FadeSecondsMax = 33f;

        // How far out the burning city feeds smoke into the cloud, against the cap. The real
        // burn radius is kilometres - the whole map at strategic yields - so the drawn field is
        // tied to the cap the way everything else is.
        public const float FireFieldFactor = 2.5f;

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
            // Where the canopy's underside sits as a fraction of the cloud - the tropopause rule,
            // carried across as a ratio so that squashing the height cannot break it.
            float baseFraction = capBase / cloudTop;

            var d = new NuclearCloudDimensions();
            // The fireball is brought down less far than the cloud around it.
            d.FireballRadius = EffectCeiling.Soft(NuclearCloud.FireballRadius(kt),
                FireballRadiusMin, FireballRadiusKnee, FireballRadiusCeiling) * FireballScale;
            d.FireballSeconds = EffectCeiling.Soft(NuclearCloud.FireballSeconds(kt),
                FireballSecondsMin, FireballSecondsKnee, FireballSecondsCeiling);
            d.CapRadius = capRadius * CloudScale * CapWidthFactor(kt);
            d.CloudTop = EffectCeiling.Soft(cloudTop * CloudScale * CloudHeightScale,
                CloudTopDrawnKnee, ScreenTopAltitude);
            d.CapBase = d.CloudTop * baseFraction;
            d.CapDepth = d.CloudTop - d.CapBase;
            // The stem's width is taken from the cap as the figures give it, before the lateral
            // spread, so widening the cap cannot thicken the column under it.
            d.StemRadius = capRadius * CloudScale * NuclearCloud.StemFraction(kt);
            d.RiseSeconds = EffectCeiling.Soft(NuclearCloud.StabiliseSeconds(kt) / RiseCompression,
                RiseSecondsMin, RiseSecondsKnee, RiseSecondsCeiling);
            float hold = d.RiseSeconds * HoldFactor;
            if (hold < HoldSecondsMin) hold = HoldSecondsMin;
            if (hold > HoldSecondsMax) hold = HoldSecondsMax;
            d.HoldSeconds = hold;
            float fade = d.RiseSeconds * FadeFactor;
            if (fade < FadeSecondsMin) fade = FadeSecondsMin;
            if (fade > FadeSecondsMax) fade = FadeSecondsMax;
            d.FadeSeconds = fade;
            d.FireFieldRadius = d.CapRadius * FireFieldFactor;
            return d;
        }
    }
}
