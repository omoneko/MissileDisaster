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
                ApplyBlast(target, spec.CraterRadius, spec.CraterDepth, spec.DestructionRadius, spec.BurnRadius, spec.RaiseCraterEdges);
            }
            else
            {
                // 子弾散布（Cluster/WhitePhosphorus）。散布点ごとに小被害を与える（決定論配置）。
                Offset2[] offsets = SubmunitionScatter.Offsets(spec.SubmunitionCount, spec.SpreadRadius);
                for (int i = 0; i < offsets.Length; i++)
                {
                    Vector3 p = new Vector3(target.x + offsets[i].X, target.y, target.z + offsets[i].Z);
                    ApplyBlast(p, spec.CraterRadius, spec.CraterDepth, spec.DestructionRadius, spec.BurnRadius, spec.RaiseCraterEdges);
                }
            }

            if (spec.Contaminates)
            {
                // 放射能汚染を土壌汚染グリッドへ書き込む（sim スレッド）。
                Contamination.ContaminationManager.Apply(target.x, target.z, spec.ContaminationRadius);
            }

            ModConfig.Log("Impact resolved: " + spec.Type + " x" + spec.SubmunitionCount + " at " + target);
        }

        /// <summary>1 発ぶんのクレーター＋範囲破壊＋延焼を適用する（sim スレッド）。</summary>
        private static void ApplyBlast(Vector3 pos, float craterRadius, float craterDepth,
            float destructionRadius, float burnRadius, bool raiseEdges)
        {
            // クレーター（バニラ SinkholeAI と同じ MakeCrater 呼び出し）。
            DisasterHelpers.MakeCrater(new Vector2(pos.x, pos.z), craterRadius, craterDepth, raiseEdges);

            // 範囲破壊＋延焼。DestroyStuff の末尾2引数 burnMin/burnMax が延焼帯（この帯の建物は着火）。
            // preRadius/totalRadius は処理外周なので destMax と burnMax の大きい方に合わせる
            // （小さいと外側が走査されない既知の罠を回避）。
            int seed = (int)SimulationManager.instance.m_randomizer.Int32(1000000u);
            float destMax = destructionRadius;
            float burnMax = burnRadius;
            float outer = destMax > burnMax ? destMax : burnMax;
            float destMin = destMax * 0.5f;
            float burnMin = destMax * 0.3f;
            if (burnMin > burnMax * 0.5f) burnMin = burnMax * 0.5f; // 延焼帯が反転しないよう内縁を抑える
            DisasterHelpers.DestroyStuff(seed, null, pos, outer, outer, 0f, destMin, destMax, burnMin, burnMax);
        }
    }
}
