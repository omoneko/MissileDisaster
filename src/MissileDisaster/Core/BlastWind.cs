using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// The wind a detonation throws outward, which is what picks cars and people up and tumbles
    /// them away from ground zero. Pure, with no UnityEngine dependency.
    ///
    /// <para>
    /// The game does the throwing itself, and this class exists to hand it the right numbers.
    /// <c>DisasterHelpers.AddWind(position, radius, directionalWind, rotationalWind, radialWind,
    /// group)</c> walks the vehicle and citizen grids and gives each one a wind of
    /// <c>(directional + outward*radial) * (1 - dist/radius)</c> - a linear falloff to nothing at
    /// the edge. <c>CarAI.AddWind</c> then blends an eighth of that into the vehicle's frame
    /// velocity, turns the body into the gust, clears the destination with SetTarget and sets
    /// <c>Vehicle.Flags2.Blown</c>, after which <c>CarAI.SimulationStepBlown</c> flies it. All of
    /// that is read out of the IL of Assembly-CSharp, and the constants below are ports of it, so
    /// what this class predicts is what the game will actually do.
    /// </para>
    /// </summary>
    public static class BlastWind
    {
        // ---------------------------------------------------------------- ports of the game

        /// <summary>CarAI.AddWind ignores any wind whose upward component is not above this. It is 2g, and it means the lift - not the radial push - is what decides whether a car moves at all.</summary>
        public const float Gate = 19.620001f;

        /// <summary>CarAI.AddWind blends rather than adds: velocity = velocity*0.875 + wind*0.125. A single call therefore delivers an eighth of the wind.</summary>
        public const float Blend = 0.125f;

        /// <summary>CarAI.SimulationStepBlown, per step: position += v/2, v.y -= 2.4525, v *= 0.99, position += v/2.</summary>
        public const float GravityPerStep = 2.4525f;
        public const float DragPerStep = 0.99f;

        /// <summary>
        /// One blown step is half a second, and the velocity is in metres per step. Solved rather
        /// than assumed: the step's gravity is g*T^2 in position units, and 2.4525 = 9.81*T^2
        /// gives T = 0.5 s. So a real speed in m/s is twice the number the frame carries.
        /// </summary>
        public const float StepSeconds = 0.5f;

        // ---------------------------------------------------------------- what we ask for

        /// <summary>
        /// How far out the wind reaches, against the destruction radius. Short of it: out at the
        /// edge buildings are damaged rather than levelled, and cars being hurled about there
        /// would read as wrong.
        /// </summary>
        public const float ReachFraction = 0.55f;
        public const float ReachMin = 20f;
        // A one-shot walk of the vehicle and citizen grids, but a walk over a square kilometre or
        // two all the same. The ceiling is a cost bound, not a physical one, so it is soft: a
        // Tsar Bomba still reaches further than a 150 kt, just not proportionally.
        public const float ReachKnee = 900f;
        public const float ReachCeiling = 1400f;

        /// <summary>
        /// How hard it throws, as the upward component of the wind. It scales with the blast
        /// radius, and getting that wrong is worth recording.
        ///
        /// <para>
        /// The first attempt held it constant, on the argument that the destruction radius is
        /// where the overpressure levels a building, and the wind behind a front goes with the
        /// overpressure, so the wind at a given fraction of that radius is the same at any yield.
        /// The first half of that is true. The conclusion is not: a car is not accelerated to the
        /// wind speed, it is accelerated by drag over the positive phase, so what throws it is an
        /// impulse - dynamic pressure times duration - and the duration goes with the cube root
        /// of the yield. Constant strength had a 1 t bomb throwing a car 310 m and 54 m into the
        /// air, which is nonsense: at the overpressure that wrecks a house, cars are rolled and
        /// shoved metres to tens of metres, never launched hundreds.
        /// </para>
        ///
        /// So the strength is linear in the blast radius, which is linear in the impulse. The
        /// floor is set just above <see cref="Gate"/> so that the smallest charge still shoves
        /// the cars right next to it rather than doing nothing at all - the failure mode this
        /// mod's effects keep finding.
        /// </summary>
        public const float LiftMin = 26f;
        public const float LiftKnee = 78f;
        public const float LiftCeiling = 110f;
        /// <summary>The destruction radius at which the lift reaches LiftKnee: a 150 kt groundburst.</summary>
        public const float LiftReferenceRadius = 3720f;

        /// <summary>Thrown outward more than upward - a blast wave travels along the ground.</summary>
        public const float RadialPerLift = 1.45f;

        /// <summary>No spin about the centre. That is a vortex's signature, and this is not one.</summary>
        public const float Rotational = 0f;

        /// <summary>How far the wind reaches, in metres, for a warhead with this destruction radius.</summary>
        public static float Reach(float destructionRadius)
        {
            if (destructionRadius <= 0f) return 0f;
            return EffectCeiling.Soft(destructionRadius * ReachFraction,
                ReachMin, ReachKnee, ReachCeiling);
        }

        /// <summary>The upward component of the wind at ground zero, before the falloff.</summary>
        public static float Lift(float destructionRadius)
        {
            if (destructionRadius <= 0f) return 0f;
            float linear = LiftMin
                + (destructionRadius / LiftReferenceRadius) * (LiftKnee - LiftMin);
            return EffectCeiling.Soft(linear, LiftMin, LiftKnee, LiftCeiling);
        }

        /// <summary>The outward component of the wind at ground zero, before the falloff.</summary>
        public static float Radial(float destructionRadius)
        {
            return Lift(destructionRadius) * RadialPerLift;
        }

        // ---------------------------------------------------------------- what the game will do

        /// <summary>DisasterHelpers' falloff: the wind fades linearly to nothing at the edge.</summary>
        public static float Falloff(float distance, float reach)
        {
            if (reach <= 0f) return 0f;
            float f = 1f - distance / reach;
            return f < 0f ? 0f : f;
        }

        /// <summary>Whether a car this far out moves at all - CarAI.AddWind's gate is on the lift, after the falloff.</summary>
        public static bool Blows(float distance, float destructionRadius)
        {
            return Lift(destructionRadius) * Falloff(distance, Reach(destructionRadius)) > Gate;
        }

        /// <summary>
        /// How far a car this far from ground zero is thrown, in metres, and how high it gets.
        /// This runs the game's own blown-vehicle step, so it is a prediction rather than an
        /// estimate - which is the whole point, because the alternative is finding out in a
        /// playtest that a hatchback crossed the river.
        /// </summary>
        public static void Predict(float distance, float destructionRadius,
            out float thrown, out float apex, out float seconds)
        {
            thrown = 0f; apex = 0f; seconds = 0f;
            float reach = Reach(destructionRadius);
            float f = Falloff(distance, reach);
            float lift = Lift(destructionRadius) * f;
            if (lift <= Gate) return;

            float vy = Blend * lift;
            float vr = Blend * Radial(destructionRadius) * f;
            float x = 0f, y = 0f;
            for (int i = 0; i < 400 && (y > 0f || i == 0); i++)
            {
                x += vr * 0.5f; y += vy * 0.5f;
                vy -= GravityPerStep;
                vy *= DragPerStep; vr *= DragPerStep;
                x += vr * 0.5f; y += vy * 0.5f;
                seconds += StepSeconds;
                if (y > apex) apex = y;
            }
            thrown = x;
        }

        /// <summary>The furthest any car is thrown by a warhead with this destruction radius - the nearest ones go furthest.</summary>
        public static float LongestThrow(float destructionRadius)
        {
            float reach = Reach(destructionRadius);
            float worst = 0f;
            for (int i = 0; i <= 40; i++)
            {
                float thrown, apex, seconds;
                Predict(reach * i / 40f, destructionRadius, out thrown, out apex, out seconds);
                if (thrown > worst) worst = thrown;
            }
            return worst;
        }
    }
}
