using ColossalFramework;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// Mod 設定（キー割り当て・ランダム攻撃モード）。ColossalFramework の Saved* で設定ファイルへ永続化する。
    /// Ensure() を OnEnabled / レベルロード / OnSettingsUI の先頭で呼んで初期化する（多重登録は防止）。
    /// </summary>
    public static class ModSettings
    {
        public const string FileName = "MissileDisasterSettings";

        // 発射ツール起動キーの候補（Mac で F9 等が OS に奪われるため選べるようにする）。
        public static readonly KeyCode[] KeyOptions =
        {
            KeyCode.F5, KeyCode.F6, KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12,
            KeyCode.Insert, KeyCode.Home, KeyCode.End, KeyCode.Backslash,
        };

        public static SavedInt LaunchKey;             // 起動キー（KeyCode を int で保存）
        public static SavedInt RandomEnabled;         // ランダム攻撃 0/1
        public static SavedInt StrikeFrequencyPct;    // 攻撃頻度（自然災害比のパーセント。25..300, 既定100=×1.0）
        public static SavedInt AttackPattern;         // 着弾パターン 0=Single,1=MIRV,2=Random
        public static SavedInt RandomWarhead;         // 0=ランダム, 1..5=固定(通常/クラスター/白リン/サーモ/核)

        // 優先照準の各層キーワード（カンマ区切り）。建物の内部名(info.name)に部分一致で判定。A>B>C。
        public static SavedString PriorityKeywordsA;
        public static SavedString PriorityKeywordsB;
        public static SavedString PriorityKeywordsC;

        // 既定キーワード（現状の優先目標）。C はランドマーク/モニュメントを MonumentAI で自動判定するため空。
        public const string DefaultKeywordsA = "Nuclear, PAC3, THAAD, Aegis, イージス";
        public const string DefaultKeywordsB = "Airport, Train Station, Railway, Cargo Train, Harbor, Harbour";
        public const string DefaultKeywordsC = "";

        private static bool _ready;

        public static void Ensure()
        {
            if (_ready) return;
            try
            {
                if (GameSettings.FindSettingsFileByName(FileName) == null)
                {
                    GameSettings.AddSettingsFile(new SettingsFile { fileName = FileName });
                }
                LaunchKey = new SavedInt("launchKey", FileName, (int)KeyCode.F9, true);
                RandomEnabled = new SavedInt("randomEnabled", FileName, 0, true);
                StrikeFrequencyPct = new SavedInt("strikeFrequencyPct", FileName, 100, true);
                AttackPattern = new SavedInt("attackPattern", FileName, 0, true);
                RandomWarhead = new SavedInt("randomWarhead", FileName, 0, true);
                PriorityKeywordsA = new SavedString("priorityKeywordsA", FileName, DefaultKeywordsA, true);
                PriorityKeywordsB = new SavedString("priorityKeywordsB", FileName, DefaultKeywordsB, true);
                PriorityKeywordsC = new SavedString("priorityKeywordsC", FileName, DefaultKeywordsC, true);
                _ready = true;
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("ModSettings.Ensure error: " + e);
            }
        }

        public static KeyCode LaunchKeyCode
        {
            get { return LaunchKey != null ? (KeyCode)LaunchKey.value : ModConfig.ManualTriggerKey; }
        }

        public static bool IsRandomEnabled
        {
            get { return RandomEnabled != null && RandomEnabled.value != 0; }
        }

        /// <summary>攻撃頻度の乗数（自然災害比）。0.25〜3.0、既定1.0。</summary>
        public static double StrikeFrequency
        {
            get { return StrikeFrequencyPct != null ? StrikeFrequencyPct.value / 100.0 : 1.0; }
        }

        /// <summary>着弾パターン 0=Single,1=MIRV,2=Random。</summary>
        public static int AttackPatternValue
        {
            get { return AttackPattern != null ? AttackPattern.value : 0; }
        }

        public static string PriorityAText { get { return PriorityKeywordsA != null ? PriorityKeywordsA.value : DefaultKeywordsA; } }
        public static string PriorityBText { get { return PriorityKeywordsB != null ? PriorityKeywordsB.value : DefaultKeywordsB; } }
        public static string PriorityCText { get { return PriorityKeywordsC != null ? PriorityKeywordsC.value : DefaultKeywordsC; } }
    }
}
