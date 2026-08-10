using System;
using System.Text;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// Utility for finding shaders that actually exist in the CS Unity runtime.
    /// CS usually strips built-in shaders it does not reference itself - "Particles/Additive",
    /// for one - so Shader.Find returns null for them, the object ends up with no material and
    /// renders in the magenta error colour. This walks the candidates until one resolves.
    /// Ported unchanged from Alien Invasion. It all touches GameObjects and Shaders, so it is
    /// main thread only.
    /// </summary>
    public static class RenderAssets
    {
        private static bool _dumped;

        /// <summary>Shader.Find over the candidate names in order, returning the first that exists, or null if none do.</summary>
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
                catch (Exception) { /* try the next candidate */ }
            }
            return null;
        }

        /// <summary>
        /// The first loaded shader whose name contains any of substrsLower and none of
        /// excludeLower. The exclusions matter: a bare substring search for "alphablend" can
        /// land on a loading-screen shader that ignores vertex colour and depth alike, which is
        /// far worse than finding nothing.
        /// The candidates are tried in order, so the most specific substring should come first.
        /// </summary>
        public static Shader FindLoadedContaining(string[] excludeLower, params string[] substrsLower)
        {
            try
            {
                Shader[] all = Resources.FindObjectsOfTypeAll<Shader>();
                for (int j = 0; j < substrsLower.Length; j++)
                {
                    string want = substrsLower[j];
                    if (string.IsNullOrEmpty(want)) continue;
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i] == null || string.IsNullOrEmpty(all[i].name)) continue;
                        string lower = all[i].name.ToLowerInvariant();
                        if (!lower.Contains(want)) continue;
                        if (Excluded(lower, excludeLower)) continue;
                        return all[i];
                    }
                }
            }
            catch (Exception e)
            {
                ModConfig.LogError("RenderAssets.FindLoadedContaining error: " + e);
            }
            return null;
        }

        private static bool Excluded(string lower, string[] excludeLower)
        {
            if (excludeLower == null) return false;
            for (int k = 0; k < excludeLower.Length; k++)
            {
                if (!string.IsNullOrEmpty(excludeLower[k]) && lower.Contains(excludeLower[k])) return true;
            }
            return false;
        }

        /// <summary>The first loaded shader whose name contains any of substrsLower, which are lowercase.</summary>
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
        /// Forces a particle material into a render state that respects the depth of the scene.
        /// That means the transparent queue, so it draws after the opaque geometry; ZTest
        /// LEqual, so buildings in front of it occlude it; and ZWrite off.
        /// Some built-in and fallback shaders behave as though ZTest were Always, which is what
        /// made smoke show through buildings standing in front of it.
        /// SetInt is harmlessly ignored on a shader without these properties.
        /// </summary>
        public static void ApplyDepthOcclusion(Material mat)
        {
            if (mat == null) return;
            try
            {
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent; // 3000, i.e. after the opaque geometry
                mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual); // occluded by opaque objects in front
                mat.SetInt("_ZWrite", 0); // translucent, so it does not write depth
            }
            catch (Exception e)
            {
                ModConfig.LogError("RenderAssets.ApplyDepthOcclusion error: " + e);
            }
        }

        /// <summary>Logs the available shader names, and whether Shader.Find resolved each main candidate. Runs once.</summary>
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
