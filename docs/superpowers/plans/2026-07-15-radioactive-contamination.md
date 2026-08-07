# Radioactive contamination, implemented properly - following the NuclearMeltdown mod, without water-treatment decontamination

> Requested: port the radioactive contamination across from NuclearMeltdown's full
> implementation.
> **The basic settings follow that mod**: an intensity of 255, expiry after 50 years, reasserting
> against the natural decay, and persistence in the save.
> **The one change**: decontamination by a water treatment plant is disabled - nothing
> decontaminates it.
> The radius comes from this mod's existing per-warhead figure,
> `WarheadSpec.ContaminationRadius`, which is above zero only for a nuclear groundburst.

## What is ported (everything but the decontamination)

- The zone ledger (ContaminationManager): AddZone, ReassertZone, ClearZone, RemoveZoneAt, Zones
  and ReplaceAll. An impact adds a zone, which is written into the ground pollution grid,
  `NaturalResourceManager.m_pollution`.
- Upkeep, spaced out across ticks on the simulation thread: zones past the 50-year expiry are
  cleared and removed, and the rest are held against the natural decay with ReassertZone.
  **IsDecontaminationActive, DecontaminateZone and ReducePollution are not ported**, since a
  water treatment plant does not decontaminate here.
- Persistence through ISerializableData: the ledger is serialised to byte[] and restored. The ground pollution itself is part of the game's own save.

## Files

Core (pure, written test-first):
- `Core/ContaminationZone.cs`, new and ported: {CenterX, CenterZ, Radius, StartTicks}
- `Core/ContaminationClock.cs`, new and ported: HasExpired(start, now, years)
- `Core/ZoneSerializer.cs`, new and ported: Serialize and Deserialize, versioned, yielding nothing on corrupt data
- Existing: PollutionGrid and CellDose

Game:
- `Game/Contamination/ContaminationManager.cs`, rewritten from the simple Apply into the ledger
  version. Maintain(nowTicks) handles the expiry and the reassert, with no decontamination. The
  radius is clamped by MaxContaminationRadius, and a radius of zero or less is ignored, as for an
  airburst.
- `Game/Contamination/PollutionField.cs`: add ClearCell for the expiry; ApplyDose and Refresh already exist.
- `Game/Serialization/ContaminationDataExtension.cs`, new: OnSaveData and OnLoadData.
- `Game/Simulation/MissileThreadingExtension.cs`: add the contamination upkeep to OnAfterSimulationTick, spaced out.
- `Game/Loading/MissileLoadingExtension.cs`: call ContaminationManager.Reset() in OnLevelUnloading, but not on load, so OnLoadData is respected.
- `Game/ImpactResolver.cs`: Apply calls AddZone(new ContaminationZone(x, z, radius, nowTicks)).
- `Game/ModConfig.cs`: add ContaminationExpiryYears=50 and ContaminationMaintainInterval, which spaces the upkeep across ticks.

## A note on performance

The per-warhead radius is larger than NuclearMeltdown's 700 m - kilometres for a standard
nuclear warhead - so reasserting every tick would be expensive. ContaminationMaintainInterval
spaces it out generously; every few seconds is enough to counter the natural decay.

## Tests

- `ContaminationClockTests`: false before the expiry, true after.
- `ZoneSerializerTests`: a round trip matches; a version mismatch and corrupt data both yield nothing.
- In game: a nuclear groundburst leaves contamination, building a water treatment plant does not clear it, the zones survive a save and reload, and it lifts after 50 years.

## Definition of done

- Every Core test is green and the build and deployment succeed. Confirm in game that a water treatment plant does not decontaminate.
