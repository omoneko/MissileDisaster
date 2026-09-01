using ColossalFramework;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// Which way the wind is blowing, from the game's own weather. Main thread.
    ///
    /// <para>
    /// <c>WeatherManager.m_windDirection</c> is a public float in degrees, and the game turns it
    /// into a vector as <c>x = sin(theta), z = cos(theta)</c> - which is what FogEffect and
    /// DayNightDynamicCloudsProperties both do, read out of their IL. Using the same conversion
    /// means the mod's smoke drifts the same way the game's own fog and clouds do, rather than at
    /// some angle to them.
    /// </para>
    ///
    /// It falls back to a fixed direction rather than throwing: a missing weather manager must
    /// cost the strike its drift, not its effect.
    /// </summary>
    public static class WindField
    {
        private static readonly Vector3 Fallback = new Vector3(1f, 0f, 0f);

        /// <summary>The unit vector the wind is blowing towards.</summary>
        public static Vector3 Direction()
        {
            try
            {
                WeatherManager weather = Singleton<WeatherManager>.instance;
                if (weather == null) return Fallback;
                float x, z;
                CloudDrift.Direction(weather.m_windDirection, out x, out z);
                return new Vector3(x, 0f, z);
            }
            catch
            {
                return Fallback;
            }
        }
    }
}
