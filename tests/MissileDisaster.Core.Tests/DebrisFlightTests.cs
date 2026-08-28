using System;
using MissileDisaster.Core;
using Xunit;

/// <summary>
/// The arc each chunk flies. These exist because the effect failed twice in ways that could not
/// be questioned from the outside: a chunk whose position is computed here either goes where the
/// physics says or it does not, and a test can say which.
/// </summary>
public class DebrisFlightTests
{
    private static DebrisLaunch L(int i) =>
        DebrisFlight.Launch(i, 7, emitRadius: 300f, speed: 82f,
            chunkSize: BlastDebris.ChunkSizeMax, variants: 4);

    [Fact]
    public void Every_chunk_starts_somewhere_on_the_destroyed_disc()
    {
        for (int i = 0; i < 200; i++)
        {
            DebrisLaunch l = L(i);
            float r = (float)Math.Sqrt(l.X * l.X + l.Z * l.Z);
            Assert.InRange(r, 0f, 300f);
            Assert.Equal(0f, l.Y, 3);
        }
    }

    [Fact]
    public void The_launch_points_fill_the_disc_rather_than_crowding_the_middle()
    {
        // sqrt on the radius roll is what spreads them evenly by area. Without it half the
        // rubble would come off the innermost quarter of the disc - which is inside the fireball.
        int outerHalf = 0;
        for (int i = 0; i < 400; i++)
        {
            DebrisLaunch l = L(i);
            if (Math.Sqrt(l.X * l.X + l.Z * l.Z) > 300f * 0.707f) outerHalf++;
        }
        // Half the disc's AREA lies outside 0.707 r, so about half the chunks should too.
        Assert.InRange(outerHalf, 140, 260);
    }

    [Fact]
    public void Every_chunk_is_thrown_outward_and_upward()
    {
        // Outward from ground zero is what makes it read as thrown by the blast rather than as
        // falling out of the sky.
        for (int i = 0; i < 200; i++)
        {
            DebrisLaunch l = L(i);
            Assert.True(l.VY > 0f, "it goes up");
            float outward = l.X * l.VX + l.Z * l.VZ; // dot of position and velocity, both radial
            if (Math.Sqrt(l.X * l.X + l.Z * l.Z) > 1f)
            {
                Assert.True(outward > 0f, $"chunk {i} is thrown outward, not inward");
            }
        }
    }

    [Fact]
    public void A_chunk_rises_then_falls_and_is_back_at_the_ground_when_its_flight_ends()
    {
        DebrisLaunch l = L(3);
        float flight = DebrisFlight.FlightSeconds(l);
        Assert.True(flight > 0.5f);

        float x, y, z;
        DebrisFlight.PositionAt(l, flight * 0.5f, out x, out y, out z);
        float apex = y;
        Assert.True(apex > 0f, "it is in the air halfway through");

        DebrisFlight.PositionAt(l, flight, out x, out y, out z);
        Assert.InRange(y, -0.5f, 0.5f); // back where it started, height-wise
    }

    [Fact]
    public void It_keeps_travelling_outward_for_the_whole_flight()
    {
        // Drag bleeds the speed, but never enough to stop it or send it back.
        DebrisLaunch l = L(11);
        float flight = DebrisFlight.FlightSeconds(l);
        float last = -1f;
        for (int k = 0; k <= 20; k++)
        {
            float x, y, z;
            DebrisFlight.PositionAt(l, flight * k / 20f, out x, out y, out z);
            float r = (float)Math.Sqrt(x * x + z * z);
            Assert.True(r >= last - 0.01f, "the chunk never travels back toward the blast");
            last = r;
        }
    }

    [Fact]
    public void Drag_shortens_the_throw_without_killing_it()
    {
        DebrisLaunch l = L(5);
        float flight = DebrisFlight.FlightSeconds(l);
        float x, y, z;
        DebrisFlight.PositionAt(l, flight, out x, out y, out z);
        float travelled = (float)Math.Sqrt((x - l.X) * (x - l.X) + (z - l.Z) * (z - l.Z));

        float horizontal = (float)Math.Sqrt(l.VX * l.VX + l.VZ * l.VZ);
        float vacuum = horizontal * flight;
        Assert.True(travelled < vacuum, "drag costs it something");
        Assert.True(travelled > vacuum * 0.5f, "but it still goes most of the way");
    }

    [Fact]
    public void The_chunks_are_a_mix_of_sizes_and_shapes()
    {
        var variants = new System.Collections.Generic.HashSet<int>();
        float min = float.MaxValue, max = 0f;
        for (int i = 0; i < 90; i++)
        {
            DebrisLaunch l = L(i);
            variants.Add(l.Variant);
            if (l.Scale < min) min = l.Scale;
            if (l.Scale > max) max = l.Scale;
        }
        Assert.Equal(4, variants.Count);
        Assert.True(max > min * 1.8f, $"a real spread of sizes ({min:F1}..{max:F1} m)");
        Assert.InRange(max, 0f, BlastDebris.ChunkSizeMax); // never larger than it was asked for
        // Car-sized, which is what a blast actually throws. The whole spread has to stay there:
        // the largest piece a van, the smallest a slab of wall - not a bin bag.
        Assert.InRange(min, 1.2f, 3.5f);
        Assert.InRange(max, 4f, 8f);
    }

    [Fact]
    public void Every_chunk_tumbles()
    {
        for (int i = 0; i < 50; i++)
        {
            DebrisLaunch l = L(i);
            float spin = Math.Abs(l.SpinX) + Math.Abs(l.SpinY) + Math.Abs(l.SpinZ);
            Assert.True(spin > 10f, $"chunk {i} is not a frozen brick");
        }
    }

    [Fact]
    public void The_same_strike_throws_the_same_rubble_twice()
    {
        DebrisLaunch a = DebrisFlight.Launch(4, 99, 300f, 82f, 20f, 4);
        DebrisLaunch b = DebrisFlight.Launch(4, 99, 300f, 82f, 20f, 4);
        Assert.Equal(a.VX, b.VX, 4);
        Assert.Equal(a.Scale, b.Scale, 4);
        Assert.Equal(a.Variant, b.Variant);
    }
}
