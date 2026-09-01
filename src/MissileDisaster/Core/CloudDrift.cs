using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// How far downwind a cloud has been carried. Pure, with no UnityEngine dependency.
    ///
    /// <para>
    /// A stabilised cloud does not sit over the city it came from; it is a body of smoke in
    /// moving air, and it goes where the air goes. Drawing it pinned to ground zero for its whole
    /// life is the thing that makes it read as an object rather than as weather.
    /// </para>
    ///
    /// <para>
    /// Two rules keep it honest. The drift starts at nothing and builds, because a column being
    /// driven up by its own buoyancy is not yet being pushed sideways by anything - it wins
    /// against the wind while it is rising and loses once it stabilises. And the top of the cloud
    /// drifts further than the bottom, because wind speed grows with height; that shear is what
    /// leans a real cloud over instead of sliding it sideways as a rigid shape.
    /// </para>
    ///
    /// This was implemented once before and taken back out, because it had not been asked for.
    /// It has been now.
    /// </summary>
    public static class CloudDrift
    {
        /// <summary>
        /// Metres per second the cloud is carried at, at the top of the column, for a wind the
        /// game reports at this strength. Cities: Skylines has no wind speed in the sense of
        /// m/s - only a direction and a weather intensity - so this is a drawing figure rather
        /// than a measurement, chosen so a cloud leans visibly over its life without sailing off
        /// the map.
        ///
        /// 5.5 m/s is about 20 km/h, a moderate breeze, and it was measured against what it
        /// produces rather than picked: a 150 kt cloud then leans about one cap-width downwind
        /// over its life, and a bomb's little column drifts clear of its own crater without
        /// sliding off the scene. At 7 the small one travelled 2.5 times its own width and read
        /// as escaping.
        /// </summary>
        public const float TopSpeed = 5.5f;

        /// <summary>What the cloud's base drifts at, against the top. Less, because the wind is weaker down there - and the difference is what leans the column over.</summary>
        public const float BaseSpeedFraction = 0.25f;

        /// <summary>
        /// The drift builds in rather than starting at full speed: while the column is climbing,
        /// its own buoyancy is winning. By the time it has stabilised the air has it.
        /// </summary>
        public static float Ramp(float seconds, float riseSeconds)
        {
            if (seconds <= 0f) return 0f;
            if (riseSeconds <= 0f) return 1f;
            float u = seconds / riseSeconds;
            if (u > 1f) u = 1f;
            return u * u; // slow to take hold, then in earnest
        }

        /// <summary>
        /// How far downwind a point at this height has been carried, in metres, after this many
        /// seconds. heightFraction is 0 at the ground and 1 at the top of the cloud.
        /// </summary>
        public static float Offset(float seconds, float riseSeconds, float heightFraction)
        {
            if (seconds <= 0f) return 0f;
            if (heightFraction < 0f) heightFraction = 0f;
            if (heightFraction > 1f) heightFraction = 1f;
            float speed = TopSpeed * (BaseSpeedFraction + (1f - BaseSpeedFraction) * heightFraction);
            // The ramp applies to the distance, not the speed, so the integral stays simple and
            // the offset is monotonic - a cloud must never drift back the way it came.
            return speed * seconds * Ramp(seconds, riseSeconds);
        }

        /// <summary>
        /// The unit vector the wind is blowing towards, from the direction the game stores in
        /// degrees. Cities: Skylines converts it as x = sin(theta), z = cos(theta) - read out of
        /// FogEffect and DayNightDynamicCloudsProperties, which both do exactly this - so the
        /// mod's smoke drifts the same way the game's own fog and clouds do.
        /// </summary>
        public static void Direction(float degrees, out float x, out float z)
        {
            double rad = degrees * Math.PI / 180.0;
            x = (float)Math.Sin(rad);
            z = (float)Math.Cos(rad);
        }
    }
}
