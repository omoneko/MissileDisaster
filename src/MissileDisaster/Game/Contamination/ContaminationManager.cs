using System;
using System.Collections.Generic;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Contamination
{
    /// <summary>
    /// The ledger of radioactive contamination zones, and applying, holding and clearing them
    /// on the ground pollution grid (NaturalResourceManager.m_pollution).
    /// The basic settings follow NuclearMeltdown: an intensity of 255, expiry after 50 years,
    /// reasserting against the game's natural decay, and persistence in the save.
    /// <b>A water treatment plant does not decontaminate.</b> Only a dedicated "Decontamination
    /// facility" operating near a zone does, removing DecontaminationMonthlyFraction of what
    /// remains per in-game month, permanently.
    /// Everything here must be called from the simulation thread.
    /// </summary>
    public static class ContaminationManager
    {
        private static List<ContaminationZone> _zones = new List<ContaminationZone>();
        private static int _maintainCounter;
        private static long _lastMaintainTicks;
        private static readonly List<Vector3> _facilities = new List<Vector3>(); // positions of the operating decontamination facilities

        /// <summary>A snapshot of the zone ledger, for saving.</summary>
        public static List<ContaminationZone> Zones
        {
            get { return new List<ContaminationZone>(_zones); }
        }

        /// <summary>Called on a level change; drops the in-memory ledger. The pollution itself is part of the game's own save.</summary>
        public static void Reset()
        {
            _zones = new List<ContaminationZone>();
            _maintainCounter = 0;
            _lastMaintainTicks = 0;
            _facilities.Clear();
        }

        /// <summary>Replaces the ledger on load and reapplies every zone to the grid.</summary>
        public static void ReplaceAll(List<ContaminationZone> zones)
        {
            _zones = zones ?? new List<ContaminationZone>();
            for (int i = 0; i < _zones.Count; i++) ReassertZone(_zones[i]);
        }

        /// <summary>Adds a contamination zone on impact and writes it to the grid. A radius of zero or less is ignored, as for an airburst.</summary>
        public static void AddZone(ContaminationZone zone)
        {
            if (zone.Radius <= 0f) return;
            if (zone.Radius > ModConfig.MaxContaminationRadius)
            {
                zone = new ContaminationZone(zone.CenterX, zone.CenterZ,
                    ModConfig.MaxContaminationRadius, zone.StartTicks, zone.Intensity);
            }
            _zones.Add(zone);
            ReassertZone(zone);
            ModConfig.Log("Contamination zone added: r=" + zone.Radius + "m at ("
                + zone.CenterX + "," + zone.CenterZ + "), total=" + _zones.Count);
        }

        /// <summary>
        /// Call every tick from the simulation thread; it spaces the work out internally.
        /// Expired zones are cleared, zones near a decontamination facility have their intensity
        /// reduced, and the rest are reasserted to hold them in place. A water treatment plant
        /// decontaminates nothing.
        /// </summary>
        public static void Maintain(long nowTicks)
        {
            if (++_maintainCounter < ModConfig.ContaminationMaintainInterval) return;
            _maintainCounter = 0;

            // Elapsed months are measured between passes. The clock advances even with no
            // zones, so a newly created zone is not charged for the time before it existed.
            double deltaMonths = _lastMaintainTicks == 0
                ? 0.0
                : ContaminationDecay.MonthsBetween(_lastMaintainTicks, nowTicks);
            _lastMaintainTicks = nowTicks;
            if (_zones.Count == 0) { _facilities.Clear(); return; }

            ScanFacilities();

            double decayFactor = ContaminationDecay.DecayFactor(deltaMonths, ModConfig.DecontaminationMonthlyFraction);

            for (int i = _zones.Count - 1; i >= 0; i--)
            {
                ContaminationZone zone = _zones[i];

                if (ContaminationClock.HasExpired(zone.StartTicks, nowTicks, ModConfig.ContaminationExpiryYears))
                {
                    ClearZone(zone);
                    _zones.RemoveAt(i);
                    ModConfig.Log("Contamination zone expired (" + ModConfig.ContaminationExpiryYears + "y) and cleared");
                    continue;
                }

                if (decayFactor < 1.0 && IsDecontaminated(zone))
                {
                    // Multiplying a float intensity by the factor means even very short
                    // intervals lose nothing to rounding, so the decay stays steady.
                    zone.Intensity = (float)(zone.Intensity * decayFactor);
                    if (zone.Intensity <= ModConfig.DecontaminationMinIntensity)
                    {
                        ClearZone(zone);
                        _zones.RemoveAt(i);
                        ModConfig.Log("Contamination zone decontaminated and removed");
                    }
                    else
                    {
                        _zones[i] = zone;
                        SetZone(zone); // write the lowered intensity over the grid
                    }
                }
                else
                {
                    ReassertZone(zone); // hold it against the natural decay, back up to its current intensity
                }
            }
        }

        /// <summary>Rounds the float intensity to the byte a ground pollution cell holds.</summary>
        private static byte ToByteIntensity(float intensity)
        {
            int v = (int)(intensity + 0.5f);
            if (v < 0) return 0;
            if (v > 255) return 255;
            return (byte)v;
        }

        /// <summary>Holds the contamination in place, raising cells the natural decay pulled down back to zone.Intensity. Redraws only on a change.</summary>
        public static void ReassertZone(ContaminationZone zone)
        {
            List<CellDose> doses = PollutionGrid.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius, ToByteIntensity(zone.Intensity));
            bool changed = false;
            for (int i = 0; i < doses.Count; i++) changed |= PollutionField.ApplyDose(doses[i]);
            if (changed) RefreshZoneTexture(zone); // not redrawing in the steady state is what stops the overlay flickering
        }

        /// <summary>Writes the contamination over the grid, to apply an intensity lowered by decontamination. Redraws only on a change.</summary>
        private static void SetZone(ContaminationZone zone)
        {
            List<CellDose> doses = PollutionGrid.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius, ToByteIntensity(zone.Intensity));
            bool changed = false;
            for (int i = 0; i < doses.Count; i++) changed |= PollutionField.SetDose(doses[i]);
            if (changed) RefreshZoneTexture(zone);
        }

        public static void ClearZone(ContaminationZone zone)
        {
            List<CellDose> doses = PollutionGrid.CellsInRadius(zone.CenterX, zone.CenterZ, zone.Radius, ToByteIntensity(zone.Intensity));
            bool changed = false;
            for (int i = 0; i < doses.Count; i++) changed |= PollutionField.ClearCell(doses[i].Index);
            if (changed) RefreshZoneTexture(zone);
        }

        /// <summary>Whether an operating decontamination facility stands near the zone, within its radius plus the facility's range.</summary>
        private static bool IsDecontaminated(ContaminationZone zone)
        {
            float reach = zone.Radius + ModConfig.DecontaminationFacilityRange;
            float reach2 = reach * reach;
            for (int i = 0; i < _facilities.Count; i++)
            {
                float dx = _facilities[i].x - zone.CenterX;
                float dz = _facilities[i].z - zone.CenterZ;
                if (dx * dx + dz * dz <= reach2) return true;
            }
            return false;
        }

        /// <summary>Walks BuildingManager and collects the positions of operating decontamination facilities, identified by name.</summary>
        private static void ScanFacilities()
        {
            _facilities.Clear();
            BuildingManager bm = BuildingManager.instance;
            if (bm == null) return;
            Building[] buffer = bm.m_buildings.m_buffer;
            if (buffer == null) return;

            for (int i = 1; i < buffer.Length; i++)
            {
                Building.Flags flags = buffer[i].m_flags;
                if ((flags & Building.Flags.Created) == 0) continue;
                if ((flags & Building.Flags.Completed) == 0) continue;
                const Building.Flags dead = Building.Flags.Abandoned | Building.Flags.BurnedDown
                    | Building.Flags.Collapsed | Building.Flags.Deleted;
                if ((flags & dead) != 0) continue;

                BuildingInfo info = buffer[i].Info;
                string name = info != null ? info.name : null;
                if (string.IsNullOrEmpty(name)) continue;
                if (name.IndexOf(ModConfig.DecontaminationKeyword, StringComparison.OrdinalIgnoreCase) < 0) continue;

                _facilities.Add(buffer[i].m_position);
            }
        }

        private static void RefreshZoneTexture(ContaminationZone zone)
        {
            int cellRadius = (int)(zone.Radius / PollutionGrid.CellSize) + 1;
            int cx = PollutionGrid.WorldToCell(zone.CenterX);
            int cz = PollutionGrid.WorldToCell(zone.CenterZ);
            int minX = Clamp(cx - cellRadius), maxX = Clamp(cx + cellRadius);
            int minZ = Clamp(cz - cellRadius), maxZ = Clamp(cz + cellRadius);
            PollutionField.Refresh(minX, minZ, maxX, maxZ);
        }

        private static int Clamp(int v)
        {
            if (v < 0) return 0;
            if (v > PollutionGrid.Resolution - 1) return PollutionGrid.Resolution - 1;
            return v;
        }
    }
}
