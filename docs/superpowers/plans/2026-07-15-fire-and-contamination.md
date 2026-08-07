# Implementing the fires and the radioactive contamination properly - plan

> Chosen: implement the fires and the radioactive contamination properly - real fires for the
> incendiary warheads (white phosphorus and thermobaric) and radioactive contamination for the
> nuclear one.
> They are two independent subsystems, so this proceeds in **two stages**, each built and
> verified in game.

## What the investigation established

- **The existing API can do fires.** In
  `DisasterHelpers.DestroyStuff(seed, null, pos, preR, totalR, removeR, destMin, destMax, burnMin, burnMax)`,
  the last two arguments are the burn band: buildings inside it are **set on fire** rather than
  destroyed. The missiles already do a little of this, with `burnMin=r*0.3, burnMax=r*0.6`. No
  new fire system is needed - it is enough to tune the burn radius per warhead. `preR` and
  `totalR` are the outer bound of the operation and have to be `max(destMax, burnMax)`, or the
  outer area is never processed.
- **Radioactive contamination goes through the ground pollution field**, the approach
  NuclearMeltdown uses: write a circle into
  `NaturalResourceManager.m_naturalResources[i].m_pollution`, which runs 0 to 255. It becomes
  part of the game's own save and shows on its pollution overlay.
  The coordinate maths in Core (`PollutionGrid`) is pure and testable; applying it happens on the
  simulation thread, since it writes to NaturalResourceManager.

## Stage 1: fires, and what makes the incendiary warheads different

- `Core/WarheadSpec.cs`: add `BurnRadius`, the outer edge of the fires in metres:
  72 conventional, 30 cluster, 90 white phosphorus, 260 thermobaric and 420 nuclear. White
  phosphorus burns far more than it destroys, which is what makes it read as an incendiary.
- `Game/ImpactResolver.cs`: make `ApplyBlast`'s DestroyStuff call honour the burn band:
  `outer=max(destR, BurnRadius)`, `burnMin=min(destR*0.3, BurnRadius*0.5)` and
  `burnMax=BurnRadius`. The conventional warhead looks exactly as it does today, since a
  BurnRadius of 72 reproduces the current values.
- Tests: white phosphorus has BurnRadius above DestructionRadius, thermobaric burns further than conventional, nuclear is the largest, and every value is non-negative.

## Stage 2: radioactive contamination (nuclear only)

- `Core/CellDose.cs`, new and ported: `{ int Index; byte Intensity; }`.
- `Core/PollutionGrid.cs`, new, ported and written test-first: CellSize 33.75, Resolution 512,
  and WorldToCell, CellIndex and CellsInRadius, the last falling off linearly from the maximum at
  the centre to zero at the edge. No UnityEngine dependency.
- `Game/Contamination/PollutionField.cs`, new: reads and writes NaturalResourceManager and refreshes the texture through AreaModifiedB.
- `Game/Contamination/ContaminationManager.cs`, new and deliberately simple:
  `Apply(centerX, centerZ, radius)` writes the contamination. Version 1 has no decay and no save
  of its own - the ground pollution is already persisted by the game, and decay is left for
  later.
- `Core/WarheadSpec.cs`: add `ContaminationRadius`, above zero for the nuclear warhead only, around 460 m.
- `Game/ImpactResolver.cs`: where `spec.Contaminates`, call
  `ContaminationManager.Apply(target.x, target.z, spec.ContaminationRadius)` on the simulation
  thread, replacing the current log-only behaviour with the real thing.
- Tests: PollutionGrid's coordinates, its radius enumeration, the falloff and excluding what lies outside; and WarheadSpec's ContaminationRadius, above zero for nuclear and zero for the rest.

## Thread discipline (unchanged)

Both the fires and the contamination are part of resolving an impact and therefore run on the
**simulation thread**, alongside ImpactResolver, DisasterHelpers and NaturalResourceManager.
The flight, the interception and creating GameObjects stay on the main thread, and the only
boundary remains the existing `_impactQueue`, unchanged.

## Explicitly out of scope

- The Geiger counter sound is not part of this plan; strengthening how the contamination looks and sounds comes later.
- Version 1 has neither decay over time nor a ledger of its own in the save. NuclearMeltdown's decontamination logic is a candidate for porting later.

## Definition of done

- Every Core test is green, including the new ones, and the build and deployment succeed.
- In game: white phosphorus and thermobaric start fires over a wide area, the nuclear warhead produces an enormous fire and leaves the contamination overlay behind, and the conventional warhead looks unchanged.
