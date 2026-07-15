using MissileDisaster.Core;
using Xunit;

public class WarheadSpecTests
{
    [Fact]
    public void Every_warhead_type_has_a_spec()
    {
        foreach (WarheadType t in System.Enum.GetValues(typeof(WarheadType)))
        {
            var spec = WarheadSpec.For(t);
            Assert.True(spec.DestructionRadius > 0f, $"{t} must have destruction radius");
            Assert.True(spec.SubmunitionCount >= 1, $"{t} must have >=1 submunition");
        }
    }

    [Fact]
    public void Conventional_keeps_baseline_values()
    {
        var s = WarheadSpec.For(WarheadType.Conventional);
        Assert.Equal(WarheadType.Conventional, s.Type);
        Assert.Equal(60f, s.CraterRadius, 3);
        Assert.Equal(16f, s.CraterDepth, 3);
        Assert.Equal(120f, s.DestructionRadius, 3);
        Assert.Equal(1, s.SubmunitionCount);
        Assert.Equal(0f, s.SpreadRadius, 3);
        Assert.False(s.RaiseCraterEdges);
        Assert.False(s.Contaminates);
    }

    [Theory]
    [InlineData(WarheadType.Cluster)]
    [InlineData(WarheadType.WhitePhosphorus)]
    public void Scatter_warheads_have_multiple_submunitions_over_an_area(WarheadType type)
    {
        var s = WarheadSpec.For(type);
        Assert.Equal(type, s.Type);
        Assert.True(s.SubmunitionCount > 1, "子弾は複数");
        Assert.True(s.SpreadRadius > 0f, "散布半径は正");
        // 子弾1発あたりのクレーターは通常弾より小さい。
        Assert.True(s.CraterRadius < WarheadSpec.For(WarheadType.Conventional).CraterRadius);
    }

    [Theory]
    [InlineData(WarheadType.Thermobaric)]
    [InlineData(WarheadType.Nuclear)]
    public void Overpressure_warheads_are_single_impact_with_raised_edges(WarheadType type)
    {
        var s = WarheadSpec.For(type);
        Assert.Equal(1, s.SubmunitionCount);
        Assert.Equal(0f, s.SpreadRadius, 3);
        Assert.True(s.RaiseCraterEdges);
    }

    [Fact]
    public void Thermobaric_maximizes_destruction_radius_without_contamination()
    {
        var thermo = WarheadSpec.For(WarheadType.Thermobaric);
        var conv = WarheadSpec.For(WarheadType.Conventional);
        Assert.True(thermo.DestructionRadius > conv.DestructionRadius, "過圧は広域破壊");
        Assert.False(thermo.Contaminates);
    }

    [Fact]
    public void Nuclear_is_the_largest_and_only_contaminating_warhead()
    {
        var nuke = WarheadSpec.For(WarheadType.Nuclear);
        Assert.True(nuke.Contaminates, "核のみ汚染");
        foreach (WarheadType t in new[] { WarheadType.Conventional, WarheadType.Cluster,
            WarheadType.WhitePhosphorus, WarheadType.Thermobaric })
        {
            var other = WarheadSpec.For(t);
            Assert.False(other.Contaminates, $"{t} は汚染しない");
            Assert.True(nuke.CraterRadius > other.CraterRadius, $"核が {t} より大クレーター");
            Assert.True(nuke.DestructionRadius >= other.DestructionRadius, $"核が {t} 以上の破壊範囲");
        }
    }
}
