using System.IO;
using UnityEngine;

namespace MissileDisaster.Game.UI
{
    /// <summary>
    /// Supplies the texture for the icon in the panel. If icon.png is present in the mod folder
    /// it is loaded; otherwise a simple upward missile is generated procedurally, on a
    /// transparent background.
    /// </summary>
    public static class MissileIcon
    {
        private static string _modDir;

        /// <summary>Sets the mod folder, from Mod.OnEnabled. Used to look for icon.png.</summary>
        public static void SetModDirectory(string dir) { _modDir = dir; }

        /// <summary>Loads icon.png if it exists, otherwise null.</summary>
        private static Texture2D TryLoadPng()
        {
            try
            {
                if (string.IsNullOrEmpty(_modDir)) return null;
                string path = Path.Combine(_modDir, "icon.png");
                if (!File.Exists(path)) return null;
                var t = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                t.wrapMode = TextureWrapMode.Clamp;
                if (!t.LoadImage(File.ReadAllBytes(path))) { Object.Destroy(t); return null; }
                ModConfig.Log("using icon.png for the panel icon");
                return t;
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("MissileIcon.TryLoadPng error: " + e);
                return null;
            }
        }

        public static Texture2D Build(int size)
        {
            Texture2D png = TryLoadPng();
            if (png != null) return png;

            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[size * size];
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color body = new Color32(228, 228, 233, 255);
            Color nose = new Color32(232, 82, 60, 255);
            Color fin = new Color32(150, 150, 160, 255);
            Color flame = new Color32(255, 170, 60, 255);
            float cx = (size - 1) * 0.5f;
            const float bodyHalf = 0.11f;

            for (int yy = 0; yy < size; yy++)
            {
                for (int xx = 0; xx < size; xx++)
                {
                    float fx = (xx - cx) / size;  // -0.5..0.5
                    float fy = (float)yy / size;  // 0 at the bottom .. 1 at the top
                    Color c = clear;

                    if (fy >= 0.20f && fy <= 0.72f && Mathf.Abs(fx) <= bodyHalf) c = body; // the body
                    if (fy > 0.72f && fy <= 0.95f)                                          // the nose, in red
                    {
                        float t = (0.95f - fy) / (0.95f - 0.72f);
                        if (Mathf.Abs(fx) <= bodyHalf * t) c = nose;
                    }
                    if (fy >= 0.18f && fy <= 0.40f)                                         // the fins
                    {
                        float t = (0.40f - fy) / (0.40f - 0.18f);
                        float finHalf = bodyHalf + 0.14f * t;
                        if (Mathf.Abs(fx) <= finHalf && Mathf.Abs(fx) > bodyHalf) c = fin;
                    }
                    if (fy >= 0.05f && fy < 0.20f)                                          // the exhaust flame
                    {
                        float t = (fy - 0.05f) / (0.20f - 0.05f);
                        if (Mathf.Abs(fx) <= bodyHalf * t) c = flame;
                    }

                    px[yy * size + xx] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
