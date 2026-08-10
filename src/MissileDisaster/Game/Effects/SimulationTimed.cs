using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// Puts one effect GameObject on the game's clock: its ParticleSystems advance at the
    /// simulation's rate rather than the wall clock's, and it is destroyed after a lifetime
    /// measured in simulation seconds. Attached by ParticleBuilder to everything the mod
    /// spawns, so no effect can be forgotten. Main thread only.
    ///
    /// The lifetime matters as much as the speed. Object.Destroy(go, seconds) counts wall
    /// seconds, so a cloud paused ten seconds into a thirty second life would be deleted on
    /// schedule while frozen - the player unpauses and it is simply gone. Counting simulation
    /// seconds instead means a paused effect waits, and one at triple speed is cleaned up three
    /// times sooner, which is what the rest of the game does.
    /// </summary>
    public class SimulationTimed : MonoBehaviour
    {
        /// <summary>Lifetime in simulation seconds. Zero or less means it is never destroyed by this component.</summary>
        public float LifetimeSeconds;

        private ParticleSystem[] _systems;
        private float _age;

        private void Start()
        {
            _systems = GetComponentsInChildren<ParticleSystem>();
            Apply(); // set the rate before the first frame is simulated, not one frame late
        }

        private void Update()
        {
            Apply();
            _age += EffectClock.Delta;
            if (LifetimeSeconds > 0f && _age >= LifetimeSeconds) Destroy(gameObject);
        }

        private void Apply()
        {
            if (_systems == null) return;
            float scale = EffectClock.Scale;
            for (int i = 0; i < _systems.Length; i++)
            {
                if (_systems[i] == null) continue;
                var main = _systems[i].main;
                main.simulationSpeed = scale;
            }
        }
    }
}
