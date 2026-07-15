namespace MissileDisaster.Core
{
    /// <summary>
    /// 放射能汚染ゾーン。ワールド座標中心・半径(m)・発生ゲーム内時刻(Ticks)・現在濃度(0-255)。
    /// Intensity は float（除染で連続的に低下し、微小な減衰も端数として蓄積される）。土壌汚染フィールドへ
    /// 書き込む際に整数へ丸める。除染で下がった濃度は元へ戻らない。UnityEngine 非依存。
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
