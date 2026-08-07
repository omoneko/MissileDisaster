namespace MissileDisaster.Core
{
    /// <summary>A horizontal bearing offset as (X, Z). No UnityEngine dependency.</summary>
    public struct Offset2
    {
        public float X;
        public float Z;
    }

    /// <summary>
    /// Works out the horizontal position of a trajectory's apex for a missile arriving from a
    /// fixed bearing. Bearings run clockwise, with 0 degrees as +Z (north) and 90 as +X (east).
    /// No UnityEngine dependency.
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
