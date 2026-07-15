namespace MissileDisaster.Core
{
    /// <summary>爆発高度。空中爆発はクレーター無し・降下物ほぼ無しだが爆風/熱線で被害面積が広がる。地上爆発はクレーター＋降下物。</summary>
    public enum BurstType
    {
        Airburst,    // 空中爆発
        Groundburst, // 地上爆発
    }
}
