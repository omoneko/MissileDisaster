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
            Assert.True(BlastDebris.Range(blast) < blast * 0.5f);
        }
    }
}
