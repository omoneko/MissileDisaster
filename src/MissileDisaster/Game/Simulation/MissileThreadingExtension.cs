using System;
using ICities;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Simulation
{
    /// <summary>
    /// 発動・進行・着弾を駆動する。
    /// OnUpdate（メイン）: 起動キーでツール起動、飛翔を進める、ランダム攻撃の発火要求を消化して発射。
    /// OnAfterSimulationTick（sim）: 着弾ダメージ解決＋汚染ゾーン維持＋ランダム攻撃スケジューラ駆動。
    ///
    /// ランダム攻撃はバニラ災害頻度に連動させる（StrikeScheduler）。頻度算出と「他災害でリセット」は
    /// DisasterManager（sim スレッド）を読んで行い、実際の発射は GameObject 生成のためメインスレッドで行う。
    /// 受け渡しは _pendingStrike フラグ（sim が立て、メインが消化）。
    /// </summary>
    public class MissileThreadingExtension : ThreadingExtensionBase
    {
        private readonly StrikeScheduler _scheduler = new StrikeScheduler();
        private long _lastGameTicks;            // 前回tickのゲーム内時刻（sim スレッドのみ）
        private volatile bool _pendingStrike;   // sim→メインへの発火要求

        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            try
            {
                // パネルは非表示で用意し、災害タブのミサイルボタンを取り付ける（出現を毎フレームで待つ）。
                MissileDisaster.Game.UI.MissilePanel.EnsureCreated();
                MissileDisaster.Game.UI.MissileDisasterButton.EnsureAttached();

                // 起動キー（Modオプションで再割り当て可能。既定 F9）。
                if (Input.GetKeyDown(ModSettings.LaunchKeyCode))
                {
                    ToolsModifierControl.SetTool<MissileDisaster.Game.UI.MissileTool>();
                }

                // ランダム攻撃の発火要求を消化（実際の発射はメインスレッド）。
                if (_pendingStrike)
                {
                    _pendingStrike = false;
                    RandomStrike.FireStrike();
                }

                if (!SimulationManager.instance.SimulationPaused)
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

                long nowTicks = SimulationManager.instance.m_currentGameTime.Ticks;
                // 汚染ゾーンの維持（期限切れ消去・自然減衰対策の reassert。内部で間引き）。除染はしない。
                MissileDisaster.Game.Contamination.ContaminationManager.Maintain(nowTicks);

                // ランダム攻撃スケジューラ（バニラ災害頻度連動）。
                AdvanceRandomStrikes(nowTicks);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("OnAfterSimulationTick error: " + e);
            }
        }

        /// <summary>sim スレッド。DisasterManager を読み、ゲーム内時間でスケジューラを進める。発火時はフラグを立てる。</summary>
        private void AdvanceRandomStrikes(long nowTicks)
        {
            if (!ModSettings.IsRandomEnabled)
            {
                _scheduler.Reset();
                _lastGameTicks = 0L;
                return;
            }

            if (_lastGameTicks == 0L) _lastGameTicks = nowTicks;
            double gameDaysDelta = (nowTicks - _lastGameTicks) / (double)TimeSpan.TicksPerDay;
            if (gameDaysDelta < 0.0) gameDaysDelta = 0.0; // セーブロード等で時刻が巻き戻っても安全側
            _lastGameTicks = nowTicks;

            DisasterManager dm = DisasterManager.instance;
            int disasterCount = dm != null ? dm.m_disasterCount : 0;
            float probability = dm != null ? dm.m_randomDisastersProbability : 0f;
            double rng = SimulationManager.instance.m_randomizer.UInt32(1000u) / 1000.0; // [0,1) sim決定論

            if (_scheduler.Advance(gameDaysDelta, disasterCount, probability, ModSettings.StrikeFrequency, rng))
            {
                _pendingStrike = true;
            }
        }
    }
}
