using MissileDisaster.Core;

namespace MissileDisaster.Game.Contamination
{
    /// <summary>
    /// NaturalResourceManager の土壌汚染セルへの書き込みラッパ。汚染はゲームのセーブに含まれ、
    /// 汚染オーバーレイに可視化される。書き込みは着弾処理と同じ sim スレッドから行うこと。
    /// NuclearMeltdown.Game.PollutionField から必要分を移植。
    /// </summary>
    public static class PollutionField
    {
        /// <summary>セルの汚染を dose 以上へ引き上げる（既存がより高ければ据え置き）。</summary>
        public static void ApplyDose(CellDose dose)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (dose.Index < 0 || dose.Index >= arr.Length) return;
            if (arr[dose.Index].m_pollution < dose.Intensity)
            {
                arr[dose.Index].m_pollution = dose.Intensity;
            }
        }

        /// <summary>指定セル範囲の汚染テクスチャを更新する。</summary>
        public static void Refresh(int minX, int minZ, int maxX, int maxZ)
        {
            NaturalResourceManager.instance.AreaModifiedB(minX, minZ, maxX, maxZ);
        }
    }
}
