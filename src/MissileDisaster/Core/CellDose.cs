namespace MissileDisaster.Core
{
    /// <summary>What to apply to one cell of the pollution grid: Index is the cell's linear index and Intensity is the contamination level (0-255). No UnityEngine dependency.</summary>
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
