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
        /// <summary>セルの汚染を dose 以上へ引き上げる（既存がより高ければ据え置き）。実際に書き換えたら true。</summary>
        public static bool ApplyDose(CellDose dose)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (dose.Index < 0 || dose.Index >= arr.Length) return false;
            if (arr[dose.Index].m_pollution < dose.Intensity)
            {
                arr[dose.Index].m_pollution = dose.Intensity;
                return true;
            }
            return false;
        }

        /// <summary>セルの汚染を dose.Intensity に上書き設定する（除染で濃度を下げる用）。実際に書き換えたら true。</summary>
        public static bool SetDose(CellDose dose)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (dose.Index < 0 || dose.Index >= arr.Length) return false;
            if (arr[dose.Index].m_pollution != dose.Intensity)
            {
                arr[dose.Index].m_pollution = dose.Intensity;
                return true;
            }
            return false;
        }

        /// <summary>セルの汚染を0にする（ゾーン期限切れのクリア用）。実際に書き換えたら true。</summary>
        public static bool ClearCell(int index)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (index < 0 || index >= arr.Length) return false;
            if (arr[index].m_pollution != 0)
            {
                arr[index].m_pollution = 0;
                return true;
            }
            return false;
        }

        /// <summary>指定セル範囲の汚染テクスチャを更新する。</summary>
        public static void Refresh(int minX, int minZ, int maxX, int maxZ)
        {
            NaturalResourceManager.instance.AreaModifiedB(minX, minZ, maxX, maxZ);
        }
    }
}
