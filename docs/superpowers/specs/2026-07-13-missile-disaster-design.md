
# Missile Disaster mod - design (Cities: Skylines 2015)

- Date: 2026-07-13
- Target game: Cities: Skylines (2015)
- Foundation: C# plus Harmony, building on the existing disaster system (`DisasterManager`,
  `MeteorStrike`, `DisasterHelpers`)
- Status: design settled, awaiting the implementation plan

## 1. Purpose and scope

A disaster mod that can throw missiles at a chosen point or across the whole city. Five features:

1. Deliver any warhead - conventional, cluster, white phosphorus, thermobaric or nuclear - to a
   chosen point.
2. A barrage disaster launching any number, from 1 to 200, at random across the city.
3. Defence installations that intercept the missiles.
4. Nuclear warheads choose from a set of six representative yield presets.
5. A nuclear warhead leaves radioactive contamination; residents in range fall ill immediately.

### Decisions from the brainstorming

- **Project structure**: a new, self-contained mod, `MissileDisaster`, reusing code from Alien
  Invasion and NuclearMeltdown. The three mods coexist independently and are published to the
  Workshop separately.
- **Flight and interception**: a parabolic flight, intercepted in the air - a defence
  installation detects a missile in flight and shoots it down with some probability.
- **Nuclear scaling**: the cube-root rule, radius proportional to yield^(1/3), with a clamp.
- **Radioactivity**: illness that turns fatal over time, plus long-lived contamination. It is
  **a new concept in its own right**, and **the existing sewage and water treatment buildings
  cannot decontaminate it**. **Only a dedicated decontamination facility** removes it.
- **Barrage warheads**: a toggle between a single warhead type and a mix assigned at random.

## 2. Architecture and what is reused

```
MissileDisaster/
├── src/MissileDisaster/
│   ├── Core/                         # pure logic with no game types, tested with xUnit
│   │   ├── BallisticMath.cs          # new: the parabola and the flight time
│   │   ├── NukeScaling.cs            # new: the cube-root rule plus the clamp (yield to radius)
│   │   ├── NukePresets.cs            # new: the table of six nuclear presets
│   │   ├── WarheadSpec.cs            # new: the parameter table per warhead
│   │   ├── InterceptResolver.cs      # new: the interception probability, per warhead
│   │   └── BarrageScheduler.cs       # new: clamps the count to 1-200 and spreads the launches
│   ├── Game/
│   │   ├── Mod.cs                    # based on Alien's Mod.cs
│   │   ├── ModConfig.cs              # reused from both mods; the constants live here
│   │   ├── MissileManager.cs         # new: creating, tracking and landing the missiles (after Alien's InvasionManager)
│   │   ├── Missile.cs                # new: one missile's state and Transform interpolation (after Alien's Invasion.cs)
│   │   ├── Warheads/
│   │   │   ├── IWarhead.cs
│   │   │   ├── ConventionalWarhead.cs
│   │   │   ├── ClusterWarhead.cs
│   │   │   ├── WhitePhosphorusWarhead.cs
│   │   │   ├── ThermobaricWarhead.cs
│   │   │   └── NuclearWarhead.cs
│   │   ├── ImpactResolver.cs         # reuses Alien's MakeCrater, DestroyStuff and fires
│   │   ├── Radiation/
│   │   │   ├── RadiationManager.cs   # new: radioactive contamination as its own concept
│   │   │   ├── RadiationGrid.cs      # new: the contamination grid, following NuclearMeltdown's pattern
│   │   │   └── RadiationSickness.cs  # new: residents in range fall ill and eventually die (after NuclearMeltdown's health handling)
│   │   ├── Buildings/
│   │   │   ├── CustomBuildingFactory.cs   # new: clones vanilla buildings to register the two new ones
│   │   │   ├── MissileDefenseAI.cs        # new: the interceptor site AI
│   │   │   └── RadDecontaminationAI.cs    # new: the decontamination facility AI
│   │   ├── UI/MissileTool.cs         # reuses Alien's click-to-place tool
│   │   └── Simulation/MissileThreadingExtension.cs  # reuses Alien's simTimeDelta driving
│   └── Effects/                      # reuses Alien's LineRenderer and effect assets
└── tests/MissileDisaster.Core.Tests/ # the same xUnit layout as both mods
```

### What comes from where

| Needed | Source | Existing implementation |
|---|---|---|
| Click-to-place impact tool | Alien Invasion | a `ToolBase` subclass, click to a position |
| Crater, area destruction and fires | Alien Invasion | `DisasterHelpers.MakeCrater` / `DestroyStuff` |
| Several at once, game-speed and pause aware | Alien Invasion | a slot array plus `simulationTimeDelta` |
| Parabolic flight, trails and effects | Alien Invasion | Transform interpolation and LineRenderer |
| The contamination grid's structure and scanning | NuclearMeltdown | how `ContaminationManager` is written |
| Lowering residents' health | NuclearMeltdown | the health manipulation logic |

### Thread discipline (the same as Alien)

- Main thread: GameObjects, Transforms, effects and writing state.
- Simulation thread: nothing but `RadiationManager`'s grid work.
- The flight, the interception and the impact are all driven by `simulationTimeDelta`, so they
  follow the game speed and freeze while paused.

## 3. Feature by feature

### 1. How the five warheads land

Split between `WarheadSpec` - a table of numbers in Core - and the individual `Warheads/*.cs`,
which handle the presentation. The baseline is Alien's crater, destruction and fires,
differentiated by coefficients.

| Warhead | Crater | Area destruction | Fires | Radioactivity | Character |
|---|---|---|---|---|---|
| Conventional | medium | medium | small | none | A straightforward single impact. The baseline. |
| Cluster | tiny, many | wide but thin | medium | none | Splits into N submunitions in the air before impact and scatters them widely. It can only be stopped by intercepting before the split. |
| White phosphorus | almost none | small | enormous and sustained | none | Purely incendiary. Buildings in range burn for a long time. |
| Thermobaric | shallow | enormous | large | none | The overpressure flattens a wide area without digging deep. |
| Nuclear | largest | largest | large | yes | Follows the yield preset. Features 1, 4 and 5 all apply. |

- Common interface: `IWarhead.Detonate(Vector3 pos, WarheadSpec spec)`.
- Only the cluster warhead carries a `splitAltitude`; `MissileManager` handles the split.

### 2. The barrage disaster (1 to 200)

- Triggered from the UI, by button or key, and optionally as a random natural occurrence.
- `BarrageScheduler.Plan(count, mode)` in Core - and therefore testable - clamps the count to
  between 1 and 200 and turns it into a launch plan spread over several frames.
- Warheads: a toggle between a single type and a mix assigned at random, with the proportion of
  nuclear adjustable.
- **Keeping the load down (the hard part)**:
  - a cap on how many are in flight at once, with the excess queued
  - a lightweight representation while in flight
  - pooled and reused impact and explosion effects
  - impacts spread across frames too, so the simulation thread never backs up

### 3. Interceptor sites (defence)

- A prop building plus `MissileDefenseAI : PlayerBuildingAI`.
- Parameters: `range`, `interceptChance` and `cooldown`. Deliberately simple - no ammunition and
  so on.
- `SimulationStep` looks for missiles in flight within range and, if off cooldown, draws against
  `InterceptResolver`; success plays the kill effect and removes the missile.
- The probability depends on the warhead, in Core and therefore testable: nuclear is lower, and a
  cluster warhead can only be stopped before it splits.

### 4. Nuclear yield presets (six representative weapons)

Six weapons spanning a wide range of yields.
`NukeScaling.BlastRadius(yieldKt, scale) = scale * yieldKt^(1/3)`, clamped with `Mathf.Min` so it
cannot cover the map. The crater, the destruction and the contamination radii all scale from that
radius, each with its own clamp. The preset selector only appears in the UI when a nuclear
warhead is chosen.

| # | Name | Yield |
|---|---|---|
| 1 | Little Boy (Hiroshima) | 16 kt |
| 2 | Fat Man (Nagasaki) | 21 kt |
| 3 | W53 / B53 | 9 Mt |
| 4 | Mk-41 (B41) | 25 Mt |
| 5 | Tsar Bomba (as tested) | 50 Mt |
| 6 | Tsar Bomba (as designed) | 100 Mt |

(The yields are the published figures; in game they are compressed by the cube-root rule and the
clamp.)

### 5. Radioactive contamination (its own concept, with a dedicated facility)

- **New concepts**: `RadiationManager` and `RadiationGrid`. Only the grid's structure and scanning
  pattern come from NuclearMeltdown; **the "detect a Water Treatment plant and decontaminate"
  logic is not reused**. The existing sewage and water treatment buildings remove no
  radioactivity at all.
- Effects: residents in range fall ill immediately, their health declines while they stay, and
  they eventually die. The contamination lingers and decays slowly on its own, scaled by the
  yield and adjustable by constants. Only nuclear warheads produce it.
- **Only the new dedicated facility removes it**: a prop building, the Decontamination Facility,
  with `RadDecontaminationAI : PlayerBuildingAI`. While operating it gradually lowers the
  `RadiationGrid` cells in range. Nothing else clears it, apart from the natural decay.
- The contamination is visualised as a translucent green or purple ground overlay.
- **Geiger counter sound**: when the camera is near a contaminated area, a faint Geiger sound
  plays. It reuses Alien's `SoundLoaderBehaviour` - WAV playback with distance falloff that
  honours the pause - with the volume scaled by the contamination level and the distance.

- Two new prop buildings: the interceptor site and the decontamination facility.
- Since they are added in code alone, `CustomBuildingFactory` clones a vanilla building's
  `BuildingInfo` and registers it with the AI, mesh and name replaced.

## 4. Testing

The pure logic is tested with xUnit, laid out as in both other mods, with `Core/**/*.cs` linked
into the test project:

- `BallisticMath`: the end points match, the apex height is right, and the coordinates at t=0
  and t=1.
- `NukeScaling`: the cube root increases monotonically, the clamp holds, and the ratios are
  right.
- `NukePresets`: the six definitions and their yields.
- `WarheadSpec`: the per-warhead coefficients are sensible.
- `InterceptResolver`: the per-warhead probabilities, and that a cluster warhead cannot be
  intercepted after splitting.
- `BarrageScheduler`: the 1-200 clamp, and that the plan launches exactly the requested number.

Anything depending on game types - the AIs, the managers, the tool - is verified in game.

## 5. Implementation order (MVP first)

Each step is built to be something that works on its own.

1. **One conventional missile**: click, parabolic flight, crater and destruction on impact. This
   completes the foundation, largely reused from Alien.
2. The other warheads: cluster, white phosphorus, thermobaric.
3. The nuclear presets and radioactivity, as a new concept with its dedicated decontamination
   facility.
4. The 1-200 barrage and spreading its load.
5. The interceptor sites, which are the most complex and come last.

## 6. Open questions and risks

- Whether the two custom buildings can be created reliably in code alone; the `BuildingInfo`
  cloning approach needs verifying.
- The simulation-thread load of a 200-missile barrage. The parameters for spreading it will be
  tuned in game.
- The `scale` and the clamps for the six nuclear presets are a balancing exercise, to be tuned
  while playing.
- How fast the radioactivity kills, and how fast it decays, are expected to need tuning.
- The Geiger counter should be audible, quietly, near a contaminated area.
