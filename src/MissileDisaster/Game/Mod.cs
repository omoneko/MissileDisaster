using System;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Plugins;
using ICities;
using MissileDisaster.Game.Models;

namespace MissileDisaster.Game
{
    public class Mod : IUserMod
    {
        public string Name => "Missile Disaster";
        public string Description =>
            "Launch missiles with 5 warhead types, adjustable yield, and air or ground burst. " +
            "Radioactive fallout, missile defense (PAC3 / THAAD / Aegis), and an optional random-strike " +
            "disaster mode. Launch from the missile button in the Disasters panel, or a rebindable hotkey.";

        public void OnEnabled()
        {
            ModSettings.Ensure();
            // Assembly.GetExecutingAssembly().Location は CS の Mod 読み込み環境下で空文字等を返すことが
            // あり例外の原因になる。ゲーム自身が管理する PluginManager から確実な modPath を取得する。
            try
            {
                PluginManager.PluginInfo info = Singleton<PluginManager>.instance.FindPluginInfo(Assembly.GetExecutingAssembly());
                if (info != null && !string.IsNullOrEmpty(info.modPath))
                {
                    MissileModelProvider.Initialize(info.modPath);
                    Audio.SoundLibrary.Initialize(info.modPath);
                    UI.MissileIcon.SetModDirectory(info.modPath); // タブアイコンに icon.png を使う
                }
                else
                {
                    ModConfig.LogError("OnEnabled: PluginManager から modPath を取得できませんでした");
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("OnEnabled error: " + e);
            }
        }

        /// <summary>Mod オプション画面（キー割り当て・ランダム攻撃）。ゲームが自動検出して呼ぶ。</summary>
        public void OnSettingsUI(UIHelperBase helper)
        {
            try
            {
                ModSettings.Ensure();

                UIHelperBase launch = helper.AddGroup("Launch");
                launch.AddButton(
                    "Open the launch panel with the missile-icon button in the Disasters info-view panel (like the vanilla disaster buttons). " +
                    "Then pick a warhead, click 'Start Targeting', and click the map. Close the panel with the X; reopen from the same button.",
                    () => { });

                string[] keyNames = new string[ModSettings.KeyOptions.Length];
                int keyIndex = 0;
                for (int i = 0; i < ModSettings.KeyOptions.Length; i++)
                {
                    keyNames[i] = ModSettings.KeyOptions[i].ToString();
                    if (ModSettings.KeyOptions[i] == ModSettings.LaunchKeyCode) keyIndex = i;
                }
                launch.AddDropdown("Launch tool hotkey", keyNames, keyIndex, i =>
                {
                    if (i >= 0 && i < ModSettings.KeyOptions.Length)
                        ModSettings.LaunchKey.value = (int)ModSettings.KeyOptions[i];
                });
                launch.AddButton("Open / reset launch panel to a visible position", () => UI.MissilePanel.ResetPosition());

                UIHelperBase rnd = helper.AddGroup("Random missile strikes");
                rnd.AddCheckbox("Enable random strikes (occur between natural disasters)",
                    ModSettings.IsRandomEnabled, b => ModSettings.RandomEnabled.value = b ? 1 : 0);
                // 頻度は自然災害頻度に対する倍率(0.25〜3.0)。内部は percent(25..300)で保存。
                rnd.AddSlider("Strike frequency (x natural disaster rate)", 0.25f, 3f, 0.25f,
                    (float)ModSettings.StrikeFrequency,
                    v => ModSettings.StrikeFrequencyPct.value = (int)Math.Round(v * 100.0));
                string[] patterns = { "Single", "MIRV", "Random" };
                rnd.AddDropdown("Attack pattern", patterns, ModSettings.AttackPatternValue,
                    i => { if (ModSettings.AttackPattern != null) ModSettings.AttackPattern.value = i; });
                string[] warheads = { "Random", "Conventional", "Cluster", "White Phosphorus", "Thermobaric", "Nuclear" };
                rnd.AddDropdown("Warhead", warheads,
                    ModSettings.RandomWarhead != null ? ModSettings.RandomWarhead.value : 0,
                    i => { if (ModSettings.RandomWarhead != null) ModSettings.RandomWarhead.value = i; });

                // 優先照準：各層のキーワード（カンマ区切り）を編集可能に。重みは表示のみ。
                UIHelperBase prio = helper.AddGroup("Priority targeting (random strikes)");
                prio.AddButton(
                    "Random strikes prefer these buildings. Weights (among tiers that have matches): A 50% / B 25% / C 15% / others 10%. " +
                    "Match is by internal building name, comma-separated, case-insensitive. Tip: add 'Oil' to a tier to target oil industry.",
                    () => { });
                prio.AddTextfield("Tier A keywords (highest)", ModSettings.PriorityAText,
                    s => { if (ModSettings.PriorityKeywordsA != null) ModSettings.PriorityKeywordsA.value = s; });
                prio.AddTextfield("Tier B keywords", ModSettings.PriorityBText,
                    s => { if (ModSettings.PriorityKeywordsB != null) ModSettings.PriorityKeywordsB.value = s; });
                prio.AddTextfield("Tier C keywords (landmarks/monuments auto-detected too)", ModSettings.PriorityCText,
                    s => { if (ModSettings.PriorityKeywordsC != null) ModSettings.PriorityKeywordsC.value = s; });
            }
            catch (Exception e)
            {
                ModConfig.LogError("OnSettingsUI error: " + e);
            }
        }
    }
}
