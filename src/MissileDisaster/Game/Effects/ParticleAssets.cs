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
        // The cloud texture's profile: opaque out to CoreEnd, a smooth fall to nothing at
        // EdgeEnd, the rim wobbled so the puffs are not perfect circles. Mirrored exactly in
        // tools/effect-preview/cloud_preview.py, which is where these numbers were verified:
        // the glow profile used before measured 0.34-0.41 opacity across the cap - the player
        // could see the sky through what was supposed to be a cloud - and this one 0.997.
        public const float CloudCoreEnd = 0.42f;
        public const float CloudEdgeEnd = 0.95f;
        public const float CloudRimWobble = 0.10f;

        private static Material _fire;
        private static Material _smoke;
        private static Material _cloud;
        private static Texture2D _glow;
        private static Texture2D _cloudTex;

        /// <summary>Additive, for fireballs and anything else that glows.</summary>
        public static Material Fire { get { Ensure(); return _fire; } }

        /// <summary>Alpha blended with a soft-glow falloff, for thin smoke, dust and trails.</summary>
        public static Material Smoke { get { Ensure(); return _smoke; } }

        /// <summary>
        /// Alpha blended with an opaque core, for cloud bodies. The soft-glow texture is almost
        /// transparent everywhere but its centre - right for a wisp of dust, fatal for a cloud,
        /// which the background must not show through.
        /// </summary>
        public static Material Cloud { get { Ensure(); return _cloud; } }

        private static void Ensure()
        {
            if (_glow == null) _glow = BuildGlowTexture(64);
            if (_cloudTex == null) _cloudTex = BuildCloudTexture(128);
            if (_fire == null) _fire = BuildMaterial(true, _glow);
            if (_smoke == null) _smoke = BuildMaterial(false, _glow);
            if (_cloud == null) _cloud = BuildMaterial(false, _cloudTex);
        }

        private static Material BuildMaterial(bool additive, Texture2D texture)
        {
            // The game's OWN particle shaders come first, and they are what actually exist:
            // enumerating every Shader in the game's assets shows no "Particles/Alpha Blended"
            // and no "Legacy Shaders/..." at all - Unity's built-ins are stripped. What CS ships
            // is Custom/Particles/*, and those are the ones that behave like particle shaders:
            // _TintColor declared, so the per-particle colour is multiplied in, and ZTest LEqual,
            // so geometry in front occludes them.
            //
            // Getting this wrong is not subtle. The old list fell through to a substring search
            // for "alphablend", which can match Custom/Loading/AlphaBlend - a loading-screen
            // shader with no _TintColor (every particle draws white, whatever colour it was
            // given) and ZTest Always (it draws over everything, so effects behind the cloud
            // appear through it). That is exactly the "no colour, and the background shows
            // through" the playtest reported.
            Shader shader = additive
                ? RenderAssets.FindFirst("Custom/Particles/Additive (Soft)", "Particles/Additive",
                    "Legacy Shaders/Particles/Additive", "Mobile/Particles/Additive")
                : RenderAssets.FindFirst("Custom/Particles/Alpha Blended", "Particles/Alpha Blended",
                    "Legacy Shaders/Particles/Alpha Blended");
            // Substring fallbacks, with the loading-screen shaders excluded by name for the
            // reason above.
            if (shader == null) shader = additive
                ? RenderAssets.FindLoadedContaining(new[] { "loading" }, "particles/additive", "additive")
                : RenderAssets.FindLoadedContaining(new[] { "loading" }, "particles/alpha blended", "alpha blend", "alphablend");
            if (shader == null) shader = RenderAssets.FindFirst("Unlit/Transparent", "Sprites/Default");
            if (shader == null) shader = RenderAssets.FindLoadedContaining(new[] { "loading" }, "particle", "sprite", "unlit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;

            var mat = new Material(shader);
            if (texture != null)
            {
                mat.mainTexture = texture;
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
            }
            // Custom/Particles/Alpha Blended multiplies by _TintColor. Unity's own particle
            // shaders take it at half grey (the classic 0.5 convention); CS's takes it straight,
            // so white here means "show the particle's own colour unchanged".
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            mat.color = Color.white;
            RenderAssets.ApplyDepthOcclusion(mat); // let buildings in front occlude it, instead of showing through
            return mat;
        }

        /// <summary>
        /// The cloud puff: opaque across its whole core, falling smoothly to nothing at a rim
        /// that wobbles three times around, so overlapping puffs read as one solid mass with a
        /// ragged edge instead of a spray of translucent dots.
        /// </summary>
        private static Texture2D BuildCloudTexture(int size)
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
                    float angle = Mathf.Atan2(dy, dx);
                    float wobble = 1f + CloudRimWobble * Mathf.Sin(3f * angle);
                    float dd = d / Mathf.Max(wobble, 0.0001f);
                    float t = Mathf.Clamp01((dd - CloudCoreEnd) / (CloudEdgeEnd - CloudCoreEnd));
                    float a = 1f - t * t * (3f - 2f * t); // 1 inside the core, smooth to 0 at the rim
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
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
