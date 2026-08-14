using System;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// A nuclear detonation, built to the figures in MissileDisaster.Core.NuclearCloud rather
    /// than to taste. Main thread only.
    ///
    /// The mushroom is a cloud of soft smoke puffs driven along the flow a real one has - the
    /// vortex ring - by Core.CloudPuffs, through MushroomCloudPuffsFx placing every puff every
    /// frame. Free-running particles could not hold the silhouette and a solid mesh read as a
    /// model growing out of the ground; computed puffs are both at once, a mushroom that is
    /// visibly made of boiling cloud. It grows out of the fireball with the column climbing
    /// first and the cap rolling over after it, stands, and thins away (Core.CloudAnimation).
    ///
    /// Around it, the other stages of the real thing:
    ///
    ///  1. the fireball at the point of burst, swelling and cooling white through orange to a
    ///     dull red - it covers the cloud's small beginnings
    ///  2. the condensation cloud, the white dome that flashes into being behind the shock front
    ///     and is gone within a second or two
    ///  3. the dust the afterwinds tear off the ground and feed into the base of the column
    ///
    /// Every size and duration comes from MissileDisaster.Core.NuclearCloudDisplay: real figures
    /// under soft ceilings, so a bigger warhead is always a bigger cloud, right up to Tsar Bomba.
    /// </summary>
    public static class NuclearMushroomFx
    {
        // The condensation dome forms out where the shock has passed, well beyond the fireball,
        // and lasts barely a second.
        private const float CondensationRadiusFactor = 2.6f;
        private const float CondensationLifetime = 1.3f;

        // Big soft billboards get clamped by the renderer's default screen-size cap, which
        // would shrink the cloud exactly when the camera is close enough to admire it.
        private const float PuffMaxScreenFraction = 4f;

        /// <summary>
        /// How much longer a fireball particle lives than the fireball takes to swell. Above 1
        /// it goes on glowing after it has reached full size, which is what a real one does as
        /// it cools - but only briefly. Lowered from 1.7 on playtest: at that value the ball was
        /// still burning while the column was well clear of it, and the strike read as two
        /// separate events rather than one.
        /// </summary>
        private const float FireballLingerFactor = 1.15f;

        // Colours, following how a real detonation looks rather than a palette: white hot, then
        // sodium yellow, then orange, then the brown of nitrogen dioxide and lofted earth.
        private static readonly Color FireballCore = new Color(1f, 0.99f, 0.94f, 1f);
        private static readonly Color FireballMid = new Color(1f, 0.82f, 0.35f, 1f);
        private static readonly Color FireballEdge = new Color(1f, 0.42f, 0.10f, 1f);
        private static readonly Color FireballCool = new Color(0.42f, 0.13f, 0.05f, 1f);
        private static readonly Color Condensation = new Color(0.96f, 0.97f, 1f, 0.42f);
        private static readonly Color DustLight = new Color(0.55f, 0.49f, 0.40f, 0.75f);
        private static readonly Color DustDark = new Color(0.32f, 0.28f, 0.23f, 0.75f);

        /// <summary>
        /// Plays the whole detonation. groundZero is the spot on the ground the cloud rises from,
        /// detonation is where the warhead actually went off - the same point for a groundburst,
        /// the burst altitude above it for an airburst - kilotons is the yield everything is
        /// built from, and airburst decides how dirty the canopy stays. A yield of zero or less
        /// falls back to the 150 kt baseline.
        /// </summary>
        public static void Play(Vector3 groundZero, Vector3 detonation, float kilotons, bool airburst)
        {
            try
            {
                NuclearCloudDimensions d = NuclearCloudDisplay.For(kilotons);
                float showSeconds = d.RiseSeconds + d.HoldSeconds + d.FadeSeconds;

                // A nuclear strike is rare enough that one line about it is not noise, and it is
                // the only way to tell from a log what the effect actually drew.
                ModConfig.LogAlways(string.Format(
                    "nuclear {0:F0} kt {1}: cloud top {2:F0} m, cap {3:F0} m wide, " +
                    "fireball {4:F0} m across, up for {5:F0} s",
                    kilotons > 0f ? kilotons : NuclearYields.StandardKilotons,
                    airburst ? "airburst" : "groundburst",
                    d.CloudTop, d.CapRadius * 2f, d.FireballRadius * 2f, showSeconds));

                CreateFireball(detonation, d.FireballRadius, d.FireballSeconds);
                CreateCondensationDome(detonation, d.FireballRadius * CondensationRadiusFactor, d.FireballSeconds);
                CreateGroundDust(groundZero, d.StemRadius, d.RiseSeconds);
                CreateCloudPuffs(groundZero, d, airburst);
            }
            catch (Exception e)
            {
                ModConfig.LogError("NuclearMushroomFx.Play error: " + e);
            }
        }

        /// <summary>
        /// The fireball: born a fraction of its final size, swelling to it over fireballT and
        /// cooling from white through orange to a dull red as it lifts. The size curve does the
        /// swelling, so the growth is the visible thing rather than a puff appearing full size.
        /// </summary>
        private static void CreateFireball(Vector3 center, float radius, float fireballT)
        {
            // Where a particle is born and how large it has grown by the end have to add up to
            // the fireball's radius, the same budget the canopy and the column are on. They did
            // not: a ball emitted over 0.28 R and grown to 0.55 R across reached only 0.83 R, so
            // a fireball whose figure is right was drawn a sixth short of it - and short is the
            // one direction it could not afford, being the smallest thing in the effect.
            const float emitFraction = 0.35f;
            const float growth = 2.0f;
            float finalDiameter = 2f * radius * (1f - emitFraction);

            var go = ParticleBuilder.NewSystem("NuclearFireball", center, ParticleAssets.Fire);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = fireballT * FireballLingerFactor;
            main.startSpeed = radius * 0.05f;
            main.startSize = finalDiameter / growth; // the size curve below swells it to full
            main.startColor = new ParticleSystem.MinMaxGradient(FireballCore, FireballMid);
            main.maxParticles = 240;

            ParticleBuilder.Burst(ps, 120);
            ParticleBuilder.Sphere(ps, radius * emitFraction);
            ParticleBuilder.LimitSpeed(ps, radius * 0.05f, 0.15f);
            ParticleBuilder.Rise(ps, radius * 0.12f); // the ball lifts as it burns, dragging the stem up after it

            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(FireballCore, 0f), new GradientColorKey(FireballMid, 0.25f),
                    new GradientColorKey(FireballEdge, 0.6f), new GradientColorKey(FireballCool, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f),
                    new GradientAlphaKey(0.55f, 0.8f), new GradientAlphaKey(0f, 1f),
                });
            ParticleBuilder.Colour(ps, grad);
            ParticleBuilder.SizeCurve(ps, 0.35f, growth);
            ParticleBuilder.PlayAndDestroy(go, fireballT * 1.7f + 1f);
        }

        /// <summary>
        /// The condensation cloud. It appears a beat after the burst, out where the shock has
        /// already gone by, as a dome that turns into a ring and vanishes - which is exactly what
        /// a wide, thin, briefly-lived shell of white particles does on its own.
        /// </summary>
        private static void CreateCondensationDome(Vector3 center, float radius, float fireballT)
        {
            var go = ParticleBuilder.NewSystem("NuclearCondensation", center, ParticleAssets.Smoke);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startDelay = fireballT * 0.3f; // a second or two at the yields normally used
            main.startLifetime = CondensationLifetime;
            main.startSpeed = radius * 0.35f;
            main.startSize = radius * 0.5f;
            main.startColor = new ParticleSystem.MinMaxGradient(Condensation);
            main.maxParticles = 140;

            ParticleBuilder.Burst(ps, 70);
            ParticleBuilder.Hemisphere(ps, radius * 0.7f);
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, 0.8f, 1.5f);
            ParticleBuilder.PlayAndDestroy(go, fireballT * 0.3f + CondensationLifetime + 0.5f);
        }

        /// <summary>
        /// The dirt the afterwinds tear off the ground. The updraft under the rising ball pulls
        /// air in along the surface and up into the base of the column, which is where a
        /// groundburst's fallout comes from.
        /// </summary>
        private static void CreateGroundDust(Vector3 groundZero, float stemR, float rise)
        {
            // The afterwinds blow for as long as the column is climbing, so the dust boils up
            // around its base for most of the rise rather than for a few seconds at the start -
            // this is the part of the effect the eye reads as the cloud welling up out of the
            // ground, and it used to be over before the column was halfway.
            float life = rise * 0.55f;
            var go = ParticleBuilder.NewSystem("NuclearGroundDust", groundZero, ParticleAssets.Smoke);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = life;
            main.startSpeed = stemR * 0.06f;
            main.startSize = stemR * 0.5f;
            main.startColor = new ParticleSystem.MinMaxGradient(DustLight, DustDark);
            main.maxParticles = 420;
            main.duration = rise * 0.75f;
            main.loop = false;

            ParticleBuilder.Stream(ps, 420f * 0.95f / life);
            ParticleBuilder.ConeUp(ps, stemR * 2.4f, 22f); // drawn inwards and up around the base
            ParticleBuilder.Rise(ps, stemR * 0.25f);
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, 0.6f, 1.9f);
            ParticleBuilder.PlayAndDestroy(go, rise * 0.75f + life + 1f);
        }

        /// <summary>
        /// The mushroom itself: a renderer-only ParticleSystem whose puffs MushroomCloudPuffsFx
        /// places along the vortex-ring flow every frame. Emission stays off - the component
        /// owns every particle - and the puffs are depth-sorted, because they are large, soft
        /// and overlapping, which is exactly when sorting artefacts show.
        /// </summary>
        private static void CreateCloudPuffs(Vector3 groundZero, NuclearCloudDimensions d, bool airburst)
        {
            var go = ParticleBuilder.NewSystem("NuclearMushroomCloud", groundZero, ParticleAssets.Cloud);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // puff positions are metres from ground zero
            main.maxParticles = CloudPuffs.TotalCount;
            var emission = ps.emission;
            emission.enabled = false;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = PuffMaxScreenFraction;

            var fx = go.AddComponent<MushroomCloudPuffsFx>();
            fx.Dims = d;
            fx.Seed = (int)(UnityEngine.Random.value * 1000000f); // each strike boils its own way
            fx.Airburst = airburst;
            ps.Play();
        }
    }
}
