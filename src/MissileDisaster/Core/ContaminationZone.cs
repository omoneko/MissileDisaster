namespace MissileDisaster.Core
{
    /// <summary>
    /// 放射能汚染ゾーン。ワールド座標中心・半径(m)・発生ゲーム内時刻(Ticks)・現在の濃度(0-255)。
    /// Intensity は除染施設により恒久的に低下する（下がった濃度で維持され、元には戻らない）。
    /// NuclearMeltdown.Core.ContaminationZone を拡張移植。UnityEngine 非依存。
    /// </summary>
    public struct ContaminationZone
    {
        public float CenterX;
        public float CenterZ;
        public float Radius;
        public long StartTicks;
        public byte Intensity;

        public ContaminationZone(float centerX, float centerZ, float radius, long startTicks)
            : this(centerX, centerZ, radius, startTicks, 255)
        {
        }

        public ContaminationZone(float centerX, float centerZ, float radius, long startTicks, byte intensity)
        {
            CenterX = centerX;
            CenterZ = centerZ;
            Radius = radius;
            StartTicks = startTicks;
            Intensity = intensity;
        }
    }
}
