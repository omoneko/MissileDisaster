using System;
using MissileDisaster.Core;
using Xunit;

/// <summary>
/// The rubble a blast throws. These pin the ballistics - the speed and the hang time are solved
/// from the range, so the three have to agree - and the budgets that keep a strategic warhead
/// from raining masonry across the map for a minute.
/// </summary>
public class BlastDebrisTests
{
    [Fact]
    public void The_launch_speed_actually_carries_a_piece_to_its_range()
    {
        // Integrate the throw rather than trusting the closed form: launch at the angle and the
        // speed the model gives, step it under gravity, and see where it lands.
        foreach (float blast in new[] { 72f, 500f, 3720f })
        {
            float range = BlastDebris.Range(blast);
            float speed = BlastDebris.LaunchSpeed(range);
            double theta = BlastDebris.LaunchAngleDegrees * Math.PI / 180.0;
            double vx = speed * Math.Cos(theta), vy = speed * Math.Sin(theta);

            double x = 0, y = 0, dt = 0.0005;
            while (y >= 0)
            {
                x += vx * dt;
                y += vy * dt;
                vy -= BlastDebris.Gravity * dt;
                if (x > 1e6) break;
            }
            Assert.InRange((float)x, range * 0.98f, range * 1.02f);
        }
    }

    [Fact]
    public void The_flight_time_matches_the_arc_it_was_solved_from()
    {
        float range = BlastDebris.Range(500f);
        float speed = BlastDebris.LaunchSpeed(range);
        float flight = BlastDebris.FlightSeconds(speed);
        // t = 2 v sin(theta) / g, and this throw is well under the ceiling.
        double theta = BlastDebris.LaunchAngleDegrees * Math.PI / 180.0;
        float expected = (float)(2.0 * speed * Math.Sin(theta) / BlastDebris.Gravity);
        Assert.Equal(expected, flight, 2);
        Assert.True(flight < BlastDebris.FlightSecondsMax);
    }

    [Fact]
    public void A_bigger_warhead_throws_its_rubble_further_and_in_more_pieces()
    {
        float small = BlastDebris.Range(72f);      // a 1 t charge
        float large = BlastDebris.Range(3720f);    // 150 kt
        Assert.True(large > small * 3f, "the throw grows with the blast");
        Assert.True(BlastDebris.ChunkCount(large) > BlastDebris.ChunkCount(small));
        Assert.True(BlastDebris.ChunkSize(large) > BlastDebris.ChunkSize(small));
    }

    [Fact]
    public void Nothing_is_thrown_by_a_warhead_with_no_blast()
    {
        Assert.Equal(0f, BlastDebris.Range(0f), 3);
        Assert.Equal(0f, BlastDebris.Range(-5f), 3);
        Assert.Equal(0f, BlastDebris.LaunchSpeed(0f), 3);
        Assert.Equal(0f, BlastDebris.FlightSeconds(0f), 3);
        Assert.Equal(0, BlastDebris.ChunkCount(0f));
    }

    [Fact]
    public void Even_the_smallest_charge_throws_something_visible()
    {
        // A cluster bomblet's blast radius is metres; without the floor its rubble would travel
        // less than its own chunk size and read as nothing happening.
        float range = BlastDebris.Range(18f);
        Assert.Equal(BlastDebris.RangeMin, range, 3);
        Assert.True(range > BlastDebris.ChunkSize(range) * 2f, "it clears its own wreckage");
        Assert.True(BlastDebris.ChunkCount(range) >= BlastDebris.ChunksMin);
    }

    [Fact]
    public void No_chunk_is_smaller_than_the_rubble_the_game_already_draws()
    {
        // The game's own rock props measure about 4 m on their longest axis. Anything under
        // that is smaller than the wreckage lying around the map already, and reads as grit -
        // which is why a 2.5 m floor made a conventional strike look like nothing happened.
        foreach (float blast in new[] { 18f, 72f, 180f, 3720f })
        {
            Assert.True(BlastDebris.ChunkSize(BlastDebris.Range(blast)) >= 4f);
        }
    }

    [Fact]
    public void No_chunk_grows_into_a_boulder()
    {
        // Debris size does not really scale with yield - the ceiling is a readability
        // allowance. It was raised to 34 m once the numbers showed a 14 m chunk is 4.5% of a
        // 150 kt fireball's width, but it still stops short of reading as terrain.
        Assert.True(BlastDebris.ChunkSize(BlastDebris.Range(1e6f)) <= BlastDebris.ChunkSizeMax);
    }

    [Fact]
    public void The_rubble_is_thrown_from_the_destroyed_area_not_from_a_point()
    {
        // Launching it all from ground zero is what buried it: at 150 kt the pieces left a 50 m
        // circle and spent their first seconds inside a 310 m fireball. The fireball vaporises
        // what stands at the centre anyway - the rubble comes from the ring around it.
        float emit = BlastDebris.EmitRadius(3720f);
        Assert.True(emit > 310f, $"the disc clears the 310 m fireball (got {emit:F0} m)");
        Assert.True(emit > BlastDebris.Range(3720f) * 0.5f, "and is a real area, not a nozzle");
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
    public void A_nuclear_chunk_is_big_enough_to_read_against_its_own_fireball()
    {
        // A 150 kt fireball is drawn 310 m across. Anything under a few per cent of that is a
        // pixel at the zoom a strike that size is watched from.
        float chunk = BlastDebris.ChunkSize(BlastDebris.Range(3720f));
        Assert.True(chunk / 310f > 0.08f, $"{chunk:F0} m against a 310 m fireball");
    }

    [Fact]
    public void The_longest_throw_still_lands_before_its_lifetime_runs_out()
    {
        // The effect gives a chunk FlightSeconds to live. If the real arc were longer than the
        // cap, the pieces would be destroyed in mid-air instead of landing - which is exactly
        // what a 900 m throw did, winking out 1.7 s short of the ground.
        float range = BlastDebris.Range(1e6f);
        float speed = BlastDebris.LaunchSpeed(range);
        double theta = BlastDebris.LaunchAngleDegrees * Math.PI / 180.0;
        float trueFlight = (float)(2.0 * speed * Math.Sin(theta) / BlastDebris.Gravity);
        Assert.True(trueFlight <= BlastDebris.FlightSecondsMax,
            $"the longest throw takes {trueFlight:F1} s against a {BlastDebris.FlightSecondsMax} s life");
        Assert.Equal(trueFlight, BlastDebris.FlightSeconds(speed), 2);
    }

    [Fact]
    public void A_strategic_warhead_does_not_rain_masonry_across_the_map()
    {
        float range = BlastDebris.Range(1e6f);
        Assert.Equal(BlastDebris.RangeMax, range, 3);
        Assert.InRange(BlastDebris.ChunkCount(range), BlastDebris.ChunksMin, BlastDebris.ChunksMax);
        Assert.InRange(BlastDebris.ChunkSize(range), BlastDebris.ChunkSizeMin, BlastDebris.ChunkSizeMax);
    }

    [Fact]
    public void The_hang_time_is_always_watchable()
    {
        foreach (float blast in new[] { 18f, 72f, 500f, 3720f, 25000f, 1e6f })
        {
            float flight = BlastDebris.FlightSeconds(BlastDebris.LaunchSpeed(BlastDebris.Range(blast)));
            Assert.InRange(flight, 0.5f, BlastDebris.FlightSecondsMax);
        }
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
