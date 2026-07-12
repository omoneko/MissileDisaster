using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>Mod 全体の定数と共通ログ。</summary>
    public static class ModConfig
    {
        public const string LogPrefix = "[MissileDisaster] ";

        // 手動発射ツールを起動するキー（Alien の F7 と衝突しないよう F9）。
        public const KeyCode ManualTriggerKey = KeyCode.F9;

        // 飛翔（メインスレッドで simulationTimeDelta 駆動）。
        public const float MissileSpeed = 900f;   // 地表投影距離に対する m/秒 相当
        public const float MissileArcHeight = 700f; // 放物線の頂点高さ（m）
        public const float MissileStartAltitude = 1200f; // 発射点の高さ

        // 着弾（通常弾頭・sim スレッドで DisasterHelpers を呼ぶ）。
        public const float SinkholeRadius = 60f;
        public const float SinkholeDepth = 16f;
        public const float DestructionRadius = 120f;

        public static void Log(string msg) { Debug.Log(LogPrefix + msg); }
        public static void LogError(string msg) { Debug.LogError(LogPrefix + msg); }
    }
}
