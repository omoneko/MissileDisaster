using MissileDisaster.Core;
using Xunit;

public class TargetTierClassifierTests
{
    // 既定相当のキーワード群。
    private static readonly string[] A = TargetTierClassifier.ParseKeywords("Nuclear, PAC3, THAAD, Aegis, イージス");
    private static readonly string[] B = TargetTierClassifier.ParseKeywords("Airport, Train Station, Railway, Cargo Train, Harbor, Harbour");
    private static readonly string[] C = TargetTierClassifier.ParseKeywords("");

    [Theory]
    [InlineData("Nuclear Power Plant")]
    [InlineData("12345.PAC3_Data")]
    [InlineData("THAAD Battery")]
    [InlineData("Aegis Ashore")]
    [InlineData("イージス基地")]
    public void Priority_A_targets(string name)
    {
        Assert.Equal(TargetTierClassifier.TierA, TargetTierClassifier.Classify(name, A, B, C));
    }

    [Theory]
    [InlineData("Airport")]
    [InlineData("Train Station")]
    [InlineData("Cargo Harbour")]
    public void Priority_B_targets(string name)
    {
        Assert.Equal(TargetTierClassifier.TierB, TargetTierClassifier.Classify(name, A, B, C));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Small House")]
    [InlineData("Bus Stop")]
    public void Non_priority_returns_none(string name)
    {
        Assert.Equal(TargetTierClassifier.TierNone, TargetTierClassifier.Classify(name, A, B, C));
    }

    [Fact]
    public void A_takes_precedence_over_B()
    {
        Assert.Equal(TargetTierClassifier.TierA, TargetTierClassifier.Classify("Nuclear Airport", A, B, C));
    }

    [Fact]
    public void Case_insensitive()
    {
        Assert.Equal(TargetTierClassifier.TierA, TargetTierClassifier.Classify("nuclear power plant", A, B, C));
        Assert.Equal(TargetTierClassifier.TierB, TargetTierClassifier.Classify("AIRPORT", A, B, C));
    }

    [Fact]
    public void Custom_keyword_can_be_added_to_a_tier()
    {
        // プレイヤーが A に "Oil" を追加 → 石油系が最優先。
        string[] customA = TargetTierClassifier.ParseKeywords("Nuclear, Oil");
        Assert.Equal(TargetTierClassifier.TierA, TargetTierClassifier.Classify("Oil Industry Building", customA, B, C));
    }

    [Fact]
    public void C_tier_keywords_work_when_provided()
    {
        string[] customC = TargetTierClassifier.ParseKeywords("Casino, Stadium");
        Assert.Equal(TargetTierClassifier.TierC, TargetTierClassifier.Classify("Grand Casino", A, B, customC));
    }

    [Theory]
    [InlineData("  Oil ,  , Gas ,", new[] { "Oil", "Gas" })]
    [InlineData("", new string[0])]
    [InlineData(null, new string[0])]
    [InlineData("Single", new[] { "Single" })]
    public void ParseKeywords_trims_and_drops_empties(string csv, string[] expected)
    {
        Assert.Equal(expected, TargetTierClassifier.ParseKeywords(csv));
    }
}
