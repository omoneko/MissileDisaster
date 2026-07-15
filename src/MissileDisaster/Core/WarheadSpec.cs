namespace MissileDisaster.Core
{
    /// <summary>
    /// 弾頭ごとの着弾パラメータ（UnityEngine 非依存の数値表）。
    /// 利用可能な着弾APIは MakeCrater / DestroyStuff のみのため、種別差は
    /// 「クレーター形状・破壊範囲・子弾散布・縁の隆起」で表現する。
    /// 放射能汚染(Nuclear)は Contaminates フラグのみで、実際の汚染は後続フェーズに委ねる。
    /// </summary>
    public struct WarheadSpec
    {
        public WarheadType Type;
        public float CraterRadius;
        public float CraterDepth;
        public float DestructionRadius;
        public int SubmunitionCount;   // 1=単一着弾、>1=子弾散布(クラスター等)
        public float SpreadRadius;     // 子弾を散布する半径(単一着弾は0)
        public bool RaiseCraterEdges;  // 過圧系(サーモバリック/核)はクレーター縁を持ち上げる
        public bool Contaminates;      // 核のみ(実汚染は後続フェーズ)

        public static WarheadSpec For(WarheadType type)
        {
            switch (type)
            {
                case WarheadType.Cluster:
                    // 広く浅い多点被害。単一の深いクレーターは作らない。
                    return new WarheadSpec
                    {
                        Type = type,
                        CraterRadius = 18f, CraterDepth = 5f, DestructionRadius = 45f,
                        SubmunitionCount = 9, SpreadRadius = 160f,
                        RaiseCraterEdges = false, Contaminates = false,
                    };
                case WarheadType.WhitePhosphorus:
                    // 焼夷弾の広域散布。火災の実発火は未対応のため、小被害の多点散布で近似する。
                    return new WarheadSpec
                    {
                        Type = type,
                        CraterRadius = 10f, CraterDepth = 3f, DestructionRadius = 40f,
                        SubmunitionCount = 12, SpreadRadius = 140f,
                        RaiseCraterEdges = false, Contaminates = false,
                    };
                case WarheadType.Thermobaric:
                    // 過圧で建物を薙ぎ倒す。クレーターは浅く広い、破壊範囲は最大級。
                    return new WarheadSpec
                    {
                        Type = type,
                        CraterRadius = 70f, CraterDepth = 10f, DestructionRadius = 220f,
                        SubmunitionCount = 1, SpreadRadius = 0f,
                        RaiseCraterEdges = true, Contaminates = false,
                    };
                case WarheadType.Nuclear:
                    // 巨大クレーター＋広域壊滅。汚染フラグを立てる(実汚染は後続)。
                    return new WarheadSpec
                    {
                        Type = type,
                        CraterRadius = 150f, CraterDepth = 40f, DestructionRadius = 380f,
                        SubmunitionCount = 1, SpreadRadius = 0f,
                        RaiseCraterEdges = true, Contaminates = true,
                    };
                default: // Conventional（Phase 1 の基準値を維持）
                    return new WarheadSpec
                    {
                        Type = WarheadType.Conventional,
                        CraterRadius = 60f, CraterDepth = 16f, DestructionRadius = 120f,
                        SubmunitionCount = 1, SpreadRadius = 0f,
                        RaiseCraterEdges = false, Contaminates = false,
                    };
            }
        }
    }
}
