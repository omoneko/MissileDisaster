using MissileDisaster.Core;
using Xunit;

public class NuclearYieldTests
{
    [Fact]
    public void Standard_kilotons_multiplier_is_one()
    {
        Assert.Equal(1f, NuclearYields.Multiplier(NuclearYields.StandardKilotons), 3);
    }

    [Fact]
    public void Multiplier_follows_cube_root_of_yield_ratio()
    {
        // The blast radius goes as the cube root of the yield: cbrt(kt/150) against the 150 kt
        // baseline.
        float expected = (float)System.Math.Pow(1000.0 / 150.0, 1.0 / 3.0);
        Assert.Equal(expected, NuclearYields.Multiplier(1000), 3);
    }

    [Fact]
    public void Multiplier_is_positive_and_monotonic_in_kilotons()
    {
        Assert.True(NuclearYields.Multiplier(1) > 0f);
        Assert.True(NuclearYields.Multiplier(50) < NuclearYields.Multiplier(500));
    }

    [Fact]
    public void Multiplier_of_nonpositive_is_zero()
    {
        Assert.Equal(0f, NuclearYields.Multiplier(0), 3);
        Assert.Equal(0f, NuclearYields.Multiplier(-10), 3);
    }
}
