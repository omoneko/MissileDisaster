using MissileDisaster.Core;
using Xunit;

public class ConventionalYieldTests
{
    [Fact]
    public void Reference_kilograms_multiplier_is_one()
    {
        Assert.Equal(1f, ConventionalYields.Multiplier(ConventionalYields.ReferenceKilograms), 3);
    }

    [Fact]
    public void Multiplier_follows_cube_root_of_charge_ratio()
    {
        // The blast radius goes as the cube root of the charge, so 8000 kg against the 1000 kg
        // baseline is twice the radius.
        Assert.Equal(2f, ConventionalYields.Multiplier(8000), 3);
    }

    [Fact]
    public void Multiplier_is_positive_and_monotonic_in_kilograms()
    {
        Assert.True(ConventionalYields.Multiplier(100) > 0f);
        Assert.True(ConventionalYields.Multiplier(500) < ConventionalYields.Multiplier(5000));
    }

    [Fact]
    public void Multiplier_of_nonpositive_is_zero()
    {
        Assert.Equal(0f, ConventionalYields.Multiplier(0), 3);
        Assert.Equal(0f, ConventionalYields.Multiplier(-5), 3);
    }
}
