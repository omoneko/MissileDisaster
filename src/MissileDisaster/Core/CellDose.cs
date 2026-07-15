namespace MissileDisaster.Core
{
    /// <summary>汚染グリッドの1セルへの適用量。Index=セル線形index、Intensity=汚染濃度(0-255)。UnityEngine 非依存。</summary>
    public struct CellDose
    {
        public int Index;
        public byte Intensity;

        public CellDose(int index, byte intensity)
        {
            Index = index;
            Intensity = intensity;
        }
    }
}
