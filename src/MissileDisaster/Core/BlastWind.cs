namespace MissileDisaster.Core
{
    /// <summary>
    /// The wind a detonation throws outward, which is what picks cars and people up and tumbles
    /// them away from ground zero. Pure, with no UnityEngine dependency.
    ///
    /// <para>
    /// The game does this itself: <c>DisasterHelpers.AddWind(position, radius, directionalWind,
    /// rotationalWind, radialWind, group)</c> walks the vehicle and citizen grids and hands each
    /// one to its AI's <c>AddWind</c>. <c>CarAI.AddWind</c> adds the wind to the vehicle's frame
    /// velocity, turns its body towards the gust, clears its destination with SetTarget, and sets
    /// <c>Vehicle.Flags2.Blown</c> - after which <c>CarAI.SimulationStepBlown</c> flies it instead
    /// of the normal path-following step. So the simulation owns the thrown state; a mod only has
    /// to ask for it. Read out of the IL of Assembly-CSharp, not guessed.
    /// </para>
    ///
    /// The vanilla meteor is the anchor for the numbers: MeteorAI calls the same helper with an
    /// upward 200 and a radial 200.
    /// </summary>
    public static class BlastWind
    {
        /// <summary>What MeteorAI passes: a 200 lift and a 200 radial push. Kept as the reference the figures below are set against.</summary>
        public const float MeteorRadial = 200f;
        public const float MeteorLift = 200f;

        /// <summary>
        /// How far out the wind reaches, against the destruction radius. Short of it: out at the
        /// edge buildings are damaged rather than levelled, and cars being hurled about out there
        /// would read as wrong.
        /// </summary>
        public const float ReachFraction = 0.55f;
        public const float ReachMin = 20f;
        // A one-shot walk of the vehicle grid, but a walk over a couple of square kilometres all
        // the same. This is the only reason for a ceiling; nothing about the physics wants one.
        public const float ReachMax = 1400f;

        /// <summary>
        /// How hard it throws. This does NOT scale with the yield, and that is not a shortcut.
        ///
        /// The destruction radius is by definition the distance at which the overpressure reaches
        /// the level that levels a building, and the wind behind a blast front goes with the
        /// overpressure - so the wind at a given fraction of the destruction radius is about the
        /// same whatever the warhead. What a bigger yield changes is how much ground that covers,
        /// which is exactly what ReachFraction already does. A 1 t bomb throws the cars within
        /// 15 m of it just as hard as a warhead throws the ones 1.4 km away; there are simply far
        /// fewer of them.
        ///
        /// The same argument the rubble's size rests on, and the vanilla meteor agrees: it uses
        /// one fixed 200 for every meteor.
        /// </summary>
        public const float Radial = 200f;

        /// <summary>Thrown outward more than upward - a blast wave travels along the ground.</summary>
        public const float Lift = 140f;

        /// <summary>No spin about the centre. That is a vortex's signature, and this is not one.</summary>
        public const float Rotational = 0f;

        /// <summary>How far the wind reaches, in metres, for a warhead with this destruction radius.</summary>
        public static float Reach(float destructionRadius)
        {
            if (destructionRadius <= 0f) return 0f;
            float r = destructionRadius * ReachFraction;
            if (r < ReachMin) return ReachMin;
            return r > ReachMax ? ReachMax : r;
        }
    }
}
