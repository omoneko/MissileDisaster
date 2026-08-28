using System;
using MissileDisaster.Core;
using Xunit;

/// <summary>
/// How the blast moves the rubble. These pin the thing that was actually asked for - one ring of
/// wreckage travelling outward with the dust, rather than a few hundred independent arcs - so it
/// cannot quietly go back to being a scatter.
/// </summary>
public class DebrisSweepTests
{
    private const float Blast = 3720f;   // 150 kt
    private static readonly float Range = BlastDebris.Range(Blast);
    private static readonly float Emit = BlastDebris.EmitRadius(Blast);
    private static readonly float Carry = DebrisSweep.CarrySeconds(ShockWave.Duration(Blast));

    private static DebrisRide R(int i) =>
        DebrisSweep.Deal(i, 7, Emit, Range, Carry, BlastDebris.ChunkSizeMax, 4);

    private static float RadiusAt(DebrisRide r, float t)
    {
        float x, y, z;
        DebrisSweep.PositionAt(r, t, out x, out y, out z);
        return (float)Math.Sqrt(x * x + z * z);
    }

    [Fact]
    public void Every_piece_travels_straight_out_from_ground_zero()
    {
        // The direction is the whole difference between a wave and a scatter: a piece that
        // starts north of the crater must end further north, never across it.
        for (int i = 0; i < 200; i++)
        {
            DebrisRide r = R(i);
            float startAngle = (float)Math.Atan2(r.StartZ, r.StartX);
            float x, y, z;
            DebrisSweep.PositionAt(r, Carry, out x, out y, out z);
            float endAngle = (float)Math.Atan2(z, x);
            Assert.Equal(startAngle, endAngle, 3);
            Assert.Equal(1f, r.DirX * r.DirX + r.DirZ * r.DirZ, 3);
        }
    }

    [Fact]
    public void Nothing_moves_inwards_or_stands_still()
    {
        for (int i = 0; i < 200; i++)
        {
            DebrisRide r = R(i);
            float before = RadiusAt(r, 0f);
            float after = RadiusAt(r, r.CarrySeconds);
            Assert.True(after > before, $"piece {i} went from {before:F0} m to {after:F0} m");
            Assert.True(r.Distance >= Range * DebrisSweep.MinTravelFraction);
        }
    }

    [Fact]
    public void The_pieces_gather_into_a_ring_instead_of_keeping_the_spread_they_started_with()
    {
        // This is the point of the effect. They start scattered across the whole destroyed disc
        // and end up in a band, so what the player sees is one expanding edge of wreckage.
        float startMin = float.MaxValue, startMax = 0f, endMin = float.MaxValue, endMax = 0f;
        for (int i = 0; i < 320; i++)
        {
            DebrisRide r = R(i);
            float a = RadiusAt(r, 0f), b = RadiusAt(r, r.CarrySeconds);
            startMin = Math.Min(startMin, a); startMax = Math.Max(startMax, a);
            endMin = Math.Min(endMin, b); endMax = Math.Max(endMax, b);
        }
        float startSpread = (startMax - startMin) / startMax;
        float endSpread = (endMax - endMin) / endMax;
        Assert.True(endSpread < startSpread,
            $"the band tightens: {startSpread:P0} of the disc at the start, {endSpread:P0} at the end");
        // And it lands where the range says, not somewhere of its own.
        Assert.InRange(endMax, Range * DebrisSweep.TargetMin, Range * DebrisSweep.TargetMax * 1.05f);
    }

    [Fact]
    public void The_sweep_decelerates_the_way_the_front_that_causes_it_does()
    {
        // Sedov again: most of the ground is covered in the first moments, and the rest is a
        // long slide. A piece that covered its distance at a constant rate would read as being
        // carried by a conveyor rather than hit by a blast.
        DebrisRide r = R(0);
        float firstQuarter = DebrisSweep.TravelAt(r, r.CarrySeconds * 0.25f);
        float lastQuarter = DebrisSweep.TravelAt(r, r.CarrySeconds)
            - DebrisSweep.TravelAt(r, r.CarrySeconds * 0.75f);
        Assert.True(firstQuarter > r.Distance * 0.5f,
            $"half the ground is gone in the first quarter of the time (got {firstQuarter / r.Distance:P0})");
        Assert.True(lastQuarter < firstQuarter * 0.3f, "and the end is a slide, not a launch");
    }

    [Fact]
    public void The_rubble_skips_along_the_ground_rather_than_flying()
    {
        // The reason the ballistic model was replaced. A piece must stay low enough to read as
        // being pushed over the ground - a few times its own size, not a hundred metres up.
        float highest = 0f;
        for (int i = 0; i < 320; i++)
        {
            DebrisRide r = R(i);
            for (float t = 0f; t <= r.CarrySeconds; t += r.CarrySeconds / 60f)
                highest = Math.Max(highest, DebrisSweep.HeightAt(r, t));
        }
        Assert.True(highest <= BlastDebris.ChunkSizeMax * DebrisSweep.HopHeightMax + 0.01f,
            $"the highest skip was {highest:F1} m");
        Assert.True(highest < Range * 0.06f, "which is nothing beside how far it travels");
    }

    [Fact]
    public void A_short_sweep_keeps_its_skips_short_too()
    {
        // "Low" is a proportion, not a number of metres. A 1 t charge pushes its rubble 30 m,
        // and before the sweep was allowed to cap the skip, a piece hopped 7.8 m on the way -
        // a quarter of the whole journey, which is flying however small the piece is.
        float range = BlastDebris.Range(72f);
        float carry = DebrisSweep.CarrySeconds(ShockWave.Duration(72f));
        float highest = 0f;
        for (int i = 0; i < 171; i++)
        {
            DebrisRide r = DebrisSweep.Deal(i, 7, BlastDebris.EmitRadius(72f), range, carry,
                BlastDebris.ChunkSize(range), 4);
            for (float t = 0f; t <= r.CarrySeconds; t += r.CarrySeconds / 60f)
                highest = Math.Max(highest, DebrisSweep.HeightAt(r, t));
        }
        Assert.True(highest <= range * DebrisSweep.HopHeightRangeCap + 0.01f,
            $"the highest skip was {highest:F1} m against a {range:F0} m sweep");
    }

    [Fact]
    public void Every_piece_ends_on_the_ground()
    {
        for (int i = 0; i < 200; i++)
        {
            DebrisRide r = R(i);
            Assert.Equal(0f, DebrisSweep.HeightAt(r, r.CarrySeconds), 3);
            Assert.Equal(0f, DebrisSweep.HeightAt(r, r.CarrySeconds * 2f), 3);
        }
    }

    [Fact]
    public void Each_bounce_is_lower_than_the_one_before()
    {
        DebrisRide r = R(3);
        float first = 0f, last = 0f;
        for (float t = 0f; t < r.CarrySeconds * 0.4f; t += r.CarrySeconds / 200f)
            first = Math.Max(first, DebrisSweep.HeightAt(r, t));
        for (float t = r.CarrySeconds * 0.6f; t < r.CarrySeconds; t += r.CarrySeconds / 200f)
            last = Math.Max(last, DebrisSweep.HeightAt(r, t));
        Assert.True(first > last, $"the skips damp out: {first:F1} m early, {last:F1} m late");
    }

    [Fact]
    public void It_rolls_in_proportion_to_the_ground_it_covers()
    {
        // Which is what rolling means, and it makes the tumble decelerate with the sweep
        // without having to be told to.
        DebrisRide r = R(0);
        Assert.Equal(0f, DebrisSweep.RollAt(r, 0f), 3);
        Assert.Equal(r.RollDegrees, DebrisSweep.RollAt(r, r.CarrySeconds), 1);
        float halfway = DebrisSweep.RollAt(r, r.CarrySeconds * 0.5f);
        float travelled = DebrisSweep.TravelAt(r, r.CarrySeconds * 0.5f) / r.Distance;
        Assert.Equal(r.RollDegrees * travelled, halfway, 1);
        Assert.True(r.RollDegrees > 360f, "a piece pushed hundreds of metres turns over many times");
    }

    [Fact]
    public void The_pieces_do_not_all_stop_at_the_same_instant()
    {
        float min = float.MaxValue, max = 0f;
        for (int i = 0; i < 200; i++)
        {
            min = Math.Min(min, R(i).CarrySeconds);
            max = Math.Max(max, R(i).CarrySeconds);
        }
        Assert.True(max > min * 1.3f, $"the ring frays rather than switching off ({min:F1}-{max:F1} s)");
    }

    [Fact]
    public void The_rubble_moves_for_as_long_as_the_blast_is_crossing_the_ground()
    {
        // It is on the front's clock: that is what makes the pieces and the dust one wave
        // rather than two effects that happen to start together.
        Assert.True(DebrisSweep.CarrySeconds(ShockWave.Duration(3720f))
            > DebrisSweep.CarrySeconds(ShockWave.Duration(72f)),
            "a bigger blast pushes for longer");
        foreach (float blast in new[] { 18f, 72f, 500f, 3720f, 25000f, 1e6f })
        {
            float carry = DebrisSweep.CarrySeconds(ShockWave.Duration(blast));
            Assert.InRange(carry, DebrisSweep.CarrySecondsMin, DebrisSweep.CarrySecondsMax);
        }
    }

    [Fact]
    public void Every_piece_starts_somewhere_on_the_destroyed_disc()
    {
        for (int i = 0; i < 320; i++)
        {
            DebrisRide r = R(i);
            Assert.InRange(RadiusAt(r, 0f), 0f, Emit);
        }
    }

    [Fact]
    public void The_starting_points_fill_the_disc_rather_than_crowding_the_middle()
    {
        int outerHalf = 0;
        for (int i = 0; i < 400; i++)
            if (RadiusAt(R(i), 0f) > Emit * 0.707f) outerHalf++;
        // Half the disc's AREA lies outside 0.707 r, so about half the pieces should too.
        Assert.InRange(outerHalf, 140, 260);
    }

    [Fact]
    public void The_pieces_are_a_mix_of_sizes_and_shapes()
    {
        var variants = new System.Collections.Generic.HashSet<int>();
        float min = float.MaxValue, max = 0f;
        for (int i = 0; i < 90; i++)
        {
            DebrisRide r = R(i);
            variants.Add(r.Variant);
            min = Math.Min(min, r.Scale); max = Math.Max(max, r.Scale);
        }
        Assert.Equal(4, variants.Count);
        Assert.True(max > min * 1.8f, $"a real spread of sizes ({min:F1}..{max:F1} m)");
        // Car-sized, which is what a blast actually throws: the largest a van, the smallest a
        // slab of wall - not a bin bag and not an office block.
        Assert.InRange(min, 1.2f, 3.5f);
        Assert.InRange(max, 4f, 8f);
    }

    [Fact]
    public void A_strike_sweeps_the_same_rubble_every_time_it_is_replayed()
    {
        DebrisRide a = DebrisSweep.Deal(11, 3, Emit, Range, Carry, 5.5f, 4);
        DebrisRide b = DebrisSweep.Deal(11, 3, Emit, Range, Carry, 5.5f, 4);
        Assert.Equal(a.StartX, b.StartX, 4);
        Assert.Equal(a.Distance, b.Distance, 4);
        Assert.Equal(a.Scale, b.Scale, 4);
        Assert.NotEqual(DebrisSweep.Deal(11, 3, Emit, Range, Carry, 5.5f, 4).StartX,
                        DebrisSweep.Deal(11, 4, Emit, Range, Carry, 5.5f, 4).StartX);
    }

    [Fact]
    public void A_conventional_strike_sweeps_its_own_yard_rather_than_the_block()
    {
        float range = BlastDebris.Range(72f);   // a 1 t charge
        float carry = DebrisSweep.CarrySeconds(ShockWave.Duration(72f));
        DebrisRide r = DebrisSweep.Deal(0, 7, BlastDebris.EmitRadius(72f), range, carry,
            BlastDebris.ChunkSize(range), 4);
        Assert.InRange(RadiusAt(r, carry), 20f, 45f);
        Assert.True(DebrisSweep.HeightAt(r, carry * 0.2f) < 15f, "and stays low doing it");
    }
}
