using System;

namespace MissileDisaster.Core
{
    /// <summary>The mushroom cloud's animation state at one moment: how far grown, how visible.</summary>
    public struct CloudAnimationState
    {
        public float HeightFraction; // of the full cloud height
        public float WidthFraction;  // of the full cap width; lags the height, so the column shoots up before it fattens
        public float Alpha;          // 0..1, honoured only when the material can actually fade
        public bool Finished;        // the cloud is over and its objects can be destroyed
    }

    /// <summary>
    /// The mushroom cloud's life as a pure timeline - grow, stand, fade - so the shape of the
    /// animation can be tested without Unity. The frame loop just asks where it is now.
    ///
    /// The growth follows an ease-out curve, which is the right shape for the physics as well as
    /// the eye: a cloud rises fastest through its first seconds and settles asymptotically into
    /// its stabilised height. The width runs the same ease raised to WidthLagPower, so it trails
    /// the height - in every photograph the column climbs first and the cap billows out of its
    /// head afterwards, and this one exponent is what reproduces that order.
    /// </summary>
    public static class CloudAnimation
    {
        /// <summary>
        /// How sharply the growth front-loads itself: 1 is a constant climb, and the higher it
        /// goes the more of the height is reached in the first moments.
        ///
        /// <para>
        /// It was a cube, and a cube is too eager - two thirds of the height inside the first
        /// third of the rise, which reads as the cloud being pushed up rather than climbing. The
        /// shape is still an ease-out, because a real cloud does decelerate into its stabilised
        /// height; it just does not leap. Lowering the exponent is the right knob for that and
        /// lengthening the rise is not, because a longer rise at the same curve simply spends
        /// more of it standing still at the top.
        /// </para>
        /// </summary>
        public const float RiseEasePower = 1.8f;

        /// <summary>The scale the cloud is born at, small enough to be hidden inside the fireball.</summary>
        public const float BirthFraction = 0.12f;

        /// <summary>The width's ease is the height's raised to this, which holds the cap back while the column climbs.</summary>
        public const float WidthLagPower = 1.6f;

        /// <summary>How far into the rise the cloud takes to fade fully in, when the material can fade at all.</summary>
        public const float FadeInFraction = 0.15f;

        /// <summary>How much the cloud keeps swelling through the fade, reading as dispersal rather than deletion.</summary>
        public const float FadeDrift = 0.06f;

        /// <summary>
        /// How much wider the cap goes over the whole of the stand and the fade, on top of the
        /// width it reached at the end of the rise.
        ///
        /// A cap that stops growing the moment the column tops out is the thing the playtest
        /// caught: the updraft is still feeding it, so it cannot be finished. A real cap spreads
        /// along the tropopause for as long as it has anything left to spread - Castle Bravo's
        /// doubled in the ten minutes after it stabilised. The height does not follow, because
        /// the lid is what stopped it climbing in the first place.
        /// </summary>
        public const float CapSpreadAfterRise = 0.45f;

        /// <summary>Where the animation is at t seconds into a cloud with the given phase lengths.</summary>
        public static CloudAnimationState At(float t, float riseSeconds, float holdSeconds, float fadeSeconds)
        {
            var s = new CloudAnimationState();
            if (t < 0f) t = 0f;
            float fadeStart = riseSeconds + holdSeconds;
            float end = fadeStart + fadeSeconds;

            // Growth, over the rise, from the birth fraction up to 1.
            float u = riseSeconds > 0f ? t / riseSeconds : 1f;
            if (u > 1f) u = 1f;
            float ease = EaseOut(u);
            s.HeightFraction = BirthFraction + (1f - BirthFraction) * ease;
            s.WidthFraction = BirthFraction + (1f - BirthFraction) * (float)Math.Pow(ease, WidthLagPower);

            // Once it has risen, the cap goes on spreading sideways for the rest of the shot.
            if (t > riseSeconds)
            {
                float rest = holdSeconds + fadeSeconds;
                float after = rest > 0f ? (t - riseSeconds) / rest : 1f;
                if (after > 1f) after = 1f;
                s.WidthFraction *= 1f + CapSpreadAfterRise * after;
            }

            // Visibility: quickly in at birth, out over the fade.
            float fadeInSeconds = riseSeconds * FadeInFraction;
            if (t < fadeInSeconds && fadeInSeconds > 0f)
            {
                s.Alpha = t / fadeInSeconds;
            }
            else if (t < fadeStart || fadeSeconds <= 0f)
            {
                s.Alpha = t < end ? 1f : 0f;
            }
            else
            {
                float f = (t - fadeStart) / fadeSeconds;
                if (f > 1f) f = 1f;
                // The thinning itself is per puff and staggered - see CloudPuffs - so the cloud
                // breaks up raggedly over many seconds instead of evaporating in one piece.
                s.Alpha = 1f;
                // Dispersal: the cloud loosens as it thins away. Height only - the width is
                // already spreading under CapSpreadAfterRise, and doubling it up would run the
                // cap past the envelope its puffs are placed inside.
                s.HeightFraction *= 1f + FadeDrift * f;
            }

            s.Finished = t >= end;
            return s;
        }

        private static float EaseOut(float u)
        {
            if (u <= 0f) return 0f;
            if (u >= 1f) return 1f;
            return 1f - (float)Math.Pow(1f - u, RiseEasePower);
        }
    }
}
