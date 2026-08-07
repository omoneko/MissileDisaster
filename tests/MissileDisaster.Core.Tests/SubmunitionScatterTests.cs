using MissileDisaster.Core;
using Xunit;

public class SubmunitionScatterTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void One_or_fewer_yields_a_single_origin_point(int count)
    {
        Offset2[] pts = SubmunitionScatter.Offsets(count, 100f);
        Assert.Single(pts);
        Assert.Equal(0f, pts[0].X, 3);
        Assert.Equal(0f, pts[0].Z, 3);
    }

    [Fact]
    public void Count_matches_requested_submunitions()
    {
        Assert.Equal(9, SubmunitionScatter.Offsets(9, 160f).Length);
    }

    [Fact]
    public void All_points_lie_within_spread_radius()
    {
        const float spread = 160f;
        Offset2[] pts = SubmunitionScatter.Offsets(12, spread);
        foreach (Offset2 p in pts)
        {
            float r = (float)System.Math.Sqrt(p.X * p.X + p.Z * p.Z);
            Assert.True(r <= spread + 1e-3f, $"a point fell outside the scatter radius: r={r}");
        }
    }

    [Fact]
    public void Zero_spread_keeps_all_points_at_origin()
    {
        Offset2[] pts = SubmunitionScatter.Offsets(8, 0f);
        Assert.Equal(8, pts.Length);
        foreach (Offset2 p in pts)
        {
            Assert.Equal(0f, p.X, 3);
            Assert.Equal(0f, p.Z, 3);
        }
    }

    [Fact]
    public void Deterministic_same_input_same_output()
    {
        Offset2[] a = SubmunitionScatter.Offsets(10, 120f);
        Offset2[] b = SubmunitionScatter.Offsets(10, 120f);
        Assert.Equal(a.Length, b.Length);
        for (int i = 0; i < a.Length; i++)
        {
            Assert.Equal(a[i].X, b[i].X, 5);
            Assert.Equal(a[i].Z, b[i].Z, 5);
        }
    }

    [Fact]
    public void Points_are_distinct_and_spread_out()
    {
        Offset2[] pts = SubmunitionScatter.Offsets(9, 160f);
        // The outer points at least are well away from the origin, so they have not all
        // collapsed to the centre.
        float maxR = 0f;
        foreach (Offset2 p in pts)
        {
            float r = (float)System.Math.Sqrt(p.X * p.X + p.Z * p.Z);
            if (r > maxR) maxR = r;
        }
        Assert.True(maxR > 80f, $"the scatter is too tight: maxR={maxR}");
    }
}
