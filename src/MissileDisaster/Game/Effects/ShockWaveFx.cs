using System;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// The blast front racing out across the ground, drawn as a ring of dust that leaves the
    /// centre far faster than sound and visibly slows as it spreads. Main thread only.
    ///
    /// The deceleration is the point. A ring that expands at a constant rate reads as a growing
    /// circle; one that follows Sedov-Taylor - see MissileDisaster.Core.ShockWave - reads as a
    /// wave, because the eye picks up that it is losing speed. The speed is fed to the particle
    /// system as a curve over the particles' lifetime with the brakes fully on, so their speed
    /// follows the curve exactly and the ring's radius traces r = R (t/T)^0.4.
    ///
    /// Two rings are drawn: the dust the front kicks up off the ground, and a fainter, quicker
    /// pale front just above it standing in for the compressed air itself.
    /// </summary>
    public static class ShockWaveFx
    {
        private const int DustParticles = 96;      // enough that the ring reads as continuous
        private const int AirParticles = 72;
        private const int SpeedCurveKeys = 10;     // resolution the Sedov curve is sampled at
        private const float DustSizeFraction = 0.055f;   // a dust puff against the full radius
        private const float AirSizeFraction = 0.075f;
        private const float GroundClearance = 6f;  // lifts the ring off the terrain so it does not z-fight

        private static readonly Color DustNear = new Color(0.52f, 0.47f, 0.40f, 0.55f); // dry earth
        private static readonly Color DustFar = new Color(0.38f, 0.35f, 0.31f, 0.55f);
        private static readonly Color AirFront = new Color(0.92f, 0.93f, 0.95f, 0.30f); // pale compressed air

        /// <summary>Sends the front out from groundZero to radius. A radius of zero or less does nothing.</summary>
        public static void Play(Vector3 groundZero, float radius)
        {
            if (radius <= 0f) return;
            try
            {
                float duration = ShockWave.Duration(radius);
                if (duration <= 0f) return;
                Vector3 origin = groundZero + Vector3.up * GroundClearance;
                AnimationCurve speed = BuildSpeedCurve(radius, duration);

                CreateRing(origin, "ShockWaveDust", ParticleAssets.Smoke, radius, duration, speed,
                    DustParticles, radius * DustSizeFraction, DustNear, DustFar, 0.7f, 2.6f, -0.01f);
                CreateRing(origin + Vector3.up * (radius * 0.01f), "ShockWaveAir", ParticleAssets.Smoke,
                    radius, duration * 0.85f, speed, AirParticles, radius * AirSizeFraction,
                    AirFront, AirFront, 1f, 1.8f, 0f);
            }
            catch (Exception e)
            {
                ModConfig.LogError("ShockWaveFx.Play error: " + e);
            }
        }

        /// <summary>
        /// The front's speed sampled across its life. The samples are spaced towards the start,
        /// where nearly all of the deceleration happens: the front is already down to a third of
        /// its opening speed a tenth of the way through.
        /// </summary>
        private static AnimationCurve BuildSpeedCurve(float radius, float duration)
        {
            var keys = new Keyframe[SpeedCurveKeys];
            for (int i = 0; i < SpeedCurveKeys; i++)
            {
                float u = (float)i / (SpeedCurveKeys - 1);
                u = u * u; // bunch the samples up early, where the curve bends
                if (u < ShockWave.MinFraction) u = ShockWave.MinFraction;
                keys[i] = new Keyframe(u, ShockWave.FrontSpeed(radius, duration, u));
            }
            return new AnimationCurve(keys);
        }

        private static void CreateRing(Vector3 origin, string name, Material mat, float radius,
            float duration, AnimationCurve speed, int count, float size,
            Color near, Color far, float sizeFrom, float sizeTo, float gravity)
        {
            var go = ParticleBuilder.NewSystem(name, origin, mat);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = duration;
            // The curve carries the speed; the start speed only has to be high enough that the
            // limit is what holds it back rather than the other way round.
            main.startSpeed = ShockWave.FrontSpeed(radius, duration, ShockWave.MinFraction);
            main.startSize = size;
            main.startColor = new ParticleSystem.MinMaxGradient(near, far);
            main.maxParticles = count * 2;

            ParticleBuilder.Burst(ps, count);
            // Born where the front already is when it is first tracked, so that the ground it
            // has yet to cover is exactly what the speed curve then carries it across.
            ParticleBuilder.GroundRing(ps, ShockWave.StartRadius(radius));
            ParticleBuilder.SpeedCurve(ps, speed, 1f);
            ParticleBuilder.Gravity(ps, gravity);
            // Thrown up hard, then thinning out as the front runs out of energy.
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.06f),
                new GradientAlphaKey(0.75f, 0.45f), new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, sizeFrom, sizeTo);
            ParticleBuilder.PlayAndDestroy(go, duration + 1f);
        }
    }
}
