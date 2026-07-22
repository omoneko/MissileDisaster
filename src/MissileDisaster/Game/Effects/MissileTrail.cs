using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// 飛来ミサイルに隕石風の燃焼トレイル（火の粉＋煙）を付与する。
    /// パーティクルはワールド空間でシミュレートするため、急降下する弾体の後方に燃える航跡を残す。
    /// マテリアルは CS ランタイムで実在するシェーダーを解決して割り当てる（"Particles/Additive" は
    /// 除去されマゼンタになりがちなため）。GameObject/Mesh/Material を生成するためメインスレッド専用。
    /// </summary>
    public static class MissileTrail
    {
        private static Material _fireMat;
        private static Material _smokeMat;
        private static Texture2D _glowTex;
        private static bool _assetsReady;

        /// <summary>弾体 GameObject に火の粉と煙のパーティクルを子として付与する。失敗しても飛翔は継続。</summary>
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
            main.scalingMode = ParticleSystemScalingMode.Local; // 親(弾体)のスケールを無視
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
            EnableSizeCurve(ps, 1f, 0.1f); // 火の粉は縮んで消える

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = _fireMat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 0f;

            ps.Play();
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
            EnableSizeCurve(ps, 0.6f, 1.4f); // 煙は膨らむ

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = _smokeMat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 20f; // 火の粉より後ろ(奥)に描画

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

        /// <summary>寿命に沿ってアルファを 1→0 へフェードさせる（色は startColor 由来を維持）。</summary>
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

        /// <summary>寿命に沿ってサイズを from→to 倍へ変化させる。</summary>
        private static void EnableSizeCurve(ParticleSystem ps, float from, float to)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, from), new Keyframe(1f, to));
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        /// <summary>加算(火) または アルファブレンド(煙) のパーティクル用マテリアルを、実在シェーダーで生成する。</summary>
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
            if (shader == null) shader = Shader.Find("Standard"); // 最後の砦(発光しないがマゼンタ回避)
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
            RenderAssets.ApplyDepthOcclusion(mat); // 手前の建物に遮蔽させる（透過防止）
            ModConfig.Log("MissileTrail: " + (additive ? "fire" : "smoke") + " shader = " + shader.name);
            return mat;
        }

        /// <summary>中心が明るく外周が透明な放射状グロー texture（丸いパーティクル用）。</summary>
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
                    a = a * a; // 中心を締めて縁を柔らかく
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
