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
            // 空中爆発は WithBurst で craterRadius=0 にされる。これで地上/空中を判別する。
            bool groundburst = craterRadius > 0f;

            // クレーターは地上爆発のみ（バニラ MakeCrater。戦略核でも地形を過剰破壊しないよう上限で丸める）。
            if (groundburst)
            {
                float cRadius = craterRadius > ModConfig.CraterRadiusMax ? ModConfig.CraterRadiusMax : craterRadius;
                float cDepth = craterDepth > ModConfig.CraterDepthMax ? ModConfig.CraterDepthMax : craterDepth;
                DisasterHelpers.MakeCrater(new Vector2(pos.x, pos.z), cRadius, cDepth, raiseEdges);
            }

            // 範囲破壊＋延焼。DestroyStuff の末尾2引数 burnMin/burnMax が延焼帯（この帯の建物は着火）。
            // preRadius/totalRadius は処理外周なので destMax と burnMax の大きい方に合わせる
            // （小さいと外側が走査されない既知の罠を回避）。
            int seed = (int)SimulationManager.instance.m_randomizer.Int32(1000000u);
            // 大威力核の実半径はマップを超えるため、DestroyStuff の極端な走査を避ける安全上限で丸める。
            float destMax = destructionRadius > ModConfig.MaxEffectRadius ? ModConfig.MaxEffectRadius : destructionRadius;
            float burnMax = burnRadius > ModConfig.MaxEffectRadius ? ModConfig.MaxEffectRadius : burnRadius;
            float outer = destMax > burnMax ? destMax : burnMax;
            // removeRadius 内は「土台ごと撤去」＝道路・橋・基礎まで破壊。
            //  - 地上爆発: 破壊半径いっぱいを撤去（クレーター化。道路・橋・基礎を破壊）。
            //  - 空中爆発: removeRadius=0。建物は倒壊のみで、基礎・道路・水道管・地下鉄は残す。
            float removeRadius = groundburst ? destMax : 0f;
            float destMin = destMax * 0.5f;
            float burnMin = destMax * 0.3f;
            if (burnMin > burnMax * 0.5f) burnMin = burnMax * 0.5f; // 延焼帯が反転しないよう内縁を抑える
            DisasterHelpers.DestroyStuff(seed, null, pos, outer, outer, removeRadius, destMin, destMax, burnMin, burnMax);
        }
    }
}
