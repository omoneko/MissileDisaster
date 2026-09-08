using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// Renders the anvil: the wide, thin, two-layer sheet a big cloud spreads into once it
    /// reaches the tropopause and stops climbing. Main thread only.
    ///
    /// Built the same way the mushroom and the base surge are - a renderer-only ParticleSystem
    /// whose puffs this component places every frame from Core.AnvilCap - so it grows and drifts
    /// on the same clock as the cloud it sits on rather than being a separate object that happens
    /// to be nearby.
    /// </summary>
    public class AnvilCapFx : MonoBehaviour
    {
        public NuclearCloudDimensions Dims;
        public float RealCloudTop;   // metres, before the drawing scale: what decides the spread
        public bool Airburst;
        public int Seed;

        private static readonly Color VapourColor = new Color(0.93f, 0.93f, 0.94f, 1f);
        private static readonly Color DustColor = new Color(0.72f, 0.68f, 0.62f, 1f);
        private const float Alpha = 0.80f;
        private const float PuffMaxScreenFraction = 4f;

        private ParticleSystem _ps;
        private ParticleSystem.Particle[] _buffer;
        private Vector3 _wind;
        private float _t;

        /// <summary>
        /// Spreads an anvil over a cloud of these dimensions, if its real height reaches the
        /// tropopause. Returns null when it does not - a small cloud never gets one.
        /// </summary>
        public static GameObject Create(string name, Vector3 groundZero,
            NuclearCloudDimensions dims, float realCloudTop, bool airburst)
        {
            if (!AnvilCap.Forms(realCloudTop)) return null;

            var go = ParticleBuilder.NewSystem(name, groundZero, ParticleAssets.Cloud);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = AnvilCap.PuffCount;
            var emission = ps.emission;
            emission.enabled = false;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = PuffMaxScreenFraction;

            var fx = go.AddComponent<AnvilCapFx>();
            fx.Dims = dims;
            fx.RealCloudTop = realCloudTop;
            fx.Airburst = airburst;
            fx.Seed = (int)(UnityEngine.Random.value * 1000000f);
            ps.Play();
            return go;
        }

        private void Start()
        {
            _ps = GetComponent<ParticleSystem>();
            _buffer = new ParticleSystem.Particle[AnvilCap.PuffCount];
            _wind = WindField.Direction();
            Apply();
        }

        private void Update()
        {
            _t += EffectClock.Delta;
            if (_ps == null || _buffer == null) return;
            Apply();
        }

        private void Apply()
        {
            CloudAnimationState anim = CloudAnimation.At(_t, Dims.RiseSeconds, Dims.HoldSeconds,
                Dims.FadeSeconds);
            if (anim.Finished)
            {
                Destroy(gameObject);
                return;
            }

            // The sheet is the highest thing in the cloud, so it takes the full downwind drift.
            float drift = CloudDrift.Offset(_t, Dims.RiseSeconds, 1f);
            Color body = Airburst ? VapourColor : Color.Lerp(VapourColor, DustColor, 0.45f);

            for (int i = 0; i < _buffer.Length; i++)
            {
                AnvilPoint p = AnvilCap.At(i, Seed, Dims.CapRadius, Dims.CloudTop, RealCloudTop,
                    anim.WidthFraction, anim.HeightFraction);

                _buffer[i].position = new Vector3(p.X + _wind.x * drift, p.Y, p.Z + _wind.z * drift);
                _buffer[i].startSize = p.Size;
                _buffer[i].rotation = AnvilCap.Hash01(i, Seed, 5) * 360f;
                float a = Mathf.Clamp01(Alpha * anim.Alpha * p.Fade);
                _buffer[i].startColor = new Color32(
                    (byte)(Mathf.Clamp01(body.r) * 255f), (byte)(Mathf.Clamp01(body.g) * 255f),
                    (byte)(Mathf.Clamp01(body.b) * 255f), (byte)(a * 255f));
                _buffer[i].remainingLifetime = 1000f;
                _buffer[i].startLifetime = 1000f;
            }
            _ps.SetParticles(_buffer, _buffer.Length);
        }
    }
}
