using System.IO;
using UnityEngine;

namespace MissileDisaster.Game.UI
{
    /// <summary>
    /// タブ内アイコンのテクスチャを用意する。Mod配置フォルダに icon.png があればそれを読み込み、
    /// 無ければ上向きミサイルの簡易アイコン（透明背景）を手続き生成する。
    /// </summary>
    public static class MissileIcon
    {
        private static string _modDir;

        /// <summary>Mod配置フォルダを設定する（Mod.OnEnabled から）。icon.png の探索に使う。</summary>
        public static void SetModDirectory(string dir) { _modDir = dir; }

        /// <summary>icon.png があれば読み込んで返す。無ければ null。</summary>
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
                ModConfig.Log("タブアイコンに icon.png を使用します");
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
                    float fy = (float)yy / size;  // 0(下)..1(上)
                    Color c = clear;

                    if (fy >= 0.20f && fy <= 0.72f && Mathf.Abs(fx) <= bodyHalf) c = body; // 機体
                    if (fy > 0.72f && fy <= 0.95f)                                          // ノーズ(赤)
                    {
                        float t = (0.95f - fy) / (0.95f - 0.72f);
                        if (Mathf.Abs(fx) <= bodyHalf * t) c = nose;
                    }
                    if (fy >= 0.18f && fy <= 0.40f)                                         // フィン
                    {
                        float t = (0.40f - fy) / (0.40f - 0.18f);
                        float finHalf = bodyHalf + 0.14f * t;
                        if (Mathf.Abs(fx) <= finHalf && Mathf.Abs(fx) > bodyHalf) c = fin;
                    }
                    if (fy >= 0.05f && fy < 0.20f)                                          // 噴射炎
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
