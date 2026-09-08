using System;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// The two stages that follow a non-nuclear detonation's fireball: the thin condensation
    /// shell thrown out behind the shock front, and the column of dirt and smoke that climbs
    /// after it and heads over into a small mushroom.
    ///
    /// <para>
    /// The column is drawn by the same machinery as the nuclear one - Core.CloudPuffs placing
    /// every puff along the vortex-ring flow each frame, through MushroomCloudPuffsFx - only at
    /// the dimensions Core.ConventionalCloudDisplay gives it. That is the whole fix here. The
    /// previous version was free-running particles released in staggered bursts, which is the
    /// technique the nuclear cloud was rebuilt to get away from: particles are given a velocity
    /// at birth and then drift, so the crowd cannot hold a shape. It had a further problem of its
    /// own - the cap was spawned at its final height on a fixed delay while the column was still
    /// climbing, so the head arrived seconds before the stem it was supposed to be sitting on and
    /// the effect read as a cap with nothing under it.
    /// </para>
    ///
    /// Order on screen, which falls out of the timings rather than being sequenced by hand:
    ///
    ///  1. the fireball, drawn by ExplosionFallback at the moment of the burst
    ///  2. the condensation shell, a beat later and gone inside a second
    ///  3. the column, born small enough to be hidden inside the fireball (CloudAnimation's birth
    ///     fraction) and climbing out of it as the flame dies
    ///
    /// Main thread only.
    /// </summary>
    public static class ConventionalMushroomFx
    {
        // Thin, cold and barely there: this is water condensing out of the air behind the front,
        // not smoke. The alpha is low because the shell is meant to be seen through.
        private static readonly Color Condensation = new Color(0.95f, 0.96f, 1f, 0.30f);
        private const int CondensationParticles = 40;
        private const float CondensationEmitFraction = 0.65f; // of the shell radius, where the puffs start
        private const float CondensationSpreadFactor = 0.55f; // of the radius per second, outward

        // The dirt thrown up around the foot of the column. The nuclear cloud has the same thing
        // and for the same reason: CloudPuffs spaces the column's puffs by an ease-out climb, so
        // they bunch near the top and the lower third of the stem is thin. Rendered on its own it
        // reads as a column that has come away from the ground. This is what joins it back on -
        // and it is also just what a groundburst does, which is why it was there first.
        private const int DustParticles = 90;
        private const float DustConeFraction = 2.1f;   // of the stem radius: how wide the skirt spreads
        private const float DustConeAngle = 24f;
        private const float DustLifeFraction = 1.1f;   // of the rise
        private const float DustEmitFraction = 0.8f;   // of the rise, how long it keeps feeding
        // Metres a second, against the stem radius. Set so the skirt climbs about halfway up the
        // column over its life: measured in tools/effect-preview/cloud_preview.py, which draws
        // this alongside the puffs for exactly this reason. At a tenth of this it never left the
        // rooftops and the gap it exists to close was still there.
        /// <summary>
        /// How high the afterwind dust is drawn, against the base of the cap.
        ///
        /// It used to climb at a fixed 2.8 x the stem radius per second for its whole life, which
        /// is not a height at all - it is a speed with nothing to stop it. At 1.5 t that carried
        /// the dust 195 m up through a cloud only 155 m tall, and at 150 kt 1890 m through a
        /// 1589 m one: in both cases a straight column of smoke overtaking the mushroom and
        /// carrying on past it, which is exactly what was reported. The speed is now solved from
        /// where the dust is meant to end up - the underside of the cap - so it arrives there and
        /// stops instead of setting off and never arriving.
        /// </summary>
        private const float DustTopOfCapBase = 0.9f;
        private static readonly Color DustLight = new Color(0.55f, 0.49f, 0.40f, 0.75f);
        private static readonly Color DustDark = new Color(0.32f, 0.28f, 0.23f, 0.75f);

        /// <summary>
        /// Plays both stages. groundZero is the spot on the ground the column rises from and
        /// burstPoint is where the warhead actually went off - the same point for a groundburst,
        /// the burst altitude above it for an airburst, which is where the condensation forms.
        /// A fireball too small to lift a column draws nothing. A failure here never stops the
        /// impact resolving.
        /// </summary>
        public static void Play(Vector3 groundZero, Vector3 burstPoint, float fireballRadius, bool airburst)
        {
            if (!ConventionalCloudDisplay.Draws(fireballRadius)) return;
            try
            {
                CreateCondensation(burstPoint,
                    fireballRadius * ConventionalCloudDisplay.CondensationRadiusFactor);

                NuclearCloudDimensions d = ConventionalCloudDisplay.For(fireballRadius);
                CreateGroundDust(groundZero, d.StemRadius, d.RiseSeconds, d.CapBase);
                MushroomCloudPuffsFx.Create("ConventionalMushroomCloud", groundZero, d, airburst);

                // The base surge: the dome of dirt that rolls out from the foot of the column
                // and grows until it swallows the mushroom. A ground burst only - an airburst
                // has nothing in contact with the ground to scour. See Core.GroundDust.
                if (!airburst)
                {
                    GroundDustFx.Create("ConventionalBaseSurge", groundZero,
                        d.CapRadius, d.CloudTop, d.RiseSeconds);
                }


                ModConfig.Log(string.Format(
                    "conventional cloud: {0:F0} m fireball, column {1:F0} m tall, "
                    + "cap {2:F0} m across, up for {3:F0} s",
                    fireballRadius, d.CloudTop, d.CapRadius * 2f,
                    ConventionalCloudDisplay.ShowSeconds(d)));
            }
            catch (Exception e)
            {
                ModConfig.LogError("ConventionalMushroomFx.Play error: " + e);
            }
        }

        /// <summary>
        /// The skirt of dirt around the foot of the column, boiling up for most of the climb.
        /// It is drawn with the opaque-cored cloud material rather than the thin smoke one: this
        /// has to be the solid base the column stands on, not a haze around it.
        /// </summary>
        private static void CreateGroundDust(Vector3 groundZero, float stemRadius, float rise,
            float capBase)
        {
            float life = rise * DustLifeFraction;
            var go = ParticleBuilder.NewSystem("ConventionalGroundDust", groundZero, ParticleAssets.Cloud);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = life;
            main.startSpeed = stemRadius * 0.15f;
            main.startSize = stemRadius * 0.9f;
            main.startColor = new ParticleSystem.MinMaxGradient(DustLight, DustDark);
            main.maxParticles = DustParticles * 2;
            main.duration = rise * DustEmitFraction;
            main.loop = false;

            ParticleBuilder.Stream(ps, DustParticles * 0.95f / life);
            ParticleBuilder.ConeUp(ps, stemRadius * DustConeFraction, DustConeAngle);
            // Solved from the destination, not dialled in: it reaches the underside of the cap
            // over its own life and no further.
            ParticleBuilder.Rise(ps, life > 0f ? capBase * DustTopOfCapBase / life : 0f);
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0.8f, 0.4f),
                new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, 0.6f, 1.9f);
            ParticleBuilder.PlayAndDestroy(go, rise * DustEmitFraction + life + 1f);
        }

        /// <summary>
        /// The condensation shell: a wide, thin, briefly-lived dome of white that expands and
        /// vanishes. Deliberately its own small effect rather than a call into the nuclear one -
        /// that shell is sized and timed against a fireball measured in hundreds of metres, and
        /// what reads as a flash of vapour there reads as fog sitting on the street here.
        /// </summary>
        private static void CreateCondensation(Vector3 burstPoint, float radius)
        {
            float life = ConventionalCloudDisplay.CondensationSeconds;
            float delay = ConventionalCloudDisplay.CondensationDelaySeconds;

            var go = ParticleBuilder.NewSystem("ConventionalCondensation", burstPoint, ParticleAssets.Smoke);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startDelay = delay;      // the front has to get out past the fireball first
            main.startLifetime = life;
            main.startSpeed = radius * CondensationSpreadFactor;
            main.startSize = radius * 0.45f;
            main.startColor = new ParticleSystem.MinMaxGradient(Condensation);
            main.maxParticles = CondensationParticles * 2;

            ParticleBuilder.Burst(ps, CondensationParticles);
            ParticleBuilder.Hemisphere(ps, radius * CondensationEmitFraction);
            // In almost at once and out over the back half: a condensation cloud does not
            // dissipate gently, it re-evaporates the moment the pressure comes back up.
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, 0.7f, 1.6f);
            ParticleBuilder.PlayAndDestroy(go, delay + life + 0.5f);
        }
    }
}
