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
        // The dust surge: the wall of earth the front tears off the ground and rolls outward
        // ahead of itself, the thing that reads as a tsunami of dirt. It needs far more
        // particles than the thin rings, because it has to be opaque.
        // Verified in tools/effect-preview/cloud_preview.py: below about 300 the wall reads as
        // a string of beads rather than a continuous front.
        private const int SurgeParticles = 360;
        private const int SpeedCurveKeys = 10;     // resolution the Sedov curve is sampled at
        private const float DustSizeFraction = 0.055f;   // a dust puff against the full radius
        private const float AirSizeFraction = 0.075f;
        private const float GroundClearance = 6f;  // lifts the ring off the terrain so it does not z-fight

        // The surge lags the shock front - the air arrives first, and the ground it lifts
        // follows a beat behind - and it keeps rolling after the front has spent itself, which
        // is why it outlives the rings.
        private const float SurgeStartDelayFraction = 0.06f;
        private const float SurgeDurationFactor = 1.35f;
        private const float SurgeSizeFraction = 0.105f;  // one clod of the wall, against the full radius
        private const float SurgeSizeVariety = 2.2f;     // power bias: many small, a few large
        private const float SurgeGrowth = 3.4f;          // the wall piles up as it rolls outward
        private const float SurgeLift = 0.02f;           // gentle rise, so the wall climbs as it spreads

        private static readonly Color DustNear = new Color(0.52f, 0.47f, 0.40f, 0.55f); // dry earth
        private static readonly Color DustFar = new Color(0.38f, 0.35f, 0.31f, 0.55f);
        private static readonly Color AirFront = new Color(0.92f, 0.93f, 0.95f, 0.30f); // pale compressed air
        // The surge is the same earth as the rings but drawn nearly opaque - it is a wall, not
        // a haze - and lit unevenly, the near face brighter than the shaded folds behind it.
        // These only reach the screen through the shader's _TintColor path - see ParticleAssets,
        // where picking a shader without it drew every one of these white.
        private static readonly Color SurgeLit = new Color(0.60f, 0.50f, 0.38f, 0.92f);
        private static readonly Color SurgeShade = new Color(0.36f, 0.29f, 0.22f, 0.92f);

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

                // Particle counts follow the size. A ring only has to look continuous, and the
                // arc each particle covers grows with the radius - so a small blast needs far
                // fewer of them, and spending 96 on a 1.5 t warhead bought nothing but frames.
                float scale = CountScale(radius);

                CreateRing(origin, "ShockWaveDust", ParticleAssets.Smoke, radius, duration, speed,
                    Count(DustParticles, scale), radius * DustSizeFraction, DustNear, DustFar, 0.7f, 2.6f, -0.01f);
                CreateRing(origin + Vector3.up * (radius * 0.01f), "ShockWaveAir", ParticleAssets.Smoke,
                    radius, duration * 0.85f, speed, Count(AirParticles, scale), radius * AirSizeFraction,
                    AirFront, AirFront, 1f, 1.8f, 0f);

                // The rolling wall of earth belongs to a large explosion. Behind a single bomb it
                // reads as a dust storm arriving from nowhere, so below the threshold the rings
                // run alone. Above it the wall is drawn at a count that follows the radius, the
                // same way the rings are - a full 360-clod wall on a 120 m front was never the
                // alternative, and it is what made the threshold have to sit so high.
                if (radius >= ShockWave.DustSurgeMinRadius)
                {
                    CreateDustSurge(origin, radius, duration, speed, Count(SurgeParticles, scale));
                }

                // The solid part: the rubble of whatever stood here, thrown out and up on
                // ballistic arcs and falling back across the city. Without it a detonation is
                // all smoke, which reads as weather rather than as a building coming apart.
                // Every warhead throws rubble - unlike the dust wall, which belongs to a large
                // blast. This sat inside the wall's threshold by mistake, so nothing under a
                // 110 m blast radius threw anything at all.
                DebrisFx.Play(groundZero, radius);
            }
            catch (Exception e)
            {
                ModConfig.LogError("ShockWaveFx.Play error: " + e);
            }
        }

        /// <summary>
        /// How much of the full particle budget a front of this radius needs, MinScale to 1. A
        /// ring reads as continuous when the gaps between its particles are small against its
        /// circumference, and that circumference grows with the radius - so the small end can be
        /// drawn with a fraction of the particles and look identical.
        /// </summary>
        /// The floor is not free, though, and a quarter was below it. Every length here is a
        /// fraction of the radius, so the arithmetic comes out the same at every size: a puff is
        /// DustSizeFraction of the radius and grows 2.6x, so covering a circumference of 2*PI*R
        /// takes 2*PI / (0.055 * 2.6) = 44 of them however large the front is. At 0.25 the rings
        /// were drawn with 24, and a small blast's front was a string of beads.
        private const float MinScale = 0.45f;   // 96 * 0.45 = 43, just over the 44 above

        private static float CountScale(float radius)
        {
            const float full = 1500f;   // at and above this the full budget is spent
            float k = radius / full;
            if (k < MinScale) return MinScale;
            return k > 1f ? 1f : k;
        }

        private static int Count(int full, float scale)
        {
            int n = Mathf.RoundToInt(full * scale);
            return n < 8 ? 8 : n;
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

        /// <summary>
        /// The dust surge: a rolling wall of earth chasing the shock front outward, piling up
        /// and climbing as it goes. It rides the same Sedov speed curve as the rings, so it
        /// decelerates with the front rather than expanding at a constant rate, but it starts a
        /// beat later - the blast arrives before the ground it lifts - and rolls on after the
        /// front has spent itself.
        /// It is drawn with the opaque-cored cloud material, unlike the thin rings: this is the
        /// part that has to look like a wall of dirt rather than a haze.
        /// </summary>
        private static void CreateDustSurge(Vector3 origin, float radius, float duration,
            AnimationCurve speed, int count)
        {
            float life = duration * SurgeDurationFactor;
            var go = ParticleBuilder.NewSystem("ShockWaveDustSurge", origin, ParticleAssets.Cloud);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startDelay = duration * SurgeStartDelayFraction;
            main.startLifetime = life;
            main.startSpeed = ShockWave.FrontSpeed(radius, duration, ShockWave.MinFraction);
            // Many small clods and a few large ones, the same size variety the cloud's puffs use.
            float biggest = radius * SurgeSizeFraction;
            main.startSize = new ParticleSystem.MinMaxCurve(biggest * 0.55f, biggest);
            main.startColor = new ParticleSystem.MinMaxGradient(SurgeLit, SurgeShade);
            main.maxParticles = count * 2;

            ParticleBuilder.Burst(ps, count);
            ParticleBuilder.GroundRing(ps, ShockWave.StartRadius(radius));
            ParticleBuilder.SpeedCurve(ps, speed, 1f);
            ParticleBuilder.Gravity(ps, -SurgeLift); // negative gravity: the wall climbs as it rolls
            // Opaque almost at once, holding through the run, and only letting go at the very
            // end - a dust wall does not thin out halfway across the city.
            ParticleBuilder.Fade(ps,
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.04f),
                new GradientAlphaKey(0.95f, 0.55f), new GradientAlphaKey(0f, 1f));
            ParticleBuilder.SizeCurve(ps, 0.8f, SurgeGrowth); // thick from the start, piling up as it rolls
            ParticleBuilder.PlayAndDestroy(go, duration * SurgeStartDelayFraction + life + 1f);
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
