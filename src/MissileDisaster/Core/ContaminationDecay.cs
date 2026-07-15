using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// 除染施設による汚染濃度の減衰計算（純粋・UnityEngine 非依存）。
    /// ゲーム内 1 か月あたり monthlyFraction（例 0.05=5%）を相対的に除去する。
    /// 濃度は float で保持して減衰係数を掛け続けることで、微小な間隔でも端数が失われず着実に減衰する。
    /// </summary>
    public static class ContaminationDecay
    {
        private static readonly long TicksPerMonth = TimeSpan.FromDays(30).Ticks; // ゲーム内1か月=30日相当

        /// <summary>start→end のゲーム内経過を「月」で返す（end&lt;=start は0）。</summary>
        public static double MonthsBetween(long startTicks, long endTicks)
        {
            if (endTicks <= startTicks) return 0.0;
            return (endTicks - startTicks) / (double)TicksPerMonth;
        }

        /// <summary>
        /// deltaMonths 分の相対減衰係数 (1-monthlyFraction)^deltaMonths を返す（0..1）。
        /// deltaMonths&lt;=0 や monthlyFraction&lt;=0 は 1（減衰なし）。現在濃度に掛けて使う。
        /// </summary>
        public static double DecayFactor(double deltaMonths, double monthlyFraction)
        {
            if (deltaMonths <= 0.0 || monthlyFraction <= 0.0) return 1.0;
            double factor = Math.Pow(1.0 - monthlyFraction, deltaMonths);
            if (factor < 0.0) return 0.0;
            if (factor > 1.0) return 1.0;
            return factor;
        }
    }
}
