namespace MissileDisaster.Core
{
    public enum InterceptorKind { Arrow, Sam, Pac }

    /// <summary>迎撃層の担当高度帯・水平射程・迎撃確率・クールダウン。UnityEngine 非依存。</summary>
    public struct InterceptorTier
    {
        public InterceptorKind Kind;
        public float AltitudeMin;
        public float AltitudeMax;
        public float HorizontalRange;
        public float InterceptChance; // 0..1
        public float CooldownSeconds;
    }

    /// <summary>
    /// ARROW(超高高度)→SAM(高高度)→PAC(終端)の3層。帯は地面から連続。
    /// InterceptChance は「1発あたりの撃墜確率(single-shot Pk)」で、実世界の公表値に近づけた暫定値:
    ///   PAC-3(終端点防御) ~0.65、THAAD(高高度) ~0.75、SM-3/Aegis(中間段・最難) ~0.60。
    /// 1回の交戦で発射器は1発だけ撃つ(発射でクールダウン消費)ため、この確率が実際の撃墜率になる。
    /// 3層すべてが撃てば累積撃墜率は約 1-(1-.65)(1-.75)(1-.60)≒0.965 と、多層防御らしい高い値になる。
    /// CooldownSeconds は再装填時間(=同一発射器が次に撃てるまで)。レーダー稼働中は Pk×1.5(上限1.0)。
    /// </summary>
    public static class InterceptorTiers
    {
        public static readonly InterceptorTier Pac = new InterceptorTier
        {
            Kind = InterceptorKind.Pac, AltitudeMin = 0f, AltitudeMax = 800f,
            HorizontalRange = 2000f, InterceptChance = 0.65f, CooldownSeconds = 4f
        };
        public static readonly InterceptorTier Sam = new InterceptorTier
        {
            Kind = InterceptorKind.Sam, AltitudeMin = 800f, AltitudeMax = 2500f,
            HorizontalRange = 4000f, InterceptChance = 0.75f, CooldownSeconds = 6f
        };
        public static readonly InterceptorTier Arrow = new InterceptorTier
        {
            Kind = InterceptorKind.Arrow, AltitudeMin = 2500f, AltitudeMax = 100000f,
            HorizontalRange = 6000f, InterceptChance = 0.60f, CooldownSeconds = 8f
        };

        /// <summary>迎撃試行順(高い帯から)。</summary>
        public static readonly InterceptorTier[] Ordered = { Arrow, Sam, Pac };
    }
}
