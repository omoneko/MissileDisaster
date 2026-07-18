using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// ランダム攻撃の優先照準：建物名から優先度層を判定する純粋ロジック（UnityEngine 非依存）。
    /// A(0)=原発/Aegis/THAAD/PAC3、B(1)=空港/鉄道駅/港、C(2)=ランドマーク/モニュメント、None(-1)。
    /// 判定対象は info.name（非ローカライズのプレハブ名/Workshop風 "12345.PAC3_Data" にも部分一致）。
    /// ランドマーク/モニュメントは名前だけでは判別しづらいため、Game 層で AI 型(MonumentAI)により
    /// TierC を補完する（本分類は名前ベースのみを担当）。
    /// </summary>
    public static class TargetTierClassifier
    {
        public const int TierA = 0;
        public const int TierB = 1;
        public const int TierC = 2;
        public const int TierNone = -1;

        // A：原子力発電所（迎撃施設 Aegis/THAAD/PAC3 は InterceptorNameMatcher で別途判定）
        public static readonly string[] NuclearKeywords = { "Nuclear" };
        // B
        public static readonly string[] AirportKeywords = { "Airport" };
        public static readonly string[] StationKeywords = { "Train Station", "Railway", "Cargo Train" };
        public static readonly string[] HarborKeywords = { "Harbor", "Harbour" };

        /// <summary>建物名から優先度層を返す（該当なしは TierNone）。判定順: A(迎撃/原発)→B。</summary>
        public static int ClassifyByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return TierNone;

            InterceptorKind kind;
            if (InterceptorNameMatcher.TryMatchTier(name, out kind)) return TierA;
            if (ContainsAny(name, NuclearKeywords)) return TierA;

            if (ContainsAny(name, AirportKeywords)) return TierB;
            if (ContainsAny(name, StationKeywords)) return TierB;
            if (ContainsAny(name, HarborKeywords)) return TierB;

            return TierNone;
        }

        private static bool ContainsAny(string name, string[] keywords)
        {
            if (keywords == null) return false;
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
