# Warhead selection UI plus nuclear yield presets - implementation plan

> Decided: selecting with the number keys was only for testing; the real thing is **operated
> through the UI**.
> The nuclear yield uses **option 3, choosing a preset at launch**. The tactical, standard and
> strategic presets scale all of the nuclear effect radii together.

## Architecture

- `Core/NuclearYield.cs`, new and written test-first: the yield presets, Tactical at 20 kt, Standard at 150 kt and Strategic at 1000 kt.
  Following the blast radius going as the cube root of the yield, the scale factor is `Multiplier(kt)=cbrt(kt/150)`: 1.0 for Standard, about 0.51 for Tactical and about 1.87 for Strategic.
  Pure, with no UnityEngine dependency.
- `Core/WarheadSpec.cs`: add `Scaled(float m)`, returning a **new struct** with the crater, destruction, burn and contamination radii multiplied - it is immutable.
- `Game/Missile.cs` and `Game/MissileManager.cs`: change `Launch` to take `(target, type, nuclearYieldMultiplier)`.
  Only a nuclear warhead uses `spec = WarheadSpec.For(type).Scaled(mult)`; Missile receives the already-scaled spec.
- `Game/UI/MissilePanel.cs`, new: a permanent panel directly under UIView, with five warhead
  buttons, three nuclear yield preset buttons and an aim-and-launch button, highlighting the
  selection. It follows AlienInvasion.InvasionUI's pattern - the `ButtonMenu` sprite, eventClick,
  and being created and destroyed on level load. The selection is written to
  `MissileTool.CurrentWarhead` and `CurrentNuclearYield`.
- `Game/UI/MissileTool.cs`: **remove** the number-key selection and the OnToolGUI label; launching uses the warhead and yield currently selected.
- `Game/Loading/MissileLoadingExtension.cs`: `MissilePanel.Create()` in OnLevelLoaded and
  `Destroy()` in OnLevelUnloading, so no static state survives a level change.
- `Game/ImpactResolver.cs`: cap the crater radius and depth (`CraterRadiusMax` and
  `CraterDepthMax`) so that even a strategic warhead does not wreck the terrain, following
  NuclearMeltdown.
- `Game/ModConfig.cs`: add the panel's size and position and the crater caps.

## Testing (test-first)

- `NuclearYieldTests`: Standard gives 1.0, Tactical is below 1 and Strategic above, it increases monotonically, stays positive, and `Multiplier(kt)` holds the cube-root relationship.
- `WarheadSpecTests`: `Scaled(1)` changes nothing, `Scaled(2)` doubles every radius while leaving SubmunitionCount, the flags and the Type alone, and the original struct is untouched.
- The UI (UIPanel and UIButton) is verified in game: the panel appears, choosing a warhead and a yield is reflected when launching, and nothing survives a level reload.

## Thread discipline (unchanged)

The UI is on the main thread. The impact - resolving the already-scaled spec - stays on the simulation thread, and `_impactQueue` remains the only boundary.

## Definition of done

- Every Core test is green, including the new ones, and the build and deployment succeed. The number-key selection is gone.
- In game: choose the warhead and the nuclear yield on the panel, then aim and click to launch.
  The nuclear scale changes with the preset, and reloading a level neither duplicates the panel
  nor leaves it behind.
