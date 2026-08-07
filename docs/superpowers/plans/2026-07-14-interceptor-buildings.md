# The interceptor sites phase - implementation plan (proper buildings, cloned at runtime)

> This implements 2B (the building meshes), 2D (the buildings, AI, registry and decision) and 2E
> (the interceptors and the explosion) from the design document
> `2026-07-13-flight-and-defense-design.md`.
> Decided: the sites are **proper buildings, cloned at runtime**. The order is the incoming
> missile first, which is done, then the sites. The terminal site uses the PAC3 mesh.

## The approach, and the risks already known

In CS 2015, **building a proper BuildingInfo in code alone is only semi-reliable** - that is the
community's conclusion. What breaks is `m_generatedInfo`, the LODs and the thumbnail atlas. The
only way to get close to reliable is:
- **Clone a vanilla building of the same footprint as the template** - the `Wind Turbine`, for instance, which is a `PowerPlantAI`: small, powered and with an upkeep.
- **Replace only the mesh, the material, the name, the cost and the AI**, inheriting `m_generatedInfo`, the atlas, the thumbnail and **the shader** from the template.
- The name must be **stable across saves**, registration goes through `InitializePrefabs` plus `BindPrefabs`, and `OnLevelLoaded` must be **idempotent**.
- The material is a copy of the template's `m_material`, which uses the `Custom/Buildings/Building` shader; that is what avoids the magenta error colour.

**So the first sub-increment, S1, is a walking skeleton: confirm in game that the building appears, can be placed, and does not crash, before anything else.** If S1 does not hold up, the approach is reconsidered - falling back to placing sites with a tool, or to an AssetBundle.

## Thread discipline

- A building AI's `SimulationStep` runs on the **simulation thread**, while the interception decision runs on the **main thread**, alongside `MissileManager.UpdateVisual` and the missiles' GameObjects.
- So **the main thread scans `BuildingManager` at intervals and updates InterceptorRegistry itself**; nothing registers from the simulation thread, and no lock is needed. It lists only the operating sites - built and powered.

---

## S1: the walking skeleton - one building can be placed (PAC3), de-risking above all

**Files:**
- Export from Blender: `src/MissileDisaster/Models/Building_PAC3.obj` and its `.mtl`, upright with up_axis Y and its origin at ground level.
- Create `src/MissileDisaster/Game/Defense/InterceptorAI.cs`, a minimal `PlayerBuildingAI` subclass holding an `InterceptorKind`.
- Create `src/MissileDisaster/Game/Defense/CustomBuildingFactory.cs`: find the template, clone it, replace the parts, register it.
- Modify `src/MissileDisaster/Game/ModConfig.cs` with the building constants: the candidate template names, the building name, the cost, the upkeep, the model name and the scale.
- Modify `src/MissileDisaster/Game/Loading/MissileLoadingExtension.cs` to call `CustomBuildingFactory.EnsureRegistered()` idempotently from `OnLevelLoaded`, gated on the LoadMode.

**CustomBuildingFactory.EnsureRegistered():**
1. The idempotence guard: return if it is already registered.
2. Find the template with `PrefabCollection<BuildingInfo>.FindLoaded("Wind Turbine")`. If that is null, walk the loaded prefabs and take the smallest `PowerPlantAI` building as a fallback.
3. `Object.Instantiate(template.gameObject)`, then `DontDestroyOnLoad`, then `SetActive(false)`.
4. Give `info.name` a unique, stable value ("MissileDisaster_PAC3") and set `m_prefabInitialized=false`.
5. Mesh and material: take them from
   `MissileModelProvider.CreateInstance("Building_PAC3")` - **but a building needs the
   Custom/Buildings/Building shader**, so the material is a copy of `template.m_material`, with
   our mesh's colour in its `mainTexture`, or just the colour where there is no texture.
   `m_mesh` and `m_lodMesh` are ours; `m_material` and `m_lodMaterial` are the copy.
   - `m_generatedInfo`, `m_cellWidth`, `m_cellLength`, `m_Atlas`, `m_Thumbnail`, `m_class` and `m_collisionHeight` are **inherited from the template**. `m_placementStyle=Manual`.
6. Swap the AI: `DestroyImmediate` the existing `BuildingAI`, then `AddComponent<InterceptorAI>()`. Set `ai.m_info=info; info.m_buildingAI=ai;`, put the cost and upkeep on the AI, and set `InterceptorKind=Pac`.
7. Register: `info.m_prefabDataIndex=-1; PrefabCollection<BuildingInfo>.InitializePrefabs("MissileDisaster", info, null); BindPrefabs(); info.RefreshLevelOfDetail(); go.SetActive(true);`
8. If it does not appear in the menu because it registered late, call `RefreshPanel()` on the relevant `GeneratedScrollPanel` - the power tab - and check the log.

**InterceptorAI:** a `PlayerBuildingAI` subclass with `public InterceptorKind Kind;`. In S1 it delegates everything to the base class - existing, power and upkeep - and S2 uses it to read state for the registry.

**Verification, by the user in game:** the building appears in the power tab, can be placed, **shows the PAC3 model** rather than magenta, draws power and costs money, and does not crash, with the registration logged. **Only once this holds does anything after S2 begin.**

---

## S2: wiring up the interception (detect, remove the missile, simple flash)

**Files:** create `Game/Defense/InterceptorRegistry.cs`; modify `MissileManager.cs`, `Missile.cs` (the current position property), `Simulation/MissileThreadingExtension.cs` (driving the registry refresh) and `Effects/` (the simple flash).

- `InterceptorRegistry`, main thread only: `Refresh()` scans `BuildingManager.instance.m_buildings` at intervals and collects, for each operating building with an `InterceptorAI` - built and powered - its position, the InterceptorTier its Kind maps to, and its remaining cooldown. `TryConsume(...)` manages the cooldown.
- `MissileManager.UpdateVisual`, on the main thread: for each incoming missile, working down from the highest layer, take a building that is in range - both the altitude band and the horizontal range - and off cooldown, and call `InterceptDecision.ShouldIntercept(alt, dist, tier, roll)`. The roll uses `UnityEngine.Random`, since `SimulationManager.instance.m_randomizer` belongs to the simulation thread. On success the missile disappears through DestroyVisual without queuing an impact, the building's cooldown starts, and `Effects.PlayInterceptFlash(meeting point)` plays.
- The missile's altitude is `pos.y - Target.y` and the horizontal distance is the XZ distance to the building.
- **Verification:** place a building, launch, and see it sometimes intercepted - disappearing with a flash - and sometimes getting through. The bands and probabilities are tuned through the constants.

---

## S3: all three sites (one building each for ARROW, SM and PAC)

- Blender: also export `Building_VLS_ARROW.obj` and `Building_VLS_SM.obj`.
- Generalise `CustomBuildingFactory` to register all three, mapping the Kind to its model name, building name, cost and template, and set each `InterceptorAI.Kind`.
- `InterceptorRegistry` looks the band, range, probability and cooldown up from the Kind through `InterceptorTiers`, using the existing Core.
- **Verification:** place all three and watch them divide the work by altitude band, from the exo-atmospheric layer down through the high-altitude one to the terminal one.

---

## S4: the interceptor's flight and the explosion

- Create `Game/Defense/InterceptorShot.cs`: the interceptor models climb from the building to the meeting point with their noses (+Z) along the flight path, using the existing LookRotation approach.
- Create `Game/Effects/InterceptFx.cs`: a flash at the meeting point and both disappear. `MissileTrail`'s asset resolution can be reused.
- Replace S2's simple flash on a successful interception with InterceptorShot plus InterceptFx.
- **Verification:** on an interception, an interceptor climbs from the site, explodes at the meeting point, and the incoming missile disappears.

---

## Definition of done

- S1: the building can be placed, shows its model, draws power and does not crash, verified in game by the user.
- S2 to S4: the three sites intercept probabilistically by altitude band, the interceptors fly and explode, and anything that gets through lands as usual. The Core tests stay green.
- Each sub-increment is built and deployed, reviewed, then committed normally.

## Risks and fallbacks

- If S1 produces no building, crashes, renders magenta or breaks the thumbnail, move registration to an earlier phase through a prefab hook or Harmony; failing that, fall back to placing sites with a tool or to an AssetBundle, after discussing it with the user.
