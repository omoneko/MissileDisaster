using System;
using System.Collections.Generic;

namespace MissileDisaster.Core
{
    /// <summary>
    /// ランダム攻撃の優先照準：建物名から優先度層を判定する純粋ロジック（UnityEngine 非依存）。
    /// A(0)＞B(1)＞C(2)＞None(-1)。各層のキーワード群はプレイヤー設定（カンマ区切り）から与えられ、
    /// 建物の内部名(info.name、非ローカライズ)に部分一致（大文字小文字無視）で判定する。
    /// 例: A に "Oil" を足すと石油系の建物が最優先で狙われる。
    /// ランドマーク/モニュメントは名前だけでは判別しづらいため、Game 層で AI 型(MonumentAI)により
    /// TierC を補完する（本分類は名前ベースのみを担当）。
    /// </summary>
    public static class TargetTierClassifier
    {
        public const int TierA = 0;
        public const int TierB = 1;
        public const int TierC = 2;
        public const int TierNone = -1;

        /// <summary>カンマ区切り文字列をキーワード配列へ（前後空白除去・空要素破棄）。</summary>
        public static string[] ParseKeywords(string csv)
        {
            if (string.IsNullOrEmpty(csv)) return new string[0];
            string[] raw = csv.Split(',');
            var list = new List<string>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                string k = raw[i].Trim();
                if (k.Length > 0) list.Add(k);
            }
            return list.ToArray();
        }

        /// <summary>建物名から優先度層を返す（該当なしは TierNone）。判定順 A→B→C。</summary>
        public static int Classify(string name, string[] aKeywords, string[] bKeywords, string[] cKeywords)
        {
            if (string.IsNullOrEmpty(name)) return TierNone;
            if (ContainsAny(name, aKeywords)) return TierA;
            if (ContainsAny(name, bKeywords)) return TierB;
            if (ContainsAny(name, cKeywords)) return TierC;
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
