using MissileDisaster.Core;
using Xunit;

public class ConventionalCloudDisplayTests
{
    /// <summary>The 1 t baseline: a 15 m fireball, which is what WarheadSpec.Conventional carries.</summary>
    private const float Baseline = 15f;

    [Fact]
    public void A_bomblet_lifts_no_column_at_all()
    {
        Assert.False(ConventionalCloudDisplay.Draws(0f));
        Assert.False(ConventionalCloudDisplay.Draws(2.9f));
        Assert.True(ConventionalCloudDisplay.Draws(ConventionalCloudDisplay.MinimumFireballRadius));
        Assert.True(ConventionalCloudDisplay.Draws(Baseline));
    }

    [Fact]
    public void The_cap_sits_on_a_stem_narrower_than_it_is()
    {
        // The contrast between a narrow stem and a wide head is the entire silhouette of a
        // mushroom. The previous effect had no stem at all, which is what was reported.
        //
        // The band matters in both directions. Too wide a stem and there is no mushroom; too
        // narrow and the column reads as a thread with a ball stuck on the end, which is what
        // the first pass at these figures drew - a nuclear cap is spread along the tropopause
        // and towers over its stem, and nothing does that to a bomb's.
        var d = ConventionalCloudDisplay.For(Baseline);
        Assert.True(d.StemRadius > 0f);
        Assert.InRange(d.CapRadius / d.StemRadius, 2.2f, 4f);
    }

    [Fact]
    public void The_head_is_a_ball_rather_than_a_flat_canopy()
    {
        // A tonne of high explosive never gets anywhere near the tropopause, so nothing flattens
        // its head: it stays a roughly round ball of smoke on a stem, which is what the
        // photographs of ordinary charges show.
        var d = ConventionalCloudDisplay.For(Baseline);
        Assert.InRange(d.CapRadius * 2f / d.CapDepth, 0.7f, 1.6f);
    }

    [Fact]
    public void The_column_stands_below_the_cap_it_grows_into()
    {
        var d = ConventionalCloudDisplay.For(Baseline);
        Assert.True(d.CapBase > 0f);
        Assert.True(d.CapBase < d.CloudTop);
        Assert.Equal(d.CloudTop - d.CapBase, d.CapDepth, 3);
    }

    [Fact]
    public void A_bigger_charge_throws_up_a_bigger_cloud()
    {
        // Everything is a factor of the fireball radius, which already follows the cube root of
        // the charge - so the cloud scales with the yield without knowing anything about it.
        var small = ConventionalCloudDisplay.For(9.4f);    // 250 kg
        var baseline = ConventionalCloudDisplay.For(Baseline);
        var thermobaric = ConventionalCloudDisplay.For(40f);

        Assert.True(small.CloudTop < baseline.CloudTop);
        Assert.True(baseline.CloudTop < thermobaric.CloudTop);
        Assert.True(small.CapRadius < baseline.CapRadius);
        Assert.True(baseline.CapRadius < thermobaric.CapRadius);
    }

    [Fact]
    public void A_bomb_is_never_mistakable_for_a_warhead()
    {
        // The complaint that started the fireball work was a 1.5 t warhead drawn at nuclear
        // scale. The two clouds are drawn at different scales - a nuclear one is brought down to
        // 6% of its real size, a conventional one is not - so without a ceiling they meet:
        // a thermobaric warhead's 40 m fireball gives a column exactly as tall as a 1 kt cloud,
        // and a hand-typed charge beats it. Every conventional cloud must stay clear of the
        // smallest nuclear one, however large a charge is typed in.
        var smallestNuke = NuclearCloudDisplay.For(1f);

        foreach (float fb in new[] { 40f, 55f, 200f, 5000f })
        {
            var d = ConventionalCloudDisplay.For(fb);
            Assert.True(d.CloudTop < smallestNuke.CloudTop * 0.9f,
                "a " + fb + " m fireball throws a " + d.CloudTop + " m column against a 1 kt cloud's "
                + smallestNuke.CloudTop + " m");
            Assert.True(d.CapRadius < smallestNuke.CapRadius,
                "a " + fb + " m fireball spreads a " + d.CapRadius + " m cap against a 1 kt cloud's "
                + smallestNuke.CapRadius + " m");
        }
    }

    [Fact]
    public void The_ordinary_warheads_are_drawn_to_their_true_figures()
    {
        // The ceiling must not bite at the charges most strikes actually use, or the figures
        // have stopped being the figures. Conventional carries a 15 m fireball, white phosphorus
        // a 10 m one.
        foreach (WarheadType type in new[] { WarheadType.Conventional, WarheadType.WhitePhosphorus })
        {
            float fb = WarheadSpec.For(type).FireballRadius;
            var d = ConventionalCloudDisplay.For(fb);
            Assert.Equal(fb * ConventionalCloudDisplay.CloudTopFactor, d.CloudTop, 0);
        }
    }

    [Fact]
    public void Thermobaric_is_held_back_but_is_still_the_largest_of_them()
    {
        // A fuel-air cloud really is the biggest non-nuclear thing here, so it is the one that
        // runs into the smallest warhead from below and the one the ceiling has to compress.
        // What must survive that is the ordering: it still out-clouds every other bomb.
        float fb = WarheadSpec.For(WarheadType.Thermobaric).FireballRadius;
        var thermobaric = ConventionalCloudDisplay.For(fb);

        Assert.True(thermobaric.CloudTop < fb * ConventionalCloudDisplay.CloudTopFactor,
            "the ceiling is not reaching thermobaric, so nothing is stopping it meeting a 1 kt cloud");
        Assert.True(thermobaric.CloudTop
            > ConventionalCloudDisplay.For(WarheadSpec.For(WarheadType.Conventional).FireballRadius).CloudTop);
    }

    [Fact]
    public void It_is_over_in_seconds_rather_than_in_half_a_minute()
    {
        // A moment in a strike, not the event itself. The nuclear cloud is deliberately far
        // longer - a 150 kt shot runs about 25 s.
        foreach (float fb in new[] { 3f, 9.4f, Baseline, 40f, 100f })
        {
            float show = ConventionalCloudDisplay.ShowSeconds(ConventionalCloudDisplay.For(fb));
            Assert.InRange(show, 5f, 19f);
        }

        Assert.True(
            ConventionalCloudDisplay.ShowSeconds(ConventionalCloudDisplay.For(40f))
            < NuclearCloudDisplay.For(150f).RiseSeconds
              + NuclearCloudDisplay.For(150f).HoldSeconds
              + NuclearCloudDisplay.For(150f).FadeSeconds);
    }

    [Fact]
    public void A_bigger_column_takes_longer_to_stand_up_but_not_proportionally()
    {
        // The square root: a bigger charge drives its column faster as well as higher, so the
        // rise grows far more slowly than the height does.
        var small = ConventionalCloudDisplay.For(9.4f);
        var big = ConventionalCloudDisplay.For(40f);

        Assert.True(big.RiseSeconds > small.RiseSeconds);
        float heightRatio = big.CloudTop / small.CloudTop;
        float riseRatio = big.RiseSeconds / small.RiseSeconds;
        Assert.True(riseRatio < heightRatio,
            "the rise is keeping pace with the height, so a big charge crawls into the sky");
    }

    [Fact]
    public void The_smoke_disperses_more_slowly_than_it_rose()
    {
        // A column that takes two seconds to form and vanishes in a blink reads as a deletion.
        var d = ConventionalCloudDisplay.For(Baseline);
        Assert.True(d.FadeSeconds > d.RiseSeconds);
    }

    [Fact]
    public void Every_phase_a_cloud_is_drawn_through_is_positive()
    {
        // CloudPuffs and CloudAnimation divide by all three; a zero anywhere is an invisible or
        // an eternal cloud.
        foreach (float fb in new[] { 0f, 1f, 3f, Baseline, 40f, 1000f })
        {
            var d = ConventionalCloudDisplay.For(fb);
            Assert.True(d.RiseSeconds > 0f);
            Assert.True(d.HoldSeconds > 0f);
            Assert.True(d.FadeSeconds > 0f);
            Assert.True(d.CapRadius > 0f);
            Assert.True(d.StemRadius > 0f);
            Assert.True(d.CapDepth > 0f);
            Assert.True(d.FireFieldRadius > 0f);
        }
    }

    [Fact]
    public void A_fireball_below_the_minimum_still_gives_a_sane_cloud()
    {
        // Draws() is the gate, but For() must not hand back a cloud of zero size to a caller
        // that skipped it.
        var floored = ConventionalCloudDisplay.For(0f);
        var minimum = ConventionalCloudDisplay.For(ConventionalCloudDisplay.MinimumFireballRadius);
        Assert.Equal(minimum.CloudTop, floored.CloudTop, 3);
        Assert.Equal(minimum.CapRadius, floored.CapRadius, 3);
    }

    [Fact]
    public void The_puff_flow_places_the_whole_crowd_inside_the_cloud()
    {
        // The conventional dimensions go through exactly the same CloudPuffs flow as the nuclear
        // ones, so the envelope has to hold at this scale too - a puff outside it is a lump of
        // smoke hanging off the side of the mushroom.
        var d = ConventionalCloudDisplay.For(Baseline);
        // The cap reaches its own radius times the spread it keeps up after the rise, and the
        // fire smoke reaches the edge of its field. With the tight conventional fire field it is
        // the cap that is the widest thing, which is the other way round from the nuclear cloud.
        float capReach = d.CapRadius * (1f + CloudAnimation.CapSpreadAfterRise);
        float outerLimit = (capReach > d.FireFieldRadius ? capReach : d.FireFieldRadius) * 1.05f;
        float topLimit = d.CloudTop * 1.2f;

        for (int i = 0; i < CloudPuffs.TotalCount; i += 7)
        {
            PuffSpec spec = CloudPuffs.Spec(i, 12345);
            for (float t = 0f; t < ConventionalCloudDisplay.ShowSeconds(d); t += 0.5f)
            {
                CloudAnimationState anim = CloudAnimation.At(t, d.RiseSeconds, d.HoldSeconds, d.FadeSeconds);
                if (anim.Finished) break;
                PuffPoint p = CloudPuffs.At(spec, t, d, anim);

                float dist = (float)System.Math.Sqrt(p.X * p.X + p.Z * p.Z);
                Assert.InRange(dist, 0f, outerLimit);
                Assert.InRange(p.Y, -0.01f, topLimit);
                Assert.True(p.Size > 0f);
            }
        }
    }
}
