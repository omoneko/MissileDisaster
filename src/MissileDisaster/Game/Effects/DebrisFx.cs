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
        private const float DustSizeFactor = 2.2f;  // against the chunk size

        // The cone the pieces leave in. Wide, because a blast throws in every direction at once,
        // but not a full hemisphere: the ones that go straight up would simply come back down on
        // the crater and read as a fountain.
        private const float ConeAngle = 58f;
        private const float EmitRadiusFraction = 0.08f; // of the range, so they start off a point

        private const float TumbleDegreesPerSecond = 220f;
        private const float ChunkSizeVariety = 0.45f;   // the smallest chunk against the largest

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
                Vector3 origin = groundZero + Vector3.up * (chunkSize * 0.5f);

                CreateChunks(origin, range, speed, flight, chunkSize, chunks);
                CreateDust(origin, range, speed, flight, chunkSize, chunks);
            }
            catch (Exception e)
            {
                ModConfig.LogError("DebrisFx.Play error: " + e);
            }
        }

        /// <summary>The solid pieces: opaque, tumbling, and landing where the physics puts them.</summary>
        private static void CreateChunks(Vector3 origin, float range, float speed, float flight,
            float chunkSize, int count)
        {
            var go = ParticleBuilder.NewSystem("BlastDebris", origin, ParticleAssets.Cloud);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = flight;
            // A spread of speeds, so they do not all land on the same ring.
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.55f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(chunkSize * ChunkSizeVariety, chunkSize);
            main.startColor = new ParticleSystem.MinMaxGradient(ChunkLit, ChunkShade);
            main.maxParticles = count * 2;
            main.gravityModifier = 1f; // real gravity: the arc is the one BlastDebris solved for

            ParticleBuilder.Burst(ps, count);
            ParticleBuilder.ConeUp(ps, range * EmitRadiusFraction, ConeAngle);
            // Tumbling is what separates a chunk of masonry from a puff of smoke.
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-TumbleDegreesPerSecond * Mathf.Deg2Rad,
                TumbleDegreesPerSecond * Mathf.Deg2Rad);
            // Solid all the way, and gone at the moment it lands rather than fading in mid-air.
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.85f),
                new GradientAlphaKey(0f, 1f));
            ParticleBuilder.PlayAndDestroy(go, flight + 1f);
        }

        /// <summary>The dust thrown with the pieces: slower, softer, and dying in the air.</summary>
        private static void CreateDust(Vector3 origin, float range, float speed, float flight,
            float chunkSize, int chunks)
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
            main.startSize = new ParticleSystem.MinMaxCurve(
                chunkSize * DustSizeFactor * 0.5f, chunkSize * DustSizeFactor);
            main.startColor = new ParticleSystem.MinMaxGradient(DustNear, DustFar);
            main.maxParticles = count * 2;
            main.gravityModifier = DustGravity;

            ParticleBuilder.Burst(ps, count);
            ParticleBuilder.ConeUp(ps, range * EmitRadiusFraction, ConeAngle);
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.08f),
                new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, 0.7f, 2.4f); // it swells as it spreads and thins
            ParticleBuilder.PlayAndDestroy(go, life + 1f);
        }
    }
}
