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
    public void A_heavier_charge_makes_a_bigger_explosion()
    {
        float small = ExplosionScale.ForSingle(Conventional(100));
        float baseline = ExplosionScale.ForSingle(Conventional(1000));
        float large = ExplosionScale.ForSingle(Conventional(10000));
        Assert.True(small < baseline, "100 kg is smaller than the 1 t baseline");
        Assert.True(baseline < large, "10 t is larger than the 1 t baseline");
    }

    [Fact]
    public void The_size_tracks_the_charge_across_the_usual_range()
    {
        // The point of the mapping: no two ordinary charges look the same. 1.5 t against 20 t is
        // the comparison the calibration was written around.
        Assert.True(ExplosionScale.ForSingle(Conventional(20000)) >
                    ExplosionScale.ForSingle(Conventional(1500)) * 2f,
            "a 20 t warhead explodes visibly larger than a 1.5 t one");
    }

    [Fact]
    public void An_incendiary_still_grows_with_the_charge_through_its_fires()
    {
        // White phosphorus has a fixed destruction radius, so the fireball has to follow the
        // burn radius instead - otherwise every charge would look identical.
        float baseline = ExplosionScale.ForSubmunition(Phosphorus(1000));
        float large = ExplosionScale.ForSubmunition(Phosphorus(8000));
        Assert.True(large > baseline * 1.5f, "a heavier incendiary charge burns visibly larger");
    }

    [Fact]
    public void Submunitions_are_played_smaller_than_a_single_detonation_of_the_same_size()
    {
        var cluster = WarheadSpec.For(WarheadType.Cluster);
        Assert.True(ExplosionScale.ForSubmunition(cluster) < ExplosionScale.ForSingle(WarheadSpec.For(WarheadType.Conventional)),
            "one bomblet is smaller than a whole conventional warhead");
    }

    [Fact]
    public void Nuclear_dwarfs_every_conventional_explosion()
    {
        float nuke = ExplosionScale.ForNuclear(WarheadSpec.For(WarheadType.Nuclear));
        Assert.True(nuke > ExplosionScale.SingleMax, "even the largest conventional fireball is far smaller");
    }

    [Fact]
    public void The_scale_is_clamped_at_both_ends()
    {
        Assert.Equal(ExplosionScale.SingleMin, ExplosionScale.ForSingle(Conventional(1)), 3);
        Assert.Equal(ExplosionScale.SingleMax, ExplosionScale.ForSingle(Conventional(100000000)), 3);
        Assert.Equal(ExplosionScale.NuclearMax, ExplosionScale.ForNuclear(
            WarheadSpec.For(WarheadType.Nuclear).Scaled(NuclearYields.Multiplier(50000))), 3);
    }

    [Fact]
    public void A_zero_charge_never_returns_a_negative_scale()
    {
        var dud = WarheadSpec.For(WarheadType.Conventional).Scaled(0f);
        Assert.Equal(0f, ExplosionScale.VisualRadius(dud), 3);
        Assert.Equal(ExplosionScale.SingleMin, ExplosionScale.ForSingle(dud), 3);
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
}
