using System;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// A nuclear detonation, built to the figures in MissileDisaster.Core.NuclearCloud rather
    /// than to taste. Main thread only.
    ///
    /// It runs as the real thing does, in five overlapping stages:
    ///
    ///  1. the fireball, at the point of burst. It is born small and blindingly white, swells to
    ///     its full radius over seconds - ten of them at 1 Mt - and cools through yellow and
    ///     orange to a dull red as it rises off the ground
    ///  2. the condensation cloud, the white dome that flashes into being a second or two behind
    ///     the front where the air rarefies behind the shock, and is gone within another second
    ///  3. the dust the afterwinds tear off the ground and feed into the base of the column
    ///  4. the stem, climbing at the rate the cloud really climbs, narrow against the cap - half
    ///     its width at 20 kt, a seventh of it in the megaton range
    ///  5. the cap, which swells at the top into a canopy of the stabilised cloud radius and
    ///     rolls over at the rim
    ///
    /// The one liberty taken is time. A real cloud takes about ten minutes to stabilise, so the
    /// rise is compressed by roughly twenty-five to one and held inside a range that is watchable
    /// without the player having to wait. Every dimension is the real one.
    ///
    /// Every size and duration comes from MissileDisaster.Core.NuclearCloudDisplay, which applies
    /// the engineering ceilings. They are soft: a bigger warhead is always a bigger cloud, right
    /// up to Tsar Bomba, rather than everything above a megaton coming out the same size.
    /// </summary>
    public static class NuclearMushroomFx
    {
        // The condensation dome forms out where the shock has passed, well beyond the fireball,
        // and lasts barely a second.
        private const float CondensationRadiusFactor = 2.6f;
        private const float CondensationLifetime = 1.3f;

        // How far up the column is when the cap starts to swell out of its head, as a fraction
        // of both the cloud top and the rise time. The two have to be the same number, or the
        // canopy appears somewhere the column is not.
        private const float CapEmergeFraction = 0.55f;

        // Colours, following how a real detonation looks rather than a palette: white hot, then
        // sodium yellow, then orange, then the brown of nitrogen dioxide and lofted earth.
        private static readonly Color FireballCore = new Color(1f, 0.99f, 0.94f, 1f);
        private static readonly Color FireballMid = new Color(1f, 0.82f, 0.35f, 1f);
        private static readonly Color FireballEdge = new Color(1f, 0.42f, 0.10f, 1f);
        private static readonly Color FireballCool = new Color(0.42f, 0.13f, 0.05f, 1f);
        private static readonly Color Condensation = new Color(0.96f, 0.97f, 1f, 0.42f);
        private static readonly Color DustLight = new Color(0.55f, 0.49f, 0.40f, 0.75f);
        private static readonly Color DustDark = new Color(0.32f, 0.28f, 0.23f, 0.75f);
        private static readonly Color CapWarm = new Color(0.40f, 0.31f, 0.24f, 0.72f); // NO2 and earth
        private static readonly Color CapCool = new Color(0.24f, 0.23f, 0.22f, 0.72f);

        /// <summary>
        /// Plays the whole detonation. groundZero is the spot on the ground the cloud rises from,
        /// detonation is where the warhead actually went off - the same point for a groundburst,
        /// the burst altitude above it for an airburst - and kilotons is the yield everything is
        /// built from. A yield of zero or less falls back to the 150 kt baseline.
        /// </summary>
        public static void Play(Vector3 groundZero, Vector3 detonation, float kilotons)
        {
            try
            {
                NuclearCloudDimensions d = NuclearCloudDisplay.For(kilotons);

                CreateFireball(detonation, d.FireballRadius, d.FireballSeconds);
                CreateCondensationDome(detonation, d.FireballRadius * CondensationRadiusFactor, d.FireballSeconds);
                CreateGroundDust(groundZero, d.StemRadius, d.RiseSeconds);
                CreateStem(groundZero, d.StemRadius, d.CloudTop, d.RiseSeconds);
                CreateCap(groundZero, d.CapRadius, d.CloudTop, d.RiseSeconds);
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
            var go = ParticleBuilder.NewSystem("NuclearFireball", center, ParticleAssets.Fire);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = fireballT * 1.7f;
            main.startSpeed = radius * 0.05f;
            main.startSize = radius * 0.55f; // the size curve below takes it from a third of this to full
            main.startColor = new ParticleSystem.MinMaxGradient(FireballCore, FireballMid);
            main.maxParticles = 240;

            ParticleBuilder.Burst(ps, 90);
            ParticleBuilder.Sphere(ps, radius * 0.28f);
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
            ParticleBuilder.SizeCurve(ps, 0.35f, 2.0f);
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
            float life = rise * 0.55f;
            var go = ParticleBuilder.NewSystem("NuclearGroundDust", groundZero, ParticleAssets.Smoke);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = life;
            main.startSpeed = stemR * 0.06f;
            main.startSize = stemR * 0.5f;
            main.startColor = new ParticleSystem.MinMaxGradient(DustLight, DustDark);
            main.maxParticles = 300;
            main.duration = rise * 0.4f;
            main.loop = false;

            ParticleBuilder.Stream(ps, 45f);
            ParticleBuilder.ConeUp(ps, stemR * 1.7f, 22f); // drawn inwards and up around the base
            ParticleBuilder.Rise(ps, stemR * 0.25f);
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, 0.6f, 1.9f);
            ParticleBuilder.PlayAndDestroy(go, rise * 0.4f + life + 1f);
        }

        /// <summary>
        /// The stem, climbing at the rate the cloud really climbs. Barely any outward speed: the
        /// column has to stay narrow for the whole thing to read as a mushroom under its cap.
        /// </summary>
        private static void CreateStem(Vector3 groundZero, float stemR, float top, float rise)
        {
            var go = ParticleBuilder.NewSystem("NuclearStem", groundZero, ParticleAssets.Smoke);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = rise + 8f; // the column stands well after it has finished rising
            main.startSpeed = stemR * 0.02f;
            main.startSize = stemR * 1.1f;
            main.startColor = new ParticleSystem.MinMaxGradient(DustDark, CapCool);
            main.maxParticles = 500;
            main.duration = rise;
            main.loop = false;

            ParticleBuilder.Stream(ps, 40f);
            ParticleBuilder.Sphere(ps, stemR);
            ParticleBuilder.Rise(ps, top / rise);
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.85f, 0.25f),
                new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, 0.8f, 1.6f);
            ParticleBuilder.PlayAndDestroy(go, rise + 9f);
        }

        /// <summary>
        /// The canopy. Its three contributions - where the particles are emitted, how far they
        /// drift in their lifetime and how large they have grown by the end - are set so that
        /// they add up to capR, the stabilised cloud radius. Letting the drift run free, as an
        /// untuned speed does over an eighteen second lifetime, is what makes a cloud spread to
        /// several times the size the yield says it should be.
        ///
        /// It is born at the head of the column, not at the cloud top, and rides the rest of the
        /// way up at the column's own rate before settling. Emitting it at the top instead - as
        /// it used to be - hangs a finished canopy in clear air seconds before the stem arrives
        /// under it, which is the one thing that gives the effect away as particles.
        /// </summary>
        private static void CreateCap(Vector3 groundZero, float capR, float top, float rise)
        {
            // It lingers at the top for a long time - and a cloud that took longer to climb has
            // to linger for longer still, or a strategic cap starts fading while its own stem is
            // still rising into it.
            float lifetime = Mathf.Max(18f, rise * 0.8f);
            const float emitFraction = 0.35f; // where the particles start, as a fraction of capR
            const float sizeFraction = 0.45f; // particle diameter at birth, likewise
            const float growth = 1.6f;        // how much larger a particle is by the end of its life
            float driftDistance = capR * (1f - emitFraction - sizeFraction * growth * 0.5f);
            if (driftDistance < 0f) driftDistance = 0f;
            float driftSpeed = driftDistance / lifetime;

            // The head of the column when the cap starts to swell, and the climb left to do.
            float delay = rise * CapEmergeFraction;
            float birthHeight = top * CapEmergeFraction;

            var go = ParticleBuilder.NewSystem("NuclearCap", groundZero + Vector3.up * birthHeight,
                ParticleAssets.Smoke);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startDelay = delay; // it starts to swell once the column is well up
            main.startLifetime = lifetime;
            // A steady outward drift that covers exactly driftDistance over the lifetime. It is
            // set here rather than through a speed limit because the climb below already uses
            // velocityOverLifetime, and the limit module is applied to a particle's whole
            // velocity - it would brake the climb along with the drift.
            main.startSpeed = driftSpeed;
            main.startSize = capR * sizeFraction;
            main.startColor = new ParticleSystem.MinMaxGradient(CapWarm, CapCool);
            main.maxParticles = 500;
            main.gravityModifier = 0.015f;  // the rim droops, which is the cap's rollover

            ParticleBuilder.Burst(ps, 100);
            ParticleBuilder.ConeUp(ps, capR * emitFraction, 62f);
            ParticleBuilder.Rise(ps, ClimbThenSettle(rise - delay, lifetime), top / rise);
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.85f, 0.25f),
                new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, 0.7f, growth);
            ParticleBuilder.PlayAndDestroy(go, delay + lifetime + 2f);
        }

        /// <summary>
        /// The cap's climb over a particle's life, as a fraction of the stem's rate: full rate
        /// for the seconds the column has left to run, then eased off to nothing so the canopy
        /// stops at the cloud top instead of carrying on out of the sky. The flat tangents are
        /// what keep the eased section from overshooting.
        /// </summary>
        private static AnimationCurve ClimbThenSettle(float climbSeconds, float lifetime)
        {
            float hold = Mathf.Clamp(climbSeconds / lifetime, 0.02f, 0.9f);
            float settled = Mathf.Min(hold * 1.15f, 0.99f);
            return new AnimationCurve(
                new Keyframe(0f, 1f, 0f, 0f), new Keyframe(hold, 1f, 0f, 0f),
                new Keyframe(settled, 0f, 0f, 0f), new Keyframe(1f, 0f, 0f, 0f));
        }
    }
}
