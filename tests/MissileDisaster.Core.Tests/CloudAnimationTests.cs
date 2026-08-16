using MissileDisaster.Core;
using Xunit;

public class CloudAnimationTests
{
    private const float Rise = 8f, Hold = 10f, Fade = 6f;

    [Fact]
    public void The_cloud_is_born_small_enough_to_hide_inside_the_fireball()
    {
        var s = CloudAnimation.At(0f, Rise, Hold, Fade);
        Assert.Equal(CloudAnimation.BirthFraction, s.HeightFraction, 3);
        Assert.Equal(CloudAnimation.BirthFraction, s.WidthFraction, 3);
        Assert.False(s.Finished);
    }

    [Fact]
    public void The_growth_is_monotonic_and_reaches_full_size_by_the_end_of_the_rise()
    {
        float lastH = 0f, lastW = 0f;
        for (int i = 0; i <= 20; i++)
        {
            var s = CloudAnimation.At(Rise * i / 20f, Rise, Hold, Fade);
            Assert.True(s.HeightFraction >= lastH, "the cloud never shrinks while it is rising");
            Assert.True(s.WidthFraction >= lastW, "nor does its cap");
            lastH = s.HeightFraction;
            lastW = s.WidthFraction;
        }
        Assert.InRange(lastH, 0.999f, 1.001f);
        Assert.InRange(lastW, 0.999f, 1.001f);
    }

    [Fact]
    public void The_column_shoots_up_before_the_cap_billows()
    {
        // Halfway through the rise the height must lead the width - that order, column first
        // and canopy after, is what every photograph shows.
        var s = CloudAnimation.At(Rise * 0.4f, Rise, Hold, Fade);
        Assert.True(s.HeightFraction > s.WidthFraction,
            "the height leads the width through the climb");
    }

    [Fact]
    public void The_growth_is_front_loaded_but_does_not_leap()
    {
        // Both halves of this matter and they pull against each other.
        //
        // It is an ease-out, because a real cloud rises fastest through its first seconds and
        // settles asymptotically - so by half time it must be past where a constant climb would
        // have it. But it was a cube, which put it over 85% up at half time and two thirds up
        // inside the first third, and that was reported as the column being pushed into the sky
        // rather than climbing it.
        float linear = CloudAnimation.BirthFraction
            + (1f - CloudAnimation.BirthFraction) * 0.5f;
        var half = CloudAnimation.At(Rise * 0.5f, Rise, Hold, Fade);

        Assert.True(half.HeightFraction > linear, "the climb has stopped easing out at all");
        Assert.InRange(half.HeightFraction, 0.65f, 0.82f);

        // And the early third, which is where the old curve did its damage.
        var third = CloudAnimation.At(Rise / 3f, Rise, Hold, Fade);
        Assert.True(third.HeightFraction < 0.62f,
            "still most of the way up inside the first third: " + third.HeightFraction);
    }

    [Fact]
    public void It_stands_at_full_size_and_full_alpha_through_the_hold()
    {
        var s = CloudAnimation.At(Rise + Hold * 0.5f, Rise, Hold, Fade);
        Assert.InRange(s.HeightFraction, 0.999f, 1.001f);
        Assert.Equal(1f, s.Alpha, 3);
        Assert.False(s.Finished);
    }

    [Fact]
    public void It_fades_in_quickly_at_birth()
    {
        Assert.Equal(0f, CloudAnimation.At(0f, Rise, Hold, Fade).Alpha, 3);
        Assert.Equal(1f, CloudAnimation.At(Rise * CloudAnimation.FadeInFraction + 0.01f, Rise, Hold, Fade).Alpha, 2);
    }

    [Fact]
    public void The_global_alpha_holds_through_the_fade_and_the_cloud_keeps_spreading()
    {
        // The thinning is per puff and staggered - CloudPuffs' dissolve - so the timeline's own
        // alpha must NOT also fade, or the two multiply and the cloud vanishes early and
        // uniformly, which is exactly the "instant disappearance" the playtest called out.
        var mid = CloudAnimation.At(Rise + Hold + Fade * 0.5f, Rise, Hold, Fade);
        Assert.Equal(1f, mid.Alpha, 3);
        Assert.True(mid.WidthFraction > 1f, "the cloud loosens outwards as it disperses");
        var end = CloudAnimation.At(Rise + Hold + Fade, Rise, Hold, Fade);
        Assert.True(end.Finished);
    }

    [Fact]
    public void A_negative_time_is_treated_as_the_start()
    {
        var s = CloudAnimation.At(-5f, Rise, Hold, Fade);
        Assert.Equal(CloudAnimation.BirthFraction, s.HeightFraction, 3);
        Assert.False(s.Finished);
    }

    [Fact]
    public void The_cap_keeps_spreading_after_the_column_has_topped_out()
    {
        // The updraft does not stop when the cloud stops climbing, so a cap that freezes at the
        // end of the rise reads as switched off. It goes on spreading for the rest of the shot.
        var atRise = CloudAnimation.At(Rise, Rise, Hold, Fade);
        var midHold = CloudAnimation.At(Rise + Hold * 0.5f, Rise, Hold, Fade);
        var atEnd = CloudAnimation.At(Rise + Hold + Fade * 0.99f, Rise, Hold, Fade);
        Assert.True(midHold.WidthFraction > atRise.WidthFraction * 1.02f, "still widening while it stands");
        Assert.True(atEnd.WidthFraction > midHold.WidthFraction, "and still widening as it disperses");
        Assert.InRange(atEnd.WidthFraction / atRise.WidthFraction,
            1f + CloudAnimation.CapSpreadAfterRise * 0.9f, 1f + CloudAnimation.CapSpreadAfterRise * 1.1f);
    }

    [Fact]
    public void The_height_does_not_follow_the_cap_outwards()
    {
        // The tropopause is what stopped the climb; the cap spreads under it rather than
        // pushing through it. Height may drift slightly as it disperses, but nothing like the
        // width's spread, or the mushroom turns back into a ball.
        var atRise = CloudAnimation.At(Rise, Rise, Hold, Fade);
        var atEnd = CloudAnimation.At(Rise + Hold + Fade * 0.99f, Rise, Hold, Fade);
        float widthGrowth = atEnd.WidthFraction / atRise.WidthFraction;
        float heightGrowth = atEnd.HeightFraction / atRise.HeightFraction;
        Assert.True(heightGrowth < 1.1f, $"the column stays put (x{heightGrowth:F2})");
        Assert.True(widthGrowth > heightGrowth * 1.25f, "and the cap outgrows it sideways");
    }
}
