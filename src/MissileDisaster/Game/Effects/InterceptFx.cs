using System;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// 迎撃成功時の簡易閃光。迎撃点で加算パーティクルを一度だけバースト放出し、短寿命で消す。
    /// バニラの爆発プレハブに依存しない自前実装（参照解決の失敗でマゼンタ化するのを避ける）。
    /// GameObject/Material を生成するためメインスレッド専用（MissileManager.UpdateVisual 側から呼ぶ）。
    /// </summary>
    public static class InterceptFx
    {
        private static Material _flashMat;
        private static Texture2D _glowTex;
        private static bool _ready;

        /// <summary>迎撃成功時の閃光を一度だけ放出する（撃墜）。</summary>
        public static void PlayFlash(Vector3 point)
        {
            Emit("InterceptFlash", point, ModConfig.InterceptFlashBurst,
                ModConfig.InterceptFlashSize, ModConfig.InterceptFlashSpeed);
        }

        /// <summary>迎撃失敗（空振り）時の小さな不発煙を放出する。</summary>
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
                EnableSizeCurve(ps, 1f, 0.15f); // 火花は縮んで消える

                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (_flashMat != null) renderer.material = _flashMat;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;

                ps.Play();
                UnityEngine.Object.Destroy(go, ModConfig.InterceptFlashLifetime + 0.3f);
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

        /// <summary>寿命に沿ってアルファを 1→0 へフェード。</summary>
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

        /// <summary>寿命に沿ってサイズを from→to 倍へ。</summary>
        private static void EnableSizeCurve(ParticleSystem ps, float from, float to)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, from), new Keyframe(1f, to));
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        /// <summary>加算パーティクル用マテリアルを、CS ランタイムで実在するシェーダーで生成する。</summary>
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

        /// <summary>中心が明るく外周が透明な放射状グロー texture。</summary>
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
