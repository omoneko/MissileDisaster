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
    public void An_airburst_stains_too_because_it_is_the_fires_that_blacken_the_rain()
    {
        // Hiroshima was an airburst at 600 m and its black rain is the case this is modelled on.
        // Tying the stain to fallout, which an airburst does not leave, gave exactly the wrong
        // answer for the one detonation everybody has heard of.
        var airburst = WarheadSpec.For(WarheadType.Nuclear).WithBurst(BurstType.Airburst);

        Assert.Equal(0f, airburst.ContaminationRadius, 3);
        Assert.True(airburst.BurnRadius > 0f, "an airburst still sets the city alight");
        Assert.True(BlackRain.StainRadius(airburst.BurnRadius) > 0f);
    }

    [Fact]
    public void The_stain_covers_the_ground_the_fires_reached()
    {
        // The soot comes off the burning city, so the mark is the size of the fire field.
        foreach (int kt in new[] { 15, 150 })
        {
            var ground = WarheadSpec.For(WarheadType.Nuclear)
                .WithBurst(BurstType.Groundburst)
                .Scaled(NuclearYields.Multiplier(kt));

            Assert.True(ground.BurnRadius < BlackRain.StainRadiusMax,
                kt + " kt already exceeds the ceiling; pick a smaller yield for this test");
            Assert.Equal(ground.BurnRadius, BlackRain.StainRadius(ground.BurnRadius), 1);
        }
    }

    [Fact]
    public void A_detonation_that_starts_no_fires_leaves_no_mark()
    {
        // No fires, no soot, no black rain - which is why a test shot in a desert produced
        // fallout and white coral ash but nothing anyone called black rain.
        Assert.Equal(0f, BlackRain.StainRadius(0f), 3);
    }

    [Fact]
    public void An_absurd_fire_radius_is_still_held_under_a_ceiling()
    {
        // A guard against a hand-typed yield asking to grey out the entire map.
        Assert.Equal(BlackRain.StainRadiusMax, BlackRain.StainRadius(1e6f), 3);
    }


    [Fact]
    public void The_stain_lifts_before_the_rain_stops()
    {
        // Soot on wet ground, not a scar: the shower that laid it down washes it off again.
        float rain = BlackRain.RainSeconds(150f);
        Assert.True(BlackRain.StainSeconds(rain) < rain,
            "the mark outlasts the rain that is supposed to be washing it away");
        Assert.Equal(rain * BlackRain.StainSecondsFactor, BlackRain.StainSeconds(rain), 3);
    }

    [Fact]
    public void It_only_rains_about_half_the_time()
    {
        // A coin toss rather than a certainty: making it follow every strike turned a striking
        // detail into scenery.
        int fell = 0;
        for (int roll = 0; roll < 100; roll++)
        {
            if (BlackRain.FallsThisTime(150f, roll)) fell++;
        }
        Assert.Equal(BlackRain.ChancePercent, fell);
    }

    [Fact]
    public void A_yield_too_small_never_rains_whatever_the_roll()
    {
        for (int roll = 0; roll < 100; roll++)
        {
            Assert.False(BlackRain.FallsThisTime(0.1f, roll));
        }
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
