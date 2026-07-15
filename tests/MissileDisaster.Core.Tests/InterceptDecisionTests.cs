using MissileDisaster.Core;
using Xunit;

public class InterceptDecisionTests
{
    private static readonly InterceptorTier Sam = InterceptorTiers.Sam; // alt[800,2500) range 4000 chance 0.75

    [Theory]
    [InlineData(1500f, 1000f, true)]   // 帯内・射程内
    [InlineData(800f, 1000f, true)]    // 下端(含む)
    [InlineData(2500f, 1000f, false)]  // 上端(含まない)
    [InlineData(500f, 1000f, false)]   // 帯下
    [InlineData(1500f, 4001f, false)]  // 射程外
    [InlineData(1500f, 4000f, true)]   // 射程端(含む)
    public void InEngagementZone_checks_band_and_range(float alt, float dist, bool expected)
    {
        Assert.Equal(expected, InterceptDecision.InEngagementZone(alt, dist, Sam));
    }

    [Theory]
    [InlineData(0.0f, true)]    // roll < 0.75 → 迎撃
    [InlineData(0.74f, true)]
    [InlineData(0.75f, false)]  // roll == chance → 失敗(未満のみ成功)
    [InlineData(0.9f, false)]
    public void ShouldIntercept_rolls_within_zone(float roll, bool expected)
    {
        Assert.Equal(expected, InterceptDecision.ShouldIntercept(1500f, 1000f, Sam, roll));
    }

    [Fact]
    public void ShouldIntercept_false_outside_zone_regardless_of_roll()
    {
        Assert.False(InterceptDecision.ShouldIntercept(5000f, 1000f, Sam, 0.0f)); // 帯外
        Assert.False(InterceptDecision.ShouldIntercept(1500f, 9999f, Sam, 0.0f)); // 射程外
    }
}
