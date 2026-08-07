namespace MissileDisaster.Core
{
    public enum InterceptorKind { Arrow, Sam, Pac }

    /// <summary>An interception layer: the altitude band it covers, its horizontal range, its hit probability and its cooldown. No UnityEngine dependency.</summary>
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
    /// Three layers, from the top down: exo-atmospheric, high altitude, then terminal. The
    /// bands are continuous from the ground up.
    /// InterceptChance is the single-shot probability of a kill, set near published real-world
    /// figures: about 0.65 for terminal point defence (PAC-3), about 0.75 at high altitude
    /// (THAAD) and about 0.60 for the hardest midcourse intercept (SM-3/Aegis).
    /// A launcher fires exactly one round per engagement, spending its cooldown, so this
    /// probability is the actual kill rate.
    /// With all three layers firing, the cumulative rate is about
    /// 1-(1-.65)(1-.75)(1-.60) = 0.965, the high figure a layered defence should give.
    /// CooldownSeconds is the reload time before the same launcher can fire again. An active
    /// radar multiplies the kill probability by 1.5, capped at 1.0.
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

        /// <summary>The order interception is attempted in, from the highest band down.</summary>
        public static readonly InterceptorTier[] Ordered = { Arrow, Sam, Pac };
    }
}
