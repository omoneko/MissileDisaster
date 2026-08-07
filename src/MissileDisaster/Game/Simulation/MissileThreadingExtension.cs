using System;
using ICities;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Simulation
{
    /// <summary>
    /// Drives triggering, flight and impact.
    /// OnUpdate, on the main thread, opens the tool on the hotkey, advances the missiles in
    /// flight, and launches whenever the random strike mode has asked for one.
    /// OnAfterSimulationTick, on the simulation thread, resolves impact damage, maintains the
    /// contamination zones and drives the random strike scheduler.
    ///
    /// Random strikes follow the vanilla disaster frequency through StrikeScheduler. Working out
    /// that frequency, and restarting the countdown when another disaster occurs, both need
    /// DisasterManager and so happen on the simulation thread; the launch itself creates
    /// GameObjects and so happens on the main thread.
    /// The two are joined by the _pendingStrike flag, which the simulation thread raises and the
    /// main thread consumes.
    /// </summary>
    public class MissileThreadingExtension : ThreadingExtensionBase
    {
        private readonly StrikeScheduler _scheduler = new StrikeScheduler();
        private long _lastGameTicks;            // in-game time at the previous tick; simulation thread only
        private volatile bool _pendingStrike;   // the simulation thread's request for the main thread to launch

        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            try
            {
                // Build the panel hidden and attach the launch button to the disasters tab,
                // retrying every frame until that tab exists.
                MissileDisaster.Game.UI.MissilePanel.EnsureCreated();
                MissileDisaster.Game.UI.MissileDisasterButton.EnsureAttached();

                // The hotkey, which can be reassigned in the mod options.
                if (Input.GetKeyDown(ModSettings.LaunchKeyCode))
                {
                    ToolsModifierControl.SetTool<MissileDisaster.Game.UI.MissileTool>();
                }

                // Consume the random strike request; the launch itself happens here, on the
                // main thread.
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
                // Maintain the contamination zones: clear the expired ones and reassert the rest
                // against the game's natural decay, spaced out internally. Nothing is
                // decontaminated here.
                MissileDisaster.Game.Contamination.ContaminationManager.Maintain(nowTicks);

                // The random strike scheduler, following the vanilla disaster frequency.
                AdvanceRandomStrikes(nowTicks);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("OnAfterSimulationTick error: " + e);
            }
        }

        /// <summary>Simulation thread. Reads DisasterManager and advances the scheduler by in-game time, raising the flag when a strike is due.</summary>
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
            if (gameDaysDelta < 0.0) gameDaysDelta = 0.0; // stays safe if the clock goes backwards, as loading a save can do
            _lastGameTicks = nowTicks;

            DisasterManager dm = DisasterManager.instance;
            int disasterCount = dm != null ? dm.m_disasterCount : 0;
            float probability = dm != null ? dm.m_randomDisastersProbability : 0f;
            double rng = SimulationManager.instance.m_randomizer.UInt32(1000u) / 1000.0; // [0,1) from the deterministic simulation RNG

            if (_scheduler.Advance(gameDaysDelta, disasterCount, probability, ModSettings.StrikeFrequency, rng))
            {
                _pendingStrike = true;
            }
        }
    }
}
