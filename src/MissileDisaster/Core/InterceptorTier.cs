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

    /// <summary>ARROW(超高高度)→SAM(高高度)→PAC(終端)の3層。帯は地面から連続。数値は暫定(実機調整)。</summary>
    public static class InterceptorTiers
    {
        public static readonly InterceptorTier Pac = new InterceptorTier
        {
            Kind = InterceptorKind.Pac, AltitudeMin = 0f, AltitudeMax = 800f,
            HorizontalRange = 2000f, InterceptChance = 0.75f, CooldownSeconds = 4f
        };
        public static readonly InterceptorTier Sam = new InterceptorTier
        {
            Kind = InterceptorKind.Sam, AltitudeMin = 800f, AltitudeMax = 2500f,
            HorizontalRange = 4000f, InterceptChance = 0.6f, CooldownSeconds = 6f
        };
        public static readonly InterceptorTier Arrow = new InterceptorTier
        {
            Kind = InterceptorKind.Arrow, AltitudeMin = 2500f, AltitudeMax = 100000f,
            HorizontalRange = 6000f, InterceptChance = 0.5f, CooldownSeconds = 8f
        };

        /// <summary>迎撃試行順(高い帯から)。</summary>
        public static readonly InterceptorTier[] Ordered = { Arrow, Sam, Pac };
    }
}
