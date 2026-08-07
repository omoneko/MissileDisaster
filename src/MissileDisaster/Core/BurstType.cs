namespace MissileDisaster.Core
{
    /// <summary>Burst height. An airburst leaves no crater and almost no fallout, but its blast and thermal radiation cover a wider area. A groundburst leaves both a crater and fallout.</summary>
    public enum BurstType
    {
        Airburst,    // detonates in the air
        Groundburst, // detonates at ground level
    }
}
