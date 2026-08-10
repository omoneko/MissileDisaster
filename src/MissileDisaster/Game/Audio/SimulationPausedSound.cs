using UnityEngine;

namespace MissileDisaster.Game.Audio
{
    /// <summary>
    /// Holds one of this mod's sounds silent while the game is paused, and takes the clip down
    /// once it has actually finished. Main thread only.
    ///
    /// The game does mute its own audio when paused - AudioManager passes a volume of 0 to
    /// AudioGroup.UpdatePlayers whenever SimulationPaused is set - but that only reaches players
    /// registered with its AudioGroup. A plain Unity AudioSource, which is what this mod spawns,
    /// is not one of those and would carry on playing over a paused city.
    ///
    /// The clip is paused rather than pitched: the base game does not speed its audio up on
    /// fast-forward either, and a blast played at triple pitch is a chipmunk, not a blast. The
    /// lifetime is counted in unpaused seconds, so a sound paused halfway through is not deleted
    /// while it waits.
    /// </summary>
    public class SimulationPausedSound : MonoBehaviour
    {
        /// <summary>How long the clip runs for, in unpaused seconds.</summary>
        public float LifetimeSeconds;

        private AudioSource _source;
        private float _played;
        private bool _paused;

        private void Start()
        {
            _source = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (_source == null) return;

            bool shouldPause = Effects.EffectClock.Delta <= 0f;
            if (shouldPause != _paused)
            {
                _paused = shouldPause;
                if (shouldPause) _source.Pause(); else _source.UnPause();
            }

            if (_paused) return;
            _played += Time.deltaTime; // real seconds, because that is what the clip plays at
            if (LifetimeSeconds > 0f && _played >= LifetimeSeconds) Destroy(gameObject);
        }
    }
}
