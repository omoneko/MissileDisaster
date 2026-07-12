namespace MissileDisaster.Core
{
    /// <summary>弾頭種別。Phase 1 は Conventional のみ挙動を実装する。</summary>
    public enum WarheadType
    {
        Conventional,
        Cluster,
        WhitePhosphorus,
        Thermobaric,
        Nuclear,
    }
}
