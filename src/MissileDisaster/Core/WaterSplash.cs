namespace MissileDisaster.Core
{
    /// <summary>
    /// How far and how deep a detonation presses the water surface down (no UnityEngine
    /// dependency).
    ///
    /// <para>
    /// The blast drives the surface down into a cavity; the water then rebounds and the wave
    /// radiates outward. Only the first half of that has to be modelled here - the game's own
    /// water simulation does the rebound and the propagation once the surface has been
    /// displaced, which is both less code and better physics than anything hand-rolled.
    /// </para>
    ///
    /// <para>
    /// The displacement goes through <c>DisasterHelpers.SplashWater(position, radius, depth)</c>,
    /// the same call the vanilla meteor makes from <c>MeteorAI.ArriveAtDestination</c> and the
    /// earthquake from <c>EarthquakeAI.SimulationStep</c>. Read out of the IL: the depth is
    /// encoded as <c>clamp(depth * 64, -32767, 32767)</c> into a signed Int16, so about 512 m
    /// either way, and the wave grid is 16 m to a cell. The meteor passes crater radius x2 and
    /// crater depth x10.
    /// </para>
    ///
    /// <para>
    /// Those meteor factors are not reused. This mod's craters are deliberately exaggerated -
    /// a real 1 t bomb digs a hole barely wider than a bus, which is invisible at play zoom - so
    /// multiplying an already-inflated crater depth by ten would put a 1.5 t warhead 69 m into
    /// the sea. The fireball is the better anchor: it is the part of the explosion that actually
    /// couples energy into the water, and it is already calibrated against real figures for every
    /// warhead. Both numbers are therefore taken from it.
    /// </para>
    /// </summary>
    public static class WaterSplash
    {
        /// <summary>The disturbance spreads wider than the fireball that drove it.</summary>
        public const float RadiusPerFireball = 3f;

        /// <summary>
        /// How deep the surface is pressed, against the fireball radius. Well under 1: the cavity
        /// a burst opens is much wider than it is deep, which is why the wave spreads rather than
        /// standing up as a column.
        /// </summary>
        public const float DepthPerFireball = 0.3f;

        /// <summary>Below one wave cell nothing registers in the simulation at all.</summary>
        public const float MinRadius = 16f;

        /// <summary>
        /// Ceilings. A strategic warhead's fireball is kilometres across and would otherwise ask
        /// to displace the whole map to the seabed; these keep the biggest yields dramatic
        /// without turning the map into a bowl. MaxDepth also stays well inside the roughly 512 m
        /// the API can encode, so the clamp never silently truncates.
        /// </summary>
        public const float MaxRadius = 2000f;
        public const float MaxDepth = 200f;

        /// <summary>Whether this detonation displaces water at all.</summary>
        public static bool Displaces(float fireballRadius)
        {
            return fireballRadius > 0f;
        }

        /// <summary>The radius the surface is disturbed over, in metres.</summary>
        public static float Radius(float fireballRadius)
        {
            if (fireballRadius <= 0f) return 0f;
            return Clamp(fireballRadius * RadiusPerFireball, MinRadius, MaxRadius);
        }

        /// <summary>
        /// How far the surface is pressed down, in metres. Positive is downwards, matching
        /// MakeCrater's depth and the vanilla callers of SplashWater.
        /// </summary>
        public static float Depth(float fireballRadius)
        {
            if (fireballRadius <= 0f) return 0f;
            float d = fireballRadius * DepthPerFireball;
            return d > MaxDepth ? MaxDepth : d;
        }

        private static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
