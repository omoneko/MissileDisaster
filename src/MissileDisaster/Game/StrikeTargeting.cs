using System.Collections.Generic;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// ランダム攻撃の優先照準。建物バッファを1回走査して優先度層ごとに位置を集め、
    /// 重み付き(A>B>C>その他)で目標を抽選する。偏りは付けるが分散も残す。メインスレッド専用。
    /// 分類は Core.TargetTierClassifier（名前ベース）＋ AI 型(MonumentAI)で TierC を補完。
    /// </summary>
    public sealed class StrikeTargeting
    {
        // 層の抽選重み（存在する層のみで正規化）。[A, B, C, その他]。
        private static readonly float[] TierWeights = { 50f, 25f, 15f, 10f };

        private readonly List<Vector3> _a = new List<Vector3>();
        private readonly List<Vector3> _b = new List<Vector3>();
        private readonly List<Vector3> _c = new List<Vector3>();
        private readonly List<Vector3> _other = new List<Vector3>();

        public bool HasAny
        {
            get { return _a.Count > 0 || _b.Count > 0 || _c.Count > 0 || _other.Count > 0; }
        }

        /// <summary>建物バッファを走査して層別に位置を集める。</summary>
        public void Scan()
        {
            _a.Clear(); _b.Clear(); _c.Clear(); _other.Clear();
            BuildingManager bm = BuildingManager.instance;
            if (bm == null) return;
            Building[] buffer = bm.m_buildings.m_buffer;
            if (buffer == null) return;

            const Building.Flags dead = Building.Flags.Deleted | Building.Flags.Collapsed
                | Building.Flags.BurnedDown | Building.Flags.Abandoned;
            for (int i = 1; i < buffer.Length; i++)
            {
                Building.Flags f = buffer[i].m_flags;
                if ((f & Building.Flags.Created) == 0) continue;
                if ((f & dead) != 0) continue;
                BuildingInfo info = buffer[i].Info;
                if (info == null) continue;

                int tier = TargetTierClassifier.ClassifyByName(info.name);
                if (tier == TargetTierClassifier.TierNone && info.m_buildingAI is MonumentAI)
                {
                    tier = TargetTierClassifier.TierC; // ランドマーク/モニュメントは AI 型で補完
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

        /// <summary>重み付きで目標を1つ抽選する。建物が1つも無ければ false。</summary>
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
            // 数値誤差フォールバック：非空の最後の層から。
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
