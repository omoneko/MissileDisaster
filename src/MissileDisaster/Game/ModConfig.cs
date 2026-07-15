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

        // 迎撃施設は Asset Editor で作成した正規アセット（PAC3/THAAD/Aegis/Radar）。
        // Mod はコスト/電力/水を上書きせず、設置された建物を「名前で検出」して迎撃判定のみ行う。
        // 迎撃判定・建物走査・クールダウンはすべてメインスレッド（MissileManager.UpdateVisual 側）。
        public const int InterceptorScanIntervalFrames = 30;  // 建物再走査の間引き（~0.5s @60fps）
        public const float RadarSupportMultiplier = 1.5f;     // レーダー稼働時の迎撃確率倍率

        // 迎撃成功時の閃光（バニラ非依存の簡易パーティクルバースト。メインスレッド）。
        public const int InterceptFlashBurst = 40;            // 一度に放出する火花数
        public const float InterceptFlashLifetime = 0.5f;     // 火花の寿命(秒)
        public const float InterceptFlashSpeed = 60f;         // 火花の初速(拡散・m/秒)
        public const float InterceptFlashSize = 40f;          // 火花の基準サイズ(m)
        public static readonly Color InterceptFlashCoreColor = new Color(1f, 0.95f, 0.7f, 1f);  // 中心の白橙
        public static readonly Color InterceptFlashEdgeColor = new Color(1f, 0.55f, 0.15f, 1f); // 縁の橙

        // 迎撃ミサイル本体（可視・メインスレッド）。命中/失敗を問わず発射器から実際に飛ばす。
        // モデルは Models/<name>.obj（+Z=機首）。層ごとに実機に近い速度を割り当てる。
        public const string InterceptorModelPac = "Interceptor_PAC";     // PAC-3
        public const string InterceptorModelThaad = "Interceptor_THAAD"; // THAAD
        public const string InterceptorModelArrow = "Interceptor_SM";    // SM-3(Aegis)
        public const float InterceptorModelScale = 12f;
        public const float InterceptorSpeedPac = 1700f;    // PAC-3 ~Mach5
        public const float InterceptorSpeedThaad = 2500f;  // THAAD ~Mach8
        public const float InterceptorSpeedArrow = 3000f;  // SM-3 ~Mach10
        public const float InterceptorCatchRadius = 60f;   // 迎撃点への到達判定距離(m)
        public const float InterceptorMaxFlightSeconds = 8f; // 到達不能時の保険（消滅まで）
        public const int InterceptFizzleBurst = 14;        // 失敗時の小さな煙玉の粒数

        // 迎撃ミサイルの噴煙トレイル（ロケット排気。煙は少しの間残す）。ワールド空間で航跡を残す。
        public const float ExhaustFireRate = 90f;          // ノズル火炎の毎秒放出数
        public const float ExhaustFireLifetime = 0.25f;    // 火炎の寿命(秒)
        public const float ExhaustFireSize = 8f;           // 火炎の基準サイズ(m)
        public const float ExhaustSmokeRate = 60f;         // 噴煙の毎秒放出数
        public const float ExhaustSmokeLifetime = 2.5f;    // 噴煙の寿命(秒)。長め=少しの間残る
        public const float ExhaustSmokeSize = 7f;          // 噴煙の基準サイズ(m)。細く
        public static readonly Color ExhaustFireColor = new Color(1f, 0.9f, 0.6f, 1f);         // 白橙の火炎
        public static readonly Color ExhaustSmokeColor = new Color(0.85f, 0.85f, 0.85f, 0.32f); // 白っぽい薄煙

        // 着弾（通常弾頭・sim スレッドで DisasterHelpers を呼ぶ）。
        public const float SinkholeRadius = 60f;
        public const float SinkholeDepth = 16f;
        public const float DestructionRadius = 120f;

        // 放射能汚染（核のみ・sim スレッドで NaturalResourceManager へ書込み）。中心の最大濃度(0-255)。
        public const byte ContaminationMaxIntensity = 255;

        public static void Log(string msg) { Debug.Log(LogPrefix + msg); }
        public static void LogError(string msg) { Debug.LogError(LogPrefix + msg); }
    }
}
