using MissileDisaster.Core;
using Xunit;

public class StrikeSchedulerTests
{
    // The typical probability, RefProbability, which makes the probability factor 1.
    private const float StdProb = (float)StrikeScheduler.RefProbability;

    [Fact]
    public void First_call_initializes_and_does_not_fire()
    {
        var s = new StrikeScheduler();
        bool fired = s.Advance(999.0, 0, StdProb, 1.0, 0.5);
        Assert.False(fired);
        Assert.True(s.CountdownDays > 0.0); // an interval has been set
    }

    [Fact]
    public void Other_disaster_resets_countdown_and_does_not_fire()
    {
        var s = new StrikeScheduler();
        s.Advance(0.0, 0, StdProb, 1.0, 0.0);      // initialise; rng=0 gives the shortest interval
        double before = s.CountdownDays;
        // Burn down some of the countdown
        s.Advance(before - 1.0, 0, StdProb, 1.0, 0.0);
        Assert.True(s.CountdownDays <= 1.0 + 1e-9);
        // Another disaster occurs here, taking the count from 0 to 1, which refills the
        // countdown so nothing fires
        bool fired = s.Advance(0.0, 1, StdProb, 1.0, 1.0);
        Assert.False(fired);
        Assert.True(s.CountdownDays > 1.0); // it went well back up
    }

    [Fact]
    public void Disaster_count_decrease_does_not_reset()
    {
        var s = new StrikeScheduler();
        s.Advance(0.0, 3, StdProb, 1.0, 1.0); // initialise with a count of 3
        double c = s.CountdownDays;
        s.Advance(5.0, 3, StdProb, 1.0, 1.0);   // burn down five days
        double afterConsume = s.CountdownDays;
        Assert.True(afterConsume < c);
        // Disasters end, taking the count from 3 to 1: no reset, and the countdown keeps going
        bool fired = s.Advance(1.0, 1, StdProb, 1.0, 1.0);
        Assert.False(fired);
        Assert.True(s.CountdownDays < afterConsume);
    }

    [Fact]
    public void Fires_when_countdown_elapses_and_reschedules()
    {
        var s = new StrikeScheduler();
        s.Advance(0.0, 0, StdProb, 1.0, 0.5); // initialise
        double interval = s.CountdownDays;
        bool fired = s.Advance(interval + 0.001, 0, StdProb, 1.0, 0.5); // burn it all down at once
        Assert.True(fired);
        Assert.True(s.CountdownDays > 0.0); // the next interval has been set
    }

    [Fact]
    public void Higher_frequency_multiplier_shortens_mean_interval()
    {
        // Tripling the multiplier at the same rng makes the mean interval about a third.
        double one = StrikeScheduler.NextInterval(StdProb, 1.0, 0.5);
        double triple = StrikeScheduler.NextInterval(StdProb, 3.0, 0.5);
        Assert.True(triple < one);
        Assert.Equal(one / 3.0, triple, 6);
    }

    [Fact]
    public void Higher_probability_shortens_mean_interval()
    {
        // Twice RefProbability doubles the factor and halves the interval; checked away from
        // the clamps.
        double atRef = StrikeScheduler.NextInterval(StdProb, 1.0, 0.5);
        double doubleProb = StrikeScheduler.NextInterval(StdProb * 2f, 1.0, 0.5);
        Assert.True(doubleProb < atRef);
        Assert.Equal(atRef / 2.0, doubleProb, 6);
    }

    [Fact]
    public void Zero_probability_falls_back_to_finite_interval()
    {
        double interval = StrikeScheduler.NextInterval(0f, 1.0, 0.5);
        Assert.True(interval >= StrikeScheduler.MinIntervalDays);
        Assert.True(interval <= StrikeScheduler.MaxIntervalDays);
        // Matches the mean at the fallback factor, ProbFactorMin
        double expectedMean = StrikeScheduler.BaseIntervalDays / (1.0 * StrikeScheduler.ProbFactorMin);
        Assert.Equal(expectedMean * 1.0, interval, 6); // rng=0.5 gives a factor of 1.0
    }

    [Fact]
    public void Interval_is_clamped_to_max()
    {
        // A tiny probability and a low frequency make the mean enormous, so it clamps to the
        // maximum.
        double interval = StrikeScheduler.NextInterval(0.0005f, 0.25, 1.0);
        Assert.Equal(StrikeScheduler.MaxIntervalDays, interval, 6);
    }

    [Fact]
    public void Interval_is_clamped_to_min()
    {
        // A large probability at the factor ceiling plus a high frequency makes the mean small,
        // and rng=0 takes it under the floor, so it clamps to the minimum.
        double interval = StrikeScheduler.NextInterval(1.0f, 3.0, 0.0);
        Assert.Equal(StrikeScheduler.MinIntervalDays, interval, 6);
    }

    [Fact]
    public void Rng_spreads_interval_between_half_and_one_and_half_mean()
    {
        double low = StrikeScheduler.NextInterval(StdProb, 1.0, 0.0);   // 0.5×mean
        double high = StrikeScheduler.NextInterval(StdProb, 1.0, 1.0);  // 1.5×mean
        Assert.Equal(low * 3.0, high, 6); // (1.5)/(0.5)=3
    }

    [Fact]
    public void ProbabilityFactor_is_clamped_both_ends()
    {
        Assert.Equal(StrikeScheduler.ProbFactorMin, StrikeScheduler.ProbabilityFactor(0f), 6);
        Assert.Equal(StrikeScheduler.ProbFactorMax, StrikeScheduler.ProbabilityFactor(999f), 6);
        Assert.Equal(1.0, StrikeScheduler.ProbabilityFactor(StdProb), 6);
    }

    [Fact]
    public void Reset_returns_to_uninitialized()
    {
        var s = new StrikeScheduler();
        s.Advance(0.0, 5, StdProb, 1.0, 0.5);
        s.Reset();
        // The first call after Reset counts as reinitialising: it takes the count without firing
        bool fired = s.Advance(999.0, 5, StdProb, 1.0, 0.5);
        Assert.False(fired);
    }
}
