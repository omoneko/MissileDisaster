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
        // The two constants the emitted count is built from, straight out of the IL above.
        public const float EmitAreaFloor = 100f;   // max(100, PI*r*r)
        public const float DensityPerMagnitude = 0.01f;

        // The spawn disc against what the warhead does. Half the visual radius reads as a
        // fireball filling the heart of the blast rather than the whole damaged area.
        public const float SpawnRadiusFraction = 0.5f;
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
        /// The radius the visible explosion should read as, in metres: the widest of what the
        /// warhead destroys, half of what it sets alight, and half the width of the crater it
        /// digs. Taking the widest is what lets an incendiary - whose destruction radius is fixed
        /// however large the charge - still grow its fireball with the yield through the fires.
        /// </summary>
        public static float VisualRadius(WarheadSpec spec)
        {
            float r = spec.DestructionRadius;
            float burn = spec.BurnRadius * 0.5f;
            if (burn > r) r = burn;
            float crater = spec.CraterRadius * 1.5f;
            if (crater > r) r = crater;
            return r < 0f ? 0f : r;
        }

        /// <summary>The radius of the disc the effect's particles are spawned over - the one argument that makes the explosion physically larger.</summary>
        public static float SpawnRadius(float visualRadius)
        {
            return Clamp(visualRadius * SpawnRadiusFraction, SpawnRadiusMin, SpawnRadiusMax);
        }

        /// <summary>Convenience: the spawn radius for a whole warhead.</summary>
        public static float SpawnRadius(WarheadSpec spec)
        {
            return SpawnRadius(VisualRadius(spec));
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
