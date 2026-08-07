using System;

namespace MissileDisaster.Core
{
    /// <summary>Decides when a contamination zone has aged out, based on in-game time. No UnityEngine dependency.</summary>
    public static class ContaminationClock
    {
        public static bool HasExpired(long startTicks, long nowTicks, int years)
        {
            DateTime start = new DateTime(startTicks);
            DateTime expiry = start.AddYears(years);
            return nowTicks >= expiry.Ticks;
        }
    }
}
