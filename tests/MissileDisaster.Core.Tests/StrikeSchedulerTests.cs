using MissileDisaster.Core;
using Xunit;

public class StrikeSchedulerTests
{
    // 標準的な probability（RefProbability）。ProbabilityFactor==1 になる。
    private const float StdProb = (float)StrikeScheduler.RefProbability;

    [Fact]
    public void First_call_initializes_and_does_not_fire()
    {
        var s = new StrikeScheduler();
        bool fired = s.Advance(999.0, 0, StdProb, 1.0, 0.5);
        Assert.False(fired);
        Assert.True(s.CountdownDays > 0.0); // 間隔が設定されている
    }

    [Fact]
    public void Other_disaster_resets_countdown_and_does_not_fire()
    {
        var s = new StrikeScheduler();
        s.Advance(0.0, 0, StdProb, 1.0, 0.0);      // 初期化（rng=0 → 最短側）
        double before = s.CountdownDays;
        // カウントダウンをある程度消化
        s.Advance(before - 1.0, 0, StdProb, 1.0, 0.0);
        Assert.True(s.CountdownDays <= 1.0 + 1e-9);
        // ここで他災害が発生（count 0→1）→ 満タンにリセットされ発火しない
        bool fired = s.Advance(0.0, 1, StdProb, 1.0, 1.0);
        Assert.False(fired);
        Assert.True(s.CountdownDays > 1.0); // 大きく戻った
    }

    [Fact]
    public void Disaster_count_decrease_does_not_reset()
    {
        var s = new StrikeScheduler();
        s.Advance(0.0, 3, StdProb, 1.0, 1.0); // 初期化（count=3）
        double c = s.CountdownDays;
        s.Advance(5.0, 3, StdProb, 1.0, 1.0);   // 5日消化
        double afterConsume = s.CountdownDays;
        Assert.True(afterConsume < c);
        // 災害が消滅（3→1）：リセットせず、消化を継続できる
        bool fired = s.Advance(1.0, 1, StdProb, 1.0, 1.0);
        Assert.False(fired);
        Assert.True(s.CountdownDays < afterConsume);
    }

    [Fact]
    public void Fires_when_countdown_elapses_and_reschedules()
    {
        var s = new StrikeScheduler();
        s.Advance(0.0, 0, StdProb, 1.0, 0.5); // 初期化
        double interval = s.CountdownDays;
        bool fired = s.Advance(interval + 0.001, 0, StdProb, 1.0, 0.5); // 一気に消化
        Assert.True(fired);
        Assert.True(s.CountdownDays > 0.0); // 次の間隔が再設定される
    }

    [Fact]
    public void Higher_frequency_multiplier_shortens_mean_interval()
    {
        // 同じ rng で乗数3倍 → 平均間隔は約1/3。
        double one = StrikeScheduler.NextInterval(StdProb, 1.0, 0.5);
        double triple = StrikeScheduler.NextInterval(StdProb, 3.0, 0.5);
        Assert.True(triple < one);
        Assert.Equal(one / 3.0, triple, 6);
    }

    [Fact]
    public void Higher_probability_shortens_mean_interval()
    {
        // RefProbability の2倍 → 係数2倍 → 間隔半分（クランプ域外で検証）。
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
        // フォールバック係数 ProbFactorMin での平均に一致
        double expectedMean = StrikeScheduler.BaseIntervalDays / (1.0 * StrikeScheduler.ProbFactorMin);
        Assert.Equal(expectedMean * 1.0, interval, 6); // rng=0.5 → 係数1.0
    }

    [Fact]
    public void Interval_is_clamped_to_max()
    {
        // probability 極小＋低頻度 → 平均が巨大 → Max にクランプ。
        double interval = StrikeScheduler.NextInterval(0.0005f, 0.25, 1.0);
        Assert.Equal(StrikeScheduler.MaxIntervalDays, interval, 6);
    }

    [Fact]
    public void Interval_is_clamped_to_min()
    {
        // probability 大（係数上限）＋高頻度 → 平均が小さく rng=0 で下限割れ → Min にクランプ。
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
        // Reset 後の初回は再初期化扱い（発火せず count を取り込む）
        bool fired = s.Advance(999.0, 5, StdProb, 1.0, 0.5);
        Assert.False(fired);
    }
}
