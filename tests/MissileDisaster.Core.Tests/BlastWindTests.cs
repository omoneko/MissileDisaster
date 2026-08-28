using MissileDisaster.Core;
using Xunit;

/// <summary>
/// The wind that throws cars and people away from ground zero.
///
/// These are unusual for this project in that they assert real outcomes rather than constants:
/// BlastWind.Predict runs the game's own blown-vehicle step, ported from CarAI's IL, so a test
/// can say how far a hatchback actually goes. That matters because the first version of this
/// effect threw a car 310 m and 54 m into the air off a 1 t charge, and nothing about the
/// constants involved looked wrong.
/// </summary>
public class BlastWindTests
{
    private const float Cluster = 18f, OneTonne = 27f, Thermobaric = 72f;
    private const float Tactical = 900f, Strategic = 3720f, Tsar = 25000f;

    private static float Thrown(float distance, float destruction)
    {
        float thrown, apex, seconds;
        BlastWind.Predict(distance, destruction, out thrown, out apex, out seconds);
        return thrown;
    }

    [Fact]
    public void A_conventional_charge_shoves_the_nearest_cars_rather_than_launching_them()
    {
        // At the overpressure that wrecks a house, cars are rolled and shoved metres to tens of
        // metres. They are not thrown across the district, whatever the film says. This is the
        // test that would have caught the 310 m version.
        foreach (float destruction in new[] { Cluster, OneTonne, Thermobaric })
        {
            float longest = BlastWind.LongestThrow(destruction);
            Assert.InRange(longest, 3f, 25f);
        }
    }

    [Fact]
    public void But_it_still_moves_something()
    {
        // A floor just above the gate, because an effect that silently does nothing for every
        // conventional warhead is this mod's most-repeated failure.
        foreach (float destruction in new[] { Cluster, OneTonne, Thermobaric })
        {
            Assert.True(BlastWind.Blows(0f, destruction),
                $"a car at ground zero is blown by a {destruction:F0} m blast");
            Assert.True(BlastWind.LongestThrow(destruction) > 0f);
        }
    }

    [Fact]
    public void A_strategic_warhead_throws_them_properly()
    {
        float longest = BlastWind.LongestThrow(Strategic);
        Assert.InRange(longest, 40f, 140f);
        Assert.True(longest > BlastWind.LongestThrow(OneTonne) * 3f,
            "and far further than a bomb does");
    }

    [Fact]
    public void The_throw_grows_with_the_yield_because_the_impulse_does()
    {
        // The correction. Holding the strength constant looked principled - the wind at a given
        // overpressure really is yield-independent - but a car is thrown by an impulse, dynamic
        // pressure times the positive phase, and that phase goes with the cube root of the yield.
        float last = 0f;
        foreach (float destruction in new[] { Cluster, OneTonne, Thermobaric, Tactical, Strategic, Tsar })
        {
            float longest = BlastWind.LongestThrow(destruction);
            Assert.True(longest >= last, $"{destruction:F0} m threw {longest:F0} m, less than the yield below it");
            last = longest;
        }
        Assert.True(BlastWind.Lift(Tsar) > BlastWind.Lift(Strategic),
            "and the ceiling is soft, so the biggest warheads are still distinguishable");
    }

    [Fact]
    public void Nothing_is_thrown_beyond_the_reach_and_the_reach_stays_inside_the_damage()
    {
        foreach (float destruction in new[] { OneTonne, Thermobaric, Tactical, Strategic })
        {
            float reach = BlastWind.Reach(destruction);
            Assert.True(reach <= destruction, "the wind stays inside what the warhead destroyed");
            Assert.False(BlastWind.Blows(reach, destruction), "and stops at its own edge");
            Assert.Equal(0f, Thrown(reach * 1.01f, destruction), 3);
        }
    }

    [Fact]
    public void The_nearest_cars_go_furthest()
    {
        float near = Thrown(BlastWind.Reach(Strategic) * 0.1f, Strategic);
        float far = Thrown(BlastWind.Reach(Strategic) * 0.6f, Strategic);
        Assert.True(near > far, $"{near:F0} m near the middle against {far:F0} m out at the rim");
    }

    [Fact]
    public void A_car_is_thrown_outward_more_than_upward()
    {
        // A blast wave travels along the ground. A fountain would be a vortex, not a detonation.
        float thrown, apex, seconds;
        BlastWind.Predict(BlastWind.Reach(Strategic) * 0.2f, Strategic, out thrown, out apex, out seconds);
        Assert.True(thrown > apex * 3f, $"{thrown:F0} m out against {apex:F0} m up");
        Assert.Equal(0f, BlastWind.Rotational, 3);
        Assert.True(BlastWind.RadialPerLift > 1f);
    }

    [Fact]
    public void Nothing_happens_without_a_blast()
    {
        Assert.Equal(0f, BlastWind.Reach(0f), 3);
        Assert.Equal(0f, BlastWind.Reach(-5f), 3);
        Assert.Equal(0f, BlastWind.Lift(0f), 3);
        Assert.False(BlastWind.Blows(0f, 0f));
    }

    [Fact]
    public void A_strategic_warhead_does_not_walk_the_whole_vehicle_grid()
    {
        // AddWind sweeps the vehicle and citizen grids, so the reach has a cost ceiling. It is
        // soft rather than a clamp, so a Tsar Bomba still reaches further than a 150 kt.
        Assert.True(BlastWind.Reach(Tsar) <= BlastWind.ReachCeiling);
        Assert.True(BlastWind.Reach(Tsar) > BlastWind.Reach(Strategic));
    }

    [Fact]
    public void The_ported_game_constants_are_the_ones_the_game_actually_uses()
    {
        // Read out of CarAI's IL. If a future Cities: Skylines patch changes them, the prediction
        // above stops being a prediction - so they are written down where a diff will show them.
        Assert.Equal(19.620001f, BlastWind.Gate, 5);        // 2g: CarAI.AddWind's threshold on lift
        Assert.Equal(0.125f, BlastWind.Blend, 5);           // velocity = velocity*0.875 + wind*0.125
        Assert.Equal(2.4525f, BlastWind.GravityPerStep, 5); // 9.81 * 0.5^2
        Assert.Equal(0.99f, BlastWind.DragPerStep, 5);
        Assert.Equal(0.5f, BlastWind.StepSeconds, 5);       // solved from 2.4525 = 9.81*T^2
    }
}
