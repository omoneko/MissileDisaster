using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// 核出力(kt)から効果半径のスケール係数を求める。UnityEngine 非依存・純粋。
    /// 爆風半径 ∝ 威力^(1/3) に倣い、Standard(150kt) を基準に Multiplier = cbrt(kt/150)。
    /// カタログ選択・数値入力の双方がこの1関数を使う。
    /// </summary>
    public static class NuclearYields
    {
        public const int StandardKilotons = 150;

        /// <summary>kt からスケール係数（爆風半径の相対比）を返す。0以下は0。</summary>
        public static float Multiplier(int kilotons)
        {
            if (kilotons <= 0) return 0f;
            return (float)Math.Pow(kilotons / (double)StandardKilotons, 1.0 / 3.0);
        }
    }
}
