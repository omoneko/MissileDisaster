using ICities;
using UnityEngine;

namespace MissileDisaster.Game.Simulation
{
    /// <summary>
    /// 発動・進行・着弾を駆動する。
    /// OnUpdate（メイン）: F9 でツール起動、飛翔を simulationTimeDelta で進める（速度連動・一時停止で凍結）。
    /// OnAfterSimulationTick（sim）: 着弾ダメージ解決＋汚染ゾーンの維持（期限切れ消去・reassert）。
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
                // 汚染ゾーンの維持（期限切れ消去・自然減衰対策の reassert。内部で間引き）。除染はしない。
                long nowTicks = SimulationManager.instance.m_currentGameTime.Ticks;
                MissileDisaster.Game.Contamination.ContaminationManager.Maintain(nowTicks);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("OnAfterSimulationTick error: " + e);
            }
        }
    }
}
