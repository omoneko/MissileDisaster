using System;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// The small, short-lived mushroom a conventional detonation throws up: a dark column off the
    /// burst that climbs, spreads into a cap and is gone in a few seconds.
    ///
    /// <para>
    /// Deliberately not NuclearMushroomFx. That one is built from a yield in kilotons and draws a
    /// cooling fireball, a condensation dome and a canopy that boils for the best part of a
    /// minute - all correct for a weapon, all wrong for a bomb. A tonne of high explosive lifts a
    /// column of dirt and smoke that reads as a mushroom for a moment and then blows apart, which
    /// is what this draws.
    /// </para>
    ///
    /// Everything is sized off the fireball radius, so it grows with the charge the same way the
    /// fireball does. Main thread only.
    /// </summary>
    public static class SmallMushroomFx
    {
        // Against the fireball radius.
        private const float ColumnRadiusFraction = 0.55f;  // the stem's thickness
        private const float CapRadiusFactor = 1.9f;        // the cap is wider than the fireball
        private const float HeightFactor = 5.5f;           // how high the column reaches
        private const float PuffSizeFraction = 0.85f;      // one puff of the column

        private const int ColumnParticles = 26;
        private const int CapParticles = 34;

        // Seconds. Short on purpose: this is a moment, not an event.
        private const float ColumnLife = 3.2f;
        private const float CapLife = 4.0f;
        private const float CapDelayFraction = 0.35f;      // the cap forms once the column is up

        // Hot and dirty at the bottom, cooling to ordinary smoke as it rises.
        private static readonly Color StemLit = new Color(0.42f, 0.34f, 0.26f, 0.85f);
        private static readonly Color StemShade = new Color(0.24f, 0.20f, 0.17f, 0.85f);
        private static readonly Color CapLit = new Color(0.34f, 0.31f, 0.28f, 0.8f);
        private static readonly Color CapShade = new Color(0.19f, 0.17f, 0.16f, 0.8f);

        /// <summary>
        /// Throws the column up from groundZero. fireballRadius sizes the whole thing; zero or
        /// less draws nothing. A failure here never stops the impact resolving.
        /// </summary>
        public static void Play(Vector3 groundZero, float fireballRadius)
        {
            if (fireballRadius <= 0f) return;
            try
            {
                float height = fireballRadius * HeightFactor;
                float puff = fireballRadius * PuffSizeFraction;

                CreateColumn(groundZero, fireballRadius, height, puff);
                CreateCap(groundZero + Vector3.up * height, fireballRadius, puff);
            }
            catch (Exception e)
            {
                ModConfig.LogError("SmallMushroomFx.Play error: " + e);
            }
        }

        /// <summary>The stem: a narrow column of dirty smoke climbing off the burst.</summary>
        private static void CreateColumn(Vector3 groundZero, float fireballRadius, float height, float puff)
        {
            var go = ParticleBuilder.NewSystem("SmallMushroomColumn", groundZero, ParticleAssets.Cloud);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = ColumnLife;
            main.startSpeed = 0f;                       // the rise curve carries it, not the birth speed
            main.startSize = new ParticleSystem.MinMaxCurve(puff * 0.5f, puff);
            main.startColor = new ParticleSystem.MinMaxGradient(StemLit, StemShade);
            main.maxParticles = ColumnParticles * 2;

            ParticleBuilder.Burst(ps, ColumnParticles);
            ParticleBuilder.ConeUp(ps, fireballRadius * ColumnRadiusFraction, 8f);
            // Fast off the ground and easing off as it climbs, so the column stretches rather
            // than travelling as a block.
            ParticleBuilder.Rise(ps, new AnimationCurve(
                new Keyframe(0f, height / ColumnLife * 1.6f),
                new Keyframe(0.6f, height / ColumnLife * 0.7f),
                new Keyframe(1f, 0f)), 1f);
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, 0.7f, 2.2f);   // the column swells as it rises and cools
            ParticleBuilder.PlayAndDestroy(go, ColumnLife + 1f);
        }

        /// <summary>The cap: a puff that spreads outward at the top once the column has arrived.</summary>
        private static void CreateCap(Vector3 top, float fireballRadius, float puff)
        {
            var go = ParticleBuilder.NewSystem("SmallMushroomCap", top, ParticleAssets.Cloud);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startDelay = ColumnLife * CapDelayFraction;
            main.startLifetime = CapLife;
            main.startSpeed = fireballRadius * CapRadiusFactor / CapLife;
            main.startSize = new ParticleSystem.MinMaxCurve(puff * 0.6f, puff * 1.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(CapLit, CapShade);
            main.maxParticles = CapParticles * 2;

            ParticleBuilder.Burst(ps, CapParticles);
            ParticleBuilder.Sphere(ps, fireballRadius * 0.4f);
            ParticleBuilder.Gravity(ps, -0.02f);         // it keeps drifting up as it spreads
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.12f),
                new GradientAlphaKey(0.7f, 0.55f), new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, 0.8f, 2.6f);
            ParticleBuilder.PlayAndDestroy(go, ColumnLife * CapDelayFraction + CapLife + 1f);
        }
    }
}
