using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// 着弾ダメージ解決。DisasterHelpers はシミュレーションスレッドから呼ぶ契約のため、
    /// このメソッドは MissileManager.UpdateSimulation（sim スレッド）からのみ呼ぶこと。
    /// </summary>
    public static class ImpactResolver
    {
        public static void Resolve(Vector3 target, WarheadSpec spec)
        {
            if (spec.SubmunitionCount <= 1)
            {
                // 単一着弾（Conventional/Thermobaric/Nuclear）。
                ApplyBlast(target, spec.CraterRadius, spec.CraterDepth, spec.DestructionRadius, spec.RaiseCraterEdges);
            }
            else
            {
                // 子弾散布（Cluster/WhitePhosphorus）。散布点ごとに小被害を与える（決定論配置）。
                Offset2[] offsets = SubmunitionScatter.Offsets(spec.SubmunitionCount, spec.SpreadRadius);
                for (int i = 0; i < offsets.Length; i++)
                {
                    Vector3 p = new Vector3(target.x + offsets[i].X, target.y, target.z + offsets[i].Z);
                    ApplyBlast(p, spec.CraterRadius, spec.CraterDepth, spec.DestructionRadius, spec.RaiseCraterEdges);
                }
            }

            if (spec.Contaminates)
            {
                // 放射能汚染の実装は後続フェーズ。現状はログのみ（着弾は上のクレーター＋破壊で表現）。
                ModConfig.Log("Nuclear warhead contamination flagged (実汚染は後続フェーズ) at " + target);
            }

            ModConfig.Log("Impact resolved: " + spec.Type + " x" + spec.SubmunitionCount + " at " + target);
        }

        /// <summary>1 発ぶんのクレーター＋範囲破壊を適用する（sim スレッド）。</summary>
        private static void ApplyBlast(Vector3 pos, float craterRadius, float craterDepth, float destructionRadius, bool raiseEdges)
        {
            // クレーター（バニラ SinkholeAI と同じ MakeCrater 呼び出し）。
            DisasterHelpers.MakeCrater(new Vector2(pos.x, pos.z), craterRadius, craterDepth, raiseEdges);

            // 範囲破壊。preRadius=totalRadius にする（0 だと何も壊れない既知の罠を回避）。
            int seed = (int)SimulationManager.instance.m_randomizer.Int32(1000000u);
            float r = destructionRadius;
            DisasterHelpers.DestroyStuff(seed, null, pos, r, r, 0f, r * 0.5f, r, r * 0.3f, r * 0.6f);
        }
    }
}
