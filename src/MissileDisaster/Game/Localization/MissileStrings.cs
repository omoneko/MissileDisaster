namespace MissileDisaster.Game
{
    /// <summary>
    /// Every player-facing string, as a public static field whose initializer is the built-in
    /// English default.
    ///
    /// How localization works:
    ///  - The field name is the key in Locales/&lt;lang&gt;.txt, e.g. "Panel_Title = ...".
    ///  - LocaleLoader.EnsureLoaded() detects the game language and overwrites these fields by
    ///    reflection from the matching file. A missing file or an unknown key leaves the English
    ///    default in place, so a half-finished translation is always safe to ship.
    ///  - UI code reads MissileStrings.Xxx instead of a literal.
    ///
    /// Nothing here may be copied into a `static readonly string[]`: that array would be built
    /// once at class load and keep the language it was built in. The label arrays the launch
    /// panel needs are methods below, rebuilt on each call.
    ///
    /// Log messages are deliberately NOT here. Logs should stay grep-able in English, and a bug
    /// report is far easier to read when the log says the same thing whoever sent it.
    ///
    /// To add a language: copy Locales/en.txt to Locales/&lt;code&gt;.txt using the code the game
    /// reports (de, fr, es, zh, ja, ...), translate the values, and open a pull request at
    /// https://github.com/omoneko/MissileDisaster - or just drop the file in the mod folder.
    /// </summary>
    public static class MissileStrings
    {
        // --- Content Manager -------------------------------------------------------------------
        public static string Mod_Description =
            "Launch missiles with 5 warhead types, adjustable yield, and air or ground burst. " +
            "Radioactive fallout, missile defense (PAC3 / THAAD / Aegis), and an optional random-strike " +
            "disaster mode. Launch from the missile button in the Disasters panel, or a rebindable hotkey.";

        // --- Build check -------------------------------------------------------------------------
        public static string Options_BuildGroup = "Build check";
        /// <summary>{0} is the folder the DLL was actually loaded from.</summary>
        public static string Options_LoadedFrom = "Loaded from: {0}";

        // --- Launch ------------------------------------------------------------------------------
        public static string Options_LaunchGroup = "Launch";
        public static string Options_LaunchHelp =
            "Open the launch panel with the missile-icon button in the Disasters info-view panel " +
            "(like the vanilla disaster buttons). Then pick a warhead, click 'Start Targeting', " +
            "and click the map. Close the panel with the X; reopen from the same button.";
        public static string Options_LaunchHotkey = "Launch tool hotkey";
        public static string Options_ResetPanel = "Open / reset launch panel to a visible position";

        // --- Random strikes ----------------------------------------------------------------------
        public static string Options_RandomGroup = "Random missile strikes (DESTRUCTIVE - off by default)";
        public static string Options_RandomEnable =
            "Enable random strikes - missiles WILL hit your city on their own and destroy " +
            "buildings, like a natural disaster. Leave this off to only launch missiles yourself.";
        public static string Options_RandomFrequency = "Strike frequency";
        public static string Options_RandomFrequencyTip =
            "Multiplier on the game's own natural-disaster rate.";
        public static string Options_AttackPattern = "Attack pattern";
        public static string Pattern_Single = "Single";
        public static string Pattern_Mirv = "MIRV";
        public static string Pattern_Random = "Random";
        public static string Options_RandomWarhead = "Warhead";

        // --- Warheads ------------------------------------------------------------------------------
        public static string Warhead_Random = "Random";
        public static string Warhead_Conventional = "Conventional";
        public static string Warhead_Cluster = "Cluster";
        public static string Warhead_WhitePhosphorus = "White Phosphorus";
        public static string Warhead_Thermobaric = "Thermobaric";
        public static string Warhead_Nuclear = "Nuclear";

        // --- Black rain ------------------------------------------------------------------------
        public static string Options_BlackRainGroup = "Black rain";
        public static string Options_BlackRain = "Black rain after a nuclear detonation";
        public static string Options_BlackRainHelp =
            "Soot lifted by the burning city, scavenged out of the column by the water the " +
            "fireball condensed and falling dirty enough to mark what it lands on. It comes " +
            "down about half the time, spreads across the ground the fires reached, " +
            "and lifts again shortly after the rain stops. The mark is only a mark: the fallout " +
            "that actually poisons the ground is the contamination the warhead already leaves.";

        // --- Priority targeting --------------------------------------------------------------------
        public static string Options_PriorityGroup = "Priority targeting (random strikes)";
        public static string Options_PriorityHelp =
            "Random strikes prefer these buildings. Weights (among tiers that have matches): " +
            "A 50% / B 25% / C 15% / others 10%. Match is by internal building name, " +
            "comma-separated, case-insensitive. Tip: add 'Oil' to a tier to target oil industry.";
        public static string Options_PriorityA = "Tier A keywords (highest)";
        public static string Options_PriorityB = "Tier B keywords";
        public static string Options_PriorityC = "Tier C keywords (landmarks/monuments auto-detected too)";

        // --- Launch panel ----------------------------------------------------------------------------
        public static string Panel_Title = "Missile Launch Control";
        public static string Panel_Close = "Close (reopen from the Missile button in the Disasters panel)";
        public static string Panel_Warhead = "Warhead";
        public static string Panel_NuclearYield = "Nuclear Yield (nuclear only, kt)";
        public static string Panel_ConventionalYield = "Conventional Yield (non-nuclear, kg TNT)";
        public static string Panel_BurstHeight = "Burst Height";
        public static string Panel_StartTargeting = "Start Targeting (click to launch)";
        public static string Panel_YieldHint = "Enter yield in kt (press Enter)";
        public static string Panel_ChargeHint = "Enter charge in kg TNT (press Enter)";
        public static string Burst_Air = "Air Burst";
        public static string Burst_Ground = "Ground Burst";

        // --- In-game button ------------------------------------------------------------------------
        public static string Button_Tooltip = "Missile Disaster - open the launch panel";

        /// <summary>
        /// The five warhead labels the launch panel shows, in WarheadType order. A method rather
        /// than a static array so it is rebuilt in the current language every time.
        /// </summary>
        public static string[] WarheadLabels()
        {
            return new[]
            {
                Warhead_Conventional, Warhead_Cluster, Warhead_WhitePhosphorus,
                Warhead_Thermobaric, Warhead_Nuclear,
            };
        }

        /// <summary>The random-strike warhead dropdown, which offers Random ahead of the five.</summary>
        public static string[] RandomWarheadLabels()
        {
            return new[]
            {
                Warhead_Random, Warhead_Conventional, Warhead_Cluster, Warhead_WhitePhosphorus,
                Warhead_Thermobaric, Warhead_Nuclear,
            };
        }

        /// <summary>The attack-pattern dropdown, in stored-value order.</summary>
        public static string[] PatternLabels()
        {
            return new[] { Pattern_Single, Pattern_Mirv, Pattern_Random };
        }

        /// <summary>The two burst buttons, in BurstType order (airburst, groundburst).</summary>
        public static string[] BurstLabels()
        {
            return new[] { Burst_Air, Burst_Ground };
        }
    }
}
