using ColossalFramework;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// Mod settings - the hotkey and the random strike mode - persisted to the settings file
    /// through ColossalFramework's Saved* types.
    /// Ensure() initialises them and is called at the start of OnEnabled, on level load and in
    /// OnSettingsUI; it guards against registering more than once.
    /// </summary>
    public static class ModSettings
    {
        public const string FileName = "MissileDisasterSettings";

        // Candidate hotkeys for the launch tool. macOS takes F9 and others for itself, so the
        // key is made selectable.
        public static readonly KeyCode[] KeyOptions =
        {
            KeyCode.F5, KeyCode.F6, KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12,
            KeyCode.Insert, KeyCode.Home, KeyCode.End, KeyCode.Backslash,
        };

        public static SavedInt LaunchKey;             // the hotkey, storing a KeyCode as an int
        public static SavedInt RandomEnabled;         // random strikes, 0 or 1
        public static SavedInt StrikeFrequencyPct;    // strike frequency as a percentage of the natural disaster rate; 25 to 300, with 100 meaning 1.0
        public static SavedInt AttackPattern;         // impact pattern: 0 single, 1 MIRV, 2 random
        public static SavedInt RandomWarhead;         // 0 picks at random; 1 to 5 fix it to conventional, cluster, white phosphorus, thermobaric or nuclear
        public static SavedInt BlackRain;             // the black rain that follows a nuclear detonation, 0 or 1

        // Keywords for each targeting tier, comma-separated, matched as substrings against the
        // building's internal info.name. Tier A outranks B, which outranks C.
        public static SavedString PriorityKeywordsA;
        public static SavedString PriorityKeywordsB;
        public static SavedString PriorityKeywordsC;

        // The default keywords. Tier C is empty because landmarks and monuments are detected
        // from MonumentAI instead.
        // The last keyword is Japanese for "Aegis". Kept as matching data, not prose: Workshop
        // authors name assets in their own language.
        public const string DefaultKeywordsA = "Nuclear, PAC3, THAAD, Aegis, \u30a4\u30fc\u30b8\u30b9";
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
                // On by default: it only follows a nuclear detonation, which the player either
                // launched or switched random strikes on for, and it costs nothing but weather.
                BlackRain = new SavedInt("blackRain", FileName, 1, true);
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

        /// <summary>Strike frequency as a multiplier of the natural disaster rate, from 0.25 to 3.0, defaulting to 1.0.</summary>
        public static double StrikeFrequency
        {
            get { return StrikeFrequencyPct != null ? StrikeFrequencyPct.value / 100.0 : 1.0; }
        }

        /// <summary>Impact pattern: 0 single, 1 MIRV, 2 random.</summary>
        public static int AttackPatternValue
        {
            get { return AttackPattern != null ? AttackPattern.value : 0; }
        }

        public static string PriorityAText { get { return PriorityKeywordsA != null ? PriorityKeywordsA.value : DefaultKeywordsA; } }
        public static string PriorityBText { get { return PriorityKeywordsB != null ? PriorityKeywordsB.value : DefaultKeywordsB; } }
        public static string PriorityCText { get { return PriorityKeywordsC != null ? PriorityKeywordsC.value : DefaultKeywordsC; } }
    }
}
