namespace MissileDisaster.Core
{
    /// <summary>水平方位オフセット(X,Z)。UnityEngine 非依存。</summary>
    public struct Offset2
    {
        public float X;
        public float Z;
    }

    /// <summary>
    /// 固定方位から飛来する弾道の apex(頂点)水平位置を算出する純粋ロジック。
    /// 方位規約: 0°=+Z(北), 90°=+X(東), 時計回りに増加。UnityEngine 非依存。
    /// </summary>
    public static class LaunchGeometry
    {
        public static Offset2 BearingOffset(float bearingDeg, float horizontalDistance)
        {
            double rad = bearingDeg * System.Math.PI / 180.0;
            return new Offset2
            {
                X = (float)(System.Math.Sin(rad) * horizontalDistance),
                Z = (float)(System.Math.Cos(rad) * horizontalDistance),
            };
        }
    }
}
