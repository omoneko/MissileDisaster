using MissileDisaster.Core;
using Xunit;

/// <summary>
/// The soft ceiling exists to bound an effect without throwing the yield away above the bound,
/// so these pin both halves of that: exact below the knee, and still strictly growing above it.
/// </summary>
public class EffectCeilingTests
{
    [Fact]
    public void Below_the_knee_it_changes_nothing()
    {
        foreach (float v in new[] { 0f, 1f, 100f, 7999f, 8000f })
        {
            Assert.Equal(v, EffectCeiling.Soft(v, 8000f, 26000f), 3);
        }
    }

    [Fact]
    public void It_never_passes_the_ceiling()
    {
        // Strictly below it across the range the effects actually ask for - a 50 Mt cap is
        // 140 km on the fit - and never above it once the exponential has underflowed.
        foreach (float v in new[] { 9000f, 40000f, 140000f, 250000f })
        {
            Assert.True(EffectCeiling.Soft(v, 8000f, 26000f) < 26000f,
                $"{v} is held under the ceiling");
        }
        Assert.True(EffectCeiling.Soft(1e30f, 8000f, 26000f) <= 26000f,
            "and never above it, however absurd the value");
    }

    [Fact]
    public void It_keeps_growing_however_far_past_the_knee_it_is_asked_for()
    {
        // This is the whole point. Under the old hard clamp a 1.2 Mt, a 10 Mt and a 50 Mt cloud
        // came out identical; each one has to be visibly larger than the last.
        float[] asked = { 9506f, 37568f, 140519f, 400000f };
        for (int i = 1; i < asked.Length; i++)
        {
            Assert.True(EffectCeiling.Soft(asked[i], 8000f, 26000f) >
                        EffectCeiling.Soft(asked[i - 1], 8000f, 26000f),
                $"{asked[i]} m draws larger than {asked[i - 1]} m");
        }
    }

    [Fact]
    public void There_is_no_kink_where_the_ceiling_takes_over()
    {
        // The curve is built so that its slope at the knee is exactly 1, which is what stops the
        // size jumping as a yield crosses it. Just above the knee it must still track the value.
        const float knee = 8000f;
        float justAbove = EffectCeiling.Soft(knee + 10f, knee, 26000f);
        Assert.InRange(justAbove, knee + 9.9f, knee + 10f);
    }

    [Fact]
    public void The_floor_overload_holds_the_bottom_end_up()
    {
        Assert.Equal(200f, EffectCeiling.Soft(5f, 200f, 8000f, 26000f), 3);
        Assert.Equal(3000f, EffectCeiling.Soft(3000f, 200f, 8000f, 26000f), 3);
    }

    [Fact]
    public void A_ceiling_at_or_below_the_knee_degenerates_to_a_hard_clamp()
    {
        Assert.Equal(100f, EffectCeiling.Soft(500f, 100f, 100f), 3);
        Assert.Equal(100f, EffectCeiling.Soft(500f, 100f, 50f), 3);
    }

    [Fact]
    public void A_negative_value_is_left_alone()
    {
        Assert.Equal(-5f, EffectCeiling.Soft(-5f, 100f, 200f), 3);
    }
}
