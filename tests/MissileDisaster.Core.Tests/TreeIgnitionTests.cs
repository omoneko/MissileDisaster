using MissileDisaster.Core;
using Xunit;

/// <summary>
/// The tree-fire policy: how far the fires reach, how the chance falls off, and the budget that
/// keeps a strategic warhead from handing the simulation a hundred thousand burning trees.
/// </summary>
public class TreeIgnitionTests
{
    [Fact]
    public void The_fires_reach_a_good_way_out_but_short_of_the_full_burn_radius()
    {
        // Short on purpose: spreading the budget over the scattered outer fringe would thin the
        // middle, where the fire should be solid.
        float burn = 5850f; // the 150 kt burn radius
        float reach = TreeIgnition.Reach(burn);
        Assert.InRange(reach, burn * 0.5f, burn * 0.75f);
    }

    [Fact]
    public void A_warhead_with_no_burn_radius_lights_nothing()
    {
        Assert.Equal(0f, TreeIgnition.Reach(0f), 3);
        Assert.Equal(0f, TreeIgnition.Reach(-10f), 3);
        Assert.Equal(0f, TreeIgnition.Chance(1f, 0f), 3);
    }

    [Fact]
    public void Everything_in_the_core_catches_and_nothing_past_the_reach_does()
    {
        float reach = 1000f;
        Assert.Equal(1f, TreeIgnition.Chance(0f, reach), 3);
        Assert.Equal(1f, TreeIgnition.Chance(reach * TreeIgnition.CoreFraction * 0.9f, reach), 3);
        Assert.Equal(0f, TreeIgnition.Chance(reach, reach), 3);
        Assert.Equal(0f, TreeIgnition.Chance(reach * 2f, reach), 3);
    }

    [Fact]
    public void The_chance_falls_off_monotonically_from_the_core_to_the_edge()
    {
        float reach = 1000f;
        float last = 1.1f;
        for (int i = 0; i <= 20; i++)
        {
            float d = reach * i / 20f;
            float c = TreeIgnition.Chance(d, reach);
            Assert.True(c <= last + 0.0001f, $"chance rose at {d} m: {c} after {last}");
            Assert.InRange(c, 0f, 1f);
            last = c;
        }
    }

    [Fact]
    public void The_fire_is_still_thick_halfway_out()
    {
        // A forest fire that is only fierce at the very centre reads as a scorch mark, not as a
        // fire. FalloffPower holds the middle up and drops it late.
        float reach = 1000f;
        Assert.True(TreeIgnition.Chance(reach * 0.5f, reach) > 0.3f);
    }

    [Fact]
    public void A_small_copse_burns_entirely()
    {
        // Fewer trees than the budget: no sampling, every one of them catches by the falloff
        // alone.
        Assert.Equal(1f, TreeIgnition.Density(50), 3);
        Assert.Equal(50, TreeIgnition.Budget(50));
    }

    [Fact]
    public void A_dense_forest_is_sampled_rather_than_lit_whole()
    {
        int trees = 40000; // a wooded map inside a 150 kt reach
        Assert.Equal(TreeIgnition.MaxTrees, TreeIgnition.Budget(trees));
        float density = TreeIgnition.Density(trees);
        Assert.True(density < 0.05f, "only a small share is taken");
        // The sampling has to aim at the budget, not undershoot it into invisibility.
        Assert.InRange(density * trees, TreeIgnition.MaxTrees * 0.9f, TreeIgnition.MaxTrees * 1.1f);
    }

    [Fact]
    public void The_budget_is_never_exceeded_at_any_forest_size()
    {
        foreach (int trees in new[] { 0, 1, 100, 319, 320, 321, 5000, 250000 })
        {
            Assert.InRange(TreeIgnition.Budget(trees), 0, TreeIgnition.MaxTrees);
            Assert.InRange(TreeIgnition.Density(trees), 0f, 1f);
        }
    }

    [Fact]
    public void An_empty_area_asks_for_nothing()
    {
        Assert.Equal(0, TreeIgnition.Budget(0));
        Assert.Equal(0f, TreeIgnition.Density(0), 3);
    }
}
