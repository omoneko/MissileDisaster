using MissileDisaster.Core;
using Xunit;

public class BlackRainTests
{
    [Fact]
    public void A_small_detonation_brings_no_black_rain()
    {
        // There is not enough column below this to scavenge anything worth seeing.
        Assert.False(BlackRain.Falls(0f));
        Assert.False(BlackRain.Falls(0.1f));
        Assert.Equal(0f, BlackRain.RainSeconds(0.1f), 3);
    }

    [Fact]
    public void It_rains_longer_after_a_bigger_yield()
    {
        Assert.True(BlackRain.RainSeconds(150f) > BlackRain.RainSeconds(15f));
        Assert.True(BlackRain.RainSeconds(1000f) > BlackRain.RainSeconds(150f));
    }

    [Fact]
    public void The_rain_is_always_minutes_rather_than_hours()
    {
        // Long enough to be part of the strike, short enough that the city is not left under a
        // permanent downpour. Checked at both extremes of what can be launched.
        foreach (float kt in new[] { 1f, 15f, 150f, 1000f, 50000f, 1000000f })
        {
            Assert.InRange(BlackRain.RainSeconds(kt),
                BlackRain.RainSecondsMin, BlackRain.RainSecondsMax);
        }
    }

    [Fact]
    public void An_airburst_leaves_no_stain()
    {
        // The stain rides the fallout down, and an airburst leaves none - which is the real
        // behaviour, and the reason airbursts were used over cities.
        var airburst = WarheadSpec.For(WarheadType.Nuclear).WithBurst(BurstType.Airburst);
        Assert.Equal(0f, airburst.ContaminationRadius, 3);
        Assert.Equal(0f, BlackRain.StainRadius(airburst.ContaminationRadius), 3);
    }

    [Fact]
    public void A_groundburst_stains_wider_than_its_fallout()
    {
        // Rain drifts on the wind and lands beyond where the column stood. Checked at 15 kt:
        // the 150 kt baseline's fallout is 5.3 km, past the ceiling below, and a stain 8 km
        // across is already most of the map - so the largest yields are the one case where the
        // mark is narrower than the fallout, by deliberate choice rather than by accident.
        var ground = WarheadSpec.For(WarheadType.Nuclear)
            .WithBurst(BurstType.Groundburst)
            .Scaled(NuclearYields.Multiplier(15));
        float stain = BlackRain.StainRadius(ground.ContaminationRadius);

        Assert.True(stain < BlackRain.StainRadiusMax, "the ceiling must not bite at 15 kt");
        Assert.True(stain > ground.ContaminationRadius,
            "the stain is no wider than the fallout that carried it");
        Assert.InRange(stain, BlackRain.StainRadiusMin, BlackRain.StainRadiusMax);
    }

    [Fact]
    public void The_biggest_yields_are_held_under_the_stain_ceiling()
    {
        // 8 km across is already half the playable map; past that the mark stops reading as a
        // mark. The ceiling is what stops a strategic warhead greying out the whole city.
        var ground = WarheadSpec.For(WarheadType.Nuclear).WithBurst(BurstType.Groundburst);
        Assert.True(ground.ContaminationRadius > BlackRain.StainRadiusMax,
            "the baseline no longer reaches the ceiling - this test proves nothing");
        Assert.Equal(BlackRain.StainRadiusMax,
            BlackRain.StainRadius(ground.ContaminationRadius), 3);
    }

    [Fact]
    public void The_stain_outlasts_the_shower_that_left_it()
    {
        float rain = BlackRain.RainSeconds(150f);
        Assert.True(BlackRain.StainSeconds(rain) > rain);
        Assert.Equal(rain * BlackRain.StainSecondsFactor, BlackRain.StainSeconds(rain), 3);
    }

    [Fact]
    public void No_rain_means_no_stain_time()
    {
        Assert.Equal(0f, BlackRain.StainSeconds(0f), 3);
        Assert.Equal(0f, BlackRain.StainRadius(0f), 3);
        Assert.Equal(0f, BlackRain.StainRadius(-5f), 3);
    }

    [Fact]
    public void Even_an_absurd_yield_is_held_inside_the_bounds()
    {
        Assert.Equal(BlackRain.RainSecondsMax, BlackRain.RainSeconds(1e9f), 3);
        Assert.Equal(BlackRain.StainRadiusMax, BlackRain.StainRadius(1e9f), 3);
    }
}
