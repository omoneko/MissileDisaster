using MissileDisaster.Core;
using Xunit;

/// <summary>
/// The vortex-ring flow the mushroom cloud's puffs follow. These pin the structure - envelope,
/// circulation, recycling, determinism - so the effect cannot quietly stop being a mushroom.
/// </summary>
public class CloudPuffsTests
{
    private static NuclearCloudDimensions Dims()
    {
        return NuclearCloudDisplay.For(150f);
    }

    private static CloudAnimationState FullyGrown(NuclearCloudDimensions d)
    {
        return CloudAnimation.At(d.RiseSeconds + 1f, d.RiseSeconds, d.HoldSeconds, d.FadeSeconds);
    }

    [Fact]
    public void Specs_are_deterministic_and_split_into_cap_column_and_fire()
    {
        int caps = 0, fires = 0;
        for (int i = 0; i < CloudPuffs.TotalCount; i++)
        {
            PuffSpec a = CloudPuffs.Spec(i, 42);
            PuffSpec b = CloudPuffs.Spec(i, 42);
            Assert.Equal(a.Azimuth, b.Azimuth, 5);
            Assert.Equal(a.Theta0, b.Theta0, 5);
            Assert.False(a.Cap && a.Fire, "a puff is one thing at a time");
            if (a.Cap) caps++;
            if (a.Fire) fires++;
        }
        Assert.Equal(CloudPuffs.CapCount, caps);
        Assert.Equal(CloudPuffs.FireCount, fires);
    }

    [Fact]
    public void A_different_seed_boils_differently()
    {
        Assert.NotEqual(CloudPuffs.Spec(0, 1).Theta0, CloudPuffs.Spec(0, 2).Theta0);
    }

    [Fact]
    public void Every_puff_stays_inside_the_cloud_envelope()
    {
        NuclearCloudDimensions d = Dims();
        for (int i = 0; i < CloudPuffs.TotalCount; i++)
        {
            PuffSpec s = CloudPuffs.Spec(i, 7);
            for (float t = 0f; t < d.RiseSeconds + d.HoldSeconds + d.FadeSeconds; t += 1.7f)
            {
                CloudAnimationState anim = CloudAnimation.At(t, d.RiseSeconds, d.HoldSeconds, d.FadeSeconds);
                PuffPoint p = CloudPuffs.At(s, t, d, anim);
                float dist = (float)System.Math.Sqrt(p.X * p.X + p.Z * p.Z);
                // The fire smoke lives out in the burn field; everything else inside the cap.
                float envelope = s.Fire ? d.FireFieldRadius : d.CapRadius * anim.WidthFraction;
                Assert.True(dist <= envelope * 1.01f + 1f,
                    $"puff {i} at t={t}: {dist} m out against an envelope of {envelope}");
                Assert.InRange(p.Y, -1f, d.CloudTop * anim.HeightFraction * 1.01f + 1f);
            }
        }
    }

    [Fact]
    public void The_cap_circulates_the_way_a_vortex_ring_does()
    {
        // Take a cap puff and follow it: its angle around the ring core must keep advancing,
        // which is the boil - up the inside, out over the top, down the outside, in underneath.
        NuclearCloudDimensions d = Dims();
        CloudAnimationState anim = FullyGrown(d);
        PuffSpec s = CloudPuffs.Spec(0, 7);
        Assert.True(s.Cap);

        // Reconstruct the angle from two nearby moments and check it moved.
        float t0 = d.RiseSeconds + 2f, dt = 0.5f;
        PuffPoint a = CloudPuffs.At(s, t0, d, anim);
        PuffPoint b = CloudPuffs.At(s, t0 + dt, d, anim);
        Assert.False(a.X == b.X && a.Y == b.Y && a.Z == b.Z, "the cap never freezes mid-boil");
    }

    [Fact]
    public void The_boil_slows_once_the_cloud_stands_and_almost_stops_as_it_fades()
    {
        const float rise = 8f, hold = 10f;
        float duringRise = CloudPuffs.RollTime(rise, rise, hold) - CloudPuffs.RollTime(rise - 1f, rise, hold);
        float duringHold = CloudPuffs.RollTime(rise + 5f, rise, hold) - CloudPuffs.RollTime(rise + 4f, rise, hold);
        float duringFade = CloudPuffs.RollTime(rise + hold + 3f, rise, hold) - CloudPuffs.RollTime(rise + hold + 2f, rise, hold);
        Assert.Equal(1f, duringRise, 2);
        Assert.Equal(CloudPuffs.RollRateHold, duringHold, 2);
        Assert.Equal(CloudPuffs.RollRateFade, duringFade, 2);
        Assert.True(duringRise > duringHold && duringHold > duringFade);
    }

    [Fact]
    public void Column_puffs_climb_and_recycle_without_popping()
    {
        NuclearCloudDimensions d = Dims();
        CloudAnimationState anim = FullyGrown(d);
        PuffSpec s = CloudPuffs.Spec(CloudPuffs.CapCount, 7); // the first column puff
        Assert.False(s.Cap);

        // Over one full loop the puff must visit both low and high, and its fade must reach
        // zero somewhere so the teleport back to the base can never be seen.
        float loop = d.RiseSeconds * 0.9f;
        float minY = float.MaxValue, maxY = float.MinValue, minFade = float.MaxValue;
        for (float t = 0f; t < loop; t += loop / 60f)
        {
            PuffPoint p = CloudPuffs.At(s, t, d, anim);
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
            if (p.Fade < minFade) minFade = p.Fade;
        }
        float columnTop = (d.CapBase + d.CapDepth * CloudPuffs.ColumnTopIntoCap) * anim.HeightFraction;
        Assert.True(minY < columnTop * 0.25f, "the puff spends time low in the column");
        Assert.True(maxY > columnTop * 0.7f, "and climbs most of the way up it");
        Assert.True(minFade < 0.05f, "and is invisible at the moment it recycles");
    }

    [Fact]
    public void The_column_has_a_skirt_a_waist_and_a_throat()
    {
        Assert.Equal(CloudPuffs.ColumnSkirtFactor, CloudPuffs.ColumnShape(0f), 3);
        Assert.Equal(CloudPuffs.ColumnWaistFactor, CloudPuffs.ColumnShape(CloudPuffs.ColumnWaistAt), 3);
        Assert.Equal(CloudPuffs.ColumnThroatFactor, CloudPuffs.ColumnShape(1f), 3);
        Assert.True(CloudPuffs.ColumnShape(0f) > CloudPuffs.ColumnShape(CloudPuffs.ColumnWaistAt),
            "the skirt is wider than the waist");
    }

    [Fact]
    public void The_fire_dies_out_of_the_folds_partway_up_the_rise()
    {
        const float rise = 8f;
        Assert.Equal(1f, CloudPuffs.EmberEnvelope(0f, rise), 3);
        Assert.Equal(0f, CloudPuffs.EmberEnvelope(rise, rise), 3);
        Assert.True(CloudPuffs.EmberEnvelope(rise * 0.3f, rise) > 0f, "still glowing early in the climb");
    }

    [Fact]
    public void The_cloud_grows_out_of_the_fireball()
    {
        // At birth every puff must huddle near the origin - inside the fireball - and at full
        // size the cap must actually reach its radius.
        NuclearCloudDimensions d = Dims();
        CloudAnimationState born = CloudAnimation.At(0f, d.RiseSeconds, d.HoldSeconds, d.FadeSeconds);
        CloudAnimationState grown = FullyGrown(d);
        float maxBorn = 0f, maxGrown = 0f;
        for (int i = 0; i < CloudPuffs.TotalCount; i++)
        {
            PuffSpec s = CloudPuffs.Spec(i, 7);
            if (s.Fire) continue; // the fires are real places in the city, ablaze from the flash - they do not grow out of the fireball
            PuffPoint pb = CloudPuffs.At(s, 0f, d, born);
            PuffPoint pg = CloudPuffs.At(s, d.RiseSeconds + 2f, d, grown);
            float db = (float)System.Math.Sqrt(pb.X * pb.X + pb.Z * pb.Z);
            float dg = (float)System.Math.Sqrt(pg.X * pg.X + pg.Z * pg.Z);
            if (db > maxBorn) maxBorn = db;
            if (dg > maxGrown) maxGrown = dg;
        }
        Assert.True(maxBorn < d.CapRadius * 0.2f, "at the flash the cloud is still inside the fireball");
        Assert.True(maxGrown > d.CapRadius * 0.85f, "fully grown, the cap reaches out to its radius");
    }

    [Fact]
    public void Fire_smoke_is_born_in_the_burn_field_and_drawn_in_toward_the_updraft()
    {
        NuclearCloudDimensions d = Dims();
        CloudAnimationState anim = FullyGrown(d);
        PuffSpec s = CloudPuffs.Spec(CloudPuffs.CapCount + CloudPuffs.ColumnCount, 7);
        Assert.True(s.Fire);

        // Follow one loop from its own start: the puff must begin far out and end close in,
        // and the pull must be monotonic - gently drawn, never jerked.
        float loop = d.RiseSeconds * CloudPuffs.FireLoopFactor;
        float t0 = (1f - s.Climb01) * loop; // where its loop wraps to u=0
        float last = float.MaxValue; float first = 0f;
        for (int k = 0; k <= 20; k++)
        {
            float u = k / 20f * 0.98f;
            PuffPoint p = CloudPuffs.At(s, t0 + u * loop, d, anim);
            float dist = (float)System.Math.Sqrt(p.X * p.X + p.Z * p.Z);
            if (k == 0) first = dist;
            Assert.True(dist <= last + 0.5f, "the drift toward the updraft never reverses");
            last = dist;
        }
        Assert.True(last < first * (1f - CloudPuffs.FireInwardPull) * 1.2f,
            "over a loop the puff gives up most of its birth radius");
    }

    [Fact]
    public void The_dissolve_is_staggered_not_uniform()
    {
        // Halfway through the fade some puffs must already be gone while others still stand -
        // the cloud shreds, it does not evaporate in one piece.
        NuclearCloudDimensions d = Dims();
        float t = d.RiseSeconds + d.HoldSeconds + d.FadeSeconds * 0.5f;
        CloudAnimationState anim = CloudAnimation.At(t, d.RiseSeconds, d.HoldSeconds, d.FadeSeconds);
        int gone = 0, standing = 0;
        for (int i = 0; i < CloudPuffs.TotalCount; i++)
        {
            PuffSpec s = CloudPuffs.Spec(i, 7);
            if (!s.Cap) continue;
            float fade = CloudPuffs.At(s, t, d, anim).Fade;
            if (fade < 0.1f) gone++;
            if (fade > 0.9f) standing++;
        }
        Assert.True(gone > 20, $"some of the cap has shredded away by half fade (gone={gone})");
        Assert.True(standing > 20, $"and some of it still stands (standing={standing})");
    }

    [Fact]
    public void The_fire_smoke_outlasts_the_column()
    {
        // The city is still burning when the cloud goes, so its smoke dissolves last.
        NuclearCloudDimensions d = Dims();
        float t = d.RiseSeconds + d.HoldSeconds + d.FadeSeconds * 0.45f;
        CloudAnimationState anim = CloudAnimation.At(t, d.RiseSeconds, d.HoldSeconds, d.FadeSeconds);
        float colFade = 0f, fireFade = 0f; int cols = 0, fires = 0;
        for (int i = 0; i < CloudPuffs.TotalCount; i++)
        {
            PuffSpec s = CloudPuffs.Spec(i, 7);
            if (s.Cap) continue;
            float fade = CloudPuffs.At(s, t, d, anim).Fade;
            if (s.Fire) { fireFade += fade; fires++; } else { colFade += fade; cols++; }
        }
        Assert.True(fireFade / fires > colFade / cols,
            "midway through the fade the fire smoke is holding on better than the column");
    }

    [Fact]
    public void Everything_is_gone_by_the_end_of_the_fade()
    {
        NuclearCloudDimensions d = Dims();
        float t = d.RiseSeconds + d.HoldSeconds + d.FadeSeconds;
        CloudAnimationState anim = CloudAnimation.At(t, d.RiseSeconds, d.HoldSeconds, d.FadeSeconds);
        for (int i = 0; i < CloudPuffs.TotalCount; i++)
        {
            Assert.True(CloudPuffs.At(CloudPuffs.Spec(i, 7), t, d, anim).Fade < 0.02f,
                $"puff {i} has dissolved by the end");
        }
    }
}
