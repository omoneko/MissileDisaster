using MissileDisaster.Core;
using Xunit;

public class ContaminationDecayTests
{
    [Fact]
    public void No_time_or_zero_intensity_is_unchanged()
    {
        Assert.Equal(255, ContaminationDecay.ReducedIntensity(255, 0.0, 0.05));
        Assert.Equal(0, ContaminationDecay.ReducedIntensity(0, 3.0, 0.05));
    }

    [Fact]
    public void One_month_removes_five_percent_relatively()
    {
        // 255 × 0.95 = 242.25 → 四捨五入 242
        Assert.Equal(242, ContaminationDecay.ReducedIntensity(255, 1.0, 0.05));
    }

    [Fact]
    public void Decays_monotonically_over_more_months()
    {
        byte oneMonth = ContaminationDecay.ReducedIntensity(255, 1.0, 0.05);
        byte sixMonths = ContaminationDecay.ReducedIntensity(255, 6.0, 0.05);
        byte twoYears = ContaminationDecay.ReducedIntensity(255, 24.0, 0.05);
        Assert.True(sixMonths < oneMonth);
        Assert.True(twoYears < sixMonths);
        Assert.True(twoYears > 0); // まだ残る（相対減衰）
    }

    [Theory]
    [InlineData(0L, 0L, 0.0)]
    [InlineData(0L, 25920000000000L, 1.0)] // 30日 = 1か月（TimeSpan.FromDays(30).Ticks）
    public void MonthsBetween_measures_game_months(long start, long end, double expected)
    {
        Assert.Equal(expected, ContaminationDecay.MonthsBetween(start, end), 3);
    }
}
