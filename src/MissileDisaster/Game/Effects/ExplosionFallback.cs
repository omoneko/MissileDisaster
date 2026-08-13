using System;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// This mod's own explosion fireball - an additive burst of flame plus black smoke.
    ///
    /// <para>
    /// It is no longer only a fallback. Every non-nuclear warhead draws its fireball here now,
    /// because the vanilla meteor impact effect cannot be made smaller: DispatchEffect takes a
    /// SpawnArea radius and a magnitude, and the magnitude is a particle *density*, so the only
    /// thing either argument changes is how widely and how thickly the particles are scattered.
    /// The size of each particle lives in the effect prefab, which is a shared game asset - a
    /// meteor-sized flame stays meteor-sized however small the disc is. Shrinking the disc for a
    /// 1.5 t warhead therefore produced fewer huge flames rather than a smaller explosion, which
    /// is exactly what a subscriber reported.
    /// </para>
    ///
    /// Here the flame size is a parameter, so the fireball is whatever size it should be.
    /// Main thread only.
    /// </summary>
    public static class ExplosionFallback
    {
        /// <summary>
        /// Draws a fireball of the given radius, in metres. A particle grows to about 1.6x its
        /// start size and is scattered over a sphere 0.3x wide, so a start size near the radius
        /// puts the visible edge of the ball about where it was asked for.
        /// </summary>
        public static void Play(Vector3 center, float fireballRadius)
        {
            try
            {
                float size = Mathf.Clamp(fireballRadius, 4f, 750f);
                CreateBurst(center, "ExplosionFire", ParticleAssets.Fire, size, 0.8f, 60,
                    new Color(1f, 0.8f, 0.35f, 1f), new Color(1f, 0.4f, 0.08f, 1f), 1f, 1.6f);
                CreateBurst(center, "ExplosionSmoke", ParticleAssets.Smoke, size * 1.2f, 2.4f, 40,
                    new Color(0.12f, 0.11f, 0.1f, 0.6f), new Color(0.12f, 0.11f, 0.1f, 0.6f), 0.7f, 2.2f);
            }
            catch (Exception e)
            {
                ModConfig.LogError("ExplosionFallback.Play error: " + e);
            }
        }

        private static void CreateBurst(Vector3 center, string name, Material mat, float startSize,
            float lifetime, int burst, Color colorA, Color colorB, float sizeFrom, float sizeTo)
        {
            var go = new GameObject(name);
            go.transform.position = center;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = lifetime;
            main.startSpeed = startSize * 0.6f;
            main.startSize = startSize;
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.maxParticles = 256;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = startSize * 0.3f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, sizeFrom), new Keyframe(1f, sizeTo)));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (mat != null) renderer.material = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
            // On the game's clock, like every other effect the mod spawns.
            go.AddComponent<SimulationTimed>().LifetimeSeconds = lifetime + 0.5f;
        }

    }
}
