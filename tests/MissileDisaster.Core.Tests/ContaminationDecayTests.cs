using MissileDisaster.Core;
using Xunit;

public class ContaminationDecayTests
{
    [Fact]
    public void No_time_or_zero_fraction_is_no_decay()
    {
        Assert.Equal(1.0, ContaminationDecay.DecayFactor(0.0, 0.05), 6);
        Assert.Equal(1.0, ContaminationDecay.DecayFactor(3.0, 0.0), 6);
    }

    [Fact]
    public void One_month_factor_is_ninety_five_percent()
    {
        Assert.Equal(0.95, ContaminationDecay.DecayFactor(1.0, 0.05), 6);
    }

    [Fact]
    public void Factor_decreases_monotonically_over_more_months()
    {
        double one = ContaminationDecay.DecayFactor(1.0, 0.05);
        double six = ContaminationDecay.DecayFactor(6.0, 0.05);
        double twoYears = ContaminationDecay.DecayFactor(24.0, 0.05);
        Assert.True(six < one);
        Assert.True(twoYears < six);
        Assert.True(twoYears > 0.0);
    }

    [Fact]
    public void Tiny_intervals_accumulate_via_float_intensity_no_stall()
    {
        // Multiplying a float intensity by the factor decays steadily even over very short
        // intervals; rounding never stalls it.
        float intensity = 255f;
        for (int i = 0; i < 100; i++) // 0.02 months a hundred times, i.e. two months
        {
            intensity *= (float)ContaminationDecay.DecayFactor(0.02, 0.05);
        }
        Assert.True(intensity < 255f, "it decays even over tiny intervals");
        Assert.True(intensity < 235f, "two months removes about 9% or more"); // 255 * 0.95^2 is about 230
    }

    [Theory]
    [InlineData(0L, 0L, 0.0)]
    [InlineData(0L, 25920000000000L, 1.0)] // 30 days is one month, i.e. TimeSpan.FromDays(30).Ticks
    public void MonthsBetween_measures_game_months(long start, long end, double expected)
    {
        Assert.Equal(expected, ContaminationDecay.MonthsBetween(start, end), 3);
    }
}
