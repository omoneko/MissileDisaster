using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// Gives an incoming missile a meteor-like burning trail of sparks and smoke.
    /// The particles simulate in world space, so a burning wake is left behind the diving
    /// missile.
    /// The materials are built from shaders that actually exist in the CS runtime, because
    /// "Particles/Additive" is usually stripped and would leave everything magenta.
    /// It creates GameObjects, Meshes and Materials, so it is main thread only.
    /// </summary>
    public static class MissileTrail
    {
        private static Material _fireMat;
        private static Material _smokeMat;
        private static Texture2D _glowTex;
        private static bool _assetsReady;

        /// <summary>Attaches the spark and smoke particles as children of the missile. A failure here does not stop it flying.</summary>
        public static void Attach(GameObject missile)
        {
            if (missile == null) return;
            try
            {
                EnsureAssets();
                CreateFire(missile);
                CreateSmoke(missile);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("MissileTrail.Attach error: " + e);
            }
        }

        private static void EnsureAssets()
        {
            if (_assetsReady) return;
            _assetsReady = true;
            RenderAssets.DumpAvailableShadersOnce();
            _glowTex = BuildGlowTexture(64);
            _fireMat = BuildParticleMaterial(true);
            _smokeMat = BuildParticleMaterial(false);
        }

        private static void CreateFire(GameObject missile)
        {
            ParticleSystem ps = NewChildSystem(missile, "MissileTrail_Fire");
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local; // ignores the missile's own scale
            main.startLifetime = ModConfig.TrailFireLifetime;
            main.startSpeed = ModConfig.TrailFireSpeed;
            main.startSize = ModConfig.TrailFireSize;
            main.startColor = new ParticleSystem.MinMaxGradient(ModConfig.TrailFireCoreColor, ModConfig.TrailFireEdgeColor);
            main.maxParticles = 500;

            var emission = ps.emission;
            emission.rateOverTime = ModConfig.TrailFireRate;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = ModConfig.TrailFireSize * 0.2f;

            EnableAlphaFade(ps);
            EnableSizeCurve(ps, 1f, 0.1f); // the sparks shrink away

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = _fireMat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 0f;

            ps.Play();
            // Speed only, no lifetime: the trail lives as long as what it is attached to.
            ps.gameObject.AddComponent<SimulationTimed>().LifetimeSeconds = 0f;
        }

        private static void CreateSmoke(GameObject missile)
        {
            ParticleSystem ps = NewChildSystem(missile, "MissileTrail_Smoke");
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = ModConfig.TrailSmokeLifetime;
            main.startSpeed = ModConfig.TrailFireSpeed * 0.4f;
            main.startSize = ModConfig.TrailSmokeSize;
            main.startColor = new ParticleSystem.MinMaxGradient(ModConfig.TrailSmokeColor);
            main.maxParticles = 400;

            var emission = ps.emission;
            emission.rateOverTime = ModConfig.TrailSmokeRate;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = ModConfig.TrailSmokeSize * 0.15f;

            EnableAlphaFade(ps);
            EnableSizeCurve(ps, 0.6f, 1.4f); // the smoke billows

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = _smokeMat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 20f; // drawn behind the sparks

            ps.Play();
        }

        private static ParticleSystem NewChildSystem(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
            return go.AddComponent<ParticleSystem>();
        }

        /// <summary>Fades the alpha from 1 to 0 over the particle's lifetime, keeping the colour from startColor.</summary>
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
                    new GradientAlphaKey(0.85f, 0.35f),
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

        /// <summary>Creates the particle material - additive for fire, alpha-blended for smoke - from a shader that exists.</summary>
        private static Material BuildParticleMaterial(bool additive)
        {
            Shader shader = additive
                ? RenderAssets.FindFirst("Particles/Additive", "Legacy Shaders/Particles/Additive", "Mobile/Particles/Additive")
                : RenderAssets.FindFirst("Particles/Alpha Blended", "Legacy Shaders/Particles/Alpha Blended", "Particles/Alpha Blended Premultiply");
            if (shader == null)
                shader = additive
                    ? RenderAssets.FindLoadedContaining("additive")
                    : RenderAssets.FindLoadedContaining("alpha blend", "alphablend");
            if (shader == null) shader = RenderAssets.FindFirst("Sprites/Default", "Unlit/Transparent");
            if (shader == null) shader = RenderAssets.FindLoadedContaining("particle", "sprite", "unlit");
            if (shader == null) shader = Shader.Find("Standard"); // last resort: it does not glow, but it is not magenta
            if (shader == null) return null;

            var mat = new Material(shader);
            if (_glowTex != null)
            {
                mat.mainTexture = _glowTex;
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", _glowTex);
            }
            Color white = Color.white;
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", white);
            mat.color = white;
            RenderAssets.ApplyDepthOcclusion(mat); // let buildings in front occlude it, instead of showing through
            ModConfig.Log("MissileTrail: " + (additive ? "fire" : "smoke") + " shader = " + shader.name);
            return mat;
        }

        /// <summary>A radial glow texture, bright at the centre and transparent at the edge, for round particles.</summary>
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
                    a = a * a; // tightens the centre and softens the edge
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
