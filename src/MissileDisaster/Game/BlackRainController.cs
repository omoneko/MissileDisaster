using ColossalFramework;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// Holds the weather at rain for a while after a nuclear detonation.
    ///
    /// <para>
    /// The game will not do this on its own: WeatherManager's simulation step never reads
    /// pollution - checked in the IL - so rain after a strike is the ordinary weather cycle and
    /// a coincidence. m_targetRain is a public field the weather simulation steers m_currentRain
    /// towards, and it rewrites that target every step from its own cycle, so setting it once
    /// does nothing. It has to be held: written every tick for as long as the rain should last.
    /// </para>
    ///
    /// Simulation thread only, driven from MissileThreadingExtension.OnAfterSimulationTick. The
    /// weather runs there, and writing a float it owns from the main thread is exactly the kind
    /// of cross-thread poke this codebase avoids everywhere else.
    /// </summary>
    public static class BlackRainController
    {
        /// <summary>Rain hard, but not the full 1.0 - a downpour that never lets up looks broken.</summary>
        private const float RainTarget = 0.85f;

        // Simulation seconds left to hold the rain for. Zero means the weather is the game's
        // own business again.
        private static float _secondsLeft;

        /// <summary>Whether black rain is falling right now.</summary>
        public static bool Active { get { return _secondsLeft > 0f; } }

        /// <summary>
        /// Starts, or extends, the rain. Called when a nuclear detonation resolves. A second
        /// strike during the first one's rain takes whichever is longer rather than adding them
        /// up: four warheads should not leave the city under a week of drizzle.
        /// </summary>
        public static void Begin(float seconds)
        {
            if (seconds <= 0f) return;
            if (seconds > _secondsLeft) _secondsLeft = seconds;
        }

        /// <summary>Simulation thread. Counts the rain down and holds the target while it lasts.</summary>
        public static void Update(float simulationSeconds)
        {
            if (_secondsLeft <= 0f) return;
            try
            {
                _secondsLeft -= simulationSeconds;
                if (_secondsLeft <= 0f)
                {
                    _secondsLeft = 0f;
                    // Deliberately not forced back to dry: the weather simulation owns the target
                    // again from here, and it will move on to whatever it was going to do.
                    ModConfig.Log("Black rain has passed.");
                    return;
                }

                WeatherManager wm = Singleton<WeatherManager>.instance;
                if (wm != null) wm.m_targetRain = RainTarget;
            }
            catch (System.Exception e)
            {
                // Losing the rain must never cost the tick. Stop trying rather than log per tick.
                _secondsLeft = 0f;
                ModConfig.LogError("BlackRainController.Update error: " + e);
            }
        }

        /// <summary>Called on a level change, so one city's rain does not follow into the next.</summary>
        public static void Reset()
        {
            _secondsLeft = 0f;
        }
    }
}
