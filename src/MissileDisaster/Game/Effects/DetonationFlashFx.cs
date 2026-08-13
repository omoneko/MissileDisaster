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
        private const float DirectionalMin = 2.5f;
        private const float DirectionalMax = 9f;
        private const float DirectionalPerKilotonRoot = 1.4f;

        // Unity intensity for the point light. Vanilla LightEffects sit near 1-3; a nuclear
        // detonation should be in a different league.
        private const float IntensityMin = 20f;
        private const float IntensityMax = 90f;
        private const float IntensityPerKilotonRoot = 14f;    // times cbrt(kt), then clamped

        // A conventional warhead flashes too - it has to, now that the mod draws its own fireball
        // instead of the vanilla effect that used to carry a LightEffect - but it lights its
        // surroundings, not the county. No directional component at all.
        private const float ConventionalIntensityPerMetre = 0.5f;
        private const float ConventionalIntensityMin = 3f;
        private const float ConventionalIntensityMax = 25f;

        // How far past the fireball the visible glow reaches. A nuclear flash is seen from the
        // next county, so its glow dwarfs the fireball that made it; a bomb's barely leaves it.
        private const float NuclearGlowFactor = 6f;
        private const float ConventionalGlowFactor = 1.8f;

        // White with the faintest warmth. A nuclear flash is near-white; tinting it orange makes
        // it read as an ordinary explosion.
        private static readonly Color FlashColor = new Color(1f, 0.97f, 0.90f, 1f);

        /// <summary>
        /// The flash of a nuclear detonation: a blaze at the burst plus a brief wash of daylight
        /// over the whole map. A failure here never stops the detonation resolving.
        /// </summary>
        public static void PlayNuclear(Vector3 burstPoint, int yieldKilotons, float fireballRadius)
        {
            Spawn("MissileDisaster_NuclearFlash", burstPoint, fireballRadius,
                PeakIntensity(yieldKilotons), DirectionalIntensity(yieldKilotons),
                yieldKilotons + " kt");
        }

        /// <summary>
        /// The flash of a conventional detonation: bright where it went off and nowhere else.
        /// Needed because the mod draws its own fireball now rather than dispatching the vanilla
        /// meteor effect, which used to carry a LightEffect of its own.
        /// </summary>
        public static void PlayConventional(Vector3 burstPoint, float fireballRadius)
        {
            float peak = Mathf.Clamp(fireballRadius * ConventionalIntensityPerMetre,
                ConventionalIntensityMin, ConventionalIntensityMax);
            Spawn("MissileDisaster_Flash", burstPoint, fireballRadius, peak, 0f,
                fireballRadius.ToString("0") + " m fireball");
        }

        private static void Spawn(string name, Vector3 burstPoint, float fireballRadius,
            float peakIntensity, float directionalIntensity, string what)
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

                // A Light lights surfaces. It puts nothing in the air, so from a camera looking at
                // the sky the flash simply is not there - which is what "it does not spread into
                // the sky" means. This is the glow itself: an additive ball at the burst that
                // swells and fades, drawn against the sky rather than on the ground.
                GlowFx.Play(burstPoint, fireballRadius, RiseSeconds + HoldSeconds + FadeSeconds,
                    directionalIntensity > 0f ? NuclearGlowFactor : ConventionalGlowFactor);

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
            if (yieldKilotons <= 0) return DirectionalMin;
            float root = (float)System.Math.Pow(yieldKilotons, 1.0 / 3.0);
            float i = DirectionalPerKilotonRoot * root;
            if (i < DirectionalMin) return DirectionalMin;
            if (i > DirectionalMax) return DirectionalMax;
            return i;
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
            private static float Envelope(float age)
            {
                if (age < RiseSeconds) return age / RiseSeconds;
                if (age < RiseSeconds + HoldSeconds) return 1f;

                float fade = (age - RiseSeconds - HoldSeconds) / FadeSeconds;
                if (fade >= 1f) return -1f;
                float k = 1f - fade;
                return k * k;
            }
        }
    }
}
