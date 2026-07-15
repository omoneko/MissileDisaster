using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// 除染施設による汚染濃度の減衰計算（純粋・UnityEngine 非依存）。
    /// ゲーム内 1 か月あたり monthlyFraction（例 0.05=5%）を相対的に除去する。
    /// </summary>
    public static class ContaminationDecay
    {
        private static readonly long TicksPerMonth = TimeSpan.FromDays(30).Ticks; // ゲーム内1か月=30日相当

        /// <summary>start→end のゲーム内経過を「月」で返す（end<=start は0）。</summary>
        public static double MonthsBetween(long startTicks, long endTicks)
        {
            if (endTicks <= startTicks) return 0.0;
            return (endTicks - startTicks) / (double)TicksPerMonth;
        }

        /// <summary>
        /// 現在濃度を deltaMonths 分だけ相対減衰させた濃度を返す（intensity×(1-monthlyFraction)^deltaMonths）。
        /// deltaMonths&lt;=0 や intensity==0 は据え置き。
        /// </summary>
        public static byte ReducedIntensity(byte intensity, double deltaMonths, double monthlyFraction)
        {
            if (intensity == 0 || deltaMonths <= 0.0 || monthlyFraction <= 0.0) return intensity;
            double factor = Math.Pow(1.0 - monthlyFraction, deltaMonths);
            if (factor < 0.0) factor = 0.0;
            int v = (int)Math.Round(intensity * factor);
            if (v < 0) v = 0;
            if (v > 255) v = 255;
            return (byte)v;
        }
    }
}
