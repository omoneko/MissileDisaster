using MissileDisaster.Core;
using Xunit;

public class BallisticMathTests
{
    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(1f, 1f)]
    [InlineData(2f, 1f)]
    public void Clamp01_clamps_to_unit_range(float input, float expected)
    {
        Assert.Equal(expected, BallisticMath.Clamp01(input), 5);
    }

    [Theory]
    [InlineData(0f, 100f, 0f, 0f)]
    [InlineData(0f, 100f, 1f, 100f)]
    [InlineData(0f, 100f, 0.25f, 25f)]
    [InlineData(0f, 100f, 2f, 100f)]   // clamped
    public void Lerp_interpolates_and_clamps(float a, float b, float t, float expected)
    {
        Assert.Equal(expected, BallisticMath.Lerp(a, b, t), 4);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(1f, 0f)]
    [InlineData(0.5f, 700f)]  // arcHeight at the apex
    public void ArcHeightAt_is_zero_at_ends_and_peaks_at_mid(float t, float expected)
    {
        Assert.Equal(expected, BallisticMath.ArcHeightAt(t, 700f), 3);
    }

    [Fact]
    public void ArcHeightAt_is_symmetric()
    {
        Assert.Equal(BallisticMath.ArcHeightAt(0.25f, 700f),
                     BallisticMath.ArcHeightAt(0.75f, 700f), 4);
    }

    [Fact]
    public void AdvanceT_progresses_by_speed_over_distance()
    {
        // A distance of 1000 at a speed of 500 over dt=1 advances t by 0.5.
        Assert.Equal(0.5f, BallisticMath.AdvanceT(0f, 1000f, 500f, 1f), 4);
    }

    [Fact]
    public void AdvanceT_handles_zero_distance_without_divide_by_zero()
    {
        float result = BallisticMath.AdvanceT(0.4f, 0f, 500f, 1f);
        Assert.True(result >= 1f); // zero distance counts as an immediate impact, so t reaches 1
    }
}
