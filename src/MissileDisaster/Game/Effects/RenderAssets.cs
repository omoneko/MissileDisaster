using System;
using System.Text;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// CS の Unity ランタイムで「実際に利用可能なシェーダー」を見つけるユーティリティ。
    /// CS は未参照の組み込みシェーダー（例: "Particles/Additive"）をビルドから除去していることが多く、
    /// Shader.Find がそれらに null を返す → マテリアルが付かず「マゼンタ」になる。実在するものを順に探す。
    /// Alien Invasion から移植（ロジック不変）。全て GameObject/Shader に触れるためメインスレッド専用。
    /// </summary>
    public static class RenderAssets
    {
        private static bool _dumped;

        /// <summary>候補名を順に Shader.Find し、最初に見つかった(非null)シェーダーを返す。全滅なら null。</summary>
        public static Shader FindFirst(params string[] names)
        {
            if (names == null) return null;
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    Shader s = Shader.Find(names[i]);
                    if (s != null) return s;
                }
                catch (Exception) { /* 次の候補へ */ }
            }
            return null;
        }

        /// <summary>ロード済みシェーダーから、名前に substrsLower のいずれか(小文字)を含む最初のものを返す。</summary>
        public static Shader FindLoadedContaining(params string[] substrsLower)
        {
            try
            {
                Shader[] all = Resources.FindObjectsOfTypeAll<Shader>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == null || string.IsNullOrEmpty(all[i].name)) continue;
                    string lower = all[i].name.ToLowerInvariant();
                    for (int j = 0; j < substrsLower.Length; j++)
                    {
                        if (!string.IsNullOrEmpty(substrsLower[j]) && lower.Contains(substrsLower[j])) return all[i];
                    }
                }
            }
            catch (Exception e)
            {
                ModConfig.LogError("RenderAssets.FindLoadedContaining error: " + e);
            }
            return null;
        }

        /// <summary>
        /// パーティクル用マテリアルに、シーンの奥行きに対して正しく遮蔽される描画状態を強制する。
        /// 透明キュー（不透明ジオメトリの後に描画）＋ ZTest LEqual（手前の建物等に遮蔽される）＋ ZWrite Off。
        /// 一部の組み込み/フォールバックシェーダーは ZTest が Always 相当で、煙が手前の建物を透過するため。
        /// シェーダーが該当プロパティを持たない場合 SetInt は無視される（無害）。
        /// </summary>
        public static void ApplyDepthOcclusion(Material mat)
        {
            if (mat == null) return;
            try
            {
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent; // 3000: 不透明の後
                mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual); // 手前の不透明物に遮蔽
                mat.SetInt("_ZWrite", 0); // 半透明なので深度書き込みはしない
            }
            catch (Exception e)
            {
                ModConfig.LogError("RenderAssets.ApplyDepthOcclusion error: " + e);
            }
        }

        /// <summary>初回のみ、利用可能なシェーダー名と主要候補の Shader.Find 可否をログ出力する。</summary>
        public static void DumpAvailableShadersOnce()
        {
            if (_dumped) return;
            _dumped = true;
            try
            {
                Shader[] all = Resources.FindObjectsOfTypeAll<Shader>();
                var sb = new StringBuilder();
                sb.Append("RenderAssets: loaded shader count=").Append(all.Length).Append("; relevant names: ");
                int n = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == null) continue;
                    string nm = all[i].name;
                    if (string.IsNullOrEmpty(nm)) continue;
                    string l = nm.ToLowerInvariant();
                    if (l.Contains("particle") || l.Contains("additive") || l.Contains("unlit") ||
                        l.Contains("transparent") || l.Contains("sprite") || l.Contains("standard") ||
                        l.Contains("glow") || l.Contains("blend"))
                    {
                        sb.Append('[').Append(nm).Append(']');
                        n++;
                    }
                }
                sb.Append(" (").Append(n).Append(" relevant)");
                ModConfig.Log(sb.ToString());
            }
            catch (Exception e)
            {
                ModConfig.LogError("RenderAssets.DumpAvailableShadersOnce error: " + e);
            }
        }
    }
}
