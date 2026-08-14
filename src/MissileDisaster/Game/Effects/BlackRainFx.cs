using System;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// The mark black rain leaves on the ground: a broad, dirty grey stain that soaks in and
    /// then fades.
    ///
    /// <para>
    /// Drawn with horizontal billboards rather than the camera-facing kind every other effect
    /// here uses. A camera-facing sprite lying on the ground turns to face the player as the
    /// camera moves, so a stain drawn that way stands up like a wall the moment you orbit it.
    /// ParticleSystemRenderMode.HorizontalBillboard keeps every sprite flat, which is what makes
    /// this read as something on the ground.
    /// </para>
    ///
    /// It is deliberately cosmetic. The fallout that actually does something is ground pollution,
    /// applied by ContaminationManager and saved with the city; this is the look of the rain that
    /// brought it down, and it goes away on its own. Main thread only.
    /// </summary>
    public static class BlackRainFx
    {
        // The stain is drawn as a field of overlapping soft patches. Enough to cover without
        // gaps; each one large against the radius so the count can stay low.
        private const int Patches = 90;
        private const float PatchSizeFraction = 0.28f;
        private const float GroundClearance = 3f;   // off the terrain, so it does not z-fight

        // How much of the life is spent soaking in before it starts to lift.
        private const float SoakFraction = 0.12f;
        private const float HoldFraction = 0.6f;

        // Wet soot: nearly black, never quite. Pure black reads as a hole in the terrain.
        private static readonly Color StainDark = new Color(0.14f, 0.13f, 0.12f, 0.62f);
        private static readonly Color StainLight = new Color(0.28f, 0.27f, 0.25f, 0.52f);

        /// <summary>
        /// Stains the ground around groundZero. A radius or duration of zero draws nothing.
        /// A failure here never stops anything else.
        /// </summary>
        public static void Play(Vector3 groundZero, float radius, float seconds)
        {
            if (radius <= 0f || seconds <= 0f) return;
            try
            {
                var go = ParticleBuilder.NewSystem("BlackRainStain",
                    groundZero + Vector3.up * GroundClearance, ParticleAssets.Smoke);
                var ps = go.GetComponent<ParticleSystem>();

                var main = ps.main;
                main.startLifetime = seconds;
                main.startSpeed = 0f;                    // it lands and stays
                main.startSize = new ParticleSystem.MinMaxCurve(
                    radius * PatchSizeFraction * 0.6f, radius * PatchSizeFraction);
                main.startColor = new ParticleSystem.MinMaxGradient(StainDark, StainLight);
                main.maxParticles = Patches * 2;
                // Each patch lies at its own angle, so the field does not read as a tiled grid.
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

                ParticleBuilder.Burst(ps, Patches);
                ParticleBuilder.FlatDisc(ps, radius);

                // Soaks in quickly, sits, then lifts. The long hold is the point: this is a mark,
                // not a puff of smoke.
                ParticleBuilder.Fade(ps,
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, SoakFraction),
                    new GradientAlphaKey(0.9f, HoldFraction), new GradientAlphaKey(0f, 1f));
                // Barely grows: rain spreads a little as it runs, and no more.
                ParticleBuilder.SizeCurve(ps, 0.9f, 1.15f);

                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;

                ParticleBuilder.PlayAndDestroy(go, seconds + 1f);
                ModConfig.Log("Black rain stained the ground at " + groundZero
                    + ": radius " + radius.ToString("0") + " m for " + seconds.ToString("0") + " s");
            }
            catch (Exception e)
            {
                ModConfig.LogError("BlackRainFx.Play error: " + e);
            }
        }
    }
}
