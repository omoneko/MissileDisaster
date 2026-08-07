using System;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// Builds the outsized mushroom cloud at a nuclear impact, scaled to the destruction radius.
    /// Main thread only.
    /// It has three parts: an additive fireball at the base, the stem rising from it, and the
    /// cap that swells at the top, emitted on a delay.
    /// The materials are built from shaders that actually exist in the CS runtime, which avoids
    /// the magenta error colour.
    /// </summary>
    public static class NuclearMushroomFx
    {
        private static Material _fireMat;
        private static Material _smokeMat;
        private static Texture2D _glowTex;
        private static bool _ready;

        public static void Play(Vector3 center, float blastRadius)
        {
            try
            {
                EnsureAssets();
                float height = Mathf.Clamp(blastRadius * 0.8f, 500f, 6000f); // rises high enough to read as reaching the stratosphere
                float capR = Mathf.Clamp(blastRadius * 0.35f, 250f, 3500f);  // the canopy at the top spreads wide
                float stemR = capR * 0.32f;
                float riseTime = Mathf.Clamp(height / 450f, 5f, 14f);        // it climbs slowly, so it appears to linger

                CreateFireball(center, capR);
                CreateStem(center, stemR, height, riseTime);
                CreateCap(center + Vector3.up * height, capR, riseTime);
            }
            catch (Exception e)
            {
                ModConfig.LogError("NuclearMushroomFx.Play error: " + e);
            }
        }

        private static void CreateFireball(Vector3 center, float capR)
        {
            var go = NewSystem("MushroomFireball", center, _fireMat);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1.6f;
            main.startSpeed = capR * 0.25f;
            main.startSize = capR * 0.9f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 0.4f, 1f), new Color(1f, 0.4f, 0.1f, 1f));
            main.maxParticles = 120;
            Burst(ps, 40);
            Sphere(ps, capR * 0.4f);
            AlphaFade(ps);
            SizeCurve(ps, 1f, 1.8f);
            ps.Play();
            UnityEngine.Object.Destroy(go, 3f);
        }

        private static void CreateStem(Vector3 center, float stemR, float height, float riseTime)
        {
            var go = NewSystem("MushroomStem", center, _smokeMat);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = riseTime + 8f; // the stem stays up well after it has risen
            main.startSpeed = stemR * 0.12f;
            main.startSize = stemR * 1.1f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.15f, 0.14f, 0.7f));
            main.maxParticles = 500;
            main.duration = riseTime;
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 40f;

            Sphere(ps, stemR);

            // The climb, at a constant upward speed in world space. A longer riseTime makes it
            // slower.
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(height / riseTime);

            AlphaFadeSlow(ps);
            SizeCurve(ps, 0.8f, 1.6f);
            ps.Play();
            UnityEngine.Object.Destroy(go, riseTime + 9f);
        }

        private static void CreateCap(Vector3 top, float capR, float riseTime)
        {
            var go = NewSystem("MushroomCap", top, _smokeMat);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startDelay = riseTime * 0.55f; // starts to swell about when the stem reaches the top
            main.startLifetime = 18f;           // lingers at the top for a long time
            main.startSpeed = capR * 0.35f;     // blows outwards to form the canopy
            main.startSize = capR * 0.7f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.18f, 0.16f, 0.15f, 0.7f), new Color(0.1f, 0.09f, 0.085f, 0.7f));
            main.maxParticles = 500;
            main.gravityModifier = 0.015f;      // the rim droops slightly, giving the cap its rollover

            Burst(ps, 100);
            ConeUp(ps, capR * 0.35f, 62f);      // a wide upward cone spreads it outwards into the canopy
            DampenRise(ps, capR * 0.22f, 0.2f); // caps the climb so it spreads sideways and lingers
            AlphaFadeSlow(ps);
            SizeCurve(ps, 0.7f, 2.5f);          // grows considerably as it spreads
            ps.Play();
            UnityEngine.Object.Destroy(go, riseTime * 0.55f + 20f);
        }

        // ---- helpers ----

        private static GameObject NewSystem(string name, Vector3 pos, Material mat)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (mat != null) renderer.material = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return go;
        }

        private static void Burst(ParticleSystem ps, int count)
        {
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        private static void Sphere(ParticleSystem ps, float radius)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
        }

        private static void AlphaFade(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
        }

        private static void SizeCurve(ParticleSystem ps, float from, float to)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, from), new Keyframe(1f, to)));
        }

        /// <summary>An alpha curve that stays visible for a while and then fades slowly, which is what makes it linger.</summary>
        private static void AlphaFadeSlow(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.85f, 0.25f),
                    new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);
        }

        /// <summary>Emits from a wide upward cone, spreading into the canopy at the top. The cone's +Z is turned upwards.</summary>
        private static void ConeUp(ParticleSystem ps, float radius, float angle)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        }

        /// <summary>Caps the upward speed so it spreads horizontally and lingers.</summary>
        private static void DampenRise(ParticleSystem ps, float limit, float dampen)
        {
            var lv = ps.limitVelocityOverLifetime;
            lv.enabled = true;
            lv.dampen = dampen;
            lv.limit = new ParticleSystem.MinMaxCurve(limit);
        }

        private static void EnsureAssets()
        {
            if (_ready) return;
            _ready = true;
            _glowTex = BuildGlowTexture(64);
            _fireMat = BuildMaterial(true);
            _smokeMat = BuildMaterial(false);
        }

        private static Material BuildMaterial(bool additive)
        {
            Shader shader = additive
                ? RenderAssets.FindFirst("Particles/Additive", "Legacy Shaders/Particles/Additive", "Mobile/Particles/Additive")
                : RenderAssets.FindFirst("Particles/Alpha Blended", "Legacy Shaders/Particles/Alpha Blended");
            if (shader == null) shader = additive
                ? RenderAssets.FindLoadedContaining("additive")
                : RenderAssets.FindLoadedContaining("alpha blend", "alphablend");
            if (shader == null) shader = RenderAssets.FindFirst("Sprites/Default", "Unlit/Transparent");
            if (shader == null) shader = RenderAssets.FindLoadedContaining("particle", "sprite", "unlit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            var mat = new Material(shader);
            if (_glowTex != null)
            {
                mat.mainTexture = _glowTex;
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", _glowTex);
            }
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            mat.color = Color.white;
            RenderAssets.ApplyDepthOcclusion(mat); // let buildings in front occlude it, instead of showing through
            return mat;
        }

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
