using System;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// 核着弾点に、爆発規模(破壊半径)に合わせた特大のキノコ雲を生成する。メインスレッド専用。
    /// 構成: 基部の火球（加算）＋立ち上る煙柱（stem, 上昇）＋頂部で膨らむ傘（cap, 遅延放出）。
    /// マテリアルは CS ランタイムで実在するシェーダーを解決して割り当てる（マゼンタ回避）。
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
                float height = Mathf.Clamp(blastRadius * 0.8f, 500f, 6000f); // 成層圏まで高く立ち上る
                float capR = Mathf.Clamp(blastRadius * 0.35f, 250f, 3500f);  // 頂部の傘（キャノピー）は広く
                float stemR = capR * 0.32f;
                float riseTime = Mathf.Clamp(height / 450f, 5f, 14f);        // ゆっくり上昇（滞留感）

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
            main.startLifetime = riseTime + 8f; // 立ち上った煙柱が長く残る
            main.startSpeed = stemR * 0.12f;
            main.startSize = stemR * 1.1f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.15f, 0.14f, 0.7f));
            main.maxParticles = 500;
            main.duration = riseTime;
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 40f;

            Sphere(ps, stemR);

            // 上昇（ワールド空間で一定の上向き速度）。riseTime が長いほどゆっくり昇る。
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
            main.startDelay = riseTime * 0.55f; // 煙柱が頂部へ到達する頃に膨らみ始める
            main.startLifetime = 18f;           // 成層圏で長く滞留
            main.startSpeed = capR * 0.35f;     // 外側へ吹き出して傘（キャノピー）を形成
            main.startSize = capR * 0.7f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.18f, 0.16f, 0.15f, 0.7f), new Color(0.1f, 0.09f, 0.085f, 0.7f));
            main.maxParticles = 500;
            main.gravityModifier = 0.015f;      // 縁がわずかに垂れて笠のロールオーバー感

            Burst(ps, 100);
            ConeUp(ps, capR * 0.35f, 62f);      // 上向き広角コーンで外側へ傘状に展開
            DampenRise(ps, capR * 0.22f, 0.2f); // 上昇を頭打ちにして水平展開・滞留させる
            AlphaFadeSlow(ps);
            SizeCurve(ps, 0.7f, 2.5f);          // 大きく横へ広がる
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

        /// <summary>長めに視認できてからゆっくり消えるアルファ（滞留感）。</summary>
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

        /// <summary>上向きの広角コーンから放出（頂部で外側へ傘状に広がる）。Cone(+Z)を上へ向ける。</summary>
        private static void ConeUp(ParticleSystem ps, float radius, float angle)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
            ps.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        }

        /// <summary>上昇速度を頭打ちにして水平展開・滞留させる。</summary>
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
            RenderAssets.ApplyDepthOcclusion(mat); // 手前の建物に遮蔽させる（透過防止）
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
