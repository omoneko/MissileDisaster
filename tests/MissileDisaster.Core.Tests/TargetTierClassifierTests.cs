using MissileDisaster.Core;
using Xunit;

public class TargetTierClassifierTests
{
    [Theory]
    [InlineData("Nuclear Power Plant")]
    [InlineData("Advanced Nuclear Power Plant")]
    [InlineData("12345.PAC3_Data")]
    [InlineData("THAAD Battery")]
    [InlineData("Aegis Ashore")]
    [InlineData("イージス基地")]
    public void Priority_A_targets(string name)
    {
        Assert.Equal(TargetTierClassifier.TierA, TargetTierClassifier.ClassifyByName(name));
    }

    [Theory]
    [InlineData("Airport")]
    [InlineData("International Airport")]
    [InlineData("Train Station")]
    [InlineData("Cargo Train Terminal")]
    [InlineData("Railway Depot")]
    [InlineData("Harbor")]
    [InlineData("Cargo Harbour")]
    public void Priority_B_targets(string name)
    {
        Assert.Equal(TargetTierClassifier.TierB, TargetTierClassifier.ClassifyByName(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Small House")]
    [InlineData("Bus Stop")]
    [InlineData("Elementary School")]
    public void Non_priority_returns_none(string name)
    {
        Assert.Equal(TargetTierClassifier.TierNone, TargetTierClassifier.ClassifyByName(name));
    }

    [Fact]
    public void Interceptor_and_nuclear_take_precedence_over_B()
    {
        // A のキーワードが含まれていれば B より先に A を返す。
        Assert.Equal(TargetTierClassifier.TierA, TargetTierClassifier.ClassifyByName("Nuclear Airport"));
    }

    [Fact]
    public void Case_insensitive()
    {
        Assert.Equal(TargetTierClassifier.TierA, TargetTierClassifier.ClassifyByName("nuclear power plant"));
        Assert.Equal(TargetTierClassifier.TierB, TargetTierClassifier.ClassifyByName("AIRPORT"));
    }
}
