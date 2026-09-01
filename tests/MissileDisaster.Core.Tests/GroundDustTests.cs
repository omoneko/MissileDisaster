using System;
using MissileDisaster.Core;
using Xunit;

/// <summary>
/// The base surge: the dome of dirt that rolls out from the foot of a ground burst and grows
/// until it swallows the mushroom above it. These pin the shape that was asked for - out and up
/// at once, faster at first, slower than the cloud, and eventually bigger than it.
/// </summary>
public class GroundDustTests
{
    private static readonly NuclearCloudDimensions D = NuclearCloudDisplay.For(150f);

    private static float R(float t) => GroundDust.RadiusAt(t, D.CapRadius, D.RiseSeconds);
    private static float H(float t) => GroundDust.HeightAt(t, D.CloudTop, D.RiseSeconds);

    [Fact]
    public void It_grows_outward_and_upward_at_the_same_time()
    {
        // Not a spread followed by a lift: both must be moving from the first second. That is
        // the whole difference between a dome rolling off the ground and a puff of smoke.
        float growth = GroundDust.GrowthSeconds(D.RiseSeconds);
        for (float t = 0.2f; t < growth; t += growth / 12f)
        {
            Assert.True(R(t + growth / 24f) > R(t), $"still spreading at t={t:F1}");
            Assert.True(H(t + growth / 24f) > H(t), $"still climbing at t={t:F1}");
        }
    }

    [Fact]
    public void It_leaps_out_and_then_creeps()
    {
        // "Fast as it currently does, but much slower after a second or two once the mushroom
        // peaks" - which is what an exponent under one gives.
        float growth = GroundDust.GrowthSeconds(D.RiseSeconds);
        float first = R(growth * 0.15f) - R(0f);
        float last = R(growth) - R(growth * 0.85f);
        Assert.True(first > last * 4f,
            $"the first 15% of the time covered {first:F0} m, the last {last:F0} m");
        Assert.True(GroundDust.GrowthExponent < 1f);
    }

    [Fact]
    public void It_ends_up_bigger_than_the_mushroom_that_raised_it()
    {
        float growth = GroundDust.GrowthSeconds(D.RiseSeconds);
        Assert.True(R(growth) > D.CapRadius,
            $"the dome finishes {R(growth):F0} m wide against a {D.CapRadius:F0} m cap");
        Assert.True(H(growth) > D.CapBase * 0.5f, "and climbs into the underside of the cap");
    }

    [Fact]
    public void It_is_slower_than_the_cloud_it_stands_under()
    {
        // If the two moved on the same clock they would read as one object being scaled up.
        Assert.True(GroundDust.GrowthSeconds(D.RiseSeconds) > D.RiseSeconds * 2f);
        Assert.True(GroundDust.TotalSeconds(D.RiseSeconds) > D.RiseSeconds);
    }

    [Fact]
    public void It_starts_as_a_ring_rather_than_from_a_point()
    {
        // Born already outside the fireball, so it is never drawn inside a brighter object.
        Assert.True(R(0f) > 0f);
        Assert.True(R(0f) < D.CapRadius * 0.3f, "but still small enough to be a skirt, not a dome");
    }

    [Fact]
    public void It_fades_in_and_then_thins_out_rather_than_blinking()
    {
        float growth = GroundDust.GrowthSeconds(D.RiseSeconds);
        Assert.Equal(0f, GroundDust.AlphaAt(0f, D.RiseSeconds), 3);
        Assert.Equal(1f, GroundDust.AlphaAt(growth * 0.5f, D.RiseSeconds), 3);
        Assert.Equal(1f, GroundDust.AlphaAt(growth, D.RiseSeconds), 3);
        float total = GroundDust.TotalSeconds(D.RiseSeconds);
        Assert.InRange(GroundDust.AlphaAt(total * 0.9f, D.RiseSeconds), 0.01f, 0.4f);
        Assert.Equal(0f, GroundDust.AlphaAt(total, D.RiseSeconds), 3);
    }

    [Fact]
    public void Every_puff_stays_inside_the_dome()
    {
        float growth = GroundDust.GrowthSeconds(D.RiseSeconds);
        for (int i = 0; i < GroundDust.PuffCount; i++)
        {
            for (float t = 0f; t <= growth; t += growth / 8f)
            {
                SurgePoint p = GroundDust.At(i, 7, t, D.CapRadius, D.CloudTop, D.RiseSeconds);
                float horizontal = (float)Math.Sqrt(p.X * p.X + p.Z * p.Z);
                Assert.True(horizontal <= R(t) * 1.1f + 1f,
                    $"puff {i} at t={t:F1} is {horizontal:F0} m out against a {R(t):F0} m dome");
                Assert.InRange(p.Y, 0f, H(t) * 1.15f + 1f);   // and never under the ground
            }
        }
    }

    [Fact]
    public void The_dome_has_a_body_rather_than_being_a_shell()
    {
        // Puffs must fill the outer part of the dome, not sit on its surface, or it reads as a
        // soap bubble at any camera angle that can see through the edge.
        float growth = GroundDust.GrowthSeconds(D.RiseSeconds);
        float nearest = float.MaxValue, furthest = 0f;
        for (int i = 0; i < GroundDust.PuffCount; i++)
        {
            SurgePoint p = GroundDust.At(i, 7, growth, D.CapRadius, D.CloudTop, D.RiseSeconds);
            float horizontal = (float)Math.Sqrt(p.X * p.X + p.Z * p.Z);
            nearest = Math.Min(nearest, horizontal);
            furthest = Math.Max(furthest, horizontal);
        }
        Assert.True(nearest < furthest * 0.6f,
            $"the shell has depth: puffs from {nearest:F0} m to {furthest:F0} m");
    }

    [Fact]
    public void It_is_dirtiest_at_the_skirt()
    {
        // It is scouring the ground down there. The crown is what has had time to thin out.
        float growth = GroundDust.GrowthSeconds(D.RiseSeconds);
        float lowDust = 0f, highDust = 0f, lowest = float.MaxValue, highest = 0f;
        for (int i = 0; i < GroundDust.PuffCount; i++)
        {
            SurgePoint p = GroundDust.At(i, 7, growth, D.CapRadius, D.CloudTop, D.RiseSeconds);
            if (p.Y < lowest) { lowest = p.Y; lowDust = p.Dust; }
            if (p.Y > highest) { highest = p.Y; highDust = p.Dust; }
        }
        Assert.True(lowDust > highDust, $"skirt {lowDust:F2} against crown {highDust:F2}");
    }

    [Fact]
    public void A_strike_raises_the_same_surge_every_time_it_is_replayed()
    {
        SurgePoint a = GroundDust.At(9, 3, 4f, D.CapRadius, D.CloudTop, D.RiseSeconds);
        SurgePoint b = GroundDust.At(9, 3, 4f, D.CapRadius, D.CloudTop, D.RiseSeconds);
        Assert.Equal(a.X, b.X, 4);
        Assert.Equal(a.Size, b.Size, 4);
        Assert.NotEqual(a.X, GroundDust.At(9, 4, 4f, D.CapRadius, D.CloudTop, D.RiseSeconds).X);
    }

    [Fact]
    public void A_conventional_burst_raises_a_proportionate_one()
    {
        NuclearCloudDimensions c = ConventionalCloudDisplay.For(17f);   // a 1.5 t charge
        float growth = GroundDust.GrowthSeconds(c.RiseSeconds);
        float final = GroundDust.RadiusAt(growth, c.CapRadius, c.RiseSeconds);
        Assert.True(final > c.CapRadius, "it still outgrows its own cap");
        Assert.True(final < 200f, $"but a bomb's surge is a yard of dirt, not a district ({final:F0} m)");
        Assert.True(growth >= 2f, "and lasts long enough to see");
    }
}
