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
