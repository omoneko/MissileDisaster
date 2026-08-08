namespace MissileDisaster.Core
{
    /// <summary>
    /// Turns a warhead spec into the scale factor its explosion effect is played at, so that the
    /// fireball on screen matches the yield instead of looking the same at every charge. Pure,
    /// with no UnityEngine dependency.
    /// The effect radii have already been scaled by the yield when this is called, so the scale
    /// factor follows the same cube-root law they do: doubling the radius doubles the fireball.
    /// The clamps are wide enough that the whole practical range of yields moves the size, and
    /// the ceilings exist to stop an extreme yield asking for an effect the size of the map.
    /// </summary>
    public static class ExplosionScale
    {
        // A single detonation - conventional or thermobaric. The reference radius is what a
        // scale of 1 corresponds to; the 1 t conventional default lands near 1.8.
        public const float SingleReferenceRadius = 40f;
        public const float SingleMin = 0.5f;
        public const float SingleMax = 14f;

        // One submunition of a scattering warhead. There are ten or more of them, so each is
        // played smaller than a single detonation of the same radius.
        public const float SubmunitionReferenceRadius = 24f;
        public const float SubmunitionMin = 0.4f;
        public const float SubmunitionMax = 5f;

        // The single very large nuclear effect. The floor keeps even a sub-kiloton device
        // looking nuclear.
        public const float NuclearReferenceRadius = 60f;
        public const float NuclearMin = 12f;
        public const float NuclearMax = 140f;

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

        /// <summary>The scale for a single detonation, conventional or thermobaric.</summary>
        public static float ForSingle(WarheadSpec spec)
        {
            return Clamp(VisualRadius(spec) / SingleReferenceRadius, SingleMin, SingleMax);
        }

        /// <summary>The scale for one submunition of a scattering warhead - cluster or white phosphorus.</summary>
        public static float ForSubmunition(WarheadSpec spec)
        {
            return Clamp(VisualRadius(spec) / SubmunitionReferenceRadius, SubmunitionMin, SubmunitionMax);
        }

        /// <summary>The scale for a nuclear detonation, whose effect is played very much larger than any other.</summary>
        public static float ForNuclear(WarheadSpec spec)
        {
            return Clamp(VisualRadius(spec) / NuclearReferenceRadius, NuclearMin, NuclearMax);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }
    }
}
