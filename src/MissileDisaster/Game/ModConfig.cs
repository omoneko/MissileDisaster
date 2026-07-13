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
        // 弾道は固定方位・高高度の apex(頂点)から着弾までの「降下枝のみ」を直線補間する。
        public const float MissileSpeed = 900f;            // 降下ペース(水平投影距離に対する m/秒 相当)
        public const float IncomingBearingDegrees = 315f;  // 飛来方位(0=北,時計回り)。全弾同一方位。315=北西
        public const float ApexHorizontalOffset = 2200f;   // apex の水平オフセット(m)。大きいほど浅い角度
        public const float ApexAltitude = 4000f;           // apex の対地高度(m)。高いほど急角度で高高度から飛来

        // 飛来ミサイルの表示モデル（Models/&lt;name&gt;.obj）。モデル軸は +Z=機首。
        public const string ModelsFolderName = "Models";
        public const string IncomingMissileModelName = "IncomingWarhead";
        public const float IncomingMissileScale = 18f;     // モデル ~2m → 実機 ~38m。実機で調整
        public const float ObjMetallic = 0.6f;             // Standard シェーダの金属質
        public const float ObjGlossiness = 0.5f;           // Standard シェーダの滑らかさ
        public static readonly Color ObjFallbackColor = new Color(0.25f, 0.25f, 0.25f, 1f); // MTL 欠落時の既定色

        // 着弾（通常弾頭・sim スレッドで DisasterHelpers を呼ぶ）。
        public const float SinkholeRadius = 60f;
        public const float SinkholeDepth = 16f;
        public const float DestructionRadius = 120f;

        public static void Log(string msg) { Debug.Log(LogPrefix + msg); }
        public static void LogError(string msg) { Debug.LogError(LogPrefix + msg); }
    }
}
