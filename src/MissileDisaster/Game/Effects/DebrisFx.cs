using System;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// The rubble the blast throws: chunks of whatever stood at ground zero, flung out and up on
    /// ballistic arcs, tumbling as they go and falling back across the city, with the dust they
    /// carry with them. Main thread only.
    ///
    /// It is drawn as two systems that share one throw. The chunks are opaque, tumbling and few
    /// - the eye follows individual pieces, so a hundred is plenty and a thousand would only
    /// cost. The dust is soft, slower, many, and dies in the air rather than landing: it is what
    /// fills the space between the pieces so they read as a wave of wreckage rather than as
    /// confetti.
    ///
    /// Everything about the flight comes from MissileDisaster.Core.BlastDebris, which solves the
    /// launch speed and the hang time from how far the pieces are meant to land. Unity then
    /// integrates the arc itself, gravity and all, which is why the pieces are handed a real
    /// gravityModifier of 1 rather than a curve pretending to be one.
    /// </summary>
    public static class DebrisFx
    {
        // The dust rides the same throw at a fraction of the speed, so it lags behind the chunks
        // and hangs where they have passed.
        private const float DustSpeedFraction = 0.55f;
        private const float DustGravity = 0.12f;   // it drifts down rather than falling
        private const float DustCountFactor = 3.5f; // against the chunk count
        private const int DustCountMax = 320;
        // The dust is sized from the throw, not from the chunk. It used to be a multiple of the
        // chunk, and when the pieces came down to car size that quietly shrank a 75 m rolling
        // cloud to a 12 m puff - the blast would have lost its dust to a change about masonry.
        private const float DustSizeFraction = 0.19f; // against the throw, in metres
        private const float DustSizeMin = 9f;

        // The cone the pieces leave in. Wide, because a blast throws in every direction at once,
        // but not a full hemisphere: the ones that go straight up would simply come back down on
        // the crater and read as a fountain.
        private const float ConeAngle = 58f;

        private const float TumbleDegreesPerSecond = 220f;
        private const float ChunkSizeVariety = 0.45f;   // the smallest chunk against the largest

        // The renderer clamps a particle to half the screen by default, which crops the dust
        // exactly when the camera is close enough to look at it.
        private const float MaxScreenFraction = 4f;

        // Chunks are real objects now, so the budget is an object budget rather than a particle
        // one. Ninety tumbling pieces read as a blast tearing a district apart; several hundred
        // GameObjects would only cost frames for pieces nobody can pick out anyway.
        // Raised with the pieces coming down to car size: ninety four-metre chunks spread over
        // a kilometre is a scatter, and what has to read at that zoom is a field of wreckage.
        // Each is a few dozen triangles with no shadow, so the cost is in the object count, and
        // three hundred of them is small change beside the city already on screen.
        private const int MaxChunkObjects = 320;

        /// <summary>How long a chunk lies where it fell before it is removed.</summary>
        private const float SettleSeconds = 2.5f;

        // Concrete and brick, lit and shaded, with the dust a shade paler - it is the same
        // material ground finer.
        private static readonly Color ChunkLit = new Color(0.50f, 0.46f, 0.42f, 1f);
        private static readonly Color ChunkShade = new Color(0.30f, 0.27f, 0.25f, 1f);
        private static readonly Color DustNear = new Color(0.60f, 0.55f, 0.48f, 0.75f);
        private static readonly Color DustFar = new Color(0.42f, 0.38f, 0.33f, 0.75f);

        /// <summary>
        /// Throws the rubble of a blast of this radius from groundZero. A radius of zero or less
        /// does nothing. Never throws: the strike's damage does not depend on this drawing.
        /// </summary>
        public static void Play(Vector3 groundZero, float blastRadius)
        {
            float range = BlastDebris.Range(blastRadius);
            if (range <= 0f) return;

            try
            {
                float speed = BlastDebris.LaunchSpeed(range);
                float flight = BlastDebris.FlightSeconds(speed);
                if (speed <= 0f || flight <= 0f) return;

                float chunkSize = BlastDebris.ChunkSize(range);
                int chunks = BlastDebris.ChunkCount(range);
                // The disc the rubble comes off - the destroyed area, not a point at the centre.
                float emitRadius = BlastDebris.EmitRadius(blastRadius);
                Vector3 origin = groundZero + Vector3.up * (chunkSize * 0.5f);

                float dustSize = range * DustSizeFraction;
                if (dustSize < DustSizeMin) dustSize = DustSizeMin;

                int emitted = CreateChunks(origin, emitRadius, speed, flight, chunkSize, chunks);
                CreateDust(origin, emitRadius, speed, flight, dustSize, chunks);

                // Unconditional, because "I still cannot see the rubble" is not answerable from
                // the screen alone: it cannot tell a system that never spawned from one drawing
                // 14 m chunks somewhere behind the fireball. This line says which.
                Material mat = DebrisMeshes.ChunkMaterial;
                Mesh[] meshes = DebrisMeshes.Chunks;
                ModConfig.LogAlways(string.Format(
                    "debris: blast {0:F0} m -> thrown from a {9:F0} m disc, {1:F0} m further, "
                    + "{2} chunks of {3:F1} m at {4:F0} m/s for {5:F1} s; spawned {6} objects; "
                    + "meshes {7}; shader {8}",
                    blastRadius, range, chunks, chunkSize, speed, flight, emitted,
                    meshes == null ? 0 : meshes.Length,
                    mat == null || mat.shader == null ? "NONE - nothing will draw" : mat.shader.name,
                    emitRadius));
            }
            catch (Exception e)
            {
                ModConfig.LogError("DebrisFx.Play error: " + e);
            }
        }

        /// <summary>
        /// The solid pieces. Each is its own GameObject with a MeshFilter and a MeshRenderer,
        /// flown by DebrisChunkFx along the arc Core.DebrisFlight computes.
        ///
        /// Not mesh particles. That was the previous attempt, and when nothing appeared there
        /// was no way to ask a ParticleSystemRenderer why. This is the same path the mod's own
        /// missile model renders through, which is known to work in this game.
        ///
        /// The count is lower than a particle system's would be - these are objects, and a few
        /// dozen tumbling chunks read as a blast throwing wreckage where several hundred would
        /// only cost frames.
        /// </summary>
        private static int CreateChunks(Vector3 origin, float emitRadius, float speed, float flight,
            float chunkSize, int count)
        {
            Mesh[] meshes = DebrisMeshes.Chunks;
            Material material = DebrisMeshes.ChunkMaterial;
            if (meshes == null || meshes.Length == 0 || meshes[0] == null || material == null)
            {
                // This used to return in silence, which is how the rubble managed to be missing
                // for two rounds without anything saying so.
                ModConfig.LogError("DebrisFx: no chunk meshes or no material - drawing nothing");
                return 0;
            }

            int wanted = count > MaxChunkObjects ? MaxChunkObjects : count;
            int seed = (int)(UnityEngine.Random.value * 1000000f);
            int made = 0;

            for (int i = 0; i < wanted; i++)
            {
                DebrisLaunch launch = DebrisFlight.Launch(i, seed, emitRadius, speed, chunkSize,
                    meshes.Length);
                // Each chunk lives its OWN arc, not the nominal one. Clamping to the average
                // flight is what destroyed the steepest pieces in mid-air: they are thrown at a
                // spread of angles, so a quarter of them are up for longer than the figure the
                // range was solved from. BlastDebris.RangeMax is what keeps even those under
                // the ceiling; this guard is only a backstop.
                float life = DebrisFlight.FlightSeconds(launch);
                if (life <= 0.05f) continue;
                if (life > BlastDebris.FlightSecondsMax) life = BlastDebris.FlightSecondsMax;

                var go = new GameObject("MissileDisaster_DebrisChunk");
                go.transform.position = origin;
                go.transform.localScale = new Vector3(launch.Scale, launch.Scale, launch.Scale);

                MeshFilter filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = meshes[launch.Variant];
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                var chunk = go.AddComponent<DebrisChunkFx>();
                chunk.Launch = launch;
                chunk.Origin = origin;
                chunk.GroundY = origin.y;
                // It lies where it fell for a moment before going, rather than blinking out the
                // instant it lands.
                chunk.LifeSeconds = life + SettleSeconds;
                made++;
            }
            return made;
        }

        /// <summary>The dust thrown with the pieces: slower, softer, and dying in the air.</summary>
        private static void CreateDust(Vector3 origin, float emitRadius, float speed, float flight,
            float dustSize, int chunks)
        {
            int count = (int)(chunks * DustCountFactor);
            if (count > DustCountMax) count = DustCountMax;
            float life = flight * 1.4f; // it hangs after the rubble has come down

            var go = ParticleBuilder.NewSystem("BlastDebrisDust", origin, ParticleAssets.Cloud);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = life;
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                speed * DustSpeedFraction * 0.4f, speed * DustSpeedFraction);
            main.startSize = new ParticleSystem.MinMaxCurve(dustSize * 0.5f, dustSize);
            main.startColor = new ParticleSystem.MinMaxGradient(DustNear, DustFar);
            main.maxParticles = count * 2;
            main.gravityModifier = DustGravity;

            var dustRenderer = ps.GetComponent<ParticleSystemRenderer>();
            dustRenderer.maxParticleSize = MaxScreenFraction;

            ParticleBuilder.Burst(ps, count);
            ParticleBuilder.ConeUp(ps, emitRadius, ConeAngle);
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.08f),
                new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, 0.7f, 2.4f); // it swells as it spreads and thins
            ParticleBuilder.PlayAndDestroy(go, life + 1f);
        }
    }
}
