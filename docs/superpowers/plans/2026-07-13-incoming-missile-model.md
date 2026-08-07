# Reworking the incoming missile (apex descent, the real model, and the nose along the path) - implementation plan

> This increment combines **2A (the reworked arrival) and 2B (the model, for the incoming
> missile only)** from `2026-07-13-flight-and-defense-design.md`.
> Decided: the incoming missile comes first and the sites second, and the terminal site uses
> PAC3. This plan covers the missile alone; the sites are the next plan.

**Goal:** rework the incoming missile so that it (1) descends only, from a high apex on a fixed bearing, (2) shows the real ballistic warhead model, and (3) points its nose - the model's +Z - along the flight path. The smallest thing that can be checked in game.

**Architecture:** built on Phase 1. Alien Invasion's OBJ loading pipeline - `ObjParser`, `ObjData` and `MtlParser` as pure Core, `ObjMeshBuilder` and `ModelProvider` in Game - is **ported in cut-down form**, without the AssetBundle, the decals or the night-time glow. `Missile` changes to the apex descent, creating the model and pointing the nose with `LookRotation`. The main and simulation thread boundary is unchanged.

**Global Constraints:**
- The mod targets `v3.5` with `LangVersion 7.3`, and `Core/**/*.cs` has no UnityEngine dependency.
- Bearings run clockwise, with 0 degrees as +Z (north) and 90 as +X.
- Model axes: +Z is the nose in Blender. The OBJ was exported with up_axis=Z and forward_axis=Y, and `ObjParser` only mirrors X, so +Z is the nose in Unity local space too. `Quaternion.LookRotation(velocity)` then points it along the flight path.
- The asset is already in place: `src/MissileDisaster/Models/IncomingWarhead.obj` and its `.mtl` - 161 vertices, 318 triangles, 2 materials, z from -1.117 to 1.0.
- Commit by `git add`ing only the files concerned, leaving untracked things like `.blend` and `.mp3` out.

---

## Files

| File | Kind | Responsibility |
|---|---|---|
| `Core/ObjData.cs` | new (ported) | the OBJ intermediate representation, `ObjData` and `ObjSubmesh` |
| `Core/ObjParser.cs` | new (ported) | parses OBJ text, mirroring X and reversing the winding |
| `Core/MtlParser.cs` | new (ported) | parses MTL: `MtlColor`, Kd and d |
| `tests/.../ObjParserTests.cs` | new (ported) | the parser tests |
| `Game/Models/MissileMeshBuilder.cs` | new | `ObjData` plus MTL into a `Mesh` and `Material[]`, Standard shader, no emission |
| `Game/Models/MissileModelProvider.cs` | new | creates and caches GameObjects from `Models/<name>.obj` |
| `Game/ModConfig.cs` | changed | the flight constants move to the apex approach, plus the model constants |
| `Game/Missile.cs` | changed | the apex descent, the real model and the nose direction, with the sphere as a fallback |
| `Game/Mod.cs` | changed | `OnEnabled` obtains the modPath and calls `MissileModelProvider.Initialize` |
| `build.ps1` | changed | deploys `Models/*.obj` and `*.mtl` into the mod folder |

---

## Task A: port the OBJ and MTL parsers into Core (test-first)

Copy Alien's `Core/ObjParser.cs`, `Core/ObjData.cs` and `Core/MtlParser.cs` into the `MissileDisaster.Core` namespace, with the logic unchanged, and port `ObjParserTests.cs` by changing nothing but the namespace. The test csproj links `Core/**/*.cs` automatically, so nothing needs adding to it.
- Verification: `dotnet test` is entirely green, the existing 38 plus the ported ones.

## Task B: creating the model (Game)

- `MissileMeshBuilder.TryBuild(ObjData, Dictionary<string,MtlColor>, Color fallback, out Mesh, out Material[])`: a cut-down version of Alien's `ObjMeshBuilder` - `FilterValidTriangles`, the Standard shader, the Kd colour and the metallic and gloss settings. **The `EmissionController` reference and the transparency registration are dropped.**
- `MissileModelProvider`: `Initialize(modDir)` and `CreateInstance(name)`. It runs `Models/<name>.obj` and its `.mtl` through the Core parsers into `MissileMeshBuilder`, caches the `Mesh` and `Material[]`, and returns a `GameObject` with a MeshFilter and MeshRenderer, or null if it is missing, in which case the caller falls back. No AssetBundle and no decals. All main thread.

## Task C: rework ModConfig

Replace the flight block - `MissileSpeed`, `MissileArcHeight`, `MissileStartAltitude` and `MissileLaunchOffset` - with:
```csharp
public const float MissileSpeed = 900f;            // descent pace, in metres per second against the horizontal distance
public const float IncomingBearingDegrees = 315f;  // bearing they arrive from, clockwise from north; 315 is north-west, and every missile shares it
public const float ApexHorizontalOffset = 2200f;   // horizontal offset of the apex (m)
public const float ApexAltitude = 4000f;           // height of the apex above the ground (m)
```
Add the model constants:
```csharp
public const string ModelsFolderName = "Models";
public const string IncomingMissileModelName = "IncomingWarhead";
public const float IncomingMissileScale = 18f;     // takes the ~2 m model to about 38 m in game; tuned in game
public const float ObjMetallic = 0.6f;
public const float ObjGlossiness = 0.5f;
public static readonly Color ObjFallbackColor = new Color(0.25f, 0.25f, 0.25f, 1f);
```

## Task D: change Missile to the apex descent, the real model and the nose direction

- `_apex = target + BearingOffset(IncomingBearingDegrees, ApexHorizontalOffset) + up*ApexAltitude`, descending only, with no ascent.
- `_groundDistance` is the horizontal `|target - apex|`, which equals ApexHorizontalOffset and is positive.
- Visuals: `MissileModelProvider.CreateInstance(IncomingMissileModelName)`, falling back to the sphere on null as in Phase 1, with `localScale = IncomingMissileScale`. The collider is destroyed.
- `UpdateVisual`: `AdvanceT` plus a straight `Lerp`, without `ArcHeightAt`. Besides the position, set `transform.rotation = Quaternion.LookRotation(vel)` from the constant `Vector3 vel = (_target - _apex)`, pointing the nose (+Z) along the path, and `return _t >= 1f`.
- The API `MissileManager` sees - `Missile(target,type)`, `UpdateVisual`, `Target`, `Spec` and `DestroyVisual` - is unchanged.

## Task E: initialise from Mod.OnEnabled

Add `OnEnabled()` to `Mod.cs`: `Singleton<PluginManager>.instance.FindPluginInfo(Assembly.GetExecutingAssembly())`, then `info.modPath`, then `MissileModelProvider.Initialize(info.modPath)`, wrapped in try/catch with LogError. It needs `System.Reflection`, `ColossalFramework` for Singleton and `ColossalFramework.Plugins` for PluginManager; the csproj already references ColossalManaged.

## Task F: deployment in build.ps1

After copying the DLL, copy the `Models` folder - the `*.obj` and `*.mtl` files - to `$modDir\Models`.

## Task G: build and verify in game (by the user)

`build.ps1` succeeds, then in CS press the hotkey and click. Confirm that it arrives **from the north-west and high up, showing only the descent**, that it is the warhead model rather than a sphere with **its nose along the path**, and that several missiles arrive from the same bearing. The scale, the bearing and the axes are fine-tuned in game.

## Definition of done

- Every Core test is green, including the ported parsers, and the build and deployment succeed. The user confirms the fixed bearing, the high altitude, the descent-only trajectory, the real model and the nose direction in game.

## The next plan (the sites)

Three new buildings for PAC3, VLS ARROW and VLS SM, plus `InterceptorAI`, `InterceptorRegistry` and the interception wiring (2D in the design), and the interceptors flying to the meeting point and exploding (2E).
