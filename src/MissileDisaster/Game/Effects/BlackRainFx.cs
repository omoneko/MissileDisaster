using System;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// The mark black rain leaves on the ground: a dirty grey wash over the contaminated area
    /// that soaks in and lifts again shortly afterwards.
    ///
    /// <para>
    /// Every patch is placed at the terrain height under it, sampled one by one. The first
    /// version emitted them from a flat disc at the burst point, which is only correct on a flat
    /// map: on any slope the whole sheet sat at the altitude of ground zero, so half of it hung
    /// in the air over the valley and the other half was buried in the hill. Sampling the height
    /// per patch is what makes the stain lie on the ground rather than at an altitude.
    /// </para>
    ///
    /// <para>
    /// Horizontal billboards, not the camera-facing kind every other effect here uses: a
    /// camera-facing sprite lying on the ground stands up like a wall the moment you orbit it.
    /// </para>
    ///
    /// It is cosmetic and short-lived. The fallout that actually poisons the ground is the
    /// contamination the warhead applied, which the city saves; this is only the look of the rain
    /// that brought it down. Main thread only - it samples the terrain and builds a
    /// ParticleSystem.
    /// </summary>
    public static class BlackRainFx
    {
        // Patches are laid on a spiral rather than at random, so the cover is even without
        // needing several times as many of them to fill the gaps luck leaves.
        private const int MaxPatches = 220;
        private const float PatchesPerRadiusUnit = 0.09f;   // against the radius, then clamped
        private const int MinPatches = 40;

        // A patch against the whole radius. Large enough that they overlap heavily, which is what
        // makes the wash continuous rather than a scatter of dots.
        private const float PatchSizeFraction = 0.22f;
        private const float GroundClearance = 2.5f;   // off the terrain, so it does not z-fight

        // Shares of the life spent soaking in and holding before it lifts. Short overall: the
        // rain washes the soot away almost as quickly as it laid it down.
        private const float SoakFraction = 0.1f;
        private const float HoldFraction = 0.45f;

        // Wet soot: nearly black, never quite. Pure black reads as a hole in the terrain.
        private static readonly Color StainDark = new Color(0.14f, 0.13f, 0.12f, 0.6f);
        private static readonly Color StainLight = new Color(0.28f, 0.27f, 0.25f, 0.5f);

        /// <summary>
        /// Stains the ground across radius around groundZero, following the terrain. A radius or
        /// duration of zero draws nothing. A failure here never stops anything else.
        /// </summary>
        public static void Play(Vector3 groundZero, float radius, float seconds)
        {
            if (radius <= 0f || seconds <= 0f) return;
            try
            {
                int patches = Mathf.Clamp(
                    Mathf.RoundToInt(radius * PatchesPerRadiusUnit), MinPatches, MaxPatches);

                var go = ParticleBuilder.NewSystem("BlackRainStain", groundZero, ParticleAssets.Smoke);
                var ps = go.GetComponent<ParticleSystem>();

                var main = ps.main;
                main.startLifetime = seconds;
                main.startSpeed = 0f;                    // it lands and stays where it landed
                main.startColor = new ParticleSystem.MinMaxGradient(StainDark, StainLight);
                main.maxParticles = MaxPatches * 2;

                // Nothing is emitted by the system itself: every patch is placed by hand at the
                // height of the ground beneath it.
                var emission = ps.emission;
                emission.enabled = false;

                ParticleBuilder.Fade(ps,
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, SoakFraction),
                    new GradientAlphaKey(0.85f, HoldFraction), new GradientAlphaKey(0f, 1f));
                // Barely grows: rain spreads a little as it runs, and no more.
                ParticleBuilder.SizeCurve(ps, 0.9f, 1.1f);

                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;

                ps.Play();
                EmitOnTerrain(ps, groundZero, radius, patches, seconds);

                go.AddComponent<SimulationTimed>().LifetimeSeconds = seconds + 1f;
                ModConfig.Log("Black rain stained the ground at " + groundZero + ": radius "
                    + radius.ToString("0") + " m, " + patches + " patches, "
                    + seconds.ToString("0") + " s");
            }
            catch (Exception e)
            {
                ModConfig.LogError("BlackRainFx.Play error: " + e);
            }
        }

        /// <summary>
        /// Places every patch individually, each at the height of the terrain under it.
        /// <para>
        /// The points follow a sunflower spiral - the golden angle between successive points and
        /// the radius going as the square root of the index - which spreads them evenly across
        /// the disc by area. Random points clump, and a clump on a wash this sparse shows.
        /// </para>
        /// </summary>
        private static void EmitOnTerrain(ParticleSystem ps, Vector3 centre, float radius,
            int patches, float seconds)
        {
            const float goldenAngle = 2.39996323f;   // radians
            float size = radius * PatchSizeFraction;
            TerrainManager terrain = TerrainManager.instance;

            var p = new ParticleSystem.EmitParams();
            p.startLifetime = seconds;
            p.startSize = size;
            p.velocity = Vector3.zero;

            for (int i = 0; i < patches; i++)
            {
                float t = (i + 0.5f) / patches;
                float r = radius * Mathf.Sqrt(t);
                float a = i * goldenAngle;

                var pos = new Vector3(centre.x + Mathf.Cos(a) * r, 0f, centre.z + Mathf.Sin(a) * r);
                pos.y = terrain != null
                    ? terrain.SampleRawHeightSmoothWithWater(pos, false, 0f) + GroundClearance
                    : centre.y + GroundClearance;

                // Size and rotation vary per patch so the wash does not read as a tiled grid.
                p.position = pos;
                p.startSize = size * (0.7f + 0.6f * Frac(i * 0.6180339f));
                p.rotation = 360f * Frac(i * 0.7548777f);
                ps.Emit(p, 1);
            }
        }

        /// <summary>The fractional part, for the two low-discrepancy sequences above.</summary>
        private static float Frac(float v)
        {
            return v - Mathf.Floor(v);
        }
    }
}
