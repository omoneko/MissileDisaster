using MissileDisaster.Core;
using Xunit;

public class ShockWaveTests
{
    [Fact]
    public void The_front_reaches_a_megaton_five_psi_contour_in_about_thirteen_seconds()
    {
        // Glasstone's 1 Mt: the 5 psi contour is about 6.9 km out, and the front gets there in
        // roughly 13 s.
        Assert.InRange(ShockWave.Duration(6900f), 11f, 14f);
    }

    [Fact]
    public void The_front_starts_supersonic_and_slows_down()
    {
        const float radius = 3720f; // the 150 kt destruction radius
        float t = ShockWave.Duration(radius);
        float opening = ShockWave.FrontSpeed(radius, t, ShockWave.MinFraction);
        float middle = ShockWave.FrontSpeed(radius, t, 0.5f);
        float end = ShockWave.FrontSpeed(radius, t, 1f);
        Assert.True(opening > 340f * 3f, "it leaves at several times the speed of sound");
        Assert.True(opening > middle && middle > end, "and decays the whole way out");
    }

    [Fact]
    public void The_front_arrives_exactly_at_the_radius_it_was_given()
    {
        Assert.Equal(3720f, ShockWave.FrontRadius(3720f, 1f), 1);
        Assert.Equal(0f, ShockWave.FrontRadius(3720f, 0f), 3);
    }

    [Fact]
    public void Most_of_the_ground_is_covered_early()
    {
        // r goes as t^0.4, so the front is already over half way out a fifth of the way through.
        // That is what makes it look like a wave rather than a growing circle.
        float half = ShockWave.FrontRadius(1000f, 0.2f);
        Assert.InRange(half, 500f, 550f);
    }

    [Fact]
    public void The_ring_starts_a_fifth_of_the_way_out()
    {
        // r goes as t^0.4, so by the time the front is first tracked - 2% of the way through its
        // life - it has already covered 0.02^0.4, better than a fifth of the distance.
        Assert.InRange(ShockWave.StartRadius(1000f), 190f, 220f);
    }

    [Fact]
    public void Starting_there_and_integrating_the_speed_traces_the_radius_exactly()
    {
        // The particle system is given a ring at StartRadius and a speed curve to carry it the
        // rest of the way. The two have to add up to the modelled radius, or the front stops
        // short of the damage it is meant to be drawing.
        const float radius = 2000f;
        float t = ShockWave.Duration(radius);
        const int steps = 8000;
        float travelled = ShockWave.StartRadius(radius);
        float from = ShockWave.MinFraction;
        for (int i = 0; i < steps; i++)
        {
            float u = from + (i + 0.5f) * (1f - from) / steps;
            travelled += ShockWave.FrontSpeed(radius, t, u) * (t * (1f - from) / steps);
        }
        Assert.InRange(travelled, radius * 0.99f, radius * 1.01f);
    }

    [Fact]
    public void A_small_charge_is_held_open_long_enough_to_be_seen()
    {
        // A 1 t warhead's front really crosses its 72 m in a tenth of a second - a frame or two.
        Assert.Equal(ShockWave.MinimumSeconds, ShockWave.Duration(72f), 3);
    }

    [Fact]
    public void A_strategic_warhead_does_not_run_on_forever()
    {
        Assert.True(ShockWave.Duration(100000f) < ShockWave.CeilingSeconds,
            "the front is always held under the ceiling");
    }

    [Fact]
    public void A_bigger_blast_still_takes_longer_to_cross_the_ground()
    {
        // Past the knee the duration is compressed, not clamped: a 50 Mt front crossing 26 km
        // must take longer than a 1 Mt one crossing 7 km, or the larger weapon is drawn with a
        // faster wave rather than a longer one.
        float megaton = ShockWave.Duration(7000f);
        float tenMegaton = ShockWave.Duration(15300f);
        float fiftyMegaton = ShockWave.Duration(25800f);
        Assert.True(megaton < tenMegaton && tenMegaton < fiftyMegaton,
            "the front lasts longer the further it has to go");
    }

    [Fact]
    public void A_zero_radius_does_nothing()
    {
        Assert.Equal(0f, ShockWave.Duration(0f), 3);
        Assert.Equal(0f, ShockWave.FrontRadius(0f, 1f), 3);
        Assert.Equal(0f, ShockWave.FrontSpeed(0f, 1f, 0.5f), 3);
    }
}
