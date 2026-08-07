namespace MissileDisaster.Core
{
    /// <summary>
    /// Scheduler deciding when a random strike fires. Pure, with no UnityEngine dependency.
    /// Strikes come at an interval proportional to the vanilla natural disaster frequency, and
    /// the countdown restarts every time another natural disaster occurs - that is, whenever
    /// disasterCount rises - so missiles land in the gaps between disasters. It is driven by
    /// in-game time, in days.
    ///
    /// The absolute scale of probability (DisasterManager.m_randomDisastersProbability) is not
    /// documented, so it is normalised against the RefProbability baseline and the resulting
    /// factor is clamped to [ProbFactorMin, ProbFactorMax]. That keeps the interval sane even
    /// if the real values turn out somewhat different from what is assumed here.
    /// BaseIntervalDays is the main knob to turn.
    /// </summary>
    public sealed class StrikeScheduler
    {
        // Tuning constants, calibrated in-game. This is pure logic, so changing the numbers is
        // not what the tests check.
        public const double BaseIntervalDays = 20.0;  // baseline interval in in-game days, at the default disaster frequency and a multiplier of 1
        public const double RefProbability = 0.05;    // the m_randomDisastersProbability assumed to be typical
        public const double ProbFactorMin = 0.25;     // floor on the probability factor, the fallback on maps with disasters off
        public const double ProbFactorMax = 4.0;      // ceiling
        public const double MinIntervalDays = 2.0;
        public const double MaxIntervalDays = 365.0;
        public const double Epsilon = 1e-6;

        private double _countdownDays;
        private int _lastDisasterCount;
        private bool _initialized;

        /// <summary>In-game days remaining until the next strike, exposed for monitoring.</summary>
        public double CountdownDays => _countdownDays;

        /// <summary>Returns the state to uninitialised. Called when random strikes are switched off and on a level change.</summary>
        public void Reset()
        {
            _initialized = false;
            _countdownDays = 0.0;
            _lastDisasterCount = 0;
        }

        /// <summary>
        /// Call once per simulation tick; true means a strike fires this tick.
        /// gameDaysDelta is the in-game days elapsed since the last call, disasterCount is the
        /// current m_disasterCount, probability is the current m_randomDisastersProbability,
        /// freqMultiplier is the player's setting from 0.25 to 3.0, and rng is a value in [0,1).
        /// </summary>
        public bool Advance(double gameDaysDelta, int disasterCount, float probability, double freqMultiplier, double rng)
        {
            if (!_initialized)
            {
                _lastDisasterCount = disasterCount;
                _countdownDays = NextInterval(probability, freqMultiplier, rng);
                _initialized = true;
                return false;
            }

            if (disasterCount > _lastDisasterCount)
            {
                // Another natural disaster occurred, so restart the countdown - missiles land
                // in the gaps between them.
                _lastDisasterCount = disasterCount;
                _countdownDays = NextInterval(probability, freqMultiplier, rng);
                return false;
            }
            if (disasterCount < _lastDisasterCount)
            {
                _lastDisasterCount = disasterCount; // a disaster ended: follow the count, but do not reset
            }

            if (gameDaysDelta > 0.0)
            {
                _countdownDays -= gameDaysDelta;
            }
            if (_countdownDays <= 0.0)
            {
                _countdownDays = NextInterval(probability, freqMultiplier, rng);
                return true;
            }
            return false;
        }

        /// <summary>The next interval in days, from the probability and the frequency multiplier. rng spreads it over 0.5x to 1.5x, clamped.</summary>
        public static double NextInterval(float probability, double freqMultiplier, double rng)
        {
            double m = freqMultiplier > Epsilon ? freqMultiplier : 1.0;
            double pf = ProbabilityFactor(probability);
            double mean = BaseIntervalDays / (m * pf);
            double interval = mean * (0.5 + Clamp01(rng));
            if (interval < MinIntervalDays) return MinIntervalDays;
            if (interval > MaxIntervalDays) return MaxIntervalDays;
            return interval;
        }

        /// <summary>Normalises probability against RefProbability and clamps it to [ProbFactorMin, ProbFactorMax].</summary>
        public static double ProbabilityFactor(float probability)
        {
            double p = probability > 0f ? probability : 0.0;
            double f = p / RefProbability;
            if (f < ProbFactorMin) return ProbFactorMin;
            if (f > ProbFactorMax) return ProbFactorMax;
            return f;
        }

        private static double Clamp01(double v)
        {
            if (v < 0.0) return 0.0;
            if (v > 1.0) return 1.0;
            return v;
        }
    }
}
