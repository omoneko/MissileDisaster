using System;
using MissileDisaster.Core;
using Xunit;

/// <summary>
/// The downwind drift. This was implemented once, taken back out because it had not been asked
/// for, and asked for since - so these pin what it must do rather than leaving it to be
/// re-litigated: build in rather than start at speed, lean the cloud over rather than slide it,
/// and never carry anything back the way it came.
/// </summary>
public class CloudDriftTests
{
    private const float Rise = 10f;

    [Fact]
    public void The_drift_builds_in_while_the_column_is_still_climbing()
    {
        // A column being driven up by its own buoyancy is not yet being pushed sideways. It wins
        // against the wind while it rises and loses once it stabilises.
        Assert.Equal(0f, CloudDrift.Ramp(0f, Rise), 3);
        Assert.True(CloudDrift.Ramp(Rise * 0.5f, Rise) < 0.5f, "still winning halfway up");
        Assert.Equal(1f, CloudDrift.Ramp(Rise, Rise), 3);
        Assert.Equal(1f, CloudDrift.Ramp(Rise * 3f, Rise), 3);
    }

    [Fact]
    public void The_top_of_the_cloud_drifts_further_than_its_base()
    {
        // Wind speed grows with height, and that shear is what leans a real cloud over instead
        // of sliding it sideways as one rigid shape.
        float top = CloudDrift.Offset(Rise * 2f, Rise, 1f);
        float middle = CloudDrift.Offset(Rise * 2f, Rise, 0.5f);
        float bottom = CloudDrift.Offset(Rise * 2f, Rise, 0f);
        Assert.True(top > middle && middle > bottom);
        Assert.True(bottom > 0f, "the base still moves - it is not pinned to the crater");
    }

    [Fact]
    public void Nothing_ever_drifts_back_the_way_it_came()
    {
        float last = -1f;
        for (float t = 0f; t < Rise * 6f; t += Rise / 20f)
        {
            float d = CloudDrift.Offset(t, Rise, 0.7f);
            Assert.True(d >= last, $"drift went backwards at t={t:F1}");
            last = d;
        }
    }

    [Fact]
    public void A_cloud_leans_over_its_life_without_sailing_off_the_map()
    {
        // A 150 kt cloud is up for the best part of a minute. Over that it should visibly lean
        // and travel, but it must not end up in the next district.
        NuclearCloudDimensions d = NuclearCloudDisplay.For(150f);
        float life = d.RiseSeconds + d.HoldSeconds + d.FadeSeconds;
        float travelled = CloudDrift.Offset(life, d.RiseSeconds, 1f);
        Assert.InRange(travelled, d.CapRadius * 0.3f, d.CapRadius * 3f);
    }

    [Fact]
    public void The_direction_matches_the_convention_the_game_uses()
    {
        // x = sin(theta), z = cos(theta), read out of FogEffect and
        // DayNightDynamicCloudsProperties - both convert m_windDirection exactly this way, so
        // the mod's smoke drifts with the game's own fog rather than at an angle to it.
        float x, z;
        CloudDrift.Direction(0f, out x, out z);
        Assert.Equal(0f, x, 3); Assert.Equal(1f, z, 3);
        CloudDrift.Direction(90f, out x, out z);
        Assert.Equal(1f, x, 3); Assert.Equal(0f, z, 3);
        CloudDrift.Direction(180f, out x, out z);
        Assert.Equal(0f, x, 3); Assert.Equal(-1f, z, 3);
        // And it is always a unit vector, whatever angle the weather reports.
        foreach (float deg in new[] { -270f, 37f, 400f, 1234f })
        {
            CloudDrift.Direction(deg, out x, out z);
            Assert.Equal(1f, (float)Math.Sqrt(x * x + z * z), 3);
        }
    }

    [Fact]
    public void Nothing_drifts_before_the_burst()
    {
        Assert.Equal(0f, CloudDrift.Offset(0f, Rise, 1f), 3);
        Assert.Equal(0f, CloudDrift.Offset(-3f, Rise, 1f), 3);
    }

    [Fact]
    public void A_height_outside_the_cloud_is_clamped_rather_than_extrapolated()
    {
        Assert.Equal(CloudDrift.Offset(20f, Rise, 1f), CloudDrift.Offset(20f, Rise, 4f), 3);
        Assert.Equal(CloudDrift.Offset(20f, Rise, 0f), CloudDrift.Offset(20f, Rise, -2f), 3);
    }
}
