using System;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// The glow of a detonation in the air: an additive ball at the burst that swells and fades.
    ///
    /// <para>
    /// This exists because a Unity Light does not. A Light changes how surfaces are shaded and
    /// puts nothing in the air itself, so a camera looking at the sky sees the ground brighten
    /// and no flash at all - the light never appears to reach the sky. What sells a flash filling
    /// the sky is something actually drawn there, which is what this is: a handful of large
    /// additive billboards, blown outward from the burst so the glow spreads rather than sitting
    /// as a disc.
    /// </para>
    ///
    /// It is drawn on the game's clock like every other effect here, and it is deliberately
    /// shorter-lived than the fireball it accompanies - a flash that lingers reads as a bug.
    /// Main thread only.
    /// </summary>
    public static class GlowFx
    {
        private const int Particles = 14;          // enough to fill; few enough to stay cheap
        private const float SpreadFraction = 0.35f; // how far they drift out over the life
        private const float Growth = 2.4f;          // the glow swells as it fades

        // Near-white with the faintest warmth, matching the flash's own colour. Alpha is high:
        // this is additive, so it reads as light rather than as a grey ball.
        private static readonly Color GlowCore = new Color(1f, 0.98f, 0.92f, 0.95f);
        private static readonly Color GlowEdge = new Color(1f, 0.86f, 0.62f, 0.85f);

        /// <summary>
        /// Draws the glow at the burst. fireballRadius sizes it through factor - a nuclear flash
        /// is seen from the next county and dwarfs its own fireball, a bomb's barely leaves it.
        /// seconds is how long the flash it belongs to lasts. A failure never stops anything else.
        /// </summary>
        /// <summary>
        /// The same ball, held back for delaySeconds and drawn in its own colour: the cooling
        /// afterglow that follows a nuclear flash, which is orange where the flash is white.
        /// </summary>
        public static void PlayDelayed(Vector3 burstPoint, float fireballRadius, float seconds,
            float factor, float delaySeconds, Color colour)
        {
            Play(burstPoint, fireballRadius, seconds, factor, delaySeconds, colour, colour);
        }

        public static void Play(Vector3 burstPoint, float fireballRadius, float seconds, float factor)
        {
            Play(burstPoint, fireballRadius, seconds, factor, 0f, GlowCore, GlowEdge);
        }

        private static void Play(Vector3 burstPoint, float fireballRadius, float seconds, float factor,
            float delaySeconds, Color core, Color edge)
        {
            if (fireballRadius <= 0f || seconds <= 0f || factor <= 0f) return;
            try
            {
                float radius = fireballRadius * factor;

                var go = ParticleBuilder.NewSystem("DetonationGlow", burstPoint, ParticleAssets.Fire);
                var ps = go.GetComponent<ParticleSystem>();
                var main = ps.main;
                main.startDelay = delaySeconds;
                main.startLifetime = seconds;
                main.startSpeed = radius * SpreadFraction / seconds;
                // One particle already spans the glow; several overlapping make it solid at the
                // centre and ragged at the edge, which is what a flash in air looks like.
                main.startSize = new ParticleSystem.MinMaxCurve(radius * 1.1f, radius * 1.8f);
                main.startColor = new ParticleSystem.MinMaxGradient(core, edge);
                main.maxParticles = Particles * 2;

                ParticleBuilder.Burst(ps, Particles);
                ParticleBuilder.Sphere(ps, radius * 0.25f);
                // Full brightness almost at once - a flash has no rise to speak of at this scale -
                // then away fast and lingering faintly, matching the light's own envelope.
                ParticleBuilder.Fade(ps,
                    new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(1f, 0.06f),
                    new GradientAlphaKey(0.35f, 0.4f), new GradientAlphaKey(0f, 1f));
                ParticleBuilder.SizeCurve(ps, 0.75f, Growth);
                ParticleBuilder.PlayAndDestroy(go, delaySeconds + seconds + 0.5f);
            }
            catch (Exception e)
            {
                ModConfig.LogError("GlowFx.Play error: " + e);
            }
        }
    }
}
