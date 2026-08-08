using System;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// Builds the outsized mushroom cloud of a nuclear detonation. Main thread only.
    /// It always rises from ground zero, even when the warhead went off high above it, and the
    /// canopy is built to the width of the destruction radius: by the time it has spread, the
    /// cloud covers exactly the ground the blast wrecked, so it reads as a marker of the damage
    /// rather than a decoration.
    /// It has three parts: an additive fireball at the base, the stem rising from it, and the
    /// cap that swells at the top, emitted on a delay.
    /// The materials are built from shaders that actually exist in the CS runtime, which avoids
    /// the magenta error colour.
    /// </summary>
    public static class NuclearMushroomFx
    {
        // Proportions of the cloud, all relative to the canopy radius - which is the destruction
        // radius - so that the whole thing scales as one shape with the yield.
        private const float HeightFactor = 2.2f;    // how tall the column stands against the canopy width
        private const float StemFactor = 0.15f;     // the stem is a narrow column below a broad cap
        private const float FireballFactor = 0.15f; // the fireball itself is a fraction of what it destroys
        // Engineering limits. The playable map is about 17 km across, so a canopy beyond this
        // already spans it and only costs particle size.
        private const float CapRadiusMin = 200f;
        private const float CapRadiusMax = 8000f;
        private const float HeightMin = 800f;
        private const float HeightMax = 12000f;

        private static Material _fireMat;
        private static Material _smokeMat;
        private static Texture2D _glowTex;
        private static bool _ready;

        /// <summary>Raises the cloud from groundZero. The canopy ends up as wide as destructionRadius.</summary>
        public static void Play(Vector3 groundZero, float destructionRadius)
        {
            try
            {
                EnsureAssets();
                float capR = Mathf.Clamp(destructionRadius, CapRadiusMin, CapRadiusMax);
                float height = Mathf.Clamp(capR * HeightFactor, HeightMin, HeightMax);
                float stemR = capR * StemFactor;
                float fireballR = capR * FireballFactor;
                float riseTime = Mathf.Clamp(height / 450f, 5f, 14f); // it climbs slowly, so it appears to linger

                CreateFireball(groundZero, fireballR);
                CreateStem(groundZero, stemR, height, riseTime);
                CreateCap(groundZero + Vector3.up * height, capR, riseTime);
            }
            catch (Exception e)
            {
                ModConfig.LogError("NuclearMushroomFx.Play error: " + e);
            }
        }

        private static void CreateFireball(Vector3 center, float fireballR)
        {
            var go = NewSystem("MushroomFireball", center, _fireMat);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 1.6f;
            main.startSpeed = fireballR * 0.25f;
            main.startSize = fireballR * 0.9f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 0.4f, 1f), new Color(1f, 0.4f, 0.1f, 1f));
            main.maxParticles = 120;
            Burst(ps, 40);
            Sphere(ps, fireballR * 0.4f);
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
            // Barely any outward speed: the column has to stay narrow for the whole cloud to read
            // as a mushroom under its much broader cap.
            main.startSpeed = stemR * 0.02f;
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

        /// <summary>
        /// The canopy. Its three contributions - where the particles are emitted, how far they
        /// drift in their lifetime and how large they have grown by the end - are set so that
        /// they add up to capR, the destruction radius: the cap starts at about half that and
        /// swells to cover it exactly. Letting the drift run free, as an untuned speed does over
        /// an 18 second lifetime, is what makes a cloud spread to several times the area the
        /// blast actually destroyed.
        /// </summary>
        private static void CreateCap(Vector3 top, float capR, float riseTime)
        {
            const float lifetime = 18f;       // it lingers at the top for a long time
            const float emitFraction = 0.35f; // where the particles start, as a fraction of capR
            const float sizeFraction = 0.45f; // particle diameter at birth, likewise
            const float growth = 1.6f;        // how much larger a particle is by the end of its life
            // What is left of capR once the emission spread and the final particle radius are
            // accounted for is the distance the canopy is allowed to drift outwards, and the
            // speed is simply that distance over the lifetime.
            float driftDistance = capR * (1f - emitFraction - sizeFraction * growth * 0.5f);
            if (driftDistance < 0f) driftDistance = 0f;
            float driftSpeed = driftDistance / lifetime;

            var go = NewSystem("MushroomCap", top, _smokeMat);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startDelay = riseTime * 0.55f; // starts to swell about when the stem reaches the top
            main.startLifetime = lifetime;
            main.startSpeed = driftSpeed * 2.5f; // it billows out quickly at first, then is damped
            main.startSize = capR * sizeFraction;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.18f, 0.16f, 0.15f, 0.7f), new Color(0.1f, 0.09f, 0.085f, 0.7f));
            main.maxParticles = 500;
            main.gravityModifier = 0.015f;      // the rim droops slightly, giving the cap its rollover

            Burst(ps, 100);
            ConeUp(ps, capR * emitFraction, 62f); // a wide upward cone spreads it outwards into the canopy
            DampenRise(ps, driftSpeed, 0.2f);     // holds it to the drift the canopy width allows
            AlphaFadeSlow(ps);
            SizeCurve(ps, 0.7f, growth);
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
