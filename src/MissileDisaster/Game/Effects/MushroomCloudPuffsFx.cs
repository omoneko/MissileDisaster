using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// Renders the mushroom cloud as a crowd of soft smoke puffs whose positions are computed
    /// every frame by Core.CloudPuffs - the vortex-ring flow - instead of being left to the
    /// particle simulation. The ParticleSystem here is only a renderer: emission is off, the
    /// particles are owned and placed by this component through SetParticles, and their
    /// lifetime is topped up so the system can never retire them.
    ///
    /// That per-frame control is what the earlier attempts lacked. A simulated particle gets a
    /// velocity at birth and drifts; these puffs circulate around the cap's torus and climb the
    /// column's profile exactly, so the crowd holds the mushroom silhouette while every
    /// individual puff visibly boils along it.
    /// Main thread only.
    /// </summary>
    public class MushroomCloudPuffsFx : MonoBehaviour
    {
        public NuclearCloudDimensions Dims;
        public int Seed;
        public bool Airburst;

        // The cap's white vapour and the column's dust, blended per puff by its Dust factor;
        // a groundburst is dustier across the board. Ember is the fire showing through early.
        private static readonly Color VapourColor = new Color(0.93f, 0.93f, 0.94f, 1f);
        private static readonly Color DustColorAir = new Color(0.62f, 0.58f, 0.53f, 1f);
        private static readonly Color DustColorGround = new Color(0.52f, 0.45f, 0.37f, 1f);
        private static readonly Color EmberColor = new Color(1f, 0.45f, 0.12f, 1f);
        // Verified in tools/effect-preview/cloud_preview.py: with the opaque-cored cloud
        // texture these measure 0.997 opacity across the cap's body. The cap is still a touch
        // denser than the column, which reads better with a hint of depth to it.
        private const float CapAlpha = 0.97f;
        private const float ColumnAlpha = 0.88f;

        private ParticleSystem _ps;
        private ParticleSystem.Particle[] _buffer;
        private PuffSpec[] _specs;
        private float _t;

        private void Start()
        {
            _ps = GetComponent<ParticleSystem>();
            _specs = new PuffSpec[CloudPuffs.TotalCount];
            _buffer = new ParticleSystem.Particle[CloudPuffs.TotalCount];
            for (int i = 0; i < _specs.Length; i++)
            {
                _specs[i] = CloudPuffs.Spec(i, Seed);
            }
            Apply(); // place the puffs before the first rendered frame, not a frame late
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_ps == null || _specs == null) return;
            Apply();
        }

        private void Apply()
        {
            CloudAnimationState anim = CloudAnimation.At(_t, Dims.RiseSeconds, Dims.HoldSeconds, Dims.FadeSeconds);
            if (anim.Finished)
            {
                Destroy(gameObject);
                return;
            }

            Color dust = Airburst ? DustColorAir : DustColorGround;
            for (int i = 0; i < _specs.Length; i++)
            {
                PuffPoint pt = CloudPuffs.At(_specs[i], _t, Dims, anim);

                Color c = Color.Lerp(VapourColor, dust, pt.Dust);
                c = Color.Lerp(c, EmberColor, pt.Ember);
                float alpha = (_specs[i].Cap ? CapAlpha : ColumnAlpha) * anim.Alpha * pt.Fade;

                _buffer[i].position = new Vector3(pt.X, pt.Y, pt.Z); // local space; the transform sits at ground zero
                _buffer[i].startSize = pt.Size;
                _buffer[i].rotation = _specs[i].Spin * _t + _specs[i].Azimuth * Mathf.Rad2Deg;
                _buffer[i].startColor = new Color32(
                    (byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f),
                    (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(Mathf.Clamp01(alpha) * 255f));
                // Topped up every frame, so the simulation can never age them out from under us.
                _buffer[i].remainingLifetime = 1000f;
                _buffer[i].startLifetime = 1000f;
            }
            _ps.SetParticles(_buffer, _buffer.Length);
        }
    }
}
