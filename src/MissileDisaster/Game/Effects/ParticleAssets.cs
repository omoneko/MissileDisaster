using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// The two materials every effect in this mod draws with - an additive one for anything
    /// glowing and an alpha-blended one for smoke and dust - plus the soft round texture they
    /// share. Main thread only.
    /// They are built from shaders that actually exist in the CS runtime, which is what avoids
    /// the magenta error colour, and they are cached.
    /// The cache is guarded on the objects rather than on a "built it already" flag: Unity
    /// destroys these when a city is unloaded, leaving a reference that compares equal to null,
    /// and a flag would leave every effect afterwards silently invisible.
    /// </summary>
    public static class ParticleAssets
    {
        private static Material _fire;
        private static Material _smoke;
        private static Texture2D _glow;

        /// <summary>Additive, for fireballs and anything else that glows.</summary>
        public static Material Fire { get { Ensure(); return _fire; } }

        /// <summary>Alpha blended, for smoke, dust and cloud.</summary>
        public static Material Smoke { get { Ensure(); return _smoke; } }

        private static void Ensure()
        {
            if (_glow == null) _glow = BuildGlowTexture(64);
            if (_fire == null) _fire = BuildMaterial(true);
            if (_smoke == null) _smoke = BuildMaterial(false);
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
            if (_glow != null)
            {
                mat.mainTexture = _glow;
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", _glow);
            }
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            mat.color = Color.white;
            RenderAssets.ApplyDepthOcclusion(mat); // let buildings in front occlude it, instead of showing through
            return mat;
        }

        /// <summary>A round white blob fading to nothing at its edge, which is what stops the particles reading as squares.</summary>
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
