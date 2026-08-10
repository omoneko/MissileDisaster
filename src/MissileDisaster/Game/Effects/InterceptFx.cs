using System;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// The flash on a successful interception: one burst of additive particles at the intercept
    /// point, gone again quickly.
    /// It owes nothing to the game's own explosion prefabs, which avoids the magenta result when
    /// a reference cannot be resolved.
    /// It creates GameObjects and Materials, so it is main thread only, called from
    /// MissileManager.UpdateVisual.
    /// </summary>
    public static class InterceptFx
    {
        private static Material _flashMat;
        private static Texture2D _glowTex;
        private static bool _ready;

        /// <summary>Emits the flash of a successful interception, once.</summary>
        public static void PlayFlash(Vector3 point)
        {
            Emit("InterceptFlash", point, ModConfig.InterceptFlashBurst,
                ModConfig.InterceptFlashSize, ModConfig.InterceptFlashSpeed);
        }

        /// <summary>Emits the small puff of smoke a miss leaves behind.</summary>
        public static void PlayFizzle(Vector3 point)
        {
            Emit("InterceptFizzle", point, ModConfig.InterceptFizzleBurst,
                ModConfig.InterceptFlashSize * 0.4f, ModConfig.InterceptFlashSpeed * 0.35f);
        }

        private static void Emit(string name, Vector3 point, int burst, float size, float speed)
        {
            try
            {
                EnsureAssets();
                var go = new GameObject(name);
                go.transform.position = point;
                var ps = go.AddComponent<ParticleSystem>();

                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = ModConfig.InterceptFlashLifetime;
                main.startSpeed = speed;
                main.startSize = size;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    ModConfig.InterceptFlashCoreColor, ModConfig.InterceptFlashEdgeColor);
                main.maxParticles = 256;

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = size * 0.2f;

                EnableAlphaFade(ps);
                EnableSizeCurve(ps, 1f, 0.15f); // the sparks shrink away

                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (_flashMat != null) renderer.material = _flashMat;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;

                ps.Play();
                go.AddComponent<SimulationTimed>().LifetimeSeconds = ModConfig.InterceptFlashLifetime + 0.3f;
            }
            catch (Exception e)
            {
                ModConfig.LogError("InterceptFx.Emit(" + name + ") error: " + e);
            }
        }

        private static void EnsureAssets()
        {
            if (_ready) return;
            _ready = true;
            _glowTex = BuildGlowTexture(64);
            _flashMat = BuildAdditiveMaterial(_glowTex);
        }

        /// <summary>Fades the alpha from 1 to 0 over the particle's lifetime.</summary>
        private static void EnableAlphaFade(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.7f, 0.4f),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);
        }

        /// <summary>Scales the size from one multiplier to another over the particle's lifetime.</summary>
        private static void EnableSizeCurve(ParticleSystem ps, float from, float to)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, from), new Keyframe(1f, to));
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        /// <summary>Creates the additive particle material from a shader that actually exists in the CS runtime.</summary>
        private static Material BuildAdditiveMaterial(Texture2D tex)
        {
            Shader shader = RenderAssets.FindFirst(
                "Particles/Additive", "Legacy Shaders/Particles/Additive", "Mobile/Particles/Additive");
            if (shader == null) shader = RenderAssets.FindLoadedContaining("additive");
            if (shader == null) shader = RenderAssets.FindFirst("Sprites/Default", "Unlit/Transparent");
            if (shader == null) shader = RenderAssets.FindLoadedContaining("particle", "sprite", "unlit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            var mat = new Material(shader);
            if (tex != null)
            {
                mat.mainTexture = tex;
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            mat.color = Color.white;
            return mat;
        }

        /// <summary>A radial glow texture, bright at the centre and transparent at the edge.</summary>
        private static Texture2D BuildGlowTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float half = (size - 1) * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
