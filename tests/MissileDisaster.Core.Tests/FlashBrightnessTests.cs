using MissileDisaster.Core;
using Xunit;

/// <summary>
/// How brightly a detonation lights the scene.
///
/// These exist because of a player report the mod could not have found on its own: the smoke
/// turned blue while the flash was lit, with conventional warheads as well as nuclear. The cause
/// was the mod asking Unity for point lights of 10 to 90 and a map-wide directional of 9, where
/// a Unity light sits between 1 and 8. Nothing could see those numbers - they were private
/// constants inside a MonoBehaviour - so nothing could object to them. That is the actual bug,
/// and this file is the fix for it.
/// </summary>
public class FlashBrightnessTests
{
    [Fact]
    public void No_flash_ever_leaves_the_range_the_renderer_is_built_for()
    {
        // The one that matters. A scene driven far past range is where the grading stops
        // behaving, and 90 was eleven times over.
        foreach (float kt in new[] { 0f, 1f, 15f, 150f, 1000f, 25000f, 50000f })
        {
            Assert.True(FlashBrightness.Sane(FlashBrightness.Nuclear(kt)),
                $"{kt} kt point light: {FlashBrightness.Nuclear(kt)}");
            Assert.True(FlashBrightness.Sane(FlashBrightness.Daylight(kt)),
                $"{kt} kt daylight: {FlashBrightness.Daylight(kt)}");
        }
        foreach (float fb in new[] { 0f, 3f, 11.9f, 17.2f, 40.7f, 400f })
        {
            Assert.True(FlashBrightness.Sane(FlashBrightness.Conventional(fb)),
                $"{fb} m fireball: {FlashBrightness.Conventional(fb)}");
        }
    }

    [Fact]
    public void The_daylight_wash_stays_close_to_a_real_sun()
    {
        // Unity's own sun sits near 1. This is a flash, so it may be brighter - but it was 9,
        // which is not a flash, it is a second sun with the exposure fighting it.
        Assert.InRange(FlashBrightness.Daylight(150f), 1f, 4f);
        Assert.InRange(FlashBrightness.Daylight(50000f), 1f, 4f);
    }

    [Fact]
    public void Brightness_follows_the_cube_root_of_the_yield()
    {
        // A 1000 kt device is not a thousand times brighter to look at than a 1 kt one, and a
        // linear law would either black out the small yields or white out the large.
        Assert.True(FlashBrightness.Nuclear(1000f) > FlashBrightness.Nuclear(15f));
        Assert.True(FlashBrightness.Nuclear(1000f) < FlashBrightness.Nuclear(15f) * 8f,
            "but not by orders of magnitude");
    }

    [Fact]
    public void A_bigger_charge_flashes_brighter_than_a_smaller_one()
    {
        Assert.True(FlashBrightness.Conventional(40.7f) > FlashBrightness.Conventional(11.9f));
        Assert.True(FlashBrightness.Nuclear(150f) > FlashBrightness.Conventional(40.7f),
            "and a nuclear flash outshines any conventional one");
    }

    [Fact]
    public void Even_a_zero_or_negative_yield_lights_something()
    {
        Assert.Equal(FlashBrightness.NuclearMin, FlashBrightness.Nuclear(0f), 3);
        Assert.Equal(FlashBrightness.NuclearMin, FlashBrightness.Nuclear(-5f), 3);
        Assert.Equal(FlashBrightness.ConventionalMin, FlashBrightness.Conventional(0f), 3);
        Assert.Equal(FlashBrightness.DaylightMin, FlashBrightness.Daylight(0f), 3);
    }

    [Fact]
    public void The_ceilings_are_held()
    {
        Assert.Equal(FlashBrightness.NuclearMax, FlashBrightness.Nuclear(1e9f), 3);
        Assert.Equal(FlashBrightness.DaylightMax, FlashBrightness.Daylight(1e9f), 3);
        Assert.Equal(FlashBrightness.ConventionalMax, FlashBrightness.Conventional(1e6f), 3);
    }
}
