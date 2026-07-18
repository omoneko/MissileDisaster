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
            "Launch missiles with 5 warhead types (conventional / cluster / white phosphorus / " +
            "thermobaric / nuclear), selectable air or ground burst, and adjustable yield. " +
            "Nuclear strikes leave radioactive fallout. Use the control panel on the left (or a rebindable " +
            "hotkey in Options). Optional random-strike mode. Build PAC3 / THAAD / Aegis / Radar interceptor " +
            "assets for defense, and a 'Decontamination facility' to clean fallout.";

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
                    "Note: the launch panel appears on the LEFT side of the screen. Click 'Start Targeting', then click the map.",
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
                launch.AddButton("Reset panel to a visible position", () => UI.MissilePanel.ResetPosition());

                UIHelperBase rnd = helper.AddGroup("Random missile strikes");
                rnd.AddCheckbox("Enable random strikes (like a natural disaster)",
                    ModSettings.IsRandomEnabled, b => ModSettings.RandomEnabled.value = b ? 1 : 0);
                rnd.AddSlider("Interval between strikes (seconds)", 30f, 900f, 10f,
                    ModSettings.RandomInterval, v => ModSettings.RandomIntervalSeconds.value = (int)v);
                string[] warheads = { "Random", "Conventional", "Cluster", "White Phosphorus", "Thermobaric", "Nuclear" };
                rnd.AddDropdown("Warhead", warheads,
                    ModSettings.RandomWarhead != null ? ModSettings.RandomWarhead.value : 0,
                    i => { if (ModSettings.RandomWarhead != null) ModSettings.RandomWarhead.value = i; });
            }
            catch (Exception e)
            {
                ModConfig.LogError("OnSettingsUI error: " + e);
            }
        }
    }
}
