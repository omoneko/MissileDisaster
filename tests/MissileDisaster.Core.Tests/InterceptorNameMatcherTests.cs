using MissileDisaster.Core;
using Xunit;

public class InterceptorNameMatcherTests
{
    [Theory]
    [InlineData("PAC3", InterceptorKind.Pac)]
    [InlineData("pac3", InterceptorKind.Pac)]
    [InlineData("THAAD", InterceptorKind.Sam)]
    [InlineData("thaad", InterceptorKind.Sam)]
    [InlineData("Aegis", InterceptorKind.Arrow)]
    [InlineData("aegis", InterceptorKind.Arrow)]
    // Japanese for "Aegis"; the matcher has to recognise assets named in other languages.
    [InlineData("\u30a4\u30fc\u30b8\u30b9", InterceptorKind.Arrow)]
    public void TryMatchTier_matches_exact_asset_names_case_insensitive(string name, InterceptorKind expected)
    {
        InterceptorKind kind;
        bool matched = InterceptorNameMatcher.TryMatchTier(name, out kind);

        Assert.True(matched);
        Assert.Equal(expected, kind);
    }

    [Theory]
    [InlineData("123456789.PAC3_Data", InterceptorKind.Pac)]
    [InlineData("987654321.THAAD_Data", InterceptorKind.Sam)]
    [InlineData("555.Aegis Ashore_Data", InterceptorKind.Arrow)]
    public void TryMatchTier_matches_workshop_style_prefixed_suffixed_names(string name, InterceptorKind expected)
    {
        InterceptorKind kind;
        bool matched = InterceptorNameMatcher.TryMatchTier(name, out kind);

        Assert.True(matched);
        Assert.Equal(expected, kind);
    }

    [Theory]
    [InlineData("Wind Turbine")]
    [InlineData("Fire House")]
    [InlineData("")]
    [InlineData(null)]
    public void TryMatchTier_returns_false_for_unrelated_or_empty_names(string name)
    {
        InterceptorKind kind;
        bool matched = InterceptorNameMatcher.TryMatchTier(name, out kind);

        Assert.False(matched);
    }

    [Fact]
    public void TryMatchTier_prefers_Aegis_over_other_keywords_when_ambiguous()
    {
        // "Aegis THAAD Hybrid" contains both keywords, but the order - Aegis, then THAAD, then
        // PAC3 - means it comes back as Arrow.
        InterceptorKind kind;
        bool matched = InterceptorNameMatcher.TryMatchTier("Aegis THAAD Hybrid", out kind);

        Assert.True(matched);
        Assert.Equal(InterceptorKind.Arrow, kind);
    }

    [Theory]
    [InlineData("Radar Site")]
    [InlineData("radar site")]
    // Japanese for "radar site".
    [InlineData("\u30ec\u30fc\u30c0\u30fc\u30b5\u30a4\u30c8")]
    [InlineData("111.Radar_Data")]
    public void IsRadar_matches_radar_asset_names_case_insensitive(string name)
    {
        Assert.True(InterceptorNameMatcher.IsRadar(name));
    }

    [Theory]
    [InlineData("PAC3")]
    [InlineData("Wind Turbine")]
    [InlineData("")]
    [InlineData(null)]
    public void IsRadar_returns_false_for_non_radar_names(string name)
    {
        Assert.False(InterceptorNameMatcher.IsRadar(name));
    }
}
