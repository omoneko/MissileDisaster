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
    public void Most_of_the_growth_happens_early()
    {
        // Ease-out: by the middle of the rise the cloud is already most of the way up. This is
        // the pace complaint made flesh - the spectacle is at the start, not the end.
        var s = CloudAnimation.At(Rise * 0.5f, Rise, Hold, Fade);
        Assert.True(s.HeightFraction > 0.85f, "over 85% grown at half time");
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
    public void The_fade_thins_it_away_while_it_keeps_spreading()
    {
        var mid = CloudAnimation.At(Rise + Hold + Fade * 0.5f, Rise, Hold, Fade);
        Assert.InRange(mid.Alpha, 0.45f, 0.55f);
        Assert.True(mid.WidthFraction > 1f, "the cloud loosens outwards as it disperses");
        var end = CloudAnimation.At(Rise + Hold + Fade, Rise, Hold, Fade);
        Assert.Equal(0f, end.Alpha, 3);
        Assert.True(end.Finished);
    }

    [Fact]
    public void A_negative_time_is_treated_as_the_start()
    {
        var s = CloudAnimation.At(-5f, Rise, Hold, Fade);
        Assert.Equal(CloudAnimation.BirthFraction, s.HeightFraction, 3);
        Assert.False(s.Finished);
    }
}
