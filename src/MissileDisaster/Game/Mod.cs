using System;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Plugins;
using ICities;
using MissileDisaster.Core;
using MissileDisaster.Game.Models;

namespace MissileDisaster.Game
{
    public class Mod : IUserMod
    {
        // Where the game actually loaded this DLL from, captured at load. Shown on the options
        // screen, because when two copies of a mod are installed - a Workshop subscription and a
        // local build - the only question that matters is which of them is running.
        private static string _loadedFrom = "(not resolved)";

        // The mod's name is its Workshop title, so it stays in English; everything else is
        // localizable. The getter loads the locale because the Content Manager can read this
        // before the options screen has ever been opened.
        public string Name => "Missile Disaster";
        public string Description
        {
            get { LocaleLoader.EnsureLoaded(); return MissileStrings.Mod_Description; }
        }

        public void OnEnabled()
        {
            LogBuildStamp();
            ModSettings.Ensure();
            // Assembly.GetExecutingAssembly().Location can come back empty the way CS loads
            // mods, which then throws. The mod path is taken from the game's own PluginManager
            // instead, which is reliable.
            try
            {
                PluginManager.PluginInfo info = Singleton<PluginManager>.instance.FindPluginInfo(Assembly.GetExecutingAssembly());
                if (info != null && !string.IsNullOrEmpty(info.modPath))
                {
                    MissileModelProvider.Initialize(info.modPath);
                    Audio.SoundLibrary.Initialize(info.modPath);
                    UI.MissileIcon.SetModDirectory(info.modPath); // so the panel icon can use icon.png
                    _loadedFrom = info.modPath;
                }
                else
                {
                    ModConfig.LogError("OnEnabled: could not get modPath from PluginManager");
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("OnEnabled error: " + e);
            }
        }

        /// <summary>
        /// Prints, once at load and whatever the log level, the dimensions this build would draw
        /// a 150 kt cloud at. It is a fingerprint of the code that is actually running: the
        /// numbers are computed, not written down, so they cannot go stale the way a version
        /// string does. If the game is loading an old copy of the DLL - a Workshop subscription
        /// shadowing a local build, a copy that never got overwritten - this line says so.
        /// </summary>
        private static void LogBuildStamp()
        {
            try
            {
                ModConfig.LogAlways("build check - " + BuildStamp());
            }
            catch (Exception e)
            {
                ModConfig.LogError("LogBuildStamp error: " + e);
            }
        }

        /// <summary>
        /// The dimensions this build would draw a 150 kt cloud at, as one line. Computed rather
        /// than written down, so it cannot go stale the way a version string does.
        /// </summary>
        private static string BuildStamp()
        {
            NuclearCloudDimensions d = NuclearCloudDisplay.For(NuclearYields.StandardKilotons);
            return string.Format(
                "150 kt draws: cloud top {0:F0} m, cap {1:F0} m wide, fireball {2:F0} m across, " +
                "rise {3:F1} s, screen top {4:F0} m",
                d.CloudTop, d.CapRadius * 2f, d.FireballRadius * 2f, d.RiseSeconds,
                NuclearCloudDisplay.ScreenTopAltitude);
        }

        /// <summary>The mod's options screen, covering the hotkey and the random strikes. The game finds and calls this itself.</summary>
        public void OnSettingsUI(UIHelperBase helper)
        {
            try
            {
                LocaleLoader.EnsureLoaded();
                ModSettings.Ensure();

                // Which build is running, on screen rather than in a log file. A report that a
                // change did nothing means one thing if these numbers are the new ones and quite
                // another if they are not, and hunting for the log to find that out is a poor
                // use of anybody's evening.
                UIHelperBase build = helper.AddGroup(MissileStrings.Options_BuildGroup);
                build.AddButton(BuildStamp(), () => { });
                build.AddButton(string.Format(MissileStrings.Options_LoadedFrom, _loadedFrom), () => { });

                UIHelperBase launch = helper.AddGroup(MissileStrings.Options_LaunchGroup);
                launch.AddButton(MissileStrings.Options_LaunchHelp, () => { });

                string[] keyNames = new string[ModSettings.KeyOptions.Length];
                int keyIndex = 0;
                for (int i = 0; i < ModSettings.KeyOptions.Length; i++)
                {
                    keyNames[i] = ModSettings.KeyOptions[i].ToString();
                    if (ModSettings.KeyOptions[i] == ModSettings.LaunchKeyCode) keyIndex = i;
                }
                launch.AddDropdown(MissileStrings.Options_LaunchHotkey, keyNames, keyIndex, i =>
                {
                    if (i >= 0 && i < ModSettings.KeyOptions.Length)
                        ModSettings.LaunchKey.value = (int)ModSettings.KeyOptions[i];
                });
                launch.AddButton(MissileStrings.Options_ResetPanel, () => UI.MissilePanel.ResetPosition());

                // The label says what it does to the city, not when it happens. A Workshop
                // report - "your missile mod destroyed my whole new town, i did not know that
                // random disaster exist" - came from a player who ticked this: the setting has
                // always shipped off, but "occur between natural disasters" reads as a schedule
                // detail rather than as a warning that a city can be flattened without warning.
                UIHelperBase rnd = helper.AddGroup(MissileStrings.Options_RandomGroup);
                rnd.AddCheckbox(MissileStrings.Options_RandomEnable,
                    ModSettings.IsRandomEnabled, b => ModSettings.RandomEnabled.value = b ? 1 : 0);
                // The frequency is a multiplier of the natural disaster rate, 0.25 to 3.0,
                // stored internally as a percentage from 25 to 300.
                OptionsSlider.Add(rnd, MissileStrings.Options_RandomFrequency, MissileStrings.Options_RandomFrequencyTip, 0.25f, 3f, 0.25f,
                    (float)ModSettings.StrikeFrequency,
                    v => ModSettings.StrikeFrequencyPct.value = (int)Math.Round(v * 100.0));
                rnd.AddDropdown(MissileStrings.Options_AttackPattern, MissileStrings.PatternLabels(),
                    ModSettings.AttackPatternValue,
                    i => { if (ModSettings.AttackPattern != null) ModSettings.AttackPattern.value = i; });
                rnd.AddDropdown(MissileStrings.Options_RandomWarhead, MissileStrings.RandomWarheadLabels(),
                    ModSettings.RandomWarhead != null ? ModSettings.RandomWarhead.value : 0,
                    i => { if (ModSettings.RandomWarhead != null) ModSettings.RandomWarhead.value = i; });

                // Targeting: the keywords for each tier are editable, comma-separated. The
                // weights are shown but not editable.
                UIHelperBase br = helper.AddGroup(MissileStrings.Options_BlackRainGroup);
                br.AddCheckbox(MissileStrings.Options_BlackRain,
                    ModSettings.BlackRain != null && ModSettings.BlackRain.value != 0,
                    b => { if (ModSettings.BlackRain != null) ModSettings.BlackRain.value = b ? 1 : 0; });
                br.AddButton(MissileStrings.Options_BlackRainHelp, () => { });

                UIHelperBase prio = helper.AddGroup(MissileStrings.Options_PriorityGroup);
                prio.AddButton(MissileStrings.Options_PriorityHelp, () => { });
                prio.AddTextfield(MissileStrings.Options_PriorityA, ModSettings.PriorityAText,
                    s => { if (ModSettings.PriorityKeywordsA != null) ModSettings.PriorityKeywordsA.value = s; });
                prio.AddTextfield(MissileStrings.Options_PriorityB, ModSettings.PriorityBText,
                    s => { if (ModSettings.PriorityKeywordsB != null) ModSettings.PriorityKeywordsB.value = s; });
                prio.AddTextfield(MissileStrings.Options_PriorityC, ModSettings.PriorityCText,
                    s => { if (ModSettings.PriorityKeywordsC != null) ModSettings.PriorityKeywordsC.value = s; });
            }
            catch (Exception e)
            {
                ModConfig.LogError("OnSettingsUI error: " + e);
            }
        }
    }
}
