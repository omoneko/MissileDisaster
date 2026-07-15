using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// 設置された建物の名称から、迎撃施設(PAC3/THAAD/Aegis)か支援施設(レーダーサイト)かを
    /// キーワードで判定する純粋ロジック。UnityEngine 非依存。
    /// NuclearMeltdown.Core.NuclearNameMatcher と同一パターン（大文字小文字無視、Workshop風の
    /// "12345.PAC3_Data" のような接頭辞/接尾辞付き名称にも部分一致で対応）。
    /// </summary>
    public static class InterceptorNameMatcher
    {
        public static readonly string[] AegisKeywords = { "Aegis", "イージス" };
        public static readonly string[] ThaadKeywords = { "THAAD" };
        public static readonly string[] Pac3Keywords = { "PAC3" };
        public static readonly string[] RadarKeywords = { "Radar", "レーダー" };

        /// <summary>建物名が迎撃施設(PAC3/THAAD/Aegis)ならその迎撃層を返す。判定順序: Aegis→THAAD→PAC3。</summary>
        public static bool TryMatchTier(string buildingName, out InterceptorKind kind)
        {
            kind = InterceptorKind.Pac;
            if (string.IsNullOrEmpty(buildingName)) return false;

            if (Contains(buildingName, AegisKeywords)) { kind = InterceptorKind.Arrow; return true; }
            if (Contains(buildingName, ThaadKeywords)) { kind = InterceptorKind.Sam; return true; }
            if (Contains(buildingName, Pac3Keywords)) { kind = InterceptorKind.Pac; return true; }
            return false;
        }

        /// <summary>建物名がレーダーサイト(支援施設)を示すか。</summary>
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
