using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// ランダム攻撃モード：一定間隔でランダムな地点へミサイルを飛来させる（バニラ災害のようなランダム発生）。
    /// 可能なら既存の建物を狙って街に着弾させ、無ければ地図中央付近のランダム座標へ。すべてメインスレッド。
    /// </summary>
    public static class RandomStrike
    {
        public static void Fire()
        {
            try
            {
                Vector3 target;
                if (!TryRandomBuilding(out target))
                {
                    float range = 4500f;
                    float x = Random.Range(-range, range);
                    float z = Random.Range(-range, range);
                    target = new Vector3(x, 0f, z);
                    target.y = TerrainManager.instance.SampleRawHeightSmoothWithWater(target, false, 0f);
                }

                WarheadType type = PickWarhead();
                float yield = type == WarheadType.Nuclear
                    ? NuclearYields.Multiplier(NuclearYields.StandardKilotons)
                    : ConventionalYields.Multiplier(ConventionalYields.ReferenceKilograms);

                MissileManager.Launch(target, type, yield, BurstType.Groundburst);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("RandomStrike.Fire error: " + e);
            }
        }

        /// <summary>建物バッファをランダムに数回サンプルし、稼働中建物の位置を返す。無ければ false。</summary>
        private static bool TryRandomBuilding(out Vector3 pos)
        {
            pos = Vector3.zero;
            BuildingManager bm = BuildingManager.instance;
            if (bm == null) return false;
            Building[] buffer = bm.m_buildings.m_buffer;
            if (buffer == null || buffer.Length <= 1) return false;

            const Building.Flags dead = Building.Flags.Deleted | Building.Flags.Collapsed | Building.Flags.BurnedDown;
            for (int tries = 0; tries < 60; tries++)
            {
                int i = Random.Range(1, buffer.Length);
                Building.Flags f = buffer[i].m_flags;
                if ((f & Building.Flags.Created) == 0) continue;
                if ((f & dead) != 0) continue;
                pos = buffer[i].m_position;
                return true;
            }
            return false;
        }

        private static WarheadType PickWarhead()
        {
            int w = ModSettings.RandomWarhead != null ? ModSettings.RandomWarhead.value : 0;
            if (w >= 1 && w <= 5) return (WarheadType)(w - 1); // 1..5 → Conventional..Nuclear
            return (WarheadType)Random.Range(0, 5);            // 0 = ランダム
        }
    }
}
