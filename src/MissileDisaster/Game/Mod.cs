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
            "Launch missiles (conventional now; more warheads coming) at any spot. " +
            "Press F9 or use the button, then click a target.";

        public void OnEnabled()
        {
            // Assembly.GetExecutingAssembly().Location は CS の Mod 読み込み環境下で空文字等を返すことが
            // あり例外の原因になる。ゲーム自身が管理する PluginManager から確実な modPath を取得する。
            try
            {
                PluginManager.PluginInfo info = Singleton<PluginManager>.instance.FindPluginInfo(Assembly.GetExecutingAssembly());
                if (info != null && !string.IsNullOrEmpty(info.modPath))
                {
                    MissileModelProvider.Initialize(info.modPath);
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
    }
}
