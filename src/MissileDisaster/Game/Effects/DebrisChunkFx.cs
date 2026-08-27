using System;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// One piece of rubble in flight: a real GameObject with a MeshFilter and a MeshRenderer,
    /// moved along the arc Core.DebrisFlight computes. Main thread only.
    ///
    /// Real objects rather than mesh particles, because this effect has now failed twice in
    /// ways a particle system cannot be questioned about. A ParticleSystemRenderer in Mesh mode
    /// either draws or does not, and there is nothing to inspect in between; a GameObject with a
    /// MeshRenderer is the path the mod's own missile already renders through, so it is known to
    /// work in this game's render pipeline.
    ///
    /// It runs on the game clock like every other effect, so a paused city holds the rubble in
    /// mid-air.
    /// </summary>
    public class DebrisChunkFx : MonoBehaviour
    {
        public DebrisLaunch Launch;
        public Vector3 Origin;      // ground zero, world space
        public float GroundY;       // the height the chunk is finished at
        public float LifeSeconds;

        private float _age;
        private Vector3 _spin;

        private void Start()
        {
            _spin = new Vector3(Launch.SpinX, Launch.SpinY, Launch.SpinZ);
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
            DebrisFlight.PositionAt(Launch, _age, out x, out y, out z);
            if (y < 0f) y = 0f; // it has landed; it sits there for the rest of its life
            transform.position = new Vector3(Origin.x + x, GroundY + y, Origin.z + z);
            transform.rotation = Quaternion.Euler(_spin * _age);
        }
    }
}
