using MissileDisaster.Core;
using Xunit;

public class InterceptorTierTests
{
    [Fact]
    public void Ordered_is_highest_band_first()
    {
        var o = InterceptorTiers.Ordered;
        Assert.Equal(3, o.Length);
        Assert.Equal(InterceptorKind.Arrow, o[0].Kind);
        Assert.Equal(InterceptorKind.Sam, o[1].Kind);
        Assert.Equal(InterceptorKind.Pac, o[2].Kind);
        Assert.True(o[0].AltitudeMin > o[1].AltitudeMin);
        Assert.True(o[1].AltitudeMin > o[2].AltitudeMin);
    }

    [Fact]
    public void Bands_are_contiguous_and_start_at_ground()
    {
        Assert.Equal(0f, InterceptorTiers.Pac.AltitudeMin, 3);
        Assert.Equal(InterceptorTiers.Pac.AltitudeMax, InterceptorTiers.Sam.AltitudeMin, 3);
        Assert.Equal(InterceptorTiers.Sam.AltitudeMax, InterceptorTiers.Arrow.AltitudeMin, 3);
    }

    [Fact]
    public void All_tiers_have_valid_chance_range_and_positive_params()
    {
        foreach (var t in InterceptorTiers.Ordered)
        {
            Assert.InRange(t.InterceptChance, 0f, 1f);
            Assert.True(t.HorizontalRange > 0f);
            Assert.True(t.CooldownSeconds > 0f);
            Assert.True(t.AltitudeMax > t.AltitudeMin);
        }
    }
}
