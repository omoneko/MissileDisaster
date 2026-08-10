using ColossalFramework;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// The clock every effect in this mod runs on: the game's simulation time, not the wall
    /// clock. Pause the game and the effects stop; run at triple speed and they run at triple
    /// speed. Main thread only.
    ///
    /// This is how the base game's own effects behave, and it was settled by reading the IL
    /// rather than guessing: EffectManager keeps a SimulationEvent whose m_timeOffset advances
    /// by SimulationManager.m_simulationTimeDelta, so a vanilla effect freezes when the game is
    /// paused and speeds up with the speed setting.
    ///
    /// Two things have to follow it, and they are separate problems:
    ///  - anything the mod animates itself, which simply asks for Delta instead of
    ///    Time.deltaTime
    ///  - Unity's own particle simulation, which runs on the wall clock. That is what Scale is
    ///    for: it is fed to ParticleSystem.main.simulationSpeed, so the particles Unity is
    ///    integrating advance at the same rate as everything else. A scale of zero freezes them
    ///    mid-flight, which is exactly what a paused game should look like.
    /// </summary>
    public static class EffectClock
    {
        /// <summary>
        /// Seconds of simulation time this frame - zero while the game is paused. This is the
        /// number to advance an effect's own age by.
        /// </summary>
        public static float Delta
        {
            get
            {
                try
                {
                    SimulationManager sm = Singleton<SimulationManager>.instance;
                    if (sm == null) return Time.deltaTime;
                    if (sm.SimulationPaused || sm.ForcedSimulationPaused) return 0f;
                    return sm.m_simulationTimeDelta;
                }
                catch (System.Exception)
                {
                    // Before a level is loaded there may be no SimulationManager. Falling back to
                    // the wall clock keeps an effect moving rather than freezing it forever.
                    return Time.deltaTime;
                }
            }
        }

        /// <summary>
        /// Simulation time against wall time this frame, for ParticleSystem.simulationSpeed.
        /// Zero when paused. Clamped at the top so that a long frame - a stutter, a load - cannot
        /// fling a particle system forward through its whole life in one step.
        /// </summary>
        public static float Scale
        {
            get
            {
                float wall = Time.deltaTime;
                if (wall <= 0.0001f) return 0f;
                float scale = Delta / wall;
                if (scale < 0f) return 0f;
                return scale > 8f ? 8f : scale;
            }
        }
    }
}
