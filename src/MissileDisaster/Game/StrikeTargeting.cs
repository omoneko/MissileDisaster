using System.Collections.Generic;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// Target selection for a random strike. It walks the building buffer once, collecting
    /// positions by priority tier, then draws a target with weights favouring A over B over C
    /// over everything else - biased, but still spread out. Main thread only.
    /// Classification comes from Core.TargetTierClassifier, by name, with tier C filled in from
    /// the AI type (MonumentAI).
    /// </summary>
    public sealed class StrikeTargeting
    {
        // Draw weights per tier, normalised over the tiers that actually exist:
        // [A, B, C, everything else].
        private static readonly float[] TierWeights = { 50f, 25f, 15f, 10f };

        private readonly List<Vector3> _a = new List<Vector3>();
        private readonly List<Vector3> _b = new List<Vector3>();
        private readonly List<Vector3> _c = new List<Vector3>();
        private readonly List<Vector3> _other = new List<Vector3>();

        public bool HasAny
        {
            get { return _a.Count > 0 || _b.Count > 0 || _c.Count > 0 || _other.Count > 0; }
        }

        /// <summary>Walks the building buffer and collects positions by tier.</summary>
        public void Scan()
        {
            _a.Clear(); _b.Clear(); _c.Clear(); _other.Clear();
            BuildingManager bm = BuildingManager.instance;
            if (bm == null) return;
            Building[] buffer = bm.m_buildings.m_buffer;
            if (buffer == null) return;

            // Expand the player's keywords once, before the scan.
            string[] aKw = TargetTierClassifier.ParseKeywords(ModSettings.PriorityAText);
            string[] bKw = TargetTierClassifier.ParseKeywords(ModSettings.PriorityBText);
            string[] cKw = TargetTierClassifier.ParseKeywords(ModSettings.PriorityCText);

            const Building.Flags dead = Building.Flags.Deleted | Building.Flags.Collapsed
                | Building.Flags.BurnedDown | Building.Flags.Abandoned;
            for (int i = 1; i < buffer.Length; i++)
            {
                Building.Flags f = buffer[i].m_flags;
                if ((f & Building.Flags.Created) == 0) continue;
                if ((f & dead) != 0) continue;
                BuildingInfo info = buffer[i].Info;
                if (info == null) continue;

                int tier = TargetTierClassifier.Classify(info.name, aKw, bKw, cKw);
                if (tier == TargetTierClassifier.TierNone && info.m_buildingAI is MonumentAI)
                {
                    tier = TargetTierClassifier.TierC; // landmarks and monuments come from the AI type
                }

                Vector3 pos = buffer[i].m_position;
                switch (tier)
                {
                    case TargetTierClassifier.TierA: _a.Add(pos); break;
                    case TargetTierClassifier.TierB: _b.Add(pos); break;
                    case TargetTierClassifier.TierC: _c.Add(pos); break;
                    default: _other.Add(pos); break;
                }
            }
        }

        /// <summary>Draws one target using the weights. False if there are no buildings at all.</summary>
        public bool TryPick(out Vector3 target)
        {
            target = Vector3.zero;
            List<Vector3>[] tiers = { _a, _b, _c, _other };

            float total = 0f;
            for (int i = 0; i < tiers.Length; i++) if (tiers[i].Count > 0) total += TierWeights[i];
            if (total <= 0f) return false;

            float r = Random.value * total;
            for (int i = 0; i < tiers.Length; i++)
            {
                if (tiers[i].Count == 0) continue;
                r -= TierWeights[i];
                if (r <= 0f)
                {
                    target = tiers[i][Random.Range(0, tiers[i].Count)];
                    return true;
                }
            }
            // Fallback against floating-point error: take the last non-empty tier.
            for (int i = tiers.Length - 1; i >= 0; i--)
            {
                if (tiers[i].Count > 0)
                {
                    target = tiers[i][Random.Range(0, tiers[i].Count)];
                    return true;
                }
            }
            return false;
        }
    }
}
