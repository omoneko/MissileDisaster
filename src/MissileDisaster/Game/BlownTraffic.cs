using System;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// Throws the traffic and the people near ground zero outward, the way a blast does.
    /// Simulation thread only - it moves vehicles and citizens, which the simulation owns.
    ///
    /// <para>
    /// This is the game's own machinery, not a takeover of it. <c>DisasterHelpers.AddWind</c>
    /// walks the vehicle and citizen grids inside the radius and hands each one to its AI, and
    /// <c>CarAI.AddWind</c> clears the vehicle's destination and sets <c>Vehicle.Flags2.Blown</c>,
    /// after which the simulation flies it with <c>CarAI.SimulationStepBlown</c> until it settles
    /// and resumes. Nothing here writes a vehicle's position frame by frame, which is the thing
    /// that would break saves.
    /// </para>
    ///
    /// <para>
    /// Cars, trucks, buses, service vehicles and parked cars are all CarAI, so all of them go, and
    /// so do pedestrians and animals (HumanAI/LivestockAI/WildlifeAI). <b>Trains, planes, ships
    /// and metro do not</b>: their AIs never override AddWind, and the base
    /// <c>VehicleAI.AddWind</c> is a two-instruction method that returns false. There is no way to
    /// ask for it, and the alternative - writing their frames directly - is the save-breaking
    /// route this class exists to avoid.
    /// </para>
    ///
    /// No DLC is required. Unlike the tree burning, neither DisasterHelpers.AddWindVehicles nor
    /// CarAI.AddWind carries an expansion check.
    /// </summary>
    public static class BlownTraffic
    {
        /// <summary>
        /// Blows everything loose away from groundZero. A destruction radius of zero or less does
        /// nothing. Never throws: the strike's damage does not depend on this.
        /// </summary>
        public static void Blow(Vector3 groundZero, float destructionRadius)
        {
            float reach = BlastWind.Reach(destructionRadius);
            if (reach <= 0f) return;

            try
            {
                // The group is what the game uses to tie thrown instances back to the disaster
                // that threw them. A missile strike is not a disaster instance, so there is
                // nothing to tie them to - InstanceManager.SetGroup takes null and means it.
                DisasterHelpers.AddWind(groundZero, reach,
                    new Vector3(0f, BlastWind.Lift, 0f),
                    BlastWind.Rotational, BlastWind.Radial, null);
            }
            catch (Exception e)
            {
                ModConfig.LogError("BlownTraffic.Blow error: " + e);
            }
        }
    }
}
