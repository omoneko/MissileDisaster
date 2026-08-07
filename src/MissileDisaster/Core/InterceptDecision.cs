namespace MissileDisaster.Core
{
    /// <summary>
    /// Pure decision on whether an interception succeeds. The random number is injected as the
    /// roll argument so it can be tested. No UnityEngine dependency.
    /// altitude is the missile's height above the ground and horizontalDistance is how far away
    /// the interceptor building is.
    /// </summary>
    public static class InterceptDecision
    {
        public static bool InEngagementZone(float missileAltitude, float horizontalDistance, InterceptorTier tier)
        {
            return missileAltitude >= tier.AltitudeMin
                && missileAltitude < tier.AltitudeMax
                && horizontalDistance <= tier.HorizontalRange;
        }

        public static bool ShouldIntercept(float missileAltitude, float horizontalDistance, InterceptorTier tier, float roll)
        {
            return InEngagementZone(missileAltitude, horizontalDistance, tier)
                && roll < tier.InterceptChance;
        }
    }
}
