using MissileDisaster.Core;
using Xunit;

public class ExplosionScaleTests
{
    private static WarheadSpec Conventional(int kilograms)
    {
        return WarheadSpec.For(WarheadType.Conventional).Scaled(ConventionalYields.Multiplier(kilograms));
    }

    private static WarheadSpec Phosphorus(int kilograms)
    {
        return WarheadSpec.For(WarheadType.WhitePhosphorus).Scaled(ConventionalYields.Multiplier(kilograms));
    }

    [Fact]
    public void A_heavier_charge_covers_more_ground()
    {
        float small = ExplosionScale.SpawnRadius(Conventional(100));
        float baseline = ExplosionScale.SpawnRadius(Conventional(1000));
        float large = ExplosionScale.SpawnRadius(Conventional(10000));
        Assert.True(small < baseline, "100 kg is smaller than the 1 t baseline");
        Assert.True(baseline < large, "10 t is larger than the 1 t baseline");
    }

    [Fact]
    public void The_size_tracks_the_charge_across_the_usual_range()
    {
        // The point of the mapping: no two ordinary charges look the same. 1.5 t against 20 t is
        // the comparison the calibration was written around.
        Assert.True(ExplosionScale.SpawnRadius(Conventional(20000)) >
                    ExplosionScale.SpawnRadius(Conventional(1500)) * 2f,
            "a 20 t warhead explodes over visibly more ground than a 1.5 t one");
    }

    [Fact]
    public void An_incendiary_still_grows_with_the_charge_through_its_fires()
    {
        // White phosphorus has a fixed destruction radius, so the fireball has to follow the
        // burn radius instead - otherwise every charge would look identical.
        float baseline = ExplosionScale.SpawnRadius(Phosphorus(1000));
        float large = ExplosionScale.SpawnRadius(Phosphorus(8000));
        Assert.True(large > baseline * 1.5f, "a heavier incendiary charge burns over visibly more ground");
    }

    [Fact]
    public void The_spawn_radius_is_clamped_at_both_ends()
    {
        Assert.Equal(ExplosionScale.SpawnRadiusMin, ExplosionScale.SpawnRadius(Conventional(1)), 3);
        Assert.Equal(ExplosionScale.SpawnRadiusMax, ExplosionScale.SpawnRadius(Conventional(100000000)), 3);
    }

    [Fact]
    public void A_zero_charge_never_returns_a_negative_radius()
    {
        var dud = WarheadSpec.For(WarheadType.Conventional).Scaled(0f);
        Assert.Equal(0f, ExplosionScale.FireballRadius(dud), 3);
        Assert.Equal(0f, ExplosionScale.BlastRadius(dud), 3);
        Assert.Equal(ExplosionScale.SpawnRadiusMin, ExplosionScale.SpawnRadius(dud), 3);
    }

    [Fact]
    public void The_blast_radius_takes_the_widest_effect()
    {
        // Conventional destroys further than half of what it burns, so the destruction leads.
        var conv = WarheadSpec.For(WarheadType.Conventional);
        Assert.Equal(conv.DestructionRadius, ExplosionScale.BlastRadius(conv), 3);
        // White phosphorus barely destroys anything, so its fires lead instead.
        var wp = WarheadSpec.For(WarheadType.WhitePhosphorus);
        Assert.Equal(wp.BurnRadius * 0.5f, ExplosionScale.BlastRadius(wp), 3);
    }

    [Fact]
    public void A_smaller_fireball_keeps_its_density_rather_than_thinning_out()
    {
        // Shrinking the fireball must not leave a sparse puff. Below the budget-limited range the
        // magnitude sits at its ceiling, so the emission falls with the area and the particles
        // per square metre stay put - the explosion gets smaller, not thinner.
        float smallR = ExplosionScale.SpawnRadius(Conventional(1500));
        float largeR = ExplosionScale.SpawnRadius(Conventional(20000));

        float smallDensity = ExplosionScale.Magnitude(smallR, ExplosionScale.SingleParticlesPerSecond);
        float largeDensity = ExplosionScale.Magnitude(largeR, ExplosionScale.SingleParticlesPerSecond);

        Assert.True(smallR < largeR);
        Assert.Equal(largeDensity, smallDensity, 3);
        Assert.Equal(ExplosionScale.MagnitudeMax, smallDensity, 3);
    }

    [Fact]
    public void The_fireball_is_far_smaller_than_the_area_the_warhead_damages()
    {
        // The reported bug: the fireball was drawn at the destruction radius, so a 1.5 t bomb
        // threw one 82 m across. It must read as the charge going off, not as the damage.
        foreach (WarheadType type in new[]
                 { WarheadType.Conventional, WarheadType.Cluster, WarheadType.Thermobaric })
        {
            var spec = WarheadSpec.For(type);
            Assert.True(ExplosionScale.FireballRadius(spec) < ExplosionScale.BlastRadius(spec),
                type + ": the fireball is not smaller than the blast");
        }
    }

    [Fact]
    public void A_1500_kg_conventional_fireball_is_about_17_m()
    {
        // The figure the fix is calibrated on: 1.5 * cbrt(1500 kg) is about 17 m, against the
        // 41 m the old destruction-driven formula produced.
        var spec = WarheadSpec.For(WarheadType.Conventional)
            .Scaled(ConventionalYields.Multiplier(1500));

        float fireball = ExplosionScale.SpawnRadius(spec);
        Assert.InRange(fireball, 15f, 20f);

        // And that it really is the large reduction that was asked for: a particle cloud reads
        // by area, so compare areas against what the destruction radius would have given.
        float oldSpawn = ExplosionScale.BlastRadius(spec) * 0.5f;   // the old fraction
        float areaRatio = (fireball * fireball) / (oldSpawn * oldSpawn);
        Assert.InRange(areaRatio, 0.10f, 0.25f);
    }

    [Fact]
    public void Every_warhead_that_uses_the_particle_effect_has_a_fireball()
    {
        // Nuclear is the one exception: NuclearMushroomFx builds its fireball from the yield, and
        // ExplosionFx returns before reading the spec's figure.
        foreach (WarheadType type in new[]
                 { WarheadType.Conventional, WarheadType.Cluster,
                   WarheadType.WhitePhosphorus, WarheadType.Thermobaric })
        {
            Assert.True(WarheadSpec.For(type).FireballRadius > 0f, type + " has no fireball radius");
        }
    }

    [Fact]
    public void The_fireball_grows_with_the_charge_even_for_an_incendiary()
    {
        // An incendiary keeps its blast fixed however large the charge, but the charge still
        // burns - so the fireball has to grow where the destruction radius does not.
        var small = WarheadSpec.For(WarheadType.WhitePhosphorus).Scaled(ConventionalYields.Multiplier(500));
        var large = WarheadSpec.For(WarheadType.WhitePhosphorus).Scaled(ConventionalYields.Multiplier(4000));

        Assert.Equal(small.DestructionRadius, large.DestructionRadius, 3);
        Assert.True(large.FireballRadius > small.FireballRadius * 1.5f);
    }

    // ---- the magnitude, which the base game's IL says is a density and not a size ----

    [Fact]
    public void The_magnitude_holds_the_particle_budget_whatever_the_size()
    {
        // A bigger explosion must spread the same rate of particles over more ground rather than
        // pile more of them onto one spot, or it stops being affordable.
        foreach (int kg in new[] { 100, 1000, 5000, 50000, 1000000 })
        {
            float r = ExplosionScale.SpawnRadius(Conventional(kg));
            float m = ExplosionScale.Magnitude(r, ExplosionScale.SingleParticlesPerSecond);
            float actual = ExplosionScale.ParticlesPerSecond(r, m);
            Assert.InRange(actual, 1f, ExplosionScale.SingleParticlesPerSecond * 1.01f);
        }
    }

    [Fact]
    public void A_larger_explosion_asks_for_a_lower_density()
    {
        // Compared well above 63 m of spawn radius, which is where the solved magnitude drops
        // below MagnitudeMax. Below that the clamp holds both sides at the ceiling and there is
        // no ordering to assert - the emission is then area-limited rather than budget-limited,
        // which is the right behaviour for a small fireball and is covered by the test below.
        float small = ExplosionScale.SpawnRadius(Conventional(100000));
        float large = ExplosionScale.SpawnRadius(Conventional(1000000));
        Assert.True(small > 63f && large > small, "the sizes must be in the unclamped range");
        Assert.True(ExplosionScale.Magnitude(large, ExplosionScale.SingleParticlesPerSecond) <
                    ExplosionScale.Magnitude(small, ExplosionScale.SingleParticlesPerSecond),
            "spreading the same budget over more ground means fewer particles per square metre");
    }

    [Fact]
    public void The_magnitude_stays_near_the_value_the_base_game_uses()
    {
        // MeteorAI dispatches its own impact with a magnitude of 1, and the same number scales
        // the light flash, so anything wildly away from it would look wrong rather than big.
        foreach (int kg in new[] { 1, 100, 1000, 20000, 1000000 })
        {
            float r = ExplosionScale.SpawnRadius(Conventional(kg));
            Assert.InRange(ExplosionScale.Magnitude(r, ExplosionScale.SingleParticlesPerSecond),
                ExplosionScale.MagnitudeMin, ExplosionScale.MagnitudeMax);
        }
    }

    [Fact]
    public void A_tiny_disc_is_charged_for_the_hundred_square_metre_floor()
    {
        // EmitParticles floors the area at 100 m^2, so a smaller disc emits no fewer particles.
        Assert.Equal(ExplosionScale.ParticlesPerSecond(0f, 1f),
                     ExplosionScale.ParticlesPerSecond(3f, 1f), 3);
        Assert.Equal(ExplosionScale.EmitAreaFloor * ExplosionScale.DensityPerMagnitude,
                     ExplosionScale.ParticlesPerSecond(0f, 1f), 3);
    }

    [Fact]
    public void The_flame_is_drawn_larger_than_life_but_not_by_much()
    {
        // The physical figure is right and still smaller than one building at the zoom the game
        // is played at, so the drawn flame gets a readability allowance - the same one the crater
        // has had all along. What it must not do is undo the report that started this work, where
        // a 1.5 t warhead threw a fireball as wide as its destruction radius.
        var spec = Conventional(1500);
        float physical = ExplosionScale.FireballRadius(spec);
        float drawn = ExplosionScale.DrawnFireballRadius(spec);

        Assert.True(drawn > physical, "the flame is drawn at the bare figure again");
        Assert.True(drawn < physical * 2f, "the allowance has grown into an exaggeration");
        // The old drawing put the spawn disc at half the destruction radius - 41 m for this
        // warhead, which is the figure that was reported. Compared by area, which is what is
        // actually seen: the flame must stay under half of what it was.
        float oldDrawn = spec.DestructionRadius * 0.5f;
        Assert.True(drawn * drawn < oldDrawn * oldDrawn * 0.5f,
            "the drawn flame covers " + (drawn * drawn / (oldDrawn * oldDrawn)).ToString("P0")
            + " of the area that was reported as too big");
    }

    [Fact]
    public void The_allowance_never_reaches_the_blast_it_sits_inside()
    {
        // However large the charge, the flame must stay inside the area the warhead damages -
        // a fireball wider than the destruction radius is the exact thing that was reported.
        foreach (int kg in new[] { 100, 1000, 1500, 10000, 100000 })
        {
            var spec = Conventional(kg);
            Assert.True(ExplosionScale.DrawnFireballRadius(spec) < ExplosionScale.BlastRadius(spec),
                kg + " kg draws a flame wider than its own blast");
        }

        var thermobaric = WarheadSpec.For(WarheadType.Thermobaric);
        Assert.True(ExplosionScale.DrawnFireballRadius(thermobaric) < ExplosionScale.BlastRadius(thermobaric));
    }

    [Fact]
    public void The_allowance_is_only_on_what_is_drawn()
    {
        // The water a burst displaces is an energy coupling and the column it lifts is already
        // large enough to read, so both stay on the physical fireball. Only the flame and its
        // flash get the allowance, and this is what pins that down.
        var spec = Conventional(1000);
        Assert.Equal(spec.FireballRadius, ExplosionScale.FireballRadius(spec), 3);
        Assert.Equal(spec.FireballRadius * ExplosionScale.DrawnFireballFactor,
            ExplosionScale.DrawnFireballRadius(spec), 3);
    }

    [Fact]
    public void A_dud_is_drawn_at_nothing_rather_than_at_a_negative_size()
    {
        var dud = WarheadSpec.For(WarheadType.Nuclear); // carries no fireball radius of its own
        Assert.Equal(0f, ExplosionScale.DrawnFireballRadius(dud), 3);
    }
}
