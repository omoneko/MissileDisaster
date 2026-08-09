using MissileDisaster.Core;
using Xunit;

/// <summary>
/// What the detonation is actually drawn at, as opposed to what the physics asks for. The point
/// of these is that the yield always shows: no two weapons in the catalogue may come out the
/// same size, which is exactly what the old hard clamps did from about a megaton upwards.
/// </summary>
public class NuclearCloudDisplayTests
{
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
    public void Everything_inside_the_verified_range_is_still_drawn_to_its_real_figures()
    {
        // The knees sit at the old clamps, so no yield that was already exact may move.
        foreach (float kt in new[] { 15f, 22f, 150f, 300f, 475f })
        {
            NuclearCloudDimensions d = NuclearCloudDisplay.For(kt);
            Assert.Equal(NuclearCloud.FireballRadius(kt), d.FireballRadius, 1);
            Assert.Equal(NuclearCloud.CloudRadius(kt), d.CapRadius, 1);
        }
    }

    [Fact]
    public void The_150kt_baseline_finally_stands_to_its_full_height()
    {
        // The old 12 km ceiling cut into the baseline itself: its real top is 13.3 km.
        NuclearCloudDimensions d = NuclearCloudDisplay.For(150f);
        Assert.InRange(d.CloudTop, 13000f, 13500f);
    }

    [Fact]
    public void Nothing_ever_leaves_the_engineering_bounds()
    {
        foreach (float kt in new[] { 0.001f, 1f, 150f, 50000f, 1000000f, 1e9f })
        {
            NuclearCloudDimensions d = NuclearCloudDisplay.For(kt);
            Assert.InRange(d.FireballRadius, NuclearCloudDisplay.FireballRadiusMin,
                NuclearCloudDisplay.FireballRadiusCeiling);
            Assert.InRange(d.CapRadius, NuclearCloudDisplay.CapRadiusMin,
                NuclearCloudDisplay.CapRadiusCeiling);
            Assert.InRange(d.CloudTop, NuclearCloudDisplay.CloudTopMin,
                NuclearCloudDisplay.CloudTopCeiling);
            Assert.InRange(d.RiseSeconds, NuclearCloudDisplay.RiseSecondsMin,
                NuclearCloudDisplay.RiseSecondsCeiling);
            Assert.InRange(d.FireballSeconds, NuclearCloudDisplay.FireballSecondsMin,
                NuclearCloudDisplay.FireballSecondsCeiling);
        }
    }

    [Fact]
    public void The_stem_stays_narrow_against_the_cap()
    {
        foreach (float kt in new[] { 15f, 150f, 1000f, 50000f })
        {
            NuclearCloudDimensions d = NuclearCloudDisplay.For(kt);
            Assert.InRange(d.StemRadius / d.CapRadius, 0.1f, 0.5f);
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
