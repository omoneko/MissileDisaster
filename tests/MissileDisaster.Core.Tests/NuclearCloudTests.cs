using MissileDisaster.Core;
using Xunit;

/// <summary>
/// These pin the cloud model to the published figures rather than to whatever looked right, so
/// that retuning the effect cannot quietly drift away from them.
/// Source: Glasstone and Dolan, "The Effects of Nuclear Weapons" (1977), chapter II.
/// </summary>
public class NuclearCloudTests
{
    [Fact]
    public void A_megaton_fireball_is_5700_feet_across_after_ten_seconds()
    {
        // 5,700 ft diameter is a radius of 869 m.
        Assert.InRange(NuclearCloud.FireballRadius(1000f), 830f, 910f);
        Assert.InRange(NuclearCloud.FireballSeconds(1000f), 9.5f, 10.5f);
    }

    [Fact]
    public void The_fireball_grows_as_the_four_tenths_power_of_the_yield()
    {
        // Ten times the yield is 10^0.4 = 2.51 times the radius, not ten times.
        float small = NuclearCloud.FireballRadius(100f);
        float large = NuclearCloud.FireballRadius(1000f);
        Assert.InRange(large / small, 2.45f, 2.57f);
    }

    [Fact]
    public void A_150kt_cloud_is_about_three_and_a_half_kilometres_across_the_cap()
    {
        Assert.InRange(NuclearCloud.CloudRadius(150f), 3300f, 3900f);
    }

    [Fact]
    public void A_150kt_cloud_tops_out_in_the_lower_stratosphere()
    {
        // The fit puts the stabilised top at about 13 km - through the tropopause, which is why
        // a real cap flattens and spreads there.
        Assert.InRange(NuclearCloud.CloudTop(150f), 11000f, 15000f);
    }

    [Fact]
    public void The_cap_happens_to_cover_what_the_blast_destroys()
    {
        // At 150 kt the cloud radius and the 5 psi destruction radius agree to within a few per
        // cent, which is why a cloud built to figures also reads as a marker of the damage.
        float cloud = NuclearCloud.CloudRadius(150f);
        float destruction = WarheadSpec.For(WarheadType.Nuclear).DestructionRadius;
        Assert.InRange(cloud / destruction, 0.85f, 1.15f);
    }

    [Fact]
    public void Up_to_a_megaton_the_cloud_stands_taller_than_its_cap_is_wide()
    {
        foreach (float kt in new[] { 15f, 150f, 1000f })
        {
            Assert.True(NuclearCloud.CloudTop(kt) > NuclearCloud.CloudRadius(kt),
                $"at {kt} kt the cloud stands taller than its cap is wide");
        }
    }

    [Fact]
    public void Beyond_a_megaton_the_cap_spreads_wider_than_the_cloud_is_tall()
    {
        // Not a flaw in the fit: a very large cloud punches into the stratosphere and then has
        // nowhere to go but sideways along the tropopause, so the cap outgrows the column.
        Assert.True(NuclearCloud.CloudRadius(10000f) > NuclearCloud.CloudTop(10000f),
            "a 10 Mt cap spreads wider than the cloud stands tall");
    }

    [Fact]
    public void The_stem_is_half_the_cap_at_twenty_kilotons_and_a_seventh_in_the_megaton_range()
    {
        Assert.InRange(NuclearCloud.StemFraction(20f), 0.48f, 0.52f);
        Assert.InRange(NuclearCloud.StemFraction(1000f), 0.13f, 0.17f);
        Assert.True(NuclearCloud.StemFraction(150f) < NuclearCloud.StemFraction(20f),
            "the stem narrows against the cap as the yield rises");
    }

    [Fact]
    public void The_stem_fraction_never_leaves_its_bounds()
    {
        foreach (float kt in new[] { 0.001f, 1f, 20f, 50000f, 1000000f })
        {
            Assert.InRange(NuclearCloud.StemFraction(kt), 0.1f, 0.5f);
        }
    }

    [Fact]
    public void A_megaton_cloud_climbs_at_nearly_three_hundred_miles_an_hour()
    {
        // 440 ft/s is 134 m/s.
        Assert.InRange(NuclearCloud.RiseSpeed(1000f), 130f, 138f);
    }

    [Fact]
    public void A_megaton_cloud_takes_about_ten_minutes_to_stabilise()
    {
        Assert.InRange(NuclearCloud.StabiliseSeconds(1000f), 570f, 630f);
    }

    [Fact]
    public void Everything_is_zero_at_a_zero_yield()
    {
        Assert.Equal(0f, NuclearCloud.FireballRadius(0f), 3);
        Assert.Equal(0f, NuclearCloud.CloudRadius(0f), 3);
        Assert.Equal(0f, NuclearCloud.CloudTop(0f), 3);
        Assert.Equal(0f, NuclearCloud.RiseSpeed(-5f), 3);
    }

    [Fact]
    public void Every_dimension_rises_with_the_yield()
    {
        float[] yields = { 1f, 15f, 150f, 1000f, 10000f };
        for (int i = 1; i < yields.Length; i++)
        {
            Assert.True(NuclearCloud.FireballRadius(yields[i]) > NuclearCloud.FireballRadius(yields[i - 1]));
            Assert.True(NuclearCloud.CloudRadius(yields[i]) > NuclearCloud.CloudRadius(yields[i - 1]));
            Assert.True(NuclearCloud.CloudTop(yields[i]) > NuclearCloud.CloudTop(yields[i - 1]));
        }
    }

    [Fact]
    public void The_yield_survives_the_round_trip_through_the_launch_multiplier()
    {
        // The launch path only carries cbrt(kt/150), and the cloud needs the kilotons back.
        foreach (int kt in new[] { 15, 150, 1000, 50000 })
        {
            float recovered = NuclearYields.Kilotons(NuclearYields.Multiplier(kt));
            Assert.InRange(recovered, kt * 0.999f, kt * 1.001f);
        }
    }
}
