using System;
using MissileDisaster.Core;
using Xunit;

/// <summary>
/// How much rubble a blast moves, how big it is and how far it goes. The motion itself is
/// DebrisSweep's, and DebrisSweepTests covers it; these pin the budgets that keep a strategic
/// warhead from strewing masonry across the map, and the sizes, which are measured rather than
/// chosen. The ballistic tests that used to live here went with the arc they described.
/// </summary>
public class BlastDebrisTests
{
    [Fact]
    public void A_bigger_warhead_sweeps_its_rubble_further_and_in_more_pieces()
    {
        float small = BlastDebris.Range(72f);      // a 1 t charge
        float large = BlastDebris.Range(3720f);    // 150 kt
        Assert.True(large > small * 3f, "the sweep grows with the blast");
        Assert.True(BlastDebris.ChunkCount(large) > BlastDebris.ChunkCount(small));
        Assert.True(BlastDebris.ChunkSize(large) > BlastDebris.ChunkSize(small));
    }

    [Fact]
    public void Nothing_is_swept_by_a_warhead_with_no_blast()
    {
        Assert.Equal(0f, BlastDebris.Range(0f), 3);
        Assert.Equal(0f, BlastDebris.Range(-5f), 3);
        Assert.Equal(0, BlastDebris.ChunkCount(0f));
    }

    [Fact]
    public void Even_the_smallest_charge_moves_something_visible()
    {
        // A cluster bomblet's blast radius is metres; without the floor its rubble would travel
        // less than its own chunk size and read as nothing happening.
        float range = BlastDebris.Range(18f);
        Assert.Equal(BlastDebris.RangeMin, range, 3);
        Assert.True(range > BlastDebris.ChunkSize(range) * 2f, "it clears its own wreckage");
        Assert.True(BlastDebris.ChunkCount(range) >= BlastDebris.ChunksMin);
    }

    [Fact]
    public void Every_piece_of_rubble_is_the_size_of_a_car()
    {
        // Measured off the game's own vehicles with UnityPy: electric-car04 is 2.8 m long,
        // a sedan 4.4 m, Van_02 7.8 m. Whatever the warhead, the wreckage it throws has to sit
        // in that range - a city is not built out of anything a blast breaks into 30 m lumps.
        foreach (float blast in new[] { 18f, 72f, 180f, 3720f, 25000f, 1e6f })
        {
            float chunk = BlastDebris.ChunkSize(BlastDebris.Range(blast));
            Assert.InRange(chunk, 2.8f, 8f);
        }
    }

    [Fact]
    public void The_biggest_warhead_does_not_throw_a_bigger_piece_of_the_city()
    {
        // Debris size does not scale with yield: a warhead does not make bigger masonry, it
        // breaks more of it and throws it further. The ceiling used to be a 34 m readability
        // allowance, and it looked like office blocks being thrown whole.
        Assert.True(BlastDebris.ChunkSize(BlastDebris.Range(1e6f)) <= BlastDebris.ChunkSizeMax);
        Assert.True(BlastDebris.ChunkSizeMax <= 8f, "still a van, not a building");
    }

    [Fact]
    public void The_rubble_is_thrown_from_the_destroyed_area_not_from_a_point()
    {
        // Launching it all from ground zero is what buried it: at 150 kt the pieces left a 50 m
        // circle and spent their first seconds inside the fireball, which vaporises what stands
        // at the centre anyway - the rubble comes from the ring around it.
        //
        // The figure to clear is the fireball's RADIUS. At 150 kt that is 155 m: the real
        // 55 W^0.4 radius is 408 m and NuclearCloudDisplay draws it at FireballScale 0.38. This
        // test used to demand 310 m, which is the width, and the disc that satisfied it was
        // 1116 m across - so wide that car-sized rubble scattered over it disappeared.
        float emit = BlastDebris.EmitRadius(3720f);
        Assert.True(emit > 155f, $"the disc clears the 155 m fireball radius (got {emit:F0} m)");
        Assert.True(emit < 155f * 3f, $"and no wider than it has to be (got {emit:F0} m)");
        Assert.True(emit > BlastDebris.EmitRadiusMin, "and is a real area, not a nozzle");
    }

    [Fact]
    public void The_emit_disc_stays_inside_what_the_warhead_destroyed()
    {
        // Rubble launching from where the buildings are still standing would read as wrong.
        foreach (float blast in new[] { 72f, 500f, 3720f, 1e6f })
        {
            Assert.True(BlastDebris.EmitRadius(blast) <= blast * 0.5f + 12f);
        }
    }

    [Fact]
    public void Even_a_tiny_blast_throws_from_a_patch_rather_than_a_pinpoint()
    {
        Assert.Equal(BlastDebris.EmitRadiusMin, BlastDebris.EmitRadius(1f), 3);
        Assert.Equal(0f, BlastDebris.EmitRadius(0f), 3);
    }

    [Fact]
    public void What_reads_at_nuclear_scale_is_the_count_not_the_piece()
    {
        // A car-sized chunk is under 2% of a 150 kt fireball's 310 m width, so no single piece
        // can carry the effect at that zoom - and the size must not be inflated until one can,
        // because that is what turned the rubble into flying office blocks. The mass of the
        // spray carries it instead: hundreds of pieces over a 400 m sweep.
        float range = BlastDebris.Range(3720f);
        Assert.True(BlastDebris.ChunkSize(range) < 310f * 0.03f, "no piece pretends to be a building");
        Assert.True(BlastDebris.ChunkCount(range) >= 400,
            $"the spray has to carry it instead (got {BlastDebris.ChunkCount(range)} pieces)");
    }

    [Fact]
    public void A_strategic_warhead_does_not_strew_masonry_across_the_map()
    {
        float range = BlastDebris.Range(1e6f);
        Assert.Equal(BlastDebris.RangeMax, range, 3);
        Assert.InRange(BlastDebris.ChunkCount(range), BlastDebris.ChunksMin, BlastDebris.ChunksMax);
        Assert.InRange(BlastDebris.ChunkSize(range), BlastDebris.ChunkSizeMin, BlastDebris.ChunkSizeMax);
    }

    [Fact]
    public void The_rubble_lands_well_inside_what_the_warhead_destroyed()
    {
        // Rubble raining down beyond the damage would read as wrong - out there the buildings
        // are still standing.
        foreach (float blast in new[] { 72f, 500f, 3720f })
        {
            Assert.True(BlastDebris.Range(blast) <= blast * 0.5f);
        }
    }
}
