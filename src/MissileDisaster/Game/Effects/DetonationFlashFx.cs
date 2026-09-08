using System;
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
        private const float NuclearFadeSeconds = 0.70f;      // back to nothing
        // A bomb's flash is not a small nuclear one drawn dimmer, it is a different event: it
        // snaps rather than glares. Sharing the nuclear fade gave it a three-quarter-second
        // afterglow, which reads as a light being turned down over a street.
        private const float ConventionalFadeSeconds = 0.35f;

        // The afterglow: the incandescent ball itself, which outlives the flash by seconds.
        //
        // A Workshop report - "i did like the big ball of light that stayed for several seconds
        // instead of just the flash now" - was about a real regression, though not the one it
        // sounds like. The fireball is still drawn for its full swell, about eight seconds at
        // 150 kt; what vanished was its LIGHT. The flash is deliberately brief, so once it was
        // over the ball went dark and read as embers rather than as something incandescent.
        //
        // Glasstone's two pulses are the answer: the flash is the first, over in a fraction of a
        // second, and the second - the one carrying 99% of the thermal energy - runs for
        // seconds. So the light does not simply switch off at the end of the fade. It drops to a
        // fraction of its peak and decays over the fireball's own lifetime, cooling as the ball
        // cools. The size of the fireball is untouched: this restores the several seconds of
        // light, not the kilometre-wide ball that came from drawing it at the damage radius.
        private const float AfterglowPeakFraction = 0.22f;  // of the flash's peak, where the fade hands over
        private const float AfterglowSecondsFactor = 0.9f;  // of the fireball's swell time
        private const float AfterglowSecondsMin = 2.5f;
        private const float AfterglowSecondsMax = 12f;
        private const float AfterglowGlowFactor = 2.6f;     // the visible ball, tighter than the flash's wash

        // The point light reaches far beyond the fireball itself, which is what makes the ground
        // near the burst read as scorched by it rather than as merely lit.
        private const float RangePerFireballRadius = 14f;
        private const float RangeMax = 8000f;      // beyond this Unity gains nothing and the cost grows

        // A point light cannot turn night into day across a city: its range is finite and it
        // falls off with distance, so districts on the far side stay dark however bright it is.
        // A directional light does exactly that - it lights every surface in the scene equally,
        // wherever the camera is - so the flash is both: a local blaze and a brief wash of
        // daylight over the whole map. Destroying the object restores the night with it, which
        // is why this is a light rather than a change to RenderSettings.
        // The figures live in Core.FlashBrightness, where a test can reach them.

        // Unity intensity for the point light. Vanilla LightEffects sit near 1-3; a nuclear
        // detonation should be in a different league.
        // The figures live in Core.FlashBrightness, where a test can reach them.

        // A conventional warhead flashes too - it has to, now that the mod draws its own fireball
        // instead of the vanilla effect that used to carry a LightEffect - but it lights its
        // surroundings, not the county. No directional component at all.
        // Raised from 0.5 per metre: at a 1 t charge's fireball that solved to 7.5, against the
        // 20 a nuclear flash starts at, and the old cap of 25 needed a 37 t charge to reach, so
        // it was a ceiling nothing could touch. A bomb at night should light the block.
        // The figures live in Core.FlashBrightness, where a test can reach them.

        // How far past the fireball the visible glow reaches. A nuclear flash is seen from the
        // next county, so its glow dwarfs the fireball that made it; a bomb's barely leaves it.
        private const float NuclearGlowFactor = 6f;
        private const float ConventionalGlowFactor = 2.6f;

        // White with the faintest warmth. A nuclear flash is near-white; tinting it orange makes
        // it read as an ordinary explosion.
        private static readonly Color FlashColor = new Color(1f, 0.97f, 0.90f, 1f);

        // The afterglow is warmer than the flash and gets warmer as it cools: the flash is
        // near-white because the fireball is at tens of thousands of degrees, and by the time it
        // is a ball hanging over the city it has dropped into orange.
        private static readonly Color AfterglowColor = new Color(1f, 0.72f, 0.38f, 1f);

        /// <summary>
        /// The flash of a nuclear detonation: a blaze at the burst plus a brief wash of daylight
        /// over the whole map. A failure here never stops the detonation resolving.
        /// </summary>
        public static void PlayNuclear(Vector3 burstPoint, int yieldKilotons, float fireballRadius,
            float fireballSeconds)
        {
            float peak = PeakIntensity(yieldKilotons);
            Spawn("MissileDisaster_NuclearFlash", burstPoint, fireballRadius,
                peak, DirectionalIntensity(yieldKilotons),
                NuclearFadeSeconds, NuclearGlowFactor, yieldKilotons + " kt");

            // The second pulse: the ball goes on burning after the flash has gone.
            float afterglowSeconds = Mathf.Clamp(fireballSeconds * AfterglowSecondsFactor,
                AfterglowSecondsMin, AfterglowSecondsMax);
            SpawnAfterglow(burstPoint, fireballRadius, peak * AfterglowPeakFraction,
                RiseSeconds + HoldSeconds + NuclearFadeSeconds, afterglowSeconds);
        }

        /// <summary>
        /// The flash of a conventional detonation: bright where it went off and nowhere else.
        /// Needed because the mod draws its own fireball now rather than dispatching the vanilla
        /// meteor effect, which used to carry a LightEffect of its own.
        /// </summary>
        public static void PlayConventional(Vector3 burstPoint, float fireballRadius)
        {
            float peak = FlashBrightness.Conventional(fireballRadius);
            Spawn("MissileDisaster_Flash", burstPoint, fireballRadius, peak, 0f,
                ConventionalFadeSeconds, ConventionalGlowFactor,
                fireballRadius.ToString("0") + " m fireball");
        }

        private static void Spawn(string name, Vector3 burstPoint, float fireballRadius,
            float peakIntensity, float directionalIntensity, float fadeSeconds, float glowFactor,
            string what)
        {
            try
            {
                var go = new GameObject(name);
                go.transform.position = burstPoint;

                Light light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = FlashColor;
                light.range = Mathf.Min(Mathf.Max(fireballRadius, 1f) * RangePerFireballRadius, RangeMax);
                light.intensity = 0f;   // the behaviour raises it; a full-brightness first frame would pop
                light.shadows = LightShadows.None;   // a shadow pass at this range costs far more than it adds

                var flash = go.AddComponent<FlashBehaviour>();
                flash.PeakIntensity = peakIntensity;
                flash.FadeSeconds = fadeSeconds;

                // A Light lights surfaces. It puts nothing in the air, so from a camera looking at
                // the sky the flash simply is not there - which is what "it does not spread into
                // the sky" means. This is the glow itself: an additive ball at the burst that
                // swells and fades, drawn against the sky rather than on the ground.
                GlowFx.Play(burstPoint, fireballRadius, RiseSeconds + HoldSeconds + fadeSeconds,
                    glowFactor);

                if (directionalIntensity > 0f)
                {
                    // Its own object, pointing straight down, so the whole map is lit rather
                    // than a sphere around the burst. Parented so it dies with the flash.
                    var sunGo = new GameObject(name + "_Daylight");
                    sunGo.transform.parent = go.transform;
                    sunGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    Light sun = sunGo.AddComponent<Light>();
                    sun.type = LightType.Directional;
                    sun.color = FlashColor;
                    sun.intensity = 0f;
                    sun.shadows = LightShadows.None;
                    flash.Daylight = sun;
                    flash.PeakDaylight = directionalIntensity;
                }

                ModConfig.Log("DetonationFlashFx: flash at " + burstPoint + " (" + what
                    + ", range " + light.range.ToString("0") + " m, peak "
                    + peakIntensity.ToString("0.0")
                    + (directionalIntensity > 0f
                        ? ", daylight " + directionalIntensity.ToString("0.0") : "") + ")");
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("DetonationFlashFx.Spawn error: " + e);
            }
        }

        /// <summary>
        /// How much daylight the flash washes over the map, from the yield. Unity's own sun sits
        /// near 1, so even the smallest of these is brighter than noon - which is the point:
        /// at night the city should read as lit by day for an instant.
        /// </summary>
        public static float DirectionalIntensity(int yieldKilotons)
        {
            return FlashBrightness.Daylight(yieldKilotons);
        }

        /// <summary>
        /// How bright the flash gets, from the yield. It follows the cube root rather than the
        /// yield itself: a 1000 kt device is not a thousand times brighter to look at than a 1 kt
        /// one, and a linear law would either black out the small yields or white out the large.
        /// </summary>
        public static float PeakIntensity(int yieldKilotons)
        {
            return FlashBrightness.Nuclear(yieldKilotons);
        }

        /// <summary>
        /// Drives one flash through rise, hold and fade on the game clock, then removes it.
        /// Its own component rather than SimulationTimed because there are no ParticleSystems
        /// here to set a simulation speed on - what has to follow the clock is the intensity.
        /// </summary>
        /// <summary>
        /// The incandescent ball after the flash has gone: a point light that starts at a
        /// fraction of the flash's peak and cools away over seconds, with the glow to match.
        /// It waits out the flash first, so the two never stack into a double-bright moment.
        /// No directional component - the map-wide wash belongs to the flash alone; a second one
        /// running for seconds would be daylight, not a detonation.
        /// </summary>
        private static void SpawnAfterglow(Vector3 burstPoint, float fireballRadius,
            float peakIntensity, float delaySeconds, float seconds)
        {
            try
            {
                var go = new GameObject("MissileDisaster_NuclearAfterglow");
                go.transform.position = burstPoint;

                Light light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = AfterglowColor;
                light.range = Mathf.Min(Mathf.Max(fireballRadius, 1f) * RangePerFireballRadius, RangeMax);
                light.intensity = 0f;
                light.shadows = LightShadows.None;

                var glow = go.AddComponent<AfterglowBehaviour>();
                glow.PeakIntensity = peakIntensity;
                glow.DelaySeconds = delaySeconds;
                glow.Seconds = seconds;

                // The visible ball, handed the same span so the light and what the eye sees on
                // the sky cool together.
                GlowFx.PlayDelayed(burstPoint, fireballRadius, seconds, AfterglowGlowFactor,
                    delaySeconds, AfterglowColor);
            }
            catch (Exception e)
            {
                ModConfig.LogError("DetonationFlashFx.SpawnAfterglow error: " + e);
            }
        }

        /// <summary>
        /// Holds off through the flash, then decays from the peak to nothing over Seconds.
        /// The decay is cubed rather than squared: a cooling body loses its light faster than it
        /// loses its heat, so most of the brightness goes early and a dull glow rides out the
        /// rest - which is what keeps this reading as a fireball rather than a lamp.
        /// </summary>
        private class AfterglowBehaviour : MonoBehaviour
        {
            public float PeakIntensity;
            public float DelaySeconds;
            public float Seconds;

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
                if (_age < DelaySeconds) { _light.intensity = 0f; return; }

                float t = Seconds > 0f ? (_age - DelaySeconds) / Seconds : 1f;
                if (t >= 1f) { Destroy(gameObject); return; }

                float k = 1f - t;
                _light.intensity = PeakIntensity * k * k * k;
            }
        }

        private class FlashBehaviour : MonoBehaviour
        {
            public float PeakIntensity;
            public float FadeSeconds;    // a bomb snaps, a warhead glares - see the two constants
            public Light Daylight;       // optional; the map-wide wash
            public float PeakDaylight;

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
                float k = Envelope(_age);
                if (k < 0f) { Destroy(gameObject); return; }

                _light.intensity = PeakIntensity * k;
                if (Daylight != null) Daylight.intensity = PeakDaylight * k;
            }

            /// <summary>
            /// 0..1 over rise and hold, then squared decay; negative once it is finished.
            /// Squared because a linear fade reads as a dimmer being turned down, where a flash
            /// drops away fast and then lingers faintly.
            /// </summary>
            private float Envelope(float age)
            {
                if (age < RiseSeconds) return age / RiseSeconds;
                if (age < RiseSeconds + HoldSeconds) return 1f;
                if (FadeSeconds <= 0f) return -1f;

                float fade = (age - RiseSeconds - HoldSeconds) / FadeSeconds;
                if (fade >= 1f) return -1f;
                float k = 1f - fade;
                return k * k;
            }
        }
    }
}
