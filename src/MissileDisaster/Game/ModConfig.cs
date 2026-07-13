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

        // 燃焼トレイル（隕石風。飛来ミサイルに付与。メインスレッドで生成）。
        // 尾は引かせない方針: 寿命を短くして弾体近くで消し、航跡を残さない。煙は薄め・少なめ。
        public const float TrailFireRate = 70f;       // 火の粉の毎秒放出数
        public const float TrailFireLifetime = 0.3f;  // 火の粉の寿命(秒)。短く=尾を引かない
        public const float TrailFireSize = 10f;       // 火の粉の基準サイズ(m)
        public const float TrailFireSpeed = 1.5f;     // 火の粉の初速(拡散・m/秒)。小さく=弾体近くに留める
        public const float TrailSmokeRate = 14f;      // 煙の毎秒放出数。少なめ
        public const float TrailSmokeLifetime = 0.45f;// 煙の寿命(秒)。短く=尾を残さない
        public const float TrailSmokeSize = 18f;      // 煙の基準サイズ(m)
        public static readonly Color TrailFireCoreColor = new Color(1f, 0.85f, 0.35f, 1f);  // 明るい黄橙(コア)
        public static readonly Color TrailFireEdgeColor = new Color(0.9f, 0.28f, 0.06f, 1f); // 赤橙(縁)
        public static readonly Color TrailSmokeColor = new Color(0.16f, 0.15f, 0.14f, 0.2f); // 暗い煙・薄く

        // 迎撃施設（正規建物・実行時クローン。S1 歩く骨格）。
        // 小型・電力/維持ありのバニラ建物をテンプレとしてクローンし、メッシュ/名前/AI を差し替える。
        public const string BuildingTemplateName = "Wind Turbine"; // クローン元テンプレ（見つからなければ列挙フォールバック）
        public const string PacBuildingName = "MissileDisaster_PAC3"; // 一意・セーブ間不変
        public const string PacBuildingModelName = "Building_PAC3";    // Models/Building_PAC3.obj

        // 着弾（通常弾頭・sim スレッドで DisasterHelpers を呼ぶ）。
        public const float SinkholeRadius = 60f;
        public const float SinkholeDepth = 16f;
        public const float DestructionRadius = 120f;

        public static void Log(string msg) { Debug.Log(LogPrefix + msg); }
        public static void LogError(string msg) { Debug.LogError(LogPrefix + msg); }
    }
}
