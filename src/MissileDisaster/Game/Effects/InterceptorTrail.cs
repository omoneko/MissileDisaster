using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// 迎撃ミサイルに付けるロケット排気トレイル（ノズル火炎＋噴煙）。ワールド空間なので飛翔経路に
    /// 白い噴煙の航跡を残し、弾体破棄後も噴煙は寿命まで漂わせる（「少しの間残る」）。
    /// GameObject/Material/Mesh を生成するためメインスレッド専用。
    /// </summary>
    public static class InterceptorTrail
    {
        private static Material _fireMat;
        private static Material _smokeMat;
        private static Texture2D _glowTex;
        private static bool _ready;

        /// <summary>迎撃ミサイル GameObject にノズル火炎と噴煙を子として付与する。失敗しても飛翔は継続。</summary>
        public static void Attach(GameObject interceptor)
        {
            if (interceptor == null) return;
            try
            {
                EnsureAssets();
                CreateFire(interceptor);
                CreateSmoke(interceptor);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("InterceptorTrail.Attach error: " + e);
            }
        }

        /// <summary>
        /// 弾体からトレイルを切り離し、新規放出だけ止めて既存の噴煙は寿命まで残す。
        /// 弾体ごと即破棄すると噴煙が一瞬で消えるため、迎撃点到達時にこれを呼ぶ。
        /// </summary>
        public static void DetachAndLinger(GameObject interceptor)
        {
            if (interceptor == null) return;
            ParticleSystem[] systems = interceptor.GetComponentsInChildren<ParticleSystem>();
            float life = Mathf.Max(ModConfig.ExhaustFireLifetime, ModConfig.ExhaustSmokeLifetime);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null) continue;
                ps.transform.SetParent(null, true); // ワールド位置維持で独立
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Object.Destroy(ps.gameObject, life + 0.1f);
            }
        }

        private static void EnsureAssets()
        {
            if (_ready) return;
            _ready = true;
            _glowTex = BuildGlowTexture(64);
            _fireMat = BuildParticleMaterial(true);
            _smokeMat = BuildParticleMaterial(false);
        }

        private static void CreateFire(GameObject parent)
        {
            ParticleSystem ps = NewChildSystem(parent, "InterceptorExhaust_Fire");
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = ModConfig.ExhaustFireLifetime;
            main.startSpeed = 1.2f;
            main.startSize = ModConfig.ExhaustFireSize;
            main.startColor = new ParticleSystem.MinMaxGradient(ModConfig.ExhaustFireColor);
            main.maxParticles = 300;

            var emission = ps.emission;
            emission.rateOverTime = ModConfig.ExhaustFireRate;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = ModConfig.ExhaustFireSize * 0.2f;

            EnableAlphaFade(ps);
            EnableSizeCurve(ps, 1f, 0.1f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (_fireMat != null) renderer.material = _fireMat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
        }

        private static void CreateSmoke(GameObject parent)
        {
            ParticleSystem ps = NewChildSystem(parent, "InterceptorExhaust_Smoke");
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = ModConfig.ExhaustSmokeLifetime; // 長め=噴煙が残る
            main.startSpeed = 0.6f;
            main.startSize = ModConfig.ExhaustSmokeSize;
            main.startColor = new ParticleSystem.MinMaxGradient(ModConfig.ExhaustSmokeColor);
            main.maxParticles = 800;

            var emission = ps.emission;
            emission.rateOverTime = ModConfig.ExhaustSmokeRate;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = ModConfig.ExhaustSmokeSize * 0.12f;

            EnableAlphaFade(ps);
            EnableSizeCurve(ps, 0.5f, 1.1f); // 噴煙は細いまま薄れる（膨張を抑制）

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (_smokeMat != null) renderer.material = _smokeMat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 20f;
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
                    new GradientAlphaKey(0.8f, 0.3f),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);
        }

        private static void EnableSizeCurve(ParticleSystem ps, float from, float to)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, from), new Keyframe(1f, to));
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static Material BuildParticleMaterial(bool additive)
        {
            Shader shader = additive
                ? RenderAssets.FindFirst("Particles/Additive", "Legacy Shaders/Particles/Additive", "Mobile/Particles/Additive")
                : RenderAssets.FindFirst("Particles/Alpha Blended", "Legacy Shaders/Particles/Alpha Blended", "Particles/Alpha Blended Premultiply");
            if (shader == null)
                shader = additive ? RenderAssets.FindLoadedContaining("additive")
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
