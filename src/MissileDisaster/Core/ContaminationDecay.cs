using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// Works out how far a decontamination facility lowers the contamination. Pure, with no
    /// UnityEngine dependency.
    /// It removes monthlyFraction of what remains per in-game month - 0.05 being 5%.
    /// Keeping the intensity as a float and repeatedly multiplying by the decay factor means
    /// even very short intervals lose nothing to rounding, so the decay stays steady.
    /// </summary>
    public static class ContaminationDecay
    {
        private static readonly long TicksPerMonth = TimeSpan.FromDays(30).Ticks; // an in-game month is 30 days

        /// <summary>The in-game time from start to end, in months. Returns 0 when end is at or before start.</summary>
        public static double MonthsBetween(long startTicks, long endTicks)
        {
            if (endTicks <= startTicks) return 0.0;
            return (endTicks - startTicks) / (double)TicksPerMonth;
        }

        /// <summary>
        /// The decay factor over deltaMonths, (1-monthlyFraction)^deltaMonths, in 0..1.
        /// A deltaMonths or monthlyFraction of zero or less gives 1, meaning no decay.
        /// Multiply the current intensity by this.
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
