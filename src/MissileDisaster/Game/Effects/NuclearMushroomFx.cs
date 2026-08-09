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
    /// </summary>
    public static class NuclearMushroomFx
    {
        // Engineering limits. The playable map is about 17 km across, so a canopy beyond this
        // already spans it and only costs particle size.
        private const float CapRadiusMin = 200f;
        private const float CapRadiusMax = 8000f;
        private const float CloudTopMin = 800f;
        private const float CloudTopMax = 12000f;
        private const float FireballRadiusMin = 25f;
        private const float FireballRadiusMax = 3000f;

        // Time. The real rise is minutes; this is what it is divided by, and the bounds it is
        // then held inside so that a small device is not over in a blink nor a large one still
        // climbing a minute later.
        private const float RiseCompression = 25f;
        private const float RiseSecondsMin = 8f;
        private const float RiseSecondsMax = 26f;
        private const float FireballSecondsMin = 0.8f;
        private const float FireballSecondsMax = 12f;

        // The condensation dome forms out where the shock has passed, well beyond the fireball,
        // and lasts barely a second.
        private const float CondensationRadiusFactor = 2.6f;
        private const float CondensationLifetime = 1.3f;

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
                float kt = kilotons > 0f ? kilotons : NuclearYields.StandardKilotons;

                float fireballR = Mathf.Clamp(NuclearCloud.FireballRadius(kt), FireballRadiusMin, FireballRadiusMax);
                float fireballT = Mathf.Clamp(NuclearCloud.FireballSeconds(kt), FireballSecondsMin, FireballSecondsMax);
                float capR = Mathf.Clamp(NuclearCloud.CloudRadius(kt), CapRadiusMin, CapRadiusMax);
                float top = Mathf.Clamp(NuclearCloud.CloudTop(kt), CloudTopMin, CloudTopMax);
                float stemR = capR * NuclearCloud.StemFraction(kt);
                float rise = Mathf.Clamp(NuclearCloud.StabiliseSeconds(kt) / RiseCompression,
                    RiseSecondsMin, RiseSecondsMax);

                CreateFireball(detonation, fireballR, fireballT);
                CreateCondensationDome(detonation, fireballR * CondensationRadiusFactor, fireballT);
                CreateGroundDust(groundZero, stemR, rise);
                CreateStem(groundZero, stemR, top, rise);
                CreateCap(groundZero + Vector3.up * top, capR, rise);
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
        /// </summary>
        private static void CreateCap(Vector3 top, float capR, float rise)
        {
            const float lifetime = 18f;       // it lingers at the top for a long time
            const float emitFraction = 0.35f; // where the particles start, as a fraction of capR
            const float sizeFraction = 0.45f; // particle diameter at birth, likewise
            const float growth = 1.6f;        // how much larger a particle is by the end of its life
            float driftDistance = capR * (1f - emitFraction - sizeFraction * growth * 0.5f);
            if (driftDistance < 0f) driftDistance = 0f;
            float driftSpeed = driftDistance / lifetime;

            var go = ParticleBuilder.NewSystem("NuclearCap", top, ParticleAssets.Smoke);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startDelay = rise * 0.55f; // starts to swell about when the stem reaches the top
            main.startLifetime = lifetime;
            main.startSpeed = driftSpeed * 2.5f; // it billows out quickly at first, then is damped
            main.startSize = capR * sizeFraction;
            main.startColor = new ParticleSystem.MinMaxGradient(CapWarm, CapCool);
            main.maxParticles = 500;
            main.gravityModifier = 0.015f;  // the rim droops, which is the cap's rollover

            ParticleBuilder.Burst(ps, 100);
            ParticleBuilder.ConeUp(ps, capR * emitFraction, 62f);
            ParticleBuilder.LimitSpeed(ps, driftSpeed, 0.2f);
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.85f, 0.25f),
                new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, 0.7f, growth);
            ParticleBuilder.PlayAndDestroy(go, rise * 0.55f + lifetime + 2f);
        }
    }
}
