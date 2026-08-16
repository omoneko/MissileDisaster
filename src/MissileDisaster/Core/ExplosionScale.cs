using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// Works out how the game's own explosion effect should be dispatched so that what appears on
    /// screen matches the yield. Pure, with no UnityEngine dependency.
    ///
    /// The important part is what the two dispatch arguments actually do, which was read out of
    /// the base game's IL rather than guessed at:
    ///
    ///   ParticleEffect.RenderEffect  particlesPerSquare = timeDelta * magnitude * 0.01
    ///   ParticleEffect.EmitParticles count = max(100, PI*r*r) * particlesPerSquare
    ///                                and each particle is placed uniformly inside a disc of
    ///                                radius r, taken from the SpawnArea
    ///
    /// So EffectManager.DispatchEffect's magnitude is a particle *density*, not a size: raising it
    /// packs more particles into the same place, and with a SpawnArea radius of zero the area is
    /// floored at 100 m^2, which is why an effect dispatched that way looks identical at every
    /// yield no matter how large the magnitude is. The radius of the SpawnArea is the only
    /// argument that makes the effect physically bigger. (Per-particle size lives in the effect
    /// prefab itself and can only be changed by modifying a shared game asset.)
    ///
    /// For reference, MeteorAI dispatches its own impact with radius 0 and magnitude 1, so the
    /// magnitudes here stay within a small factor of 1 - other parts of the same effect, such as
    /// LightEffect's brightness, are multiplied by it too.
    /// </summary>
    public static class ExplosionScale
    {
        // ---------------------------------------------------------------------------------------
        // NOT CURRENTLY USED BY THE MOD. Everything from here down to Magnitude() describes how to
        // dispatch a vanilla effect, and the mod stopped doing that: the size of an individual
        // particle lives in the effect prefab, a shared game asset, so neither argument below can
        // make a vanilla explosion smaller - only sparser or wider. Non-nuclear warheads are drawn
        // by ExplosionFallback instead, where the flame size is a parameter.
        //
        // It is kept because it is the only written record of what DispatchEffect's magnitude
        // actually means, read out of the IL and confirmed by the tests below. Anyone who reaches
        // for DispatchEffect in this codebase should read it first and not have to rediscover it.
        // ---------------------------------------------------------------------------------------

        // The two constants the emitted count is built from, straight out of the IL above.
        public const float EmitAreaFloor = 100f;   // max(100, PI*r*r)
        public const float DensityPerMagnitude = 0.01f;

        // The spawn disc against the fireball. It is 1:1 because FireballRadius already IS the
        // fireball: this used to be 0.5 against the destruction radius, which is a gameplay
        // figure sized to bring city blocks down, and the result was a 1.5 t bomb throwing a
        // fireball 80 m across. Halving that number was never the fix - it was the wrong number.
        public const float SpawnRadiusFraction = 1f;
        public const float SpawnRadiusMin = 8f;
        // Ceiling on the disc. The count goes with its area, so this is what keeps a strategic
        // warhead from asking for hundreds of thousands of particles a second.
        public const float SpawnRadiusMax = 250f;

        // Particle budgets, in particles per second, that the magnitude is solved for. The single
        // figure is set so that a disc at SpawnRadiusMax still solves to a magnitude above
        // MagnitudeMin - that is, so the clamp never quietly pushes the emission over budget.
        public const float SingleParticlesPerSecond = 1000f;
        public const float SubmunitionParticlesPerSecond = 250f; // there are ten or more of these at once
        public const float NuclearParticlesPerSecond = 1200f;

        // Bounds on the magnitude. The base game uses 1; straying far from it would leave the
        // light flash either invisible or blinding.
        public const float MagnitudeMin = 0.5f;
        public const float MagnitudeMax = 8f;

        /// <summary>
        /// How wide the fireball should read, in metres: the warhead's own FireballRadius, which
        /// is what the explosion looks like rather than what it damages.
        /// <para>
        /// This used to be the widest of the destruction, burn and crater radii - that is, the
        /// visual was as large as the damage. A 1.5 t conventional warhead came out with a
        /// particle disc 82 m across against a real fireball nearer 34 m, roughly six times the
        /// area, which is what a subscriber reported. The damage figures are untouched; only what
        /// is drawn moved.
        /// </para>
        /// </summary>
        public static float FireballRadius(WarheadSpec spec)
        {
            return spec.FireballRadius < 0f ? 0f : spec.FireballRadius;
        }

        /// <summary>
        /// How much larger than life the flame is drawn, against the physical fireball above.
        ///
        /// <para>
        /// The physical figure is right and it is still too small to read. A 1 t charge's
        /// fireball is 35 m across; a Cities: Skylines building footprint is 32 m, so a
        /// correctly-sized explosion is about the size of one house and is gone inside a second.
        /// A subscriber's report after the last change - that conventional strikes had stopped
        /// having any impact - is that, and it is a rendering problem rather than a physics one.
        /// </para>
        ///
        /// <para>
        /// This mod already answers it twice elsewhere, in the same terms. WarheadSpec exaggerates
        /// every non-nuclear crater by about 2.4x because a real 1 t crater is "barely wider than
        /// a bus, which is invisible at the zoom the game is played at". NuclearCloudDisplay draws
        /// its fireball at 0.38 against the cloud's 0.06 - over six times its proportional share -
        /// because "it is the smallest part of the effect and the only part judged against the
        /// buildings around it". The conventional fireball was given neither allowance and was
        /// held to the bare figure, which is the inconsistency.
        /// </para>
        ///
        /// It is kept modest on purpose. The complaint that started this work was the opposite
        /// one - the fireball was drawn at the damage radius, 41 m for a 1.5 t warhead - and at
        /// 1.45 the drawn ball is still barely a quarter of that in area. What must not come back
        /// is a bomb that looks like a bomb four times its size.
        public const float DrawnFireballFactor = 1.45f;

        /// <summary>
        /// The radius the flame and its flash are actually drawn at. Only those two: the water it
        /// displaces and the column it lifts are both driven by the physical fireball, because
        /// one is an energy coupling and the other is already large enough to read.
        /// </summary>
        public static float DrawnFireballRadius(WarheadSpec spec)
        {
            return FireballRadius(spec) * DrawnFireballFactor;
        }

        /// <summary>
        /// How far the blast front sweeps, in metres: the widest of what the warhead destroys,
        /// half of what it sets alight, and one and a half times the crater. The shock wave
        /// follows this - it is the one effect that genuinely should span the damaged area, and
        /// it is deliberately left at the figure the fireball used to be drawn at.
        /// </summary>
        public static float BlastRadius(WarheadSpec spec)
        {
            float r = spec.DestructionRadius;
            float burn = spec.BurnRadius * 0.5f;
            if (burn > r) r = burn;
            float crater = spec.CraterRadius * 1.5f;
            if (crater > r) r = crater;
            return r < 0f ? 0f : r;
        }

        /// <summary>The radius of the disc the effect's particles are spawned over - the one argument that makes the explosion physically larger.</summary>
        public static float SpawnRadius(float fireballRadius)
        {
            return Clamp(fireballRadius * SpawnRadiusFraction, SpawnRadiusMin, SpawnRadiusMax);
        }

        /// <summary>Convenience: the spawn radius for a whole warhead.</summary>
        public static float SpawnRadius(WarheadSpec spec)
        {
            return SpawnRadius(FireballRadius(spec));
        }

        /// <summary>
        /// The magnitude to dispatch with, solved from the count formula so that a disc of this
        /// radius emits roughly particlesPerSecond however large it is. A bigger explosion
        /// therefore covers more ground at a steady density instead of piling particles onto one
        /// spot.
        /// </summary>
        public static float Magnitude(float spawnRadius, float particlesPerSecond)
        {
            float area = (float)(Math.PI * spawnRadius * spawnRadius);
            if (area < EmitAreaFloor) area = EmitAreaFloor;
            return Clamp(particlesPerSecond / (area * DensityPerMagnitude), MagnitudeMin, MagnitudeMax);
        }

        /// <summary>The particles a second a disc of this radius actually emits at this magnitude. Used to keep the budget honest in tests.</summary>
        public static float ParticlesPerSecond(float spawnRadius, float magnitude)
        {
            float area = (float)(Math.PI * spawnRadius * spawnRadius);
            if (area < EmitAreaFloor) area = EmitAreaFloor;
            return area * magnitude * DensityPerMagnitude;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }
    }
}
