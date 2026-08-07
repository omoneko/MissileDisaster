namespace MissileDisaster.Core
{
    /// <summary>
    /// A radioactive contamination zone: world-space centre, radius in metres, the in-game time
    /// it started (ticks) and its current intensity (0-255).
    /// Intensity is a float because decontamination lowers it continuously and very small
    /// decrements need to accumulate as fractions; it is rounded to an integer when written to
    /// the ground pollution field. An intensity lowered by decontamination never rises again.
    /// No UnityEngine dependency.
    /// </summary>
    public struct ContaminationZone
    {
        public float CenterX;
        public float CenterZ;
        public float Radius;
        public long StartTicks;
        public float Intensity;

        public ContaminationZone(float centerX, float centerZ, float radius, long startTicks)
            : this(centerX, centerZ, radius, startTicks, 255f)
        {
        }

        public ContaminationZone(float centerX, float centerZ, float radius, long startTicks, float intensity)
        {
            CenterX = centerX;
            CenterZ = centerZ;
            Radius = radius;
            StartTicks = startTicks;
            Intensity = intensity;
        }
    }
}
