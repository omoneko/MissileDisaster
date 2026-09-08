using System;
using MissileDisaster.Core;
using Xunit;

/// <summary>
/// The anvil: the wide, thin, two-layer sheet a big cloud spreads into once it reaches the
/// tropopause. These pin the trigger - which is the easy thing to get wrong - and the shape.
/// </summary>
public class AnvilCapTests
{
    private static readonly NuclearCloudDimensions D = NuclearCloudDisplay.For(150f);
    private static float RealTop(float kt) => NuclearCloud.CloudTop(kt);

    [Fact]
    public void It_is_keyed_to_the_real_ceiling_and_not_to_the_drawn_one()
    {
        // The distinction the class exists for. The mod's drawn height only starts compressing
        // above about 15 Mt, so an anvil keyed to that would be a feature almost nobody saw.
        // A real cloud tops out at the tropopause from about 150 kt up, which is where it belongs.
        Assert.False(AnvilCap.Forms(RealTop(1f)), "a 1 kt cloud never reaches the tropopause");
        Assert.False(AnvilCap.Forms(RealTop(15f)), "nor does Little Boy");
        Assert.True(AnvilCap.Forms(RealTop(150f)), "a 150 kt cloud does");
        Assert.True(AnvilCap.Forms(RealTop(50000f)), "and so does a Tsar Bomba");
    }

    [Fact]
    public void The_sheet_widens_with_how_far_past_the_ceiling_the_cloud_pushed()
    {
        float small = AnvilCap.Radius(D.CapRadius, RealTop(150f));
        float large = AnvilCap.Radius(D.CapRadius, RealTop(50000f));
        Assert.True(large > small, $"{large:F0} m at 50 Mt against {small:F0} m at 150 kt");
        Assert.Equal(0f, AnvilCap.Overshoot(RealTop(15f)), 3);
        Assert.InRange(AnvilCap.Overshoot(RealTop(50000f)), 0.5f, 1f);
    }

    [Fact]
    public void It_is_always_wider_than_the_cap_it_sits_on()
    {
        // Otherwise it is not an anvil, it is a lid.
        foreach (float kt in new[] { 150f, 1000f, 15000f, 50000f })
        {
            NuclearCloudDimensions d = NuclearCloudDisplay.For(kt);
            float r = AnvilCap.Radius(d.CapRadius, RealTop(kt));
            Assert.True(r > d.CapRadius, $"{kt} kt: sheet {r:F0} m against cap {d.CapRadius:F0} m");
        }
    }

    [Fact]
    public void Nothing_is_drawn_for_a_cloud_that_never_reaches_the_ceiling()
    {
        Assert.Equal(0f, AnvilCap.Radius(D.CapRadius, RealTop(15f)), 3);
        AnvilPoint p = AnvilCap.At(0, 7, D.CapRadius, D.CloudTop, RealTop(15f), 1f, 1f);
        Assert.Equal(0f, p.Size, 3);
    }

    [Fact]
    public void It_is_a_sheet_rather_than_a_second_cloud()
    {
        // Thin is its whole character: it must be far wider than it is deep, or it reads as
        // another mushroom stacked on the first - the mistake the base surge already made once.
        float top = float.MinValue, bottom = float.MaxValue, widest = 0f;
        for (int i = 0; i < AnvilCap.PuffCount; i++)
        {
            AnvilPoint p = AnvilCap.At(i, 7, D.CapRadius, D.CloudTop, RealTop(150f), 1f, 1f);
            top = Math.Max(top, p.Y); bottom = Math.Min(bottom, p.Y);
            widest = Math.Max(widest, (float)Math.Sqrt(p.X * p.X + p.Z * p.Z));
        }
        Assert.True(widest > (top - bottom) * 2f,
            $"{widest:F0} m wide against {top - bottom:F0} m deep");
    }

    [Fact]
    public void It_comes_in_two_layers_and_the_lower_one_is_the_smaller()
    {
        float upperWidest = 0f, lowerWidest = 0f, upperY = 0f, lowerY = 0f;
        for (int i = 0; i < AnvilCap.PuffCount; i++)
        {
            AnvilPoint p = AnvilCap.At(i, 7, D.CapRadius, D.CloudTop, RealTop(150f), 1f, 1f);
            float r = (float)Math.Sqrt(p.X * p.X + p.Z * p.Z);
            if (p.Upper) { upperWidest = Math.Max(upperWidest, r); upperY += p.Y; }
            else { lowerWidest = Math.Max(lowerWidest, r); lowerY += p.Y; }
        }
        Assert.True(lowerWidest > 0f, "the lower skirt exists");
        Assert.True(lowerWidest < upperWidest, "and is the smaller of the two");
        Assert.True(lowerY / (AnvilCap.PuffCount * (1f - AnvilCap.UpperShare))
                  < upperY / (AnvilCap.PuffCount * AnvilCap.UpperShare), "and hangs below it");
    }

    [Fact]
    public void The_sheet_dishes_so_its_rim_rides_above_its_middle()
    {
        AnvilPoint centre = default(AnvilPoint), rim = default(AnvilPoint);
        float nearest = float.MaxValue, furthest = 0f;
        for (int i = 0; i < AnvilCap.PuffCount; i++)
        {
            AnvilPoint p = AnvilCap.At(i, 7, D.CapRadius, D.CloudTop, RealTop(150f), 1f, 1f);
            if (!p.Upper) continue;
            float r = (float)Math.Sqrt(p.X * p.X + p.Z * p.Z);
            if (r < nearest) { nearest = r; centre = p; }
            if (r > furthest) { furthest = r; rim = p; }
        }
        Assert.True(rim.Y > centre.Y, $"rim {rim.Y:F0} m against centre {centre.Y:F0} m");
        Assert.True(rim.Fade < centre.Fade, "and the rim is the thinner - it is still spreading");
    }

    [Fact]
    public void It_sits_on_top_of_the_cloud_rather_than_beside_it()
    {
        for (int i = 0; i < AnvilCap.PuffCount; i++)
        {
            AnvilPoint p = AnvilCap.At(i, 7, D.CapRadius, D.CloudTop, RealTop(150f), 1f, 1f);
            Assert.True(p.Y > D.CapBase, $"puff {i} at {p.Y:F0} m is below the cap base {D.CapBase:F0} m");
        }
    }

    [Fact]
    public void A_strike_spreads_the_same_anvil_every_time_it_is_replayed()
    {
        AnvilPoint a = AnvilCap.At(5, 3, D.CapRadius, D.CloudTop, RealTop(150f), 1f, 1f);
        AnvilPoint b = AnvilCap.At(5, 3, D.CapRadius, D.CloudTop, RealTop(150f), 1f, 1f);
        Assert.Equal(a.X, b.X, 4);
        Assert.NotEqual(a.X, AnvilCap.At(5, 4, D.CapRadius, D.CloudTop, RealTop(150f), 1f, 1f).X);
    }
}
