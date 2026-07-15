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
    public void Conventional_is_a_single_realistic_high_explosive_impact()
    {
        var s = WarheadSpec.For(WarheadType.Conventional);
        Assert.Equal(WarheadType.Conventional, s.Type);
        Assert.Equal(1, s.SubmunitionCount);
        Assert.Equal(0f, s.SpreadRadius, 3);
        Assert.False(s.RaiseCraterEdges);
        Assert.False(s.Contaminates);
        // 大型 HE 弾頭 ~1t 相当の実被害規模（核より桁違いに小さい）。
        Assert.InRange(s.DestructionRadius, 40f, 150f);
        Assert.True(s.CraterRadius > 0f && s.CraterRadius < 30f);
    }

    [Fact]
    public void Nuclear_damage_dwarfs_conventional_using_real_radii()
    {
        var conv = WarheadSpec.For(WarheadType.Conventional);
        var nuke = WarheadSpec.For(WarheadType.Nuclear);
        // 150kt 基準の 5psi 実半径 ~3.7km は通常弾の数十倍。
        Assert.True(nuke.DestructionRadius > conv.DestructionRadius * 20f, "核は通常弾の20倍超の破壊半径");
        Assert.True(nuke.BurnRadius > nuke.DestructionRadius, "熱線/延焼は破壊より広い");
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
    public void Groundburst_leaves_spec_unchanged()
    {
        var s = WarheadSpec.For(WarheadType.Nuclear);
        var g = s.WithBurst(BurstType.Groundburst);
        Assert.Equal(s.CraterRadius, g.CraterRadius, 3);
        Assert.Equal(s.ContaminationRadius, g.ContaminationRadius, 3);
        Assert.Equal(s.Contaminates, g.Contaminates);
        Assert.Equal(s.DestructionRadius, g.DestructionRadius, 3);
        Assert.Equal(s.BurnRadius, g.BurnRadius, 3);
    }

    [Fact]
    public void Airburst_removes_crater_and_contamination_and_widens_blast()
    {
        var s = WarheadSpec.For(WarheadType.Nuclear);
        var a = s.WithBurst(BurstType.Airburst);
        Assert.Equal(0f, a.CraterRadius, 3);
        Assert.Equal(0f, a.CraterDepth, 3);
        Assert.False(a.RaiseCraterEdges);
        Assert.False(a.Contaminates, "空中爆発は降下物ほぼ無し");
        Assert.Equal(0f, a.ContaminationRadius, 3);
        Assert.True(a.DestructionRadius > s.DestructionRadius, "空中爆発は破壊が広がる");
        Assert.True(a.BurnRadius > s.BurnRadius, "空中爆発は延焼が広がる");
    }

    [Fact]
    public void WithBurst_does_not_mutate_the_original()
    {
        var s = WarheadSpec.For(WarheadType.Nuclear);
        float beforeCrater = s.CraterRadius;
        bool beforeContam = s.Contaminates;
        s.WithBurst(BurstType.Airburst);
        Assert.Equal(beforeCrater, s.CraterRadius, 3);
        Assert.Equal(beforeContam, s.Contaminates);
    }

    [Fact]
    public void Scaled_by_one_is_unchanged()
    {
        var s = WarheadSpec.For(WarheadType.Nuclear);
        var scaled = s.Scaled(1f);
        Assert.Equal(s.CraterRadius, scaled.CraterRadius, 3);
        Assert.Equal(s.DestructionRadius, scaled.DestructionRadius, 3);
        Assert.Equal(s.BurnRadius, scaled.BurnRadius, 3);
        Assert.Equal(s.ContaminationRadius, scaled.ContaminationRadius, 3);
    }

    [Fact]
    public void Scaled_multiplies_all_radii_but_keeps_flags_and_type()
    {
        var s = WarheadSpec.For(WarheadType.Nuclear);
        var scaled = s.Scaled(2f);
        Assert.Equal(s.CraterRadius * 2f, scaled.CraterRadius, 3);
        Assert.Equal(s.CraterDepth * 2f, scaled.CraterDepth, 3);
        Assert.Equal(s.DestructionRadius * 2f, scaled.DestructionRadius, 3);
        Assert.Equal(s.BurnRadius * 2f, scaled.BurnRadius, 3);
        Assert.Equal(s.ContaminationRadius * 2f, scaled.ContaminationRadius, 3);
        Assert.Equal(s.SubmunitionCount, scaled.SubmunitionCount);
        Assert.Equal(s.RaiseCraterEdges, scaled.RaiseCraterEdges);
        Assert.Equal(s.Contaminates, scaled.Contaminates);
        Assert.Equal(s.Type, scaled.Type);
    }

    [Fact]
    public void Scaled_does_not_mutate_the_original()
    {
        var s = WarheadSpec.For(WarheadType.Nuclear);
        float before = s.CraterRadius;
        s.Scaled(3f);
        Assert.Equal(before, s.CraterRadius, 3);
    }

    [Fact]
    public void All_warheads_have_nonnegative_burn_radius()
    {
        foreach (WarheadType t in System.Enum.GetValues(typeof(WarheadType)))
        {
            Assert.True(WarheadSpec.For(t).BurnRadius >= 0f, $"{t} の延焼半径は非負");
        }
    }

    [Fact]
    public void WhitePhosphorus_is_incendiary_burn_exceeds_destruction()
    {
        var wp = WarheadSpec.For(WarheadType.WhitePhosphorus);
        Assert.True(wp.BurnRadius > wp.DestructionRadius, "焼夷弾は延焼が破壊を上回る");
    }

    [Fact]
    public void Thermobaric_and_nuclear_burn_wider_than_conventional()
    {
        float conv = WarheadSpec.For(WarheadType.Conventional).BurnRadius;
        Assert.True(WarheadSpec.For(WarheadType.Thermobaric).BurnRadius > conv);
        Assert.True(WarheadSpec.For(WarheadType.Nuclear).BurnRadius > conv);
    }

    [Fact]
    public void Nuclear_has_the_largest_burn_radius()
    {
        float nuke = WarheadSpec.For(WarheadType.Nuclear).BurnRadius;
        foreach (WarheadType t in new[] { WarheadType.Conventional, WarheadType.Cluster,
            WarheadType.WhitePhosphorus, WarheadType.Thermobaric })
        {
            Assert.True(nuke > WarheadSpec.For(t).BurnRadius, $"核が {t} より広い延焼");
        }
    }

    [Fact]
    public void Only_nuclear_has_a_positive_contamination_radius()
    {
        Assert.True(WarheadSpec.For(WarheadType.Nuclear).ContaminationRadius > 0f, "核は汚染半径>0");
        foreach (WarheadType t in new[] { WarheadType.Conventional, WarheadType.Cluster,
            WarheadType.WhitePhosphorus, WarheadType.Thermobaric })
        {
            Assert.Equal(0f, WarheadSpec.For(t).ContaminationRadius, 3);
        }
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
