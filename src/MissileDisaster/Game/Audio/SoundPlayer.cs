using System;
using UnityEngine;

namespace MissileDisaster.Game.Audio
{
    /// <summary>
    /// Plays a loaded AudioClip in 3D at a world position. Main thread only.
    /// With spatialBlend at 1 and a linear rolloff, the volume follows the distance from the
    /// listener, which is the camera.
    /// A temporary GameObject carries the playback and is destroyed once the clip has finished.
    /// </summary>
    public static class SoundPlayer
    {
        public static void PlayAt(string clipName, Vector3 position, float volume, float minDistance, float maxDistance)
        {
            try
            {
                AudioClip clip = SoundLibrary.Get(clipName);
                if (clip == null) return; // silent if it never loaded; the impact still resolves

                var go = new GameObject("MissileDisasterSound_" + clipName);
                go.transform.position = position;
                var src = go.AddComponent<AudioSource>();
                src.clip = clip;
                src.volume = Mathf.Clamp01(volume);
                src.spatialBlend = 1f; // fully 3D
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = minDistance;
                src.maxDistance = maxDistance;
                src.dopplerLevel = 0f;
                src.playOnAwake = false;
                src.Play();

                UnityEngine.Object.Destroy(go, clip.length + 0.5f);
            }
            catch (Exception e)
            {
                ModConfig.LogError("SoundPlayer.PlayAt(" + clipName + ") error: " + e);
            }
        }
    }
}
