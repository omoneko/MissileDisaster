using System;
using MissileDisaster.Core;
using Xunit;

/// <summary>
/// The base surge: the collar of dirt that rolls out from the foot of a ground burst.
///
/// These exist because the first version was built to a Workshop request - "it should grow until
/// it subsumes the mushroom cloud" - rather than to the phenomenon, and it hid the mushroom. The
/// real thing is a doughnut: Crossroads Baker's rolled out over two and a half miles and topped
/// out around a thousand feet under a column kilometres high. So what these pin is that it stays
/// LOW and goes WIDE, and that its middle stays open.
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
        float from = GroundDust.DelaySeconds(D.RiseSeconds) + 0.2f;
        for (float t = from; t < from + growth; t += growth / 12f)
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
        float d = GroundDust.DelaySeconds(D.RiseSeconds);
        float first = R(d + growth * 0.15f) - R(d);
        float last = R(d + growth) - R(d + growth * 0.85f);
        Assert.True(first > last * 4f,
            $"the first 15% of the time covered {first:F0} m, the last {last:F0} m");
        Assert.True(GroundDust.GrowthExponent < 1f);
    }

    [Fact]
    public void It_goes_wide_and_stays_low()
    {
        // The correction. Wider than the cap - that is the surge's whole character - but nowhere
        // near its height: Baker's reached about a sixth of its column, and anything that reaches
        // the canopy is a second cloud standing in front of the one the player came to watch.
        float end = GroundDust.DelaySeconds(D.RiseSeconds) + GroundDust.GrowthSeconds(D.RiseSeconds);
        Assert.True(R(end) > D.CapRadius,
            $"it finishes {R(end):F0} m wide against a {D.CapRadius:F0} m cap");
        Assert.True(H(end) < D.CapBase * 0.5f,
            $"and only {H(end):F0} m tall, well under the {D.CapBase:F0} m cap base");
        Assert.True(R(end) > H(end) * 3f,
            $"far wider than it is tall: {R(end):F0} m wide against {H(end):F0} m tall");
    }

    [Fact]
    public void It_arrives_after_the_column_rather_than_with_it()
    {
        // Baker's began to form ten to twelve seconds in, once the plume was collapsing. Starting
        // it at t=0 made it read as part of the same puff of smoke as the stem.
        float delay = GroundDust.DelaySeconds(D.RiseSeconds);
        Assert.True(delay > 0f);
        Assert.Equal(0f, GroundDust.AlphaAt(delay * 0.5f, D.RiseSeconds), 3);
        Assert.True(GroundDust.AlphaAt(delay + 1f, D.RiseSeconds) > 0f);
    }

    [Fact]
    public void The_middle_is_left_open_so_the_stem_still_shows()
    {
        // It is a doughnut, not a filled dome. Nothing should be drawn over the column itself.
        float end = GroundDust.DelaySeconds(D.RiseSeconds) + GroundDust.GrowthSeconds(D.RiseSeconds);
        float nearest = float.MaxValue;
        for (int i = 0; i < GroundDust.PuffCount; i++)
        {
            SurgePoint p = GroundDust.At(i, 7, end, D.CapRadius, D.CloudTop, D.RiseSeconds);
            nearest = Math.Min(nearest, (float)Math.Sqrt(p.X * p.X + p.Z * p.Z));
        }
        Assert.True(nearest > D.StemRadius,
            $"the hole clears the {D.StemRadius:F0} m stem (nearest puff {nearest:F0} m)");
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
        float d = GroundDust.DelaySeconds(D.RiseSeconds);
        Assert.True(R(d) > 0f);
        Assert.True(R(d) < D.CapRadius * 0.3f, "but still small enough to be a skirt, not a collar");
    }

    [Fact]
    public void It_fades_in_and_then_thins_out_rather_than_blinking()
    {
        float growth = GroundDust.GrowthSeconds(D.RiseSeconds);
        float d = GroundDust.DelaySeconds(D.RiseSeconds);
        Assert.Equal(0f, GroundDust.AlphaAt(0f, D.RiseSeconds), 3);
        Assert.Equal(1f, GroundDust.AlphaAt(d + growth * 0.5f, D.RiseSeconds), 3);
        Assert.Equal(1f, GroundDust.AlphaAt(d + growth, D.RiseSeconds), 3);
        float total = GroundDust.TotalSeconds(D.RiseSeconds);
        Assert.InRange(GroundDust.AlphaAt(total * 0.9f, D.RiseSeconds), 0.01f, 0.4f);
        Assert.Equal(0f, GroundDust.AlphaAt(total, D.RiseSeconds), 3);
    }

    [Fact]
    public void Every_puff_stays_inside_the_dome()
    {
        float growth = GroundDust.GrowthSeconds(D.RiseSeconds);
        float d = GroundDust.DelaySeconds(D.RiseSeconds);
        for (int i = 0; i < GroundDust.PuffCount; i++)
        {
            for (float t = d; t <= d + growth; t += growth / 8f)
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
    public void The_collar_has_a_body_rather_than_being_a_wire_ring()
    {
        // A band, not a circle: puffs must fill the annulus or the surge reads as an outline.
        float end = GroundDust.DelaySeconds(D.RiseSeconds) + GroundDust.GrowthSeconds(D.RiseSeconds);
        float nearest = float.MaxValue, furthest = 0f;
        for (int i = 0; i < GroundDust.PuffCount; i++)
        {
            SurgePoint p = GroundDust.At(i, 7, end, D.CapRadius, D.CloudTop, D.RiseSeconds);
            float horizontal = (float)Math.Sqrt(p.X * p.X + p.Z * p.Z);
            nearest = Math.Min(nearest, horizontal);
            furthest = Math.Max(furthest, horizontal);
        }
        Assert.True(nearest < furthest * 0.8f,
            $"the band has width: puffs from {nearest:F0} m to {furthest:F0} m");
    }

    [Fact]
    public void It_is_dirtiest_at_the_skirt()
    {
        // It is scouring the ground down there. The crown is what has had time to thin out.
        float end = GroundDust.DelaySeconds(D.RiseSeconds) + GroundDust.GrowthSeconds(D.RiseSeconds);
        float lowDust = 0f, highDust = 0f, lowest = float.MaxValue, highest = 0f;
        for (int i = 0; i < GroundDust.PuffCount; i++)
        {
            SurgePoint p = GroundDust.At(i, 7, end, D.CapRadius, D.CloudTop, D.RiseSeconds);
            if (p.Y < lowest) { lowest = p.Y; lowDust = p.Dust; }
            if (p.Y > highest) { highest = p.Y; highDust = p.Dust; }
        }
        Assert.True(lowDust > highDust, $"skirt {lowDust:F2} against crown {highDust:F2}");
    }

    [Fact]
    public void A_strike_raises_the_same_surge_every_time_it_is_replayed()
    {
        float t = GroundDust.DelaySeconds(D.RiseSeconds) + 4f;
        SurgePoint a = GroundDust.At(9, 3, t, D.CapRadius, D.CloudTop, D.RiseSeconds);
        SurgePoint b = GroundDust.At(9, 3, t, D.CapRadius, D.CloudTop, D.RiseSeconds);
        Assert.Equal(a.X, b.X, 4);
        Assert.Equal(a.Size, b.Size, 4);
        Assert.NotEqual(a.X, GroundDust.At(9, 4, t, D.CapRadius, D.CloudTop, D.RiseSeconds).X);
    }

    [Fact]
    public void A_conventional_burst_raises_a_proportionate_one()
    {
        NuclearCloudDimensions c = ConventionalCloudDisplay.For(17f);   // a 1.5 t charge
        float growth = GroundDust.GrowthSeconds(c.RiseSeconds);
        float final = GroundDust.RadiusAt(GroundDust.DelaySeconds(c.RiseSeconds) + growth,
            c.CapRadius, c.RiseSeconds);
        Assert.True(final > c.CapRadius, "it still outgrows its own cap");
        Assert.True(final < 200f, $"but a bomb's surge is a yard of dirt, not a district ({final:F0} m)");
        Assert.True(growth >= 2f, "and lasts long enough to see");
    }
}
