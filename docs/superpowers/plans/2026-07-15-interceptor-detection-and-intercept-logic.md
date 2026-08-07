# Interceptor sites: detection and interception logic - implementation plan (the asset-based successor)

> A change of premise: the interceptor sites now exist in the game as **ordinary assets** made
> in the Asset Editor - PAC3, THAAD, Aegis and Radar, named consistently. The runtime cloning
> approach (`CustomBuildingFactory` and `InterceptorAI`) is no longer needed at all and is
> removed.
> Decided: **the cost, power, water and upkeep come from the asset's own settings**, and the mod
> does not override them.
> The mod's job is limited to **detecting the placed buildings by name** and **running the
> interception logic**.

## Architecture

- `Core/InterceptorNameMatcher.cs`, new and written test-first: keyword matching on building names, with no UnityEngine dependency, following the same pattern as `NuclearMeltdown.Core.NuclearNameMatcher`.
  - PAC3 maps to `InterceptorKind.Pac`, THAAD to `Sam`, Aegis to `Arrow`, and Radar to the supporting role (IsRadar). Each also matches its Japanese name, since Workshop assets are often named in the author's own language.
- `Game/Defense/InterceptorRegistry.cs`, new and main thread only: scans `BuildingManager` at intervals of about a second and tracks the **operating** buildings whose names match, ticking the cooldowns down every frame. `TryIntercept(missilePos, targetGroundPos, out interceptPoint)` works down from the highest layer, testing the engagement envelope and the probability through the existing Core `InterceptDecision` and `InterceptorTiers`. An operating radar multiplies the probability by 1.5.
- `Game/Effects/InterceptFx.cs`, new: a simple flash on a successful interception, borrowing the game's own explosion effect, following Alien's `Effects.PlayImpactBurst`.
- `Game/Missile.cs`: expose `CurrentPosition` for the interception test.
- `Game/MissileManager.cs`: inside `UpdateVisual`, call `InterceptorRegistry.Tick`, test each missile, and on success let it disappear with a flash instead of queuing an impact.
- `Game/Simulation/MissileThreadingExtension.cs`: remove the Ctrl+1..4 hotkeys and the `PumpPanelRefresh` call, stopgap code the move to the Asset Editor made unnecessary.
- `Game/Loading/MissileLoadingExtension.cs`: remove the `CustomBuildingFactory.EnsureRegistered()` call and add `InterceptorRegistry.Reset()` on load and unload, the same static-state hygiene as `MissileManager.Reset()`.
- **Deleted**: `Game/Defense/CustomBuildingFactory.cs` and `Game/Defense/InterceptorAI.cs`.
- `ModConfig.cs`: remove the cloning-only constants such as `FallbackBuildingTemplateName`, and add `RadarSupportMultiplier`, `InterceptorScanIntervalFrames` and `InterceptFlashMagnitude`.

## Thread discipline (unchanged)

The interception test, the building scan and the cooldowns are **all on the main thread**, alongside `MissileManager.UpdateVisual` and the missiles' GameObjects. Impact damage still resolves on the simulation thread, and the only point of contact between them remains the existing lock-protected `_impactQueue`, unchanged.

## Testing

`InterceptorNameMatcher` is covered by xUnit in Core: case insensitivity, Workshop-style names with an ID prefix and a `_Data` suffix, and buildings that should not match. `InterceptorRegistry` and `InterceptFx` depend on game types and are verified in game - the build succeeding, and actually placing the sites and intercepting a missile.

## Definition of done

- Every Core test is green, including the new ones, and the build and deployment succeed.
- In game, place PAC3, THAAD and Aegis: an incoming missile is intercepted with the probability of whichever band it is in, disappearing with a flash, and anything that gets through lands as usual.
- Building and operating a radar site noticeably raises the interception rate. The exact numbers are tuned from in-game feedback in the next round.
