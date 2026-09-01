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
    public void Conventional_warheads_no_longer_all_take_the_same_time()
    {
        // The reported problem: every conventional blast spread at the same rate whatever its
        // size, because they all sat on the duration floor. The floor still exists - a front that
        // crosses in two frames cannot be seen - but it now sits below the range these warheads
        // occupy, so a bigger charge visibly takes longer.
        float small = ShockWave.Duration(82f);     // 1.5 t conventional
        float medium = ShockWave.Duration(206f);   // 1.5 t thermobaric
        float large = ShockWave.Duration(388f);    // 20 t thermobaric

        Assert.True(small < medium, "82 m and 206 m fronts take the same time");
        Assert.True(medium < large, "206 m and 388 m fronts take the same time");
    }

    [Fact]
    public void The_floor_still_holds_the_very_smallest_open_long_enough_to_see()
    {
        // Roughly twenty frames. Below that the ring is gone before the eye finds it.
        Assert.Equal(ShockWave.MinimumSeconds, ShockWave.Duration(1f), 3);
        Assert.InRange(ShockWave.MinimumSeconds, 0.25f, 0.6f);
    }

    [Fact]
    public void An_ordinary_bomb_does_raise_a_dust_surge_now_but_a_bomblet_does_not()
    {
        // The threshold used to exclude every conventional warhead - a 2 t charge included -
        // on the argument that a rolling wall of earth behind one bomb reads as a dust storm
        // arriving from nowhere. The Workshop's verdict was the opposite: conventional
        // explosions had no ground smoke worth the name. The wall is sized to its own radius,
        // so a small one is a puff of dirt rather than a full-height wall on a short leash.
        var bomb = WarheadSpec.For(WarheadType.Conventional).Scaled(ConventionalYields.Multiplier(2000));
        Assert.True(ExplosionScale.BlastRadius(bomb) >= ShockWave.DustSurgeMinRadius,
            "a 2 t bomb raises one");

        // A single cluster bomblet still does not: fourteen of those each dragging a wall of
        // earth behind it is the case the threshold was really protecting against.
        var bomblet = WarheadSpec.For(WarheadType.Cluster);
        Assert.True(ExplosionScale.BlastRadius(bomblet) < ShockWave.DustSurgeMinRadius,
            "a cluster bomblet does not");
    }

    [Fact]
    public void The_big_conventional_warheads_do_raise_one()
    {
        // The other half, and the half that was broken. At the old 250 m threshold nothing below
        // a nuclear warhead ever reached it - a thermobaric needed 2.7 t and a conventional 42 t -
        // so the longest-lived and most substantial part of a blast was silently switched off for
        // every ordinary strike. A thermobaric at its default charge must raise one.
        var thermobaric = WarheadSpec.For(WarheadType.Thermobaric);
        Assert.True(ExplosionScale.BlastRadius(thermobaric) >= ShockWave.DustSurgeMinRadius,
            "a thermobaric warhead raises no dust surge, which is what left conventional strikes flat");

        // And a heavy conventional charge, somewhere the player can actually reach by typing one.
        var heavy = WarheadSpec.For(WarheadType.Conventional).Scaled(ConventionalYields.Multiplier(10000));
        Assert.True(ExplosionScale.BlastRadius(heavy) >= ShockWave.DustSurgeMinRadius,
            "a 10 t charge raises no dust surge");

        var nuke = WarheadSpec.For(WarheadType.Nuclear);
        Assert.True(nuke.DestructionRadius > ShockWave.DustSurgeMinRadius,
            "a nuclear burst must still raise the dust surge");
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
