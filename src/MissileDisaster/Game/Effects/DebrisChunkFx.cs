using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// One piece of rubble being swept outward by the blast: a real GameObject with a MeshFilter
    /// and a MeshRenderer, moved along the run Core.DebrisSweep computes. Main thread only.
    ///
    /// Real objects rather than mesh particles, because this effect failed twice in ways a
    /// particle system cannot be questioned about. A ParticleSystemRenderer in Mesh mode either
    /// draws or does not, and there is nothing to inspect in between; a GameObject with a
    /// MeshRenderer is the path the mod's own missile already renders through, so it is known to
    /// work in this game's render pipeline.
    ///
    /// It runs on the game clock like every other effect, so a paused city holds the rubble
    /// mid-skip.
    /// </summary>
    public class DebrisChunkFx : MonoBehaviour
    {
        public DebrisRide Ride;
        public Vector3 Origin;      // ground zero, world space
        public float GroundY;       // the height the piece is swept across
        public float LifeSeconds;

        private float _age;
        private Vector3 _rollAxis;

        private void Start()
        {
            // It rolls over the axis across its direction of travel - the way anything pushed
            // along the ground turns, rather than spinning on the spot. That axis is up x dir,
            // which for a horizontal unit vector is just (dz, 0, -dx), already unit length.
            _rollAxis = new Vector3(Ride.DirZ, 0f, -Ride.DirX);
            Apply();
        }

        private void Update()
        {
            _age += EffectClock.Delta;
            if (_age >= LifeSeconds) { Destroy(gameObject); return; }
            Apply();
        }

        private void Apply()
        {
            float x, y, z;
            DebrisSweep.PositionAt(Ride, _age, out x, out y, out z);
            transform.position = new Vector3(Origin.x + x, GroundY + y, Origin.z + z);
            transform.rotation =
                Quaternion.AngleAxis(DebrisSweep.RollAt(Ride, _age), _rollAxis)
                * Quaternion.Euler(0f, Ride.YawDegreesPerSecond * _age, 0f);
        }
    }
}
