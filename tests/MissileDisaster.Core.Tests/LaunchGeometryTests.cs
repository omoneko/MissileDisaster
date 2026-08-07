using MissileDisaster.Core;
using Xunit;

public class LaunchGeometryTests
{
    [Theory]
    [InlineData(0f, 100f, 0f, 100f)]     // north is +Z
    [InlineData(90f, 100f, 100f, 0f)]    // east is +X
    [InlineData(180f, 100f, 0f, -100f)]  // south is -Z
    [InlineData(270f, 100f, -100f, 0f)]  // west is -X
    public void BearingOffset_maps_compass_directions(float deg, float dist, float ex, float ez)
    {
        Offset2 o = LaunchGeometry.BearingOffset(deg, dist);
        Assert.Equal(ex, o.X, 3);
        Assert.Equal(ez, o.Z, 3);
    }

    [Theory]
    [InlineData(37f, 1234f)]
    [InlineData(315f, 2200f)]
    public void BearingOffset_preserves_horizontal_distance(float deg, float dist)
    {
        Offset2 o = LaunchGeometry.BearingOffset(deg, dist);
        float mag = (float)System.Math.Sqrt(o.X * o.X + o.Z * o.Z);
        Assert.Equal(dist, mag, 2);
    }
}
