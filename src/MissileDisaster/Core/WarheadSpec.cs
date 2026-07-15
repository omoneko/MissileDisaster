namespace MissileDisaster.Core
{
    /// <summary>
    /// 弾頭ごとの着弾パラメータ（UnityEngine 非依存の数値表）。半径は実世界の被害半径に基づく（ゲームバランス非考慮）。
    /// 核は「爆風半径 ∝ 威力^(1/3)」で較正: 150kt 基準で 5psi(建物倒壊)≈3.7km、熱線/延焼≈5.9km、降下物≈5.3km。
    /// これに Scaled(cbrt(kt/150)) を掛けると任意威力で実半径 R = C×kt^(1/3) になる
    /// （例 1Mt: 破壊≈7.0km・延焼≈11km は Nukemap 等の実値と一致）。
    /// 利用可能な着弾APIは MakeCrater/DestroyStuff のみのため、火災は延焼帯、汚染は土壌汚染で表現する。
    /// </summary>
    public struct WarheadSpec
    {
        public WarheadType Type;
        public float CraterRadius;
        public float CraterDepth;
        public float DestructionRadius;   // 建物倒壊（核=5psi 相当）
        public int SubmunitionCount;
        public float SpreadRadius;
        public bool RaiseCraterEdges;
        public float BurnRadius;          // 延焼/熱線（核=3度熱傷相当。破壊より広い）
        public bool Contaminates;
        public float ContaminationRadius; // 放射性降下物（核のみ>0）

        // 空中爆発時に破壊・延焼半径へ掛ける係数（爆風/熱線が広い面積に及ぶため）。
        public const float AirBurstBlastFactor = 1.35f;

        /// <summary>
        /// 爆発高度を反映した新しい spec を返す（不変・元は変えない）。
        /// 地上爆発は変化なし。空中爆発はクレーター/汚染を無くし、破壊・延焼を AirBurstBlastFactor 倍に広げる。
        /// </summary>
        public WarheadSpec WithBurst(BurstType burst)
        {
            if (burst == BurstType.Groundburst) return this;

            WarheadSpec s = this; // struct のコピー（元は不変）
            s.CraterRadius = 0f;
            s.CraterDepth = 0f;
            s.RaiseCraterEdges = false;
            s.Contaminates = false;
            s.ContaminationRadius = 0f;
            s.DestructionRadius *= AirBurstBlastFactor;
            s.BurnRadius *= AirBurstBlastFactor;
            return s;
        }

        /// <summary>効果半径(クレーター/破壊/延焼/汚染)を multiplier 倍した新しい spec を返す（不変・元は変えない）。</summary>
        public WarheadSpec Scaled(float multiplier)
        {
            WarheadSpec s = this; // struct のコピー（呼び出し元の元 spec は不変）
            s.CraterRadius *= multiplier;
            s.CraterDepth *= multiplier;
            s.DestructionRadius *= multiplier;
            s.BurnRadius *= multiplier;
            s.ContaminationRadius *= multiplier;
            return s;
        }

        public static WarheadSpec For(WarheadType type)
        {
            switch (type)
            {
                case WarheadType.Cluster:
                    // クラスター爆弾: 子弾を広く散布。1発は小威力だが被害面積が広い。
                    return new WarheadSpec
                    {
                        Type = type,
                        CraterRadius = 4f, CraterDepth = 2f, DestructionRadius = 20f,
                        SubmunitionCount = 10, SpreadRadius = 260f,
                        RaiseCraterEdges = false, BurnRadius = 12f, Contaminates = false,
                    };
                case WarheadType.WhitePhosphorus:
                    // 白リン(焼夷): 爆発破壊は小さいが延焼が広い。子弾散布で広域炎上。
                    return new WarheadSpec
                    {
                        Type = type,
                        CraterRadius = 3f, CraterDepth = 1f, DestructionRadius = 15f,
                        SubmunitionCount = 14, SpreadRadius = 220f,
                        RaiseCraterEdges = false, BurnRadius = 70f, Contaminates = false,
                    };
                case WarheadType.Thermobaric:
                    // サーモバリック(気化爆弾, 大型 FAE 相当): 過圧で広範囲を薙ぎ倒す。クレーターは浅い。
                    return new WarheadSpec
                    {
                        Type = type,
                        CraterRadius = 20f, CraterDepth = 6f, DestructionRadius = 200f,
                        SubmunitionCount = 1, SpreadRadius = 0f,
                        RaiseCraterEdges = true, BurnRadius = 220f, Contaminates = false,
                    };
                case WarheadType.Nuclear:
                    // 核(基準 150kt・地上爆発の実被害半径): 5psi=3.7km, 熱線=5.9km, 降下物=5.3km, クレーター=210m。
                    // 任意威力は Scaled(cbrt(kt/150)) で実半径にスケールする。
                    return new WarheadSpec
                    {
                        Type = type,
                        CraterRadius = 210f, CraterDepth = 64f, DestructionRadius = 3720f,
                        SubmunitionCount = 1, SpreadRadius = 0f,
                        RaiseCraterEdges = true, BurnRadius = 5850f,
                        Contaminates = true, ContaminationRadius = 5300f,
                    };
                default: // Conventional（大型 HE 弾頭 ~1t 相当の実被害半径）
                    return new WarheadSpec
                    {
                        Type = WarheadType.Conventional,
                        CraterRadius = 10f, CraterDepth = 4f, DestructionRadius = 80f,
                        SubmunitionCount = 1, SpreadRadius = 0f,
                        RaiseCraterEdges = false, BurnRadius = 40f, Contaminates = false,
                    };
            }
        }
    }
}
