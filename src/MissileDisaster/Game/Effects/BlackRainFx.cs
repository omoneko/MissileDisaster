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
        /// <summary>
        /// The size of one patch, in metres, and the cap on how many are drawn.
        /// <para>
        /// The first version sized a patch as a fraction of the whole radius, which meant a big
        /// strike got bigger blobs rather than more of them: at a 2.5 km radius each one was
        /// 540 m across, and a handful of soft 540 m sprites is not a stain on the ground, it is
        /// weather. A patch is now a fixed size in metres, so the count grows with the area and
        /// the grain stays the same however large the strike.
        /// </para>
        /// Past the cap the patches do grow again - there is a limit to how many billboards are
        /// worth drawing for a decoration - but from a base fine enough that even the largest
        /// strike is drawn at a seventh of the old blob size.
        /// </summary>
        /// The cap is high because raising it is nearly free here: the patches shrink as they
        /// multiply, so the coverage - and therefore the fill rate, which is what actually costs
        /// - stays the same however many there are. Only the per-particle CPU work grows, and
        /// four thousand billboards is not much of that.
        private const float PatchMetres = 60f;
        private const int MaxPatches = 4000;
        private const int MinPatches = 60;

        /// <summary>
        /// How many times over the patches cover the ground. Above 1 they overlap, which is what
        /// turns a scatter of discs into a continuous wash - and what lets each one be drawn
        /// faint, so the overlaps build the colour up instead of one sprite carrying it.
        /// </summary>
        private const float Coverage = 1.6f;

        private const float GroundClearance = 1.2f;   // off the terrain, so it does not z-fight

        // Shares of the life spent soaking in and holding before it lifts. Short overall: the
        // rain washes the soot away almost as quickly as it laid it down.
        private const float SoakFraction = 0.1f;
        private const float HoldFraction = 0.45f;

        // Wet soot: nearly black, never quite. Pure black reads as a hole in the terrain.
        // The alpha is low because the patches overlap Coverage times over - it is the stack that
        // makes the colour, not the individual sprite, and that is what stops the wash looking
        // like a set of discs laid on the map.
        private static readonly Color StainDark = new Color(0.11f, 0.10f, 0.09f, 0.30f);
        private static readonly Color StainLight = new Color(0.24f, 0.23f, 0.21f, 0.24f);

        /// <summary>
        /// Stains the ground across radius around groundZero, following the terrain. A radius or
        /// duration of zero draws nothing. A failure here never stops anything else.
        /// </summary>
        public static void Play(Vector3 groundZero, float radius, float seconds)
        {
            if (radius <= 0f || seconds <= 0f) return;
            try
            {
                // Enough patches of PatchMetres to cover the disc Coverage times over. Where that
                // exceeds the cap the patches grow instead, keeping the cover rather than leaving
                // gaps - so the grain coarsens with the very largest strikes and never thins.
                float wanted = Coverage * (2f * radius / PatchMetres) * (2f * radius / PatchMetres);
                int patches = Mathf.Clamp(Mathf.RoundToInt(wanted), MinPatches, MaxPatches);
                float patchSize = wanted > MaxPatches
                    ? 2f * radius * Mathf.Sqrt(Coverage / patches)
                    : PatchMetres;

                // The cloud material, not the soft-glow smoke: smoke is nearly transparent
                // everywhere but its centre, which is right for a wisp in the air and is exactly
                // what made this read as haze lying over the map rather than as wet ground.
                var go = ParticleBuilder.NewSystem("BlackRainStain", groundZero, ParticleAssets.Cloud);
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
                EmitOnTerrain(ps, groundZero, radius, patches, patchSize, seconds);

                go.AddComponent<SimulationTimed>().LifetimeSeconds = seconds + 1f;
                ModConfig.Log("Black rain stained the ground at " + groundZero + ": radius "
                    + radius.ToString("0") + " m, " + patches + " patches of "
                    + patchSize.ToString("0") + " m, " + seconds.ToString("0") + " s");
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
            int patches, float size, float seconds)
        {
            const float goldenAngle = 2.39996323f;   // radians
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
                // The size band is narrow: with this many patches the variety only has to break
                // the pattern, and a wide band brings back the blobs it is meant to avoid.
                p.position = pos;
                p.startSize = size * (0.85f + 0.3f * Frac(i * 0.6180339f));
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
