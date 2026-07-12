using MissileDisaster.Core;
using Xunit;

public class WarheadSpecTests
{
    [Fact]
    public void Conventional_has_positive_crater_and_destruction()
    {
        var spec = WarheadSpec.For(WarheadType.Conventional);
        Assert.True(spec.CraterRadius > 0f);
        Assert.True(spec.CraterDepth > 0f);
        Assert.True(spec.DestructionRadius > 0f);
    }

    [Fact]
    public void Conventional_does_not_contaminate()
    {
        Assert.False(WarheadSpec.For(WarheadType.Conventional).Contaminates);
    }

    [Fact]
    public void Every_warhead_type_has_a_spec()
    {
        foreach (WarheadType t in System.Enum.GetValues(typeof(WarheadType)))
        {
            var spec = WarheadSpec.For(t);
            Assert.True(spec.DestructionRadius > 0f, $"{t} must have destruction radius");
        }
    }
}
