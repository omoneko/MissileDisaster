using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// 非核弾頭の装薬量(kg TNT)から効果半径のスケール係数を求める。UnityEngine 非依存・純粋。
    /// 爆風半径 ∝ 装薬量^(1/3)。基準1000kg(=各非核弾頭の既定スペック)に対し Multiplier = cbrt(kg/1000)。
    /// 通常弾/クラスター/白リン/サーモバリックの各既定値を、選んだ出力で相対スケールする。
    /// </summary>
    public static class ConventionalYields
    {
        public const int ReferenceKilograms = 1000; // 基準 1t TNT（既定スペック相当）

        /// <summary>kg TNT からスケール係数（爆風半径の相対比）を返す。0以下は0。</summary>
        public static float Multiplier(int kilograms)
        {
            if (kilograms <= 0) return 0f;
            return (float)Math.Pow(kilograms / (double)ReferenceKilograms, 1.0 / 3.0);
        }
    }
}
