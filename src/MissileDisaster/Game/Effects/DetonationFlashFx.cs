using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// The flash of a nuclear detonation: one very bright point light at the burst, gone again in
    /// under a second.
    /// <para>
    /// Why this exists as its own effect. Every other warhead dispatches the game's meteor impact
    /// effect, and that effect carries a LightEffect - the flash that lights the city up for a
    /// moment. A nuclear detonation deliberately does not use the vanilla effect at all (nothing
    /// in it can be stretched over the kilometres involved), so rebuilding the nuclear visual as
    /// our own fireball and cloud took the flash away with it. A subscriber noticed. This puts it
    /// back without touching the mushroom.
    /// </para>
    /// It runs on the game clock like everything else here, so it freezes with a paused game
    /// rather than burning through its life while nothing else moves. Main thread only.
    /// </summary>
    public static class DetonationFlashFx
    {
        // Real figures, loosely. The visible flash of a fission burst is over in well under a
        // second at these yields; the eye keeps it far longer than the physics does. Held brief
        // on purpose - a long flash reads as a bug, not as a bomb.
        private const float RiseSeconds = 0.06f;   // to full brightness
        private const float HoldSeconds = 0.10f;   // at full brightness
        private const float FadeSeconds = 0.70f;   // back to nothing

        // The light reaches far beyond the fireball itself, which is what makes it read as a
        // flash lighting the city rather than as a glowing ball.
        private const float RangePerFireballRadius = 14f;
        private const float RangeMax = 8000f;      // beyond this Unity gains nothing and the cost grows

        // Unity intensity. Vanilla LightEffects sit near 1-3; a nuclear flash should be plainly
        // brighter than anything else on screen without turning the frame white for a second.
        private const float IntensityMin = 6f;
        private const float IntensityMax = 22f;
        private const float IntensityPerKilotonRoot = 3.2f;   // times cbrt(kt), then clamped

        // White with the faintest warmth. A nuclear flash is near-white; tinting it orange makes
        // it read as an ordinary explosion.
        private static readonly Color FlashColor = new Color(1f, 0.97f, 0.90f, 1f);

        /// <summary>
        /// Plays the flash at the point of burst. yieldKilotons sizes it; fireballRadius is the
        /// radius the fireball itself is being drawn at, which the light's reach is built from.
        /// A failure here never stops the detonation resolving.
        /// </summary>
        public static void Play(Vector3 burstPoint, int yieldKilotons, float fireballRadius)
        {
            try
            {
                var go = new GameObject("MissileDisaster_NuclearFlash");
                go.transform.position = burstPoint;

                Light light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = FlashColor;
                light.range = Mathf.Min(fireballRadius * RangePerFireballRadius, RangeMax);
                light.intensity = 0f;   // the behaviour raises it; a full-brightness first frame would pop
                light.shadows = LightShadows.None;   // a shadow pass at this range costs far more than it adds

                var flash = go.AddComponent<FlashBehaviour>();
                flash.PeakIntensity = PeakIntensity(yieldKilotons);

                ModConfig.Log("DetonationFlashFx: flash at " + burstPoint + " (" + yieldKilotons
                    + " kt, range " + light.range.ToString("0") + " m, peak "
                    + flash.PeakIntensity.ToString("0.0") + ")");
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("DetonationFlashFx.Play error: " + e);
            }
        }

        /// <summary>
        /// How bright the flash gets, from the yield. It follows the cube root rather than the
        /// yield itself: a 1000 kt device is not a thousand times brighter to look at than a 1 kt
        /// one, and a linear law would either black out the small yields or white out the large.
        /// </summary>
        public static float PeakIntensity(int yieldKilotons)
        {
            if (yieldKilotons <= 0) return IntensityMin;
            float root = (float)System.Math.Pow(yieldKilotons, 1.0 / 3.0);
            float i = IntensityPerKilotonRoot * root;
            if (i < IntensityMin) return IntensityMin;
            if (i > IntensityMax) return IntensityMax;
            return i;
        }

        /// <summary>
        /// Drives one flash through rise, hold and fade on the game clock, then removes it.
        /// Its own component rather than SimulationTimed because there are no ParticleSystems
        /// here to set a simulation speed on - what has to follow the clock is the intensity.
        /// </summary>
        private class FlashBehaviour : MonoBehaviour
        {
            public float PeakIntensity;

            private Light _light;
            private float _age;

            private void Start()
            {
                _light = GetComponent<Light>();
            }

            private void Update()
            {
                if (_light == null) { Destroy(gameObject); return; }

                _age += EffectClock.Delta;

                if (_age < RiseSeconds)
                {
                    _light.intensity = PeakIntensity * (_age / RiseSeconds);
                    return;
                }
                if (_age < RiseSeconds + HoldSeconds)
                {
                    _light.intensity = PeakIntensity;
                    return;
                }

                float fade = (_age - RiseSeconds - HoldSeconds) / FadeSeconds;
                if (fade >= 1f) { Destroy(gameObject); return; }

                // Squared, so it drops away fast and then lingers faintly, the way an
                // afterglow does - a linear fade reads as a dimmer being turned down.
                float k = 1f - fade;
                _light.intensity = PeakIntensity * k * k;
            }
        }
    }
}
