using System.Collections.Generic;
using UnityEngine;

namespace MissileDisaster.Game.Audio
{
    /// <summary>
    /// Sounds/*.mp3 を実行時に読み込んで AudioClip をキャッシュする。読込はコルーチンで行うため、
    /// DontDestroyOnLoad の隠しホスト GameObject 上の SoundLoaderBehaviour が実処理を担う。
    /// Initialize(modPath) を Mod.OnEnabled で、EnsureLoaded() をレベルロードで呼ぶ（1回だけ読み込む）。
    /// すべてメインスレッド。
    /// </summary>
    public static class SoundLibrary
    {
        // Sounds フォルダに置く mp3 のベース名（拡張子なし）。
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
        /// Mod.OnEnabled から呼ぶ。DontDestroyOnLoad の常駐ホストを作り、Sounds/*.wav の読込を即開始する
        /// （多重起動しない）。AlienInvasion.SoundManager と同じ実績パターン。メインスレッドから。
        /// </summary>
        public static void Initialize(string modDir)
        {
            if (_loadStarted) return;
            if (string.IsNullOrEmpty(modDir))
            {
                ModConfig.LogError("SoundLibrary.Initialize: modDir が空");
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

        /// <summary>読込済みなら AudioClip を返す。未読込/失敗なら null。</summary>
        public static AudioClip Get(string name)
        {
            AudioClip c;
            return _clips.TryGetValue(name, out c) ? c : null;
        }
    }
}
