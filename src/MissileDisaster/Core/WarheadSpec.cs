namespace MissileDisaster.Core
{
    /// <summary>
    /// 弾頭ごとの着弾パラメータ（UnityEngine 非依存の数値表）。
    /// Phase 1 は Conventional のみ実値。他種別は暫定で Conventional と同値を返し、
    /// 後続 Phase（弾頭分岐・核）で差別化する。
    /// </summary>
    public struct WarheadSpec
    {
        public float CraterRadius;
        public float CraterDepth;
        public float DestructionRadius;
        public bool Contaminates;

        public static WarheadSpec For(WarheadType type)
        {
            // Phase 1: すべて通常弾頭相当（後続 Phase で type ごとに分岐）。
            return new WarheadSpec
            {
                CraterRadius = 60f,
                CraterDepth = 16f,
                DestructionRadius = 120f,
                Contaminates = false,
            };
        }
    }
}
