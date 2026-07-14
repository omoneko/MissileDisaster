using ICities;
using UnityEngine;

namespace MissileDisaster.Game.Simulation
{
    /// <summary>
    /// 発動・進行・着弾を駆動する。
    /// OnUpdate（メイン）: F9 でツール起動、飛翔を simulationTimeDelta で進める（速度連動・一時停止で凍結）。
    /// OnAfterSimulationTick（sim）: 着弾ダメージ解決のみ（DisasterHelpers）。
    /// </summary>
    public class MissileThreadingExtension : ThreadingExtensionBase
    {
        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            try
            {
                if (Input.GetKeyDown(ModConfig.ManualTriggerKey))
                {
                    ToolsModifierControl.SetTool<MissileDisaster.Game.UI.MissileTool>();
                }

                // 迎撃施設の設置ホットキー（Ctrl+1..4）。ツールバー非依存の確実な設置手段。
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    HandleBuildingHotkey();
                }

                // 迎撃施設ボタンを災害タブへ反映（パネル生成後に一度だけ実行される）。
                MissileDisaster.Game.Defense.CustomBuildingFactory.PumpPanelRefresh();

                bool paused = SimulationManager.instance.SimulationPaused;
                if (!paused)
                {
                    MissileManager.UpdateVisual(simulationTimeDelta);
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("OnUpdate error: " + e);
            }
        }

        /// <summary>Ctrl+1..4 で対応する迎撃施設を BuildingTool に載せる（クリックで設置）。</summary>
        private static void HandleBuildingHotkey()
        {
            KeyCode[] keys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };
            string[] names = MissileDisaster.Game.Defense.CustomBuildingFactory.BuildingNames();
            for (int i = 0; i < keys.Length && i < names.Length; i++)
            {
                if (!Input.GetKeyDown(keys[i])) continue;
                BuildingInfo info = MissileDisaster.Game.Defense.CustomBuildingFactory.Get(names[i]);
                if (info == null)
                {
                    ModConfig.LogError("BuildingHotkey: 未登録 " + names[i]);
                    return;
                }
                BuildingTool tool = ToolsModifierControl.SetTool<BuildingTool>();
                if (tool != null)
                {
                    tool.m_prefab = info;
                    tool.m_relocate = 0;
                    ModConfig.Log("BuildingHotkey: 設置ツールに載せました " + info.name);
                }
                return;
            }
        }

        public override void OnAfterSimulationTick()
        {
            try
            {
                MissileManager.UpdateSimulation();
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("OnAfterSimulationTick error: " + e);
            }
        }
    }
}
