using MissileDisaster.Core;
using Xunit;

public class InterceptDecisionTests
{
    private static readonly InterceptorTier Sam = InterceptorTiers.Sam; // alt[800,2500) range 4000 chance 0.75

    [Theory]
    [InlineData(1500f, 1000f, true)]   // inside the band and in range
    [InlineData(800f, 1000f, true)]    // the lower bound, which is inclusive
    [InlineData(2500f, 1000f, false)]  // the upper bound, which is exclusive
    [InlineData(500f, 1000f, false)]   // below the band
    [InlineData(1500f, 4001f, false)]  // out of range
    [InlineData(1500f, 4000f, true)]   // exactly at the range limit, which is inclusive
    public void InEngagementZone_checks_band_and_range(float alt, float dist, bool expected)
    {
        Assert.Equal(expected, InterceptDecision.InEngagementZone(alt, dist, Sam));
    }

    [Theory]
    [InlineData(0.0f, true)]    // a roll under 0.75 intercepts
    [InlineData(0.74f, true)]
    [InlineData(0.75f, false)]  // a roll equal to the chance fails; only under it succeeds
    [InlineData(0.9f, false)]
    public void ShouldIntercept_rolls_within_zone(float roll, bool expected)
    {
        Assert.Equal(expected, InterceptDecision.ShouldIntercept(1500f, 1000f, Sam, roll));
    }

    [Fact]
    public void ShouldIntercept_false_outside_zone_regardless_of_roll()
    {
        Assert.False(InterceptDecision.ShouldIntercept(5000f, 1000f, Sam, 0.0f)); // outside the band
        Assert.False(InterceptDecision.ShouldIntercept(1500f, 9999f, Sam, 0.0f)); // out of range
    }
}
