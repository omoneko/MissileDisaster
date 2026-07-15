using System.Collections.Generic;
using MissileDisaster.Core;

namespace MissileDisaster.Game.Contamination
{
    /// <summary>
    /// 核着弾時に円形の放射能汚染を土壌汚染グリッドへ書き込む（sim スレッド）。
    /// v1 は減衰/独自セーブ台帳を持たない（土壌汚染はゲームのセーブに含まれ永続、汚染オーバーレイに表示）。
    /// 時間減衰・除染は後続フェーズで検討（NuclearMeltdown の除染ロジック移植候補）。
    /// </summary>
    public static class ContaminationManager
    {
        /// <summary>中心(worldX,worldZ)・半径radiusMetersの放射能汚染を適用する（sim スレッド専用）。</summary>
        public static void Apply(float worldX, float worldZ, float radiusMeters)
        {
            if (radiusMeters <= 0f) return;
            // グリッド範囲を超える半径はマップ全域を覆うため、走査の無駄を省く安全上限で丸める。
            if (radiusMeters > ModConfig.MaxContaminationRadius) radiusMeters = ModConfig.MaxContaminationRadius;

            List<CellDose> doses = PollutionGrid.CellsInRadius(
                worldX, worldZ, radiusMeters, ModConfig.ContaminationMaxIntensity);
            for (int i = 0; i < doses.Count; i++)
            {
                PollutionField.ApplyDose(doses[i]);
            }
            RefreshArea(worldX, worldZ, radiusMeters);

            ModConfig.Log("Contamination applied: r=" + radiusMeters + "m cells=" + doses.Count
                + " at (" + worldX + "," + worldZ + ")");
        }

        /// <summary>汚染円を含むセル範囲のテクスチャを更新する。</summary>
        private static void RefreshArea(float worldX, float worldZ, float radiusMeters)
        {
            int cellRadius = (int)(radiusMeters / PollutionGrid.CellSize) + 1;
            int cx = PollutionGrid.WorldToCell(worldX);
            int cz = PollutionGrid.WorldToCell(worldZ);
            int minX = Clamp(cx - cellRadius), maxX = Clamp(cx + cellRadius);
            int minZ = Clamp(cz - cellRadius), maxZ = Clamp(cz + cellRadius);
            PollutionField.Refresh(minX, minZ, maxX, maxZ);
        }

        private static int Clamp(int v)
        {
            if (v < 0) return 0;
            if (v > PollutionGrid.Resolution - 1) return PollutionGrid.Resolution - 1;
            return v;
        }
    }
}
