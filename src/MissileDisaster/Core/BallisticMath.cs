namespace MissileDisaster.Core
{
    /// <summary>
    /// Pure maths for a missile's parabolic flight. No UnityEngine dependency - floats only -
    /// so it is unit testable. The Game layer builds the Vector3 by lerping x and z, and
    /// composing y from a lerp plus ArcHeightAt.
    /// </summary>
    public static class BallisticMath
    {
        public static float Clamp01(float t)
        {
            if (t < 0f) return 0f;
            if (t > 1f) return 1f;
            return t;
        }

        public static float Lerp(float a, float b, float t)
        {
            t = Clamp01(t);
            return a + (b - a) * t;
        }

        /// <summary>The height component of the arc: 0 at t=0 and t=1, and arcHeight at t=0.5.</summary>
        public static float ArcHeightAt(float t, float arcHeight)
        {
            t = Clamp01(t);
            return arcHeight * 4f * t * (1f - t);
        }

        /// <summary>Advances t by however far speed carries it along groundDistance, the distance projected on the ground.</summary>
        public static float AdvanceT(float t, float groundDistance, float speed, float dt)
        {
            if (groundDistance <= 0.0001f) return 1f; // zero distance counts as an immediate impact
            return t + (speed * dt) / groundDistance;
        }
    }
}
