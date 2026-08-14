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
        private const int MaxPatches = 6000;
        private const int MinPatches = 60;

        /// <summary>
        /// How many times over the patches cover the ground. Well above 1: the survivors describe
        /// rain that fell like dissolved ink, and what it left behind was general filth with
        /// splatter in it - not a pattern. Discs that merely touch read as a pattern, so they are
        /// stacked several deep and each is drawn faint enough that no single one is findable.
        /// </summary>
        private const float Coverage = 2.4f;

        /// <summary>
        /// How far a patch is jittered off its lattice point, against its own size. The points
        /// come off an even spiral, and an even spiral is exactly what the eye reads as leopard
        /// print - real splatter clumps and leaves bare ground, so the regularity is broken here.
        /// </summary>
        private const float Jitter = 0.85f;

        private const float GroundClearance = 1.2f;   // off the terrain, so it does not z-fight

        // Shares of the life spent soaking in and holding before it lifts. Short overall: the
        // rain washes the soot away almost as quickly as it laid it down.
        private const float SoakFraction = 0.1f;
        private const float HoldFraction = 0.45f;

        // Wet soot: nearly black, never quite - pure black reads as a hole in the terrain.
        // The alpha is very low because the patches stack Coverage deep; it is the stack that
        // makes the colour, not any one sprite. That is the whole difference between a wash and
        // a set of discs laid on the map.
        private static readonly Color StainDark = new Color(0.10f, 0.09f, 0.08f, 1f);
        private static readonly Color StainLight = new Color(0.22f, 0.20f, 0.18f, 1f);
        private const float AlphaMin = 0.07f;
        private const float AlphaMax = 0.16f;

        /// <summary>
        /// Stains the ground across radius around groundZero, following the terrain. A radius or
        /// duration of zero draws nothing. A failure here never stops anything else.
        /// </summary>
        public static void Play(Vector3 groundZero, float radius, float seconds,
            float windX, float windZ)
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

                // The soft-glow smoke material, not the opaque-cored cloud. The cloud texture has
                // a hard core, and a hard-edged disc is findable however small it is - which is
                // what turned the wash into leopard print. Soft edges are what let neighbouring
                // patches blend into one another instead of tiling.
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
                EmitOnTerrain(ps, groundZero, radius, patches, patchSize, seconds, windX, windZ);

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
            int patches, float size, float seconds, float windX, float windZ)
        {
            const float goldenAngle = 2.39996323f;   // radians
            TerrainManager terrain = TerrainManager.instance;

            // The disc is stretched along the wind and squeezed across it, so the stain is an
            // ellipse lying downwind rather than a ring around the burst.
            var along = new Vector2(windX, windZ);
            if (along.sqrMagnitude < 0.0001f) along = new Vector2(0f, 1f);
            along.Normalize();
            var across = new Vector2(-along.y, along.x);

            var p = new ParticleSystem.EmitParams();
            p.startLifetime = seconds;
            p.velocity = Vector3.zero;

            for (int i = 0; i < patches; i++)
            {
                float t = (i + 0.5f) / patches;
                float r = radius * Mathf.Sqrt(t);
                float a = i * goldenAngle;

                // Off the lattice, by up to Jitter of a patch in each direction. Two different
                // irrational strides so x and z do not move together and re-form a pattern.
                float jx = (Frac(i * 0.7548777f) - 0.5f) * 2f * Jitter * size;
                float jz = (Frac(i * 0.5698402f) - 0.5f) * 2f * Jitter * size;

                // The spiral point, mapped onto the wind's axes before it becomes a world
                // position: long downwind, short across.
                float u = Mathf.Cos(a) * r * BlackRain.DownwindStretch;
                float v = Mathf.Sin(a) * r * BlackRain.CrosswindSquash;
                var pos = new Vector3(
                    centre.x + along.x * u + across.x * v + jx, 0f,
                    centre.z + along.y * u + across.y * v + jz);
                pos.y = terrain != null
                    ? terrain.SampleRawHeightSmoothWithWater(pos, false, 0f) + GroundClearance
                    : centre.y + GroundClearance;

                // Colour is set per particle rather than left to the module. With Emit() the
                // module's start colour is not reliably what reaches the shader's _TintColor, and
                // an untinted particle draws white - which is what put pale blobs through the
                // stain. Setting it here removes the question, and lets each patch carry its own
                // weight so the wash varies the way filth does.
                float mix = Frac(i * 0.6180339f);
                Color c = Color.Lerp(StainDark, StainLight, mix);
                c.a = AlphaMin + (AlphaMax - AlphaMin) * Frac(i * 0.3819660f);

                p.position = pos;
                p.startColor = c;
                // A wide size band on purpose: splatter is not one drop size, and varied sizes
                // stacked several deep stop any one disc being findable.
                p.startSize = size * (0.55f + 1.1f * Frac(i * 0.4142135f));
                p.rotation = 360f * Frac(i * 0.2360679f);
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
