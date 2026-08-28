using MissileDisaster.Core;
using Xunit;

/// <summary>
/// The wind that throws cars and people away from ground zero. The figures are set against the
/// vanilla meteor, which calls the same DisasterHelpers.AddWind with a 200 lift and a 200 radial -
/// read out of MeteorAI's IL - so these pin the mod to the same order of magnitude the game
/// already uses rather than to taste.
/// </summary>
public class BlastWindTests
{
    [Fact]
    public void The_wind_reaches_well_inside_what_the_warhead_destroys()
    {
        // Out at the destruction radius buildings are damaged rather than levelled, and cars
        // being hurled about there would read as wrong.
        foreach (float destruction in new[] { 27f, 72f, 500f, 3720f })
        {
            Assert.True(BlastWind.Reach(destruction) <= destruction,
                $"the wind stays inside the damage ({destruction:F0} m)");
        }
        Assert.True(BlastWind.Reach(500f) > 200f, "and is not a token radius either");
    }

    [Fact]
    public void Even_a_bomblet_blows_the_car_parked_next_to_it()
    {
        // Without the floor a small charge's reach rounds to nothing and the effect silently
        // does not exist for conventional warheads - which is how the debris went missing twice.
        Assert.Equal(BlastWind.ReachMin, BlastWind.Reach(1f), 3);
        Assert.Equal(0f, BlastWind.Reach(0f), 3);
        Assert.Equal(0f, BlastWind.Reach(-5f), 3);
    }

    [Fact]
    public void A_strategic_warhead_does_not_walk_the_whole_vehicle_grid()
    {
        // The ceiling is a cost bound, not a physical one: AddWind sweeps the vehicle and citizen
        // grids, and a 150 kt strike would otherwise ask it to cover four square kilometres.
        Assert.Equal(BlastWind.ReachMax, BlastWind.Reach(1e6f), 3);
        Assert.InRange(BlastWind.Reach(3720f), 500f, BlastWind.ReachMax);
    }

    [Fact]
    public void The_strength_does_not_scale_with_the_yield()
    {
        // Deliberate, and the same argument the rubble's size rests on. The destruction radius is
        // where the overpressure levels a building, and the wind behind the front goes with the
        // overpressure - so the wind at a given fraction of that radius is about the same at any
        // yield. What a bigger warhead changes is how much ground it covers, which is Reach.
        // The vanilla meteor agrees: one fixed 200 for every meteor.
        Assert.Equal(BlastWind.MeteorRadial, BlastWind.Radial, 3);
        Assert.True(BlastWind.Lift < BlastWind.Radial,
            "a blast wave travels along the ground - it throws outward more than up");
        Assert.Equal(0f, BlastWind.Rotational, 3);  // a spin about the centre is a vortex, not a blast
    }

    [Fact]
    public void A_bigger_warhead_throws_more_traffic_rather_than_throwing_it_harder()
    {
        Assert.True(BlastWind.Reach(3720f) > BlastWind.Reach(72f) * 3f);
    }
}
