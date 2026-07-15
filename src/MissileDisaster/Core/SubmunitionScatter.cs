using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// 子弾(サブミュニション)の散布点を決定論的に配置する純粋ロジック。UnityEngine 非依存・乱数不使用。
    /// 乱数を使わないため着弾は再現可能（医療データ同様、再現性を優先）。
    /// 向日葵配置(phyllotaxis): k 番目を角度 k×黄金角、半径 SpreadRadius×sqrt((k+0.5)/count) に置くことで、
    /// 点が中心に偏らず散布半径内へ均等に広がる。
    /// </summary>
    public static class SubmunitionScatter
    {
        // 黄金角 = π(3 - √5) ラジアン（約137.5°）。連続する点が最も重なりにくい配置になる。
        private const double GoldenAngle = 2.399963229728653;

        /// <summary>
        /// count 個の散布オフセット(X,Z)を返す。count&lt;=1 は原点1点。SpreadRadius=0 は全点原点。
        /// すべての点は原点からの距離が spreadRadius 以内。
        /// </summary>
        public static Offset2[] Offsets(int count, float spreadRadius)
        {
            if (count <= 1)
            {
                return new[] { new Offset2 { X = 0f, Z = 0f } };
            }

            var result = new Offset2[count];
            for (int k = 0; k < count; k++)
            {
                double angle = k * GoldenAngle;
                double radius = spreadRadius * Math.Sqrt((k + 0.5) / count);
                result[k] = new Offset2
                {
                    X = (float)(radius * Math.Cos(angle)),
                    Z = (float)(radius * Math.Sin(angle)),
                };
            }
            return result;
        }
    }
}
