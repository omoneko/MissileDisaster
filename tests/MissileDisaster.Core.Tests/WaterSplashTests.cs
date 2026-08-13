using MissileDisaster.Core;
using Xunit;

public class WaterSplashTests
{
    private static float ConventionalFireball(int kg)
    {
        return WarheadSpec.For(WarheadType.Conventional)
            .Scaled(ConventionalYields.Multiplier(kg)).FireballRadius;
    }

    [Fact]
    public void A_detonation_with_no_fireball_displaces_nothing()
    {
        // An airburst incendiary, a dud, a nuclear spec read before its yield is set.
        Assert.False(WaterSplash.Displaces(0f));
        Assert.False(WaterSplash.Displaces(-1f));
        Assert.Equal(0f, WaterSplash.Radius(0f), 3);
        Assert.Equal(0f, WaterSplash.Depth(0f), 3);
    }

    [Fact]
    public void The_disturbance_is_wider_than_it_is_deep()
    {
        // The cavity a burst opens is a shallow bowl, not a shaft - which is why the wave
        // spreads outward instead of standing up as a column.
        foreach (float fireball in new[] { 10f, 17f, 50f, 200f, 600f })
        {
            Assert.True(WaterSplash.Radius(fireball) > WaterSplash.Depth(fireball) * 2f,
                "fireball " + fireball + " gave a splash deeper than it is wide");
        }
    }

    [Fact]
    public void A_1500_kg_conventional_burst_makes_a_splash_a_boat_would_notice_and_no_more()
    {
        float fireball = ConventionalFireball(1500);   // about 17 m
        Assert.InRange(WaterSplash.Radius(fireball), 45f, 60f);
        Assert.InRange(WaterSplash.Depth(fireball), 3f, 8f);
    }

    [Fact]
    public void A_bigger_charge_displaces_more_water()
    {
        float small = ConventionalFireball(500);
        float large = ConventionalFireball(20000);

        Assert.True(WaterSplash.Radius(large) > WaterSplash.Radius(small));
        Assert.True(WaterSplash.Depth(large) > WaterSplash.Depth(small));
    }

    [Fact]
    public void A_tiny_charge_still_reaches_one_wave_cell()
    {
        // The water grid is 16 m to a cell; anything smaller would be silently dropped, which
        // reads as the feature being broken rather than as a small explosion.
        Assert.Equal(WaterSplash.MinRadius, WaterSplash.Radius(0.1f), 3);
        Assert.True(WaterSplash.Depth(0.1f) > 0f);
    }

    [Fact]
    public void The_biggest_yields_are_held_under_a_ceiling()
    {
        // A strategic fireball is kilometres across and would otherwise ask to displace the whole
        // map down to the seabed.
        Assert.Equal(WaterSplash.MaxRadius, WaterSplash.Radius(100000f), 3);
        Assert.Equal(WaterSplash.MaxDepth, WaterSplash.Depth(100000f), 3);
    }

    [Fact]
    public void The_depth_never_reaches_what_the_api_can_encode()
    {
        // SplashWater encodes depth as clamp(depth * 64) into an Int16, so about 512 m. Staying
        // well inside it means the clamp can never silently truncate what we asked for.
        const float apiLimit = 32767f / 64f;
        Assert.True(WaterSplash.MaxDepth < apiLimit * 0.5f,
            "MaxDepth " + WaterSplash.MaxDepth + " is close to the API's " + apiLimit + " m limit");
        Assert.Equal(WaterSplash.MaxDepth, WaterSplash.Depth(float.MaxValue), 3);
    }

    [Fact]
    public void Every_warhead_that_can_land_on_water_displaces_some()
    {
        foreach (WarheadType type in new[]
                 { WarheadType.Conventional, WarheadType.Cluster,
                   WarheadType.WhitePhosphorus, WarheadType.Thermobaric })
        {
            float fireball = WarheadSpec.For(type).FireballRadius;
            Assert.True(WaterSplash.Displaces(fireball), type + " displaces no water");
            Assert.True(WaterSplash.Radius(fireball) >= WaterSplash.MinRadius);
        }
    }
}
