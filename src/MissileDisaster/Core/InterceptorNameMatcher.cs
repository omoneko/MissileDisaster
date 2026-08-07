using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// Works out from a building's name whether it is an interceptor site (PAC3, THAAD or
    /// Aegis) or a supporting radar site. Pure, with no UnityEngine dependency.
    /// It follows the same pattern as NuclearMeltdown.Core.NuclearNameMatcher: case-insensitive
    /// substring matching, so Workshop-style names carrying a prefix or suffix - like
    /// "12345.PAC3_Data" - still match.
    /// </summary>
    public static class InterceptorNameMatcher
    {
        // The second keyword is Japanese for "Aegis". Kept as matching data, not prose:
        // Workshop authors name assets in their own language.
        public static readonly string[] AegisKeywords = { "Aegis", "\u30a4\u30fc\u30b8\u30b9" };
        public static readonly string[] ThaadKeywords = { "THAAD" };
        public static readonly string[] Pac3Keywords = { "PAC3" };
        // The second keyword is Japanese for "radar", kept for the same reason.
        public static readonly string[] RadarKeywords = { "Radar", "\u30ec\u30fc\u30c0\u30fc" };

        /// <summary>The interception layer for an interceptor site's name, tested as Aegis, then THAAD, then PAC3.</summary>
        public static bool TryMatchTier(string buildingName, out InterceptorKind kind)
        {
            kind = InterceptorKind.Pac;
            if (string.IsNullOrEmpty(buildingName)) return false;

            if (Contains(buildingName, AegisKeywords)) { kind = InterceptorKind.Arrow; return true; }
            if (Contains(buildingName, ThaadKeywords)) { kind = InterceptorKind.Sam; return true; }
            if (Contains(buildingName, Pac3Keywords)) { kind = InterceptorKind.Pac; return true; }
            return false;
        }

        /// <summary>Whether the name identifies a radar site, which is a supporting facility.</summary>
        public static bool IsRadar(string buildingName)
        {
            return Contains(buildingName, RadarKeywords);
        }

        private static bool Contains(string name, string[] keywords)
        {
            if (string.IsNullOrEmpty(name) || keywords == null) return false;
            for (int i = 0; i < keywords.Length; i++)
            {
                string kw = keywords[i];
                if (!string.IsNullOrEmpty(kw) && name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
