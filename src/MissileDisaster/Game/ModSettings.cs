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
        public static SavedInt RandomIntervalSeconds; // ランダム攻撃の間隔（実時間秒）
        public static SavedInt RandomWarhead;         // 0=ランダム, 1..5=固定(通常/クラスター/白リン/サーモ/核)

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
                RandomIntervalSeconds = new SavedInt("randomIntervalSeconds", FileName, 180, true);
                RandomWarhead = new SavedInt("randomWarhead", FileName, 0, true);
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

        public static int RandomInterval
        {
            get { return RandomIntervalSeconds != null ? RandomIntervalSeconds.value : 180; }
        }
    }
}
