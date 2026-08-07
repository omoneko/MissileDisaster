using System;
using System.Collections.Generic;

namespace MissileDisaster.Core
{
    /// <summary>
    /// Target priority for a random strike: works out a building's priority tier from its name.
    /// Pure, with no UnityEngine dependency.
    /// The tiers run A (0), then B (1), then C (2), with None (-1) for anything unmatched. The
    /// keywords for each tier come from the player's settings as a comma-separated list, and are
    /// matched as case-insensitive substrings against the building's internal, unlocalised
    /// info.name. Adding "Oil" to tier A, for instance, makes oil facilities the first choice.
    /// Landmarks and monuments are hard to identify by name alone, so the Game layer fills tier
    /// C in from the AI type (MonumentAI); this class only ever classifies by name.
    /// </summary>
    public static class TargetTierClassifier
    {
        public const int TierA = 0;
        public const int TierB = 1;
        public const int TierC = 2;
        public const int TierNone = -1;

        /// <summary>Splits a comma-separated string into keywords, trimming whitespace and dropping empty entries.</summary>
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

        /// <summary>The priority tier for a building name, or TierNone if nothing matches. Tested A, then B, then C.</summary>
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
