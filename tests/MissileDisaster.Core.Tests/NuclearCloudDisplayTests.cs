using MissileDisaster.Core;
using Xunit;

/// <summary>
/// What the detonation is actually drawn at, as opposed to what the physics asks for. The point
/// of these is that the yield always shows: no two weapons in the catalogue may come out the
/// same size, which is exactly what the old hard clamps did from about a megaton upwards.
/// </summary>
public class NuclearCloudDisplayTests
{
    private const float S = NuclearCloudDisplay.CloudScale;

    [Fact]
    public void Every_weapon_in_the_catalogue_is_drawn_larger_than_the_one_below_it()
    {
        NuclearWeapon[] catalog = NuclearWeapons.Catalog;
        for (int i = 1; i < catalog.Length; i++)
        {
            NuclearCloudDimensions small = NuclearCloudDisplay.For(catalog[i - 1].Kilotons);
            NuclearCloudDimensions large = NuclearCloudDisplay.For(catalog[i].Kilotons);
            string what = catalog[i].Name + " against " + catalog[i - 1].Name;
            Assert.True(large.FireballRadius > small.FireballRadius, "fireball: " + what);
            Assert.True(large.CapRadius > small.CapRadius, "cap: " + what);
            Assert.True(large.CloudTop > small.CloudTop, "cloud top: " + what);
            // The stem is deliberately not checked. Glasstone's stem width is a fraction of the
            // cap, and that fraction falls with the yield faster than the cap grows over one
            // stretch of the catalogue - a 1.2 Mt stem is about 2% narrower than a 475 kt one.
            // That is what the published figures say, and it is well below anything visible; the
            // fraction itself is held in its band by The_stem_stays_narrow_against_the_cap.
        }
    }

    [Fact]
    public void The_strategic_range_is_no_longer_flattened_into_one_size()
    {
        // The three yields the old clamps could not tell apart: all three came out at an 8 km
        // cap under a 12 km top. A tenfold step in yield has to read as a step in size.
        NuclearCloudDimensions b83 = NuclearCloudDisplay.For(1200f);
        NuclearCloudDimensions bravo = NuclearCloudDisplay.For(15000f);
        NuclearCloudDimensions tsar = NuclearCloudDisplay.For(50000f);

        Assert.True(bravo.CapRadius > b83.CapRadius * 2f, "Castle Bravo dwarfs a B83");
        Assert.True(tsar.CapRadius > bravo.CapRadius, "and Tsar Bomba is larger again");
        Assert.True(tsar.CloudTop > bravo.CloudTop && bravo.CloudTop > b83.CloudTop,
            "the column keeps growing too");
    }

    [Fact]
    public void Everything_inside_the_verified_range_is_drawn_to_its_real_figures_times_the_scales()
    {
        // The knees sit at the old clamps, so no yield that was already exact may move except
        // by the two deliberate scales: the overall CloudScale and the cap's lateral spread.
        foreach (float kt in new[] { 15f, 22f, 150f, 300f, 475f })
        {
            NuclearCloudDimensions d = NuclearCloudDisplay.For(kt);
            Assert.Equal(NuclearCloud.CloudRadius(kt) * S * NuclearCloudDisplay.CapWidthFactor(kt),
                d.CapRadius, 1);
        }
    }

    [Fact]
    public void The_cap_spreads_wider_than_true_but_the_column_does_not_thicken_with_it()
    {
        // The playtest asked for a broader cap. The spread must not leak into the stem, or the
        // mushroom loses the contrast that makes it read: the stem is taken from the cap as the
        // figures give it, before the widening.
        NuclearCloudDimensions d = NuclearCloudDisplay.For(150f);
        float unwidenedCap = NuclearCloud.CloudRadius(150f) * S;
        Assert.Equal(unwidenedCap * NuclearCloud.StemFraction(150f), d.StemRadius, 1);
        Assert.True(d.StemRadius / d.CapRadius < NuclearCloud.StemFraction(150f),
            "against the widened cap, the stem is proportionally narrower still");
    }

    [Fact]
    public void The_fireball_comes_down_less_far_than_the_cloud_around_it()
    {
        // It is judged against the buildings around it, not against the canopy, and it is the
        // smallest part of the effect already. Shrinking it in step with the cloud is what left
        // a spark under a mushroom.
        Assert.True(NuclearCloudDisplay.FireballScale > NuclearCloudDisplay.CloudScale,
            "the fireball keeps more of its size than the cloud does");
        foreach (float kt in new[] { 15f, 150f, 1200f, 10400f })
        {
            NuclearCloudDimensions d = NuclearCloudDisplay.For(kt);
            Assert.Equal(NuclearCloud.FireballRadius(kt) * NuclearCloudDisplay.FireballScale,
                d.FireballRadius, 1);
        }
    }

    [Fact]
    public void The_fireball_reads_against_the_canopy_at_every_yield()
    {
        // Not so small that it is lost under the cloud, and not so large that it swallows it.
        // The band's top was 0.55 until the playtest asked twice for a bigger fireball; across
        // the yields anyone actually launches - 15 kt to 1.2 Mt - it now sits at 0.48-0.55.
        // Only the extreme end reaches 0.75, and that is the cap's ceiling compressing while
        // the fireball's does not, which is the documented price of the height guarantee.
        foreach (float kt in new[] { 15f, 150f, 1200f, 10400f, 50000f })
        {
            NuclearCloudDimensions d = NuclearCloudDisplay.For(kt);
            Assert.InRange(d.FireballRadius / d.CapRadius, 0.15f, 0.80f);
        }
        foreach (float kt in new[] { 15f, 150f, 1200f })
        {
            NuclearCloudDimensions d = NuclearCloudDisplay.For(kt);
            Assert.InRange(d.FireballRadius / d.CapRadius, 0.40f, 0.60f);
        }
    }

    [Fact]
    public void The_canopy_sits_on_the_tropopause_once_the_cloud_punches_through()
    {
        // Ivy Mike's cap base was measured at about 0.46 of its top and Castle Bravo's at 0.42,
        // both of them the tropopause over the Pacific. Below the tropopause there is no lid,
        // and the cap is simply the top half of a rising ball - which is what the Hiroshima and
        // Nagasaki photographs show.
        NuclearCloudDimensions small = NuclearCloudDisplay.For(15f);
        Assert.Equal(small.CloudTop * 0.5f, small.CapBase, 1);

        // The rule is carried as a ratio, so squashing the height cannot break it.
        NuclearCloudDimensions big = NuclearCloudDisplay.For(10400f);
        Assert.InRange(big.CapBase / big.CloudTop, 0.40f, 0.50f);
    }

    [Fact]
    public void A_small_canopy_is_rounder_than_a_strategic_one()
    {
        // The whole point of taking the depth from where the cloud stopped rising: a fixed
        // fraction made every canopy exactly twice as wide as it was deep, at every yield.
        // The absolute flatness carries the height squash and the screen ceiling, both of which
        // flatten everything, so what is pinned is the ordering the physics decides.
        NuclearCloudDimensions small = NuclearCloudDisplay.For(15f);
        NuclearCloudDimensions large = NuclearCloudDisplay.For(10400f);
        float smallFlatness = small.CapRadius * 2f / small.CapDepth;
        float largeFlatness = large.CapRadius * 2f / large.CapDepth;
        Assert.True(largeFlatness > smallFlatness * 3f,
            "a 10 Mt canopy spreads out along the tropopause");
    }

    [Fact]
    public void The_column_is_taller_than_it_is_wide()
    {
        // A stem wider than it is tall is not a stem, and no arrangement of particles reads as a
        // mushroom from there. This is the check that squashing the height on its own broke: it
        // left a 150 kt column 459 m across and 375 m tall, under a canopy three times too wide.
        foreach (float kt in new[] { 15f, 150f, 1200f })
        {
            NuclearCloudDimensions d = NuclearCloudDisplay.For(kt);
            Assert.True(d.CapBase > d.StemRadius * 2f * 1.3f,
                $"{kt} kt: column {d.StemRadius * 2f:F0} m wide, {d.CapBase:F0} m tall");
        }
    }

    [Fact]
    public void The_cloud_keeps_the_proportions_the_figures_give_it()
    {
        // Width and height come down by one number, CloudScale, so the drawn shape is the real
        // shape once the two declared departures are divided back out: the cap's lateral spread
        // and the column's extra height. Scaling them separately *by accident* is what turned
        // the effect into a pancake on a lump; these two are the deliberate, bounded version,
        // and dividing them out is what keeps this test able to catch the accident.
        foreach (float kt in new[] { 15f, 22f, 150f })
        {
            NuclearCloudDimensions d = NuclearCloudDisplay.For(kt);
            float drawnWidth = d.CapRadius / NuclearCloudDisplay.CapWidthFactor(kt) * 2f;
            float drawnHeight = d.CloudTop / NuclearCloudDisplay.CloudHeightScale;
            float drawn = drawnWidth / drawnHeight;
            float real = NuclearCloud.CloudRadius(kt) * 2f / NuclearCloud.CloudTop(kt);
            Assert.InRange(drawn / real, 0.9f, 1.15f);
        }
    }

    [Fact]
    public void The_column_is_drawn_taller_than_the_figures_give_it()
    {
        // A deliberate departure, asked for on playtest: the largest yields read as squat
        // because a game is watched from low angles the photographs were not taken at. It is
        // the height alone - widening the cloud with it would undo the point.
        NuclearCloudDimensions d = NuclearCloudDisplay.For(150f);
        float honestHeight = NuclearCloud.CloudTop(150f) * NuclearCloudDisplay.CloudScale;

        Assert.True(d.CloudTop > honestHeight * 1.5f,
            "the column is not visibly taller than the honest figure");
        // Up to the multiplier, never past it: the soft ceiling may take a little back at the
        // top of the range, but nothing may hand out more height than was asked for.
        Assert.InRange(d.CloudTop / honestHeight,
            NuclearCloudDisplay.CloudHeightScale * 0.9f, NuclearCloudDisplay.CloudHeightScale);
    }

    [Fact]
    public void No_cloud_is_ever_drawn_above_the_top_of_the_screen()
    {
        // The height is what runs off the screen, so it is the one dimension with a hard
        // guarantee rather than a soft one: whatever yield is asked for, the canopy stays where
        // the player can see it - under the altitude the mod already refuses to burst above.
        foreach (float kt in new[] { 1f, 15f, 150f, 1200f, 10400f, 50000f, 1000000f, 1e9f })
        {
            NuclearCloudDimensions d = NuclearCloudDisplay.For(kt);
            Assert.InRange(d.CloudTop, 1f, NuclearCloudDisplay.ScreenTopAltitude);
        }
    }

    [Fact]
    public void The_height_still_grows_with_the_yield_under_that_ceiling()
    {
        // Bounded, but never flattened: a Tsar Bomba still stands visibly taller than a
        // Little Boy, and every step of the catalogue in between is a step up.
        Assert.True(NuclearCloudDisplay.For(50000f).CloudTop >
                    NuclearCloudDisplay.For(15f).CloudTop * 1.5f,
            "the largest weapon stands half again the smallest");
    }

    [Fact]
    public void The_canopy_always_has_a_base_below_its_top()
    {
        foreach (float kt in new[] { 0.001f, 1f, 150f, 50000f, 1000000f })
        {
            NuclearCloudDimensions d = NuclearCloudDisplay.For(kt);
            Assert.True(d.CapDepth > 0f, $"{kt} kt has a canopy with some depth");
            Assert.True(d.CapBase > 0f && d.CapBase < d.CloudTop, $"{kt} kt: base under the top");
            Assert.Equal(d.CloudTop - d.CapBase, d.CapDepth, 1);
        }
    }

    [Fact]
    public void The_cloud_forms_fast_and_does_not_outstay_its_welcome()
    {
        // The playtest verdict on the previous pace - a 31 s rise, then most of a minute of
        // lingering - was "too slow to form and stays too long". The cloud now forms in seconds
        // and the whole 150 kt shot is under half a minute.
        NuclearCloudDimensions d = NuclearCloudDisplay.For(150f);
        Assert.InRange(d.RiseSeconds, 5f, 12f);
        // The dissolve at the end is deliberately the longest phase - vanishing faster than it
        // formed is what read as a deletion - so the whole shot runs a little over half a
        // minute now, most of it the cloud shredding away.
        Assert.InRange(d.RiseSeconds + d.HoldSeconds + d.FadeSeconds, 25f, 40f);
        Assert.True(d.FadeSeconds > d.RiseSeconds, "it takes longer to shred than it took to rise");
    }

    [Fact]
    public void A_bigger_yield_still_plays_longer()
    {
        Assert.True(NuclearCloudDisplay.For(50000f).RiseSeconds > NuclearCloudDisplay.For(15f).RiseSeconds,
            "Tsar Bomba climbs for longer than Little Boy");
        // But even Tsar Bomba's whole shot stays under a minute.
        NuclearCloudDimensions tsar = NuclearCloudDisplay.For(50000f);
        Assert.True(tsar.RiseSeconds + tsar.HoldSeconds + tsar.FadeSeconds < 60f);
    }

    [Fact]
    public void The_hold_and_fade_are_always_present()
    {
        foreach (float kt in new[] { 0.001f, 15f, 150f, 50000f })
        {
            NuclearCloudDimensions d = NuclearCloudDisplay.For(kt);
            Assert.InRange(d.HoldSeconds, NuclearCloudDisplay.HoldSecondsMin, NuclearCloudDisplay.HoldSecondsMax);
            Assert.InRange(d.FadeSeconds, NuclearCloudDisplay.FadeSecondsMin, NuclearCloudDisplay.FadeSecondsMax);
            Assert.Equal(d.CapRadius * NuclearCloudDisplay.FireFieldFactor, d.FireFieldRadius, 1);
        }
    }

    [Fact]
    public void Nothing_ever_leaves_the_engineering_bounds()
    {
        foreach (float kt in new[] { 0.001f, 1f, 150f, 50000f, 1000000f, 1e9f })
        {
            NuclearCloudDimensions d = NuclearCloudDisplay.For(kt);
            Assert.InRange(d.FireballRadius,
                NuclearCloudDisplay.FireballRadiusMin * NuclearCloudDisplay.FireballScale,
                NuclearCloudDisplay.FireballRadiusCeiling * NuclearCloudDisplay.FireballScale);
            Assert.InRange(d.CapRadius, NuclearCloudDisplay.CapRadiusMin * S,
                NuclearCloudDisplay.CapRadiusCeiling * S * NuclearCloudDisplay.CapWidthFactor(kt));
            Assert.InRange(d.CloudTop, 1f, NuclearCloudDisplay.ScreenTopAltitude);
            Assert.InRange(d.RiseSeconds, NuclearCloudDisplay.RiseSecondsMin,
                NuclearCloudDisplay.RiseSecondsCeiling);
            Assert.InRange(d.FireballSeconds, NuclearCloudDisplay.FireballSecondsMin,
                NuclearCloudDisplay.FireballSecondsCeiling);
        }
    }

    [Fact]
    public void The_stem_stays_narrow_against_the_cap()
    {
        // Glasstone's stem fraction runs 0.5 down to 0.1 with yield; against the deliberately
        // widened cap the drawn ratio is that divided by CapWidthScale.
        foreach (float kt in new[] { 15f, 150f, 1000f, 50000f })
        {
            NuclearCloudDimensions d = NuclearCloudDisplay.For(kt);
            Assert.InRange(d.StemRadius / d.CapRadius,
                0.099f / NuclearCloudDisplay.CapWidthFactor(kt),
                0.501f / NuclearCloudDisplay.CapWidthFactor(kt));
        }
    }

    [Fact]
    public void A_missing_yield_falls_back_to_the_baseline()
    {
        NuclearCloudDimensions fallback = NuclearCloudDisplay.For(0f);
        NuclearCloudDimensions baseline = NuclearCloudDisplay.For(NuclearYields.StandardKilotons);
        Assert.Equal(baseline.CapRadius, fallback.CapRadius, 1);
        Assert.Equal(baseline.CloudTop, fallback.CloudTop, 1);
    }
}
