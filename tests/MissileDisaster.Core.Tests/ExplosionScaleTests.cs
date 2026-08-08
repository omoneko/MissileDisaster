using MissileDisaster.Core;
using Xunit;

public class ExplosionScaleTests
{
    private static WarheadSpec Conventional(int kilograms)
    {
        return WarheadSpec.For(WarheadType.Conventional).Scaled(ConventionalYields.Multiplier(kilograms));
    }

    private static WarheadSpec Phosphorus(int kilograms)
    {
        return WarheadSpec.For(WarheadType.WhitePhosphorus).Scaled(ConventionalYields.Multiplier(kilograms));
    }

    [Fact]
    public void A_heavier_charge_covers_more_ground()
    {
        float small = ExplosionScale.SpawnRadius(Conventional(100));
        float baseline = ExplosionScale.SpawnRadius(Conventional(1000));
        float large = ExplosionScale.SpawnRadius(Conventional(10000));
        Assert.True(small < baseline, "100 kg is smaller than the 1 t baseline");
        Assert.True(baseline < large, "10 t is larger than the 1 t baseline");
    }

    [Fact]
    public void The_size_tracks_the_charge_across_the_usual_range()
    {
        // The point of the mapping: no two ordinary charges look the same. 1.5 t against 20 t is
        // the comparison the calibration was written around.
        Assert.True(ExplosionScale.SpawnRadius(Conventional(20000)) >
                    ExplosionScale.SpawnRadius(Conventional(1500)) * 2f,
            "a 20 t warhead explodes over visibly more ground than a 1.5 t one");
    }

    [Fact]
    public void An_incendiary_still_grows_with_the_charge_through_its_fires()
    {
        // White phosphorus has a fixed destruction radius, so the fireball has to follow the
        // burn radius instead - otherwise every charge would look identical.
        float baseline = ExplosionScale.SpawnRadius(Phosphorus(1000));
        float large = ExplosionScale.SpawnRadius(Phosphorus(8000));
        Assert.True(large > baseline * 1.5f, "a heavier incendiary charge burns over visibly more ground");
    }

    [Fact]
    public void The_spawn_radius_is_clamped_at_both_ends()
    {
        Assert.Equal(ExplosionScale.SpawnRadiusMin, ExplosionScale.SpawnRadius(Conventional(1)), 3);
        Assert.Equal(ExplosionScale.SpawnRadiusMax, ExplosionScale.SpawnRadius(Conventional(100000000)), 3);
    }

    [Fact]
    public void A_zero_charge_never_returns_a_negative_radius()
    {
        var dud = WarheadSpec.For(WarheadType.Conventional).Scaled(0f);
        Assert.Equal(0f, ExplosionScale.VisualRadius(dud), 3);
        Assert.Equal(ExplosionScale.SpawnRadiusMin, ExplosionScale.SpawnRadius(dud), 3);
    }

    [Fact]
    public void The_visual_radius_takes_the_widest_effect()
    {
        // Conventional destroys further than half of what it burns, so the destruction leads.
        var conv = WarheadSpec.For(WarheadType.Conventional);
        Assert.Equal(conv.DestructionRadius, ExplosionScale.VisualRadius(conv), 3);
        // White phosphorus barely destroys anything, so its fires lead instead.
        var wp = WarheadSpec.For(WarheadType.WhitePhosphorus);
        Assert.Equal(wp.BurnRadius * 0.5f, ExplosionScale.VisualRadius(wp), 3);
    }

    // ---- the magnitude, which the base game's IL says is a density and not a size ----

    [Fact]
    public void The_magnitude_holds_the_particle_budget_whatever_the_size()
    {
        // A bigger explosion must spread the same rate of particles over more ground rather than
        // pile more of them onto one spot, or it stops being affordable.
        foreach (int kg in new[] { 100, 1000, 5000, 50000, 1000000 })
        {
            float r = ExplosionScale.SpawnRadius(Conventional(kg));
            float m = ExplosionScale.Magnitude(r, ExplosionScale.SingleParticlesPerSecond);
            float actual = ExplosionScale.ParticlesPerSecond(r, m);
            Assert.InRange(actual, 1f, ExplosionScale.SingleParticlesPerSecond * 1.01f);
        }
    }

    [Fact]
    public void A_larger_explosion_asks_for_a_lower_density()
    {
        float small = ExplosionScale.SpawnRadius(Conventional(100));
        float large = ExplosionScale.SpawnRadius(Conventional(50000));
        Assert.True(ExplosionScale.Magnitude(large, ExplosionScale.SingleParticlesPerSecond) <
                    ExplosionScale.Magnitude(small, ExplosionScale.SingleParticlesPerSecond),
            "spreading the same budget over more ground means fewer particles per square metre");
    }

    [Fact]
    public void The_magnitude_stays_near_the_value_the_base_game_uses()
    {
        // MeteorAI dispatches its own impact with a magnitude of 1, and the same number scales
        // the light flash, so anything wildly away from it would look wrong rather than big.
        foreach (int kg in new[] { 1, 100, 1000, 20000, 1000000 })
        {
            float r = ExplosionScale.SpawnRadius(Conventional(kg));
            Assert.InRange(ExplosionScale.Magnitude(r, ExplosionScale.SingleParticlesPerSecond),
                ExplosionScale.MagnitudeMin, ExplosionScale.MagnitudeMax);
        }
    }

    [Fact]
    public void A_tiny_disc_is_charged_for_the_hundred_square_metre_floor()
    {
        // EmitParticles floors the area at 100 m^2, so a smaller disc emits no fewer particles.
        Assert.Equal(ExplosionScale.ParticlesPerSecond(0f, 1f),
                     ExplosionScale.ParticlesPerSecond(3f, 1f), 3);
        Assert.Equal(ExplosionScale.EmitAreaFloor * ExplosionScale.DensityPerMagnitude,
                     ExplosionScale.ParticlesPerSecond(0f, 1f), 3);
    }
}
