using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// Renders the base surge - the dome of dust that rolls out from the foot of a ground burst's
    /// column and eventually swallows the mushroom above it. Main thread only.
    ///
    /// It is built the same way the mushroom is: a renderer-only ParticleSystem whose puffs this
    /// component places every frame from Core.GroundDust, rather than a simulated system left to
    /// drift on its own. That is what lets the dome expand outward and upward as one motion, on
    /// its own clock, and drift downwind - none of which a velocity handed out at birth can do.
    /// </summary>
    public class GroundDustFx : MonoBehaviour
    {
        public float CapRadius;
        public float CloudTop;
        public float CloudRiseSeconds;
        public int Seed;

        // The surge is scoured off the ground, so it is dirt rather than vapour - the same aged
        // brown the cloud's ground dust uses, a little darker at the skirt where it is thickest.
        private static readonly Color DirtColor = new Color(0.52f, 0.40f, 0.33f, 1f);
        private static readonly Color PaleColor = new Color(0.70f, 0.63f, 0.56f, 1f);
        private const float Alpha = 0.92f;

        private const float PuffMaxScreenFraction = 4f;

        private ParticleSystem _ps;
        private ParticleSystem.Particle[] _buffer;
        private Vector3 _wind;
        private float _t;

        /// <summary>
        /// Raises a surge under a cloud of these dimensions. Ground bursts only - an airburst has
        /// nothing in contact with the ground to scour, and gets none.
        /// </summary>
        public static GameObject Create(string name, Vector3 groundZero, float capRadius,
            float cloudTop, float cloudRiseSeconds)
        {
            var go = ParticleBuilder.NewSystem(name, groundZero, ParticleAssets.Cloud);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // positions are metres from ground zero
            main.maxParticles = GroundDust.PuffCount;
            var emission = ps.emission;
            emission.enabled = false;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = PuffMaxScreenFraction;

            var fx = go.AddComponent<GroundDustFx>();
            fx.CapRadius = capRadius;
            fx.CloudTop = cloudTop;
            fx.CloudRiseSeconds = cloudRiseSeconds;
            fx.Seed = (int)(UnityEngine.Random.value * 1000000f);
            ps.Play();
            return go;
        }

        private void Start()
        {
            _ps = GetComponent<ParticleSystem>();
            _buffer = new ParticleSystem.Particle[GroundDust.PuffCount];
            _wind = WindField.Direction();
            Apply();
        }

        private void Update()
        {
            // The game's clock: the surge freezes with a paused city, like every other effect.
            _t += EffectClock.Delta;
            if (_ps == null || _buffer == null) return;
            Apply();
        }

        private void Apply()
        {
            if (_t >= GroundDust.TotalSeconds(CloudRiseSeconds))
            {
                Destroy(gameObject);
                return;
            }

            float top = Mathf.Max(GroundDust.HeightAt(_t, CloudTop, CloudRiseSeconds), 0.001f);
            for (int i = 0; i < _buffer.Length; i++)
            {
                SurgePoint p = GroundDust.At(i, Seed, _t, CapRadius, CloudTop, CloudRiseSeconds);

                // The dome leans downwind, and its crown leans further than its skirt - the shear
                // that tips a real cloud over instead of sliding it sideways as a rigid shape.
                float drift = CloudDrift.Offset(_t, CloudRiseSeconds, Mathf.Clamp01(p.Y / top));
                Vector3 pos = new Vector3(p.X + _wind.x * drift, p.Y, p.Z + _wind.z * drift);

                Color c = Color.Lerp(DirtColor, PaleColor, 1f - p.Dust);
                _buffer[i].position = pos;
                _buffer[i].startSize = p.Size;
                _buffer[i].rotation = GroundDust.Hash01(i, Seed, 6) * 360f + _t * 6f;
                float a = Mathf.Clamp01(Alpha * p.Fade);
                _buffer[i].startColor = new Color32(
                    (byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f),
                    (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(a * 255f));
                _buffer[i].remainingLifetime = 1000f;
                _buffer[i].startLifetime = 1000f;
            }
            _ps.SetParticles(_buffer, _buffer.Length);
        }
    }
}
