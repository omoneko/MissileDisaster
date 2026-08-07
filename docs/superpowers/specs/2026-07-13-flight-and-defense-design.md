# Missile Disaster mod - reworked incoming trajectory and three-layer interception - design

- Date: 2026-07-13
- Where this sits: an increment on top of Phase 1 (the conventional warhead MVP, already on
  master). It covers the improvements identified from playing it, plus feature 3, interception,
  brought forward and worked out in detail.
- Status: design settled, awaiting the implementation plan

## 1. Purpose

Improving how the missiles arrive, as identified in game, and introducing three-layer
interception.

1. Have missiles arrive **from a fixed bearing** rather than a random one.
2. Have them **arrive from high altitude**, drawing **only the descending half** of the
   trajectory, from the apex to the impact.
3. Use **the models in model.blend**, replacing the placeholder spheres.
4. Introduce three layers of interception - **exo-atmospheric, high altitude and terminal** - as
   one building each.

## 2. Decisions from the brainstorming

| Item | Decision |
|---|---|
| Bearing | A fixed world bearing, as a constant. Every missile arrives from the same direction. |
| Trajectory and drawing | The logic is defined as apex to impact, t=0 to t=1. The apex sits at high altitude with a horizontal offset, and **only the descending half is drawn** - the ascent is not. |
| Warhead model | The ballistic warhead model, as OBJ, with its nose pointed along the flight path. |
| Interception | Layered defence by altitude band, automatic and probabilistic. Working down from the top layer, each rolls where its band and horizontal range both cover the missile. Anything that gets through lands. |
| Interception presentation | On success an interceptor - the model for that layer - flies from the building to the meeting point, and a flash destroys both. |
| Buildings | Three **new buildings** with a cost and a power draw, created independently rather than cloned from vanilla. The mesh, AI, name, cost and power are all our own; only the minimum engine plumbing is reused. The building meshes are being made in model.blend and will be supplied. |
| Which thread decides | The main thread, since that is where the incoming missiles' positions live. The buildings register themselves in a registry. |

## 3. What is in model.blend

| Mesh name | Purpose | Vertices |
|---|---|---|
| ballistic warhead | the incoming missile itself | 161 |
| `ARROW` | exo-atmospheric interceptor | 769 |
| `SAM` | high-altitude interceptor | 1121 |
| `PAC` | terminal interceptor | 1569 |

There are no building meshes yet. The ARROW, SAM and PAC models are used as the
**interceptors in flight**. The **three dedicated building meshes are being made in
model.blend** and will be added and exported once finished.

## 4. Architecture (files added and changed)

```
src/MissileDisaster/
├── Core/
│   ├── BallisticMath.cs           # changed: helpers for the apex descent; the existing code is untouched
│   ├── LaunchGeometry.cs          # new: the apex position for a fixed bearing (pure, testable)
│   └── InterceptDecision.cs       # new: the band, range and probability test (pure, testable)
├── Game/
│   ├── ModConfig.cs               # changed: constants for the bearing, the bands and the interception
│   ├── Missile.cs                 # changed: apex descent, fixed bearing, the model, the nose direction, being intercepted
│   ├── MissileManager.cs          # changed: the interception test runs inside the flight loop, on the main thread
│   ├── Models/
│   │   ├── ModelLoader.cs         # new: loads OBJ and MTL (reusing Alien's ObjParser, MtlParser and ObjMeshBuilder)
│   │   └── MissileModels.cs       # new: a facade loading the four models and creating GameObjects
│   ├── Defense/
│   │   ├── InterceptorRegistry.cs # new: the operating interceptor buildings - position, band, range, probability, cooldown - read from the main thread
│   │   ├── InterceptorAI.cs       # new: a PlayerBuildingAI subclass; power and upkeep plus registering itself
│   │   ├── InterceptorTier.cs     # new: the bands, ranges and probabilities of the three layers
│   │   ├── InterceptorShot.cs     # new: the interceptor in flight, from the building to the meeting point to the explosion
│   │   └── CustomBuildingFactory.cs # new: registers the three buildings
│   └── Effects/
│       └── InterceptFx.cs         # new: the flash at the meeting point
├── Models/                        # where the OBJ and MTL live; build.ps1 deploys them
tests/MissileDisaster.Core.Tests/
├── LaunchGeometryTests.cs         # new
└── InterceptDecisionTests.cs      # new
```

Reused from Alien Invasion: `ObjParser`, `MtlParser` and `ObjMeshBuilder` for loading OBJ,
`RenderAssets` for finding shaders, and `Effects` for the LineRenderer and the flash.

## 5. Feature by feature

### A. Reworking the incoming missile

- **Fixed bearing**: `ModConfig.IncomingBearingDegrees`, for instance 315 degrees for
  north-west. The apex's horizontal offset is that bearing vector times
  `ApexHorizontalOffset`. Every missile shares the bearing.
- **Descending from a high apex**:
  `_apex = target + bearingVec * ApexHorizontalOffset + up * ApexAltitude`, with `ApexAltitude`
  set high, around 4000. `t=0` is the apex and `t=1` is the impact, so **only the descent
  exists**. The parabola is limited to that descending half; since the apex is the highest
  point, the extra arc term is small or zero, giving a steep dive with a slight gravitational
  curve.
  - `LaunchGeometry.ApexPosition(target, bearingDeg, horizOffset, altitude)` is a pure function
    in Core, and therefore testable.
- **What is drawn**: only apex to impact is created and drawn. There is no ascent, because the
  apex is the start. That satisfies both "only the terminal phase is drawn" and "arrives from
  high altitude".
- **The model**: the sphere is replaced by the ballistic warhead model, with **the nose pointed
  along the velocity vector**, through `Quaternion.LookRotation(velocity)`.
- **Passing through the bands**: on the way down it passes through the exo-atmospheric band, then
  the high-altitude band, then the terminal band, whose boundaries are altitude constants.

### B. Three-layer interception

- **Three new buildings**: `CustomBuildingFactory` builds **new BuildingInfos**, reusing only the
  minimum engine plumbing and setting the mesh, AI, name, cost, power and upkeep itself. The
  building meshes come from the dedicated models being made in model.blend. Nothing about a
  vanilla building's appearance or behaviour is reused.
- **`InterceptorAI : PlayerBuildingAI`**: the vanilla power and upkeep behaviour is delegated to
  the base class. While operating, i.e. powered, it registers itself in `InterceptorRegistry`,
  and deregisters when it stops or is destroyed.
- **`InterceptorTier`**, constants in Core: each layer's `AltitudeMin`/`Max`,
  `HorizontalRange`, `InterceptChance` and `CooldownSeconds`.
  - The exo-atmospheric layer has the highest band, the widest range and a low-to-medium
    probability; the high-altitude layer sits in the middle; the terminal layer has the highest
    probability over a narrower area. The balance is tuned through the constants.
- **The decision, on the main thread**: during `MissileManager`'s flight update, each incoming
  missile is checked against the registered buildings, highest layer first. Where the missile is
  inside a building's altitude band and horizontal range, and that building is off cooldown,
  `InterceptDecision.ShouldIntercept(...)` rolls. On success the missile is intercepted - it
  disappears without queuing an impact - the building's cooldown starts, and the interception
  presentation is created.
  - `InterceptDecision` in Core, and therefore testable: the band test, the horizontal distance
    test and the probability, all pure, with the random number injected as an argument so it can
    be tested.
- **The presentation**: on success an `InterceptorShot` with the matching model climbs from the
  building to the meeting point, where `InterceptFx` flashes and both disappear.
- **Getting through**: a missile no layer intercepts lands as usual, through the existing
  `ImpactResolver`.

### C. The model loading pipeline

- The four meshes in model.blend are exported as OBJ plus MTL, one file each, through
  `bpy.ops.wm.obj_export`, into `src/MissileDisaster/Models/`. `build.ps1` deploys them into the
  mod folder, as Alien does.
- At startup `MissileModels` loads the four and caches their meshes and materials. The incoming
  missile and each interceptor create their GameObjects from there. The in-game scale is a
  constant.
- The shader constraints of CS's Unity 5.6 are worked around by reusing Alien's `RenderAssets`.

## 6. Thread discipline

- The incoming missiles (`_missiles`), the interception GameObjects and **the interception
  decision and its resolution** are all on the main thread.
- `InterceptorRegistry` is read by the main thread. Registering and deregistering from the
  building AI is applied on the main thread; since `SimulationStep` runs on the simulation
  thread, registration goes through a lock or a queue applied on the main thread.
- Impact damage (`ImpactResolver`) stays on the simulation thread as before, drained from the
  queue in `MissileManager.UpdateSimulation`.

## 7. Testing

The pure Core logic is tested with xUnit:
- `LaunchGeometry`: the apex lands where the bearing, offset and altitude say it should, and its
  horizontal distance from the impact equals horizOffset.
- `InterceptDecision`: true and false as expected inside and outside the band, inside and
  outside the range, and at the probability boundaries, with the random number injected.
- The new helpers added to `BallisticMath`.

Anything depending on game types - the AI, the buildings, the models, the presentation - is
verified in game.

## 8. Implementation order (each step verifiable in game, split into separate plans)

**Two groups, split by their dependency on the models.** 2B, 2D and 2E have to wait for the
building meshes being made in model.blend and their OBJ export; 2A and 2C do not depend on any
model and go first.

First, independent of the models:
- **Plan 2A (reworked arrival)**: the fixed bearing, the apex descent and the higher altitude,
  still with the sphere. Verify in game that they descend from the same direction, from high up,
  showing only the descending half.
- **Plan 2C (interception core)**: `LaunchGeometry`, `InterceptDecision` and `InterceptorTier`,
  test-first, pure logic only.

Then, once the models are ready:
- **Plan 2B (models)**: export the warhead, ARROW, SAM, PAC and the building meshes from
  model.blend as OBJ and load them. Give the incoming missile its real model and point its nose.
- **Plan 2D (buildings and AI)**: `CustomBuildingFactory` for the new buildings, plus
  `InterceptorAI` and `InterceptorRegistry`. Place the three buildings and have missiles
  intercepted where the band and range cover them, with a simple flash for now.
- **Plan 2E (interception presentation)**: `InterceptorShot` flying to the meeting point,
  `InterceptFx` for the explosion, and the models for each.

## 9. Open questions and risks

- If power and cost are to be inherited by cloning a vanilla building, both the choice of source
  and the procedure for swapping in `InterceptorAI` need verifying in game, following Phase 1's
  `CustomBuildingFactory` approach.
- The band boundaries, ranges, probabilities and cooldowns are a balancing exercise, to be tuned
  while playing.
- How much arc the descent should have - a steep dive or a gentler curve - is a matter of
  appearance.
- The models' scale and which axis their nose points along will be adjusted in game.
