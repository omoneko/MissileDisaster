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
            // クレーター（バニラ SinkholeAI と同じ MakeCrater 呼び出し。1 回だけ）。
            DisasterHelpers.MakeCrater(new Vector2(target.x, target.z), spec.CraterRadius, spec.CraterDepth, false);

            // 範囲破壊。preRadius=totalRadius にする（0 だと何も壊れない既知の罠を回避）。
            int seed = (int)SimulationManager.instance.m_randomizer.Int32(1000000u);
            float r = spec.DestructionRadius;
            DisasterHelpers.DestroyStuff(seed, null, target, r, r, 0f, r * 0.5f, r, r * 0.3f, r * 0.6f);

            ModConfig.Log("Impact resolved (crater+destruction) at " + target);
        }
    }
}
