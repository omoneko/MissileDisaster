using System.Collections.Generic;
using UnityEngine;

namespace MissileDisaster.Game.Audio
{
    /// <summary>
    /// Loads the files in Sounds at runtime and caches the AudioClips. Loading runs as a
    /// coroutine, so the work itself is done by SoundLoaderBehaviour on a hidden
    /// DontDestroyOnLoad host GameObject.
    /// Mod.OnEnabled calls Initialize(modPath) and level load calls EnsureLoaded(); between them
    /// the files are read exactly once. All main thread.
    /// </summary>
    public static class SoundLibrary
    {
        // Base names, without extensions, of the files in the Sounds folder.
        public const string Launcher2 = "launcher2";
        public const string Launcher7 = "launcher7";
        public const string Explosion = "explosion1";
        public const string Nuclear = "atomic_bomb";
        public const string Intercept = "small_explosion2";

        public static readonly string[] FileNames =
            { Nuclear, Explosion, Launcher2, Launcher7, Intercept };

        private static bool _loadStarted;
        private static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

        /// <summary>
        /// Called from Mod.OnEnabled. Creates the long-lived DontDestroyOnLoad host and starts
        /// loading immediately, guarding against being started twice. This follows the same
        /// pattern AlienInvasion.SoundManager uses. Main thread.
        /// </summary>
        public static void Initialize(string modDir)
        {
            if (_loadStarted) return;
            if (string.IsNullOrEmpty(modDir))
            {
                ModConfig.LogError("SoundLibrary.Initialize: modDir is empty");
                return;
            }
            _loadStarted = true;
            try
            {
                var go = new GameObject("MissileDisasterAudioLoader");
                Object.DontDestroyOnLoad(go);
                var loader = go.AddComponent<SoundLoaderBehaviour>();
                loader.Begin(modDir);
                ModConfig.Log("SoundLibrary initialized: " + modDir);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("SoundLibrary.Initialize error: " + e);
            }
        }

        public static void Register(string name, AudioClip clip)
        {
            if (!string.IsNullOrEmpty(name) && clip != null) _clips[name] = clip;
        }

        /// <summary>The AudioClip once it has loaded, or null if it has not or the load failed.</summary>
        public static AudioClip Get(string name)
        {
            AudioClip c;
            return _clips.TryGetValue(name, out c) ? c : null;
        }
    }
}
