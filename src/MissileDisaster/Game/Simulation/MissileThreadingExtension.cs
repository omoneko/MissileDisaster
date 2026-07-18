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
        private float _randomTimer;

        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            try
            {
                // パネルを確実に表示（UIView 準備前に失敗しても毎フレームでリトライ）。Mac 等で左のパネルが出ない対策。
                MissileDisaster.Game.UI.MissilePanel.EnsureCreated();

                // 起動キー（Modオプションで再割り当て可能。既定 F9）。
                if (Input.GetKeyDown(ModSettings.LaunchKeyCode))
                {
                    ToolsModifierControl.SetTool<MissileDisaster.Game.UI.MissileTool>();
                }

                bool paused = SimulationManager.instance.SimulationPaused;
                if (!paused)
                {
                    MissileManager.UpdateVisual(simulationTimeDelta);

                    // ランダム攻撃モード（バニラ災害のように一定間隔でランダム着弾）。実時間で計測。
                    if (ModSettings.IsRandomEnabled)
                    {
                        _randomTimer += realTimeDelta;
                        if (_randomTimer >= ModSettings.RandomInterval)
                        {
                            _randomTimer = 0f;
                            RandomStrike.Fire();
                        }
                    }
                    else
                    {
                        _randomTimer = 0f;
                    }
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
