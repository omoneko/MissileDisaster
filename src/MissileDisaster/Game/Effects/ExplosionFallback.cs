using System;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// This mod's own explosion fireball - an additive burst of flame plus black smoke - for
    /// when the meteor impact effect is unavailable. It scales with the destruction radius.
    /// Main thread only.
    /// </summary>
    public static class ExplosionFallback
    {
        public static void Play(Vector3 center, float radius)
        {
            try
            {
                float size = Mathf.Clamp(radius * 0.25f, 10f, 750f);
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
            UnityEngine.Object.Destroy(go, lifetime + 0.5f);
        }

    }
}
