namespace MissileDisaster.Core
{
    /// <summary>
    /// 迎撃可否の純粋判定。乱数は引数(roll)注入でテスト可能に。UnityEngine 非依存。
    /// altitude はミサイルの対地高度、horizontalDistance は迎撃建物までの水平距離。
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
