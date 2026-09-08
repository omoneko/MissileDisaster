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
                // The fire smoke lives out in the burn field; everything else inside the cap,
                // plus the cauliflower lobes, which are the whole point of the envelope not
                // being a circle any more.
                // The cap's crown is flared out beyond its nominal radius as well as lobed, so
                // the envelope has to allow for both or it fails on the very shape it is meant
                // to be describing.
                float envelope = s.Fire
                    ? d.FireFieldRadius
                    : d.CapRadius * anim.WidthFraction
                        * (1f + CloudPuffs.CapLobeDepth) * (1f + CloudPuffs.CapTopFlare);
                Assert.True(dist <= envelope * 1.01f + 1f,
                    $"puff {i} at t={t}: {dist} m out against an envelope of {envelope}");
                // The lobes ride up as well as out, so the canopy's crown is above the figure.
                float top = d.CloudTop * anim.HeightFraction
                    + d.CapDepth * anim.HeightFraction * CloudPuffs.CapLobeRise;
                Assert.InRange(p.Y, -1f, top * 1.01f + 1f);
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
    public void The_cap_is_wider_at_its_crown_than_at_its_underside()
    {
        // What makes it read as a mushroom rather than a torus: the top spreads along the
        // tropopause it has hit and the underside tucks in around the stem. Reported from the
        // Workshop as the nuclear cap wanting a far more pronounced shape than the conventional
        // one - and this is the difference, since a bomb's column has nothing to spread against.
        NuclearCloudDimensions d = Dims();
        CloudAnimationState anim = FullyGrown(d);
        float topWidest = 0f, bottomWidest = 0f;
        for (int i = 0; i < CloudPuffs.CapCount; i++)
        {
            PuffSpec s = CloudPuffs.Spec(i, 7);
            PuffPoint p = CloudPuffs.At(s, d.RiseSeconds + 1f, d, anim);
            float dist = (float)System.Math.Sqrt(p.X * p.X + p.Z * p.Z);
            float capBase = d.CapBase * anim.HeightFraction;
            float capDepth = d.CapDepth * anim.HeightFraction;
            float inCap = (p.Y - capBase) / capDepth;
            if (inCap > 0.75f && dist > topWidest) topWidest = dist;
            if (inCap < 0.25f && dist > bottomWidest) bottomWidest = dist;
        }
        Assert.True(topWidest > bottomWidest * 1.2f,
            $"the crown overhangs: {topWidest:F0} m at the top against {bottomWidest:F0} m at the base");
        Assert.True(CloudPuffs.CapTopFlare > 0f);
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
        // Halfway through the fade the cap must show a real spread of transparencies: every
        // puff is thinning, but each on its own cue, so some are nearly gone while others are
        // still substantial. A single uniform alpha across the crowd is the "switched off"
        // look this replaces; holes punched in something otherwise solid is the other failure.
        NuclearCloudDimensions d = Dims();
        float t = d.RiseSeconds + d.HoldSeconds + d.FadeSeconds * 0.5f;
        CloudAnimationState anim = CloudAnimation.At(t, d.RiseSeconds, d.HoldSeconds, d.FadeSeconds);
        float min = float.MaxValue, max = 0f;
        for (int i = 0; i < CloudPuffs.TotalCount; i++)
        {
            PuffSpec s = CloudPuffs.Spec(i, 7);
            if (!s.Cap) continue;
            float fade = CloudPuffs.At(s, t, d, anim).Fade;
            if (fade < min) min = fade;
            if (fade > max) max = fade;
        }
        Assert.True(max > 0.3f, $"the cap is still there at half fade (max={max:F2})");
        Assert.True(min < max * 0.35f, $"and visibly ragged, not one flat alpha ({min:F2}..{max:F2})");
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

    [Fact]
    public void Puffs_come_in_a_range_of_sizes_weighted_small()
    {
        // A crowd of one size reads as a bag of identical blobs. What a cloud actually is: a
        // handful of big lobes with many smaller ones packed around them.
        NuclearCloudDimensions d = Dims();
        CloudAnimationState anim = FullyGrown(d);
        float min = float.MaxValue, max = 0f, sum = 0f; int n = 0;
        for (int i = 0; i < CloudPuffs.TotalCount; i++)
        {
            PuffSpec s = CloudPuffs.Spec(i, 7);
            if (!s.Cap) continue;
            float size = CloudPuffs.At(s, d.RiseSeconds + 2f, d, anim).Size;
            if (size < min) min = size;
            if (size > max) max = size;
            sum += size; n++;
        }
        Assert.True(max > min * 2.5f, $"the largest puff dwarfs the smallest ({min:F0}..{max:F0} m)");
        // Weighted small: the mean sits well below the midpoint of the range.
        Assert.True(sum / n < (min + max) * 0.5f, "most puffs are at the small end");
    }

    [Fact]
    public void The_size_roll_is_biased_towards_the_small_end()
    {
        Assert.Equal(0f, CloudPuffs.SizeRoll(0f), 3);
        Assert.Equal(1f, CloudPuffs.SizeRoll(1f), 3);
        Assert.True(CloudPuffs.SizeRoll(0.5f) < 0.35f, "a middling roll still yields a smallish puff");
    }

    [Fact]
    public void The_whole_cloud_goes_transparent_as_it_disperses()
    {
        // Not only does each puff take its turn to dissolve - every puff still present is also
        // steadily more see-through, so the cloud thins as a whole rather than punching holes.
        NuclearCloudDimensions d = Dims();
        float early = d.RiseSeconds + d.HoldSeconds + d.FadeSeconds * 0.15f;
        float late = d.RiseSeconds + d.HoldSeconds + d.FadeSeconds * 0.6f;
        float sumEarly = 0f, sumLate = 0f;
        for (int i = 0; i < CloudPuffs.TotalCount; i++)
        {
            PuffSpec s = CloudPuffs.Spec(i, 7);
            if (!s.Cap) continue;
            sumEarly += CloudPuffs.At(s, early, d,
                CloudAnimation.At(early, d.RiseSeconds, d.HoldSeconds, d.FadeSeconds)).Fade;
            sumLate += CloudPuffs.At(s, late, d,
                CloudAnimation.At(late, d.RiseSeconds, d.HoldSeconds, d.FadeSeconds)).Fade;
        }
        Assert.True(sumLate < sumEarly * 0.6f, "the cap is markedly more transparent later in the fade");
        Assert.True(sumLate > 0f, "but it has not simply been switched off");
    }

    // ------------------------------------------------------------------ the boundary layer

    [Fact]
    public void The_column_has_a_velocity_profile_rather_than_one_speed()
    {
        // A rising column is a jet: the core carries the buoyancy and the outside is dragged
        // along by it. Every puff used to climb at one rate whatever its radial position, which
        // is what made the stem look extruded rather than drawn up.
        Assert.Equal(1f, CloudPuffs.ClimbSpeed(0f), 3);
        Assert.Equal(CloudPuffs.ColumnEdgeSpeed, CloudPuffs.ClimbSpeed(1f), 3);
        Assert.True(CloudPuffs.ColumnEdgeSpeed < 1f, "there is no boundary layer at all");

        float previous = float.MaxValue;
        for (int i = 0; i <= 10; i++)
        {
            float v = CloudPuffs.ClimbSpeed(i / 10f);
            Assert.True(v < previous, "the profile is not falling away from the axis");
            previous = v;
        }
    }

    [Fact]
    public void The_edge_of_the_column_never_reaches_the_cap()
    {
        // The visible half of the same effect: slow air at the edge of a jet is entrained rather
        // than merely late, so it tops out short and the column tapers.
        Assert.Equal(1f, CloudPuffs.ClimbReach(0f), 3);
        Assert.Equal(CloudPuffs.ColumnEdgeReach, CloudPuffs.ClimbReach(1f), 3);
        Assert.InRange(CloudPuffs.ColumnEdgeReach, 0.5f, 0.95f);

        // And through the flow itself: follow a core puff and an edge puff through a whole loop
        // and the edge one must peak lower.
        NuclearCloudDimensions d = Dims();
        CloudAnimationState anim = FullyGrown(d);
        PuffSpec core = ColumnPuff(7);
        core.Rho01 = 0f;
        PuffSpec edge = core;
        edge.Rho01 = 1f;

        Assert.True(PeakHeight(edge, d, anim) < PeakHeight(core, d, anim) * 0.95f,
            "the outside of the column climbs as high as the middle");
    }

    // ------------------------------------------------------------------ the lumps

    [Fact]
    public void One_strike_agrees_with_itself_about_where_its_lumps_are()
    {
        // Phase is dealt from the seed alone, so every puff of a strike shares it. Anything
        // per-puff averages out over hundreds of them and leaves a surface of revolution.
        Assert.Equal(CloudPuffs.Spec(0, 7).Phase, CloudPuffs.Spec(1, 7).Phase, 6);
        Assert.Equal(CloudPuffs.Spec(0, 7).Phase, CloudPuffs.Spec(CloudPuffs.TotalCount - 1, 7).Phase, 6);
        Assert.NotEqual(CloudPuffs.Spec(0, 7).Phase, CloudPuffs.Spec(0, 8).Phase);
    }

    [Fact]
    public void The_canopy_is_not_a_surface_of_revolution()
    {
        // The cauliflower heads. Hold everything about a cap puff fixed except where it sits
        // around the cloud's axis, and the distance it is placed at has to change: if it does
        // not, the canopy is a torus however much each puff boils along it.
        NuclearCloudDimensions d = Dims();
        CloudAnimationState anim = FullyGrown(d);
        PuffSpec s = CloudPuffs.Spec(0, 7);
        Assert.True(s.Cap);
        s.Theta0 = 0.7f; s.Omega = 0f; s.Swirl = 0f; s.Rho01 = 0.6f;

        float min = float.MaxValue, max = 0f;
        for (int i = 0; i < 64; i++)
        {
            PuffSpec a = s;
            a.Azimuth = (float)(2.0 * System.Math.PI * i / 64.0);
            float dist = Radius(CloudPuffs.At(a, 0f, d, anim));
            if (dist < min) min = dist;
            if (dist > max) max = dist;
        }
        Assert.True((max - min) / max > 0.25f,
            "the canopy varies by only " + ((max - min) / max).ToString("P0") + " around its axis");
    }

    [Fact]
    public void The_column_is_lumpy_around_its_axis_too()
    {
        NuclearCloudDimensions d = Dims();
        CloudAnimationState anim = FullyGrown(d);
        PuffSpec s = ColumnPuff(7);
        s.Rho01 = 0.6f; s.Swirl = 0f;

        float min = float.MaxValue, max = 0f;
        for (int i = 0; i < 64; i++)
        {
            PuffSpec a = s;
            a.Azimuth = (float)(2.0 * System.Math.PI * i / 64.0);
            float dist = Radius(CloudPuffs.At(a, 0f, d, anim));
            if (dist < min) min = dist;
            if (dist > max) max = dist;
        }
        Assert.True((max - min) / max > 0.18f,
            "the stem varies by only " + ((max - min) / max).ToString("P0") + " around its axis");
    }

    [Fact]
    public void The_columns_ripple_never_repeats()
    {
        // Two wobbles on incommensurate periods rather than one. A single sine is a period, and
        // a period is exactly what the eye picks out of a crowd of puffs.
        Assert.True(CloudPuffs.ColumnWobble > 0f && CloudPuffs.ColumnWobbleFast > 0f);
        Assert.NotEqual(CloudPuffs.Spec(3, 7).Wobble, CloudPuffs.Spec(3, 7).Wobble2);
    }

    private static PuffSpec ColumnPuff(int seed)
    {
        for (int i = 0; i < CloudPuffs.TotalCount; i++)
        {
            PuffSpec s = CloudPuffs.Spec(i, seed);
            if (!s.Cap && !s.Fire) return s;
        }
        throw new System.InvalidOperationException("no column puffs");
    }

    private static float Radius(PuffPoint p)
    {
        return (float)System.Math.Sqrt(p.X * p.X + p.Z * p.Z);
    }

    private static float PeakHeight(PuffSpec s, NuclearCloudDimensions d, CloudAnimationState anim)
    {
        float peak = 0f;
        for (float t = 0f; t < d.RiseSeconds * 3f; t += 0.05f)
        {
            float y = CloudPuffs.At(s, t, d, anim).Y;
            if (y > peak) peak = y;
        }
        return peak;
    }
}
