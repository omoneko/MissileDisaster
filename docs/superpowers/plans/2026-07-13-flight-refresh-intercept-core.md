# Reworked arrival plus the interception core logic - implementation plan (independent of the models, done first)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** rework the incoming missile so it descends only, from a high apex on a fixed bearing, still drawn as a sphere; and provide the pure decision logic for the three interception layers - the altitude bands, the ranges and the probabilities - with tests. The buildings and the real models are a separate plan, once model.blend is finished.

**Architecture:** built on Phase 1. Pure functions with no UnityEngine dependency are added to `Core` - `LaunchGeometry`, `InterceptorTiers` and `InterceptDecision` - and covered by xUnit. `Missile` changes to interpolating the descent from the apex to the impact; the existing main and simulation thread boundary is unchanged.

**Tech stack:** C# 7.3 on .NET 3.5 for the mod itself, net8.0 with xUnit for the tests. The CS DLL references are already in place.

## Global Constraints

- The mod targets `TargetFrameworkVersion=v3.5` with `LangVersion=7.3`; no .NET 4.5 or later APIs.
- `Core/**/*.cs` has no UnityEngine dependency, so no `Mathf` - use `System.Math` and cast to float. Floats and built-in types only.
- The namespaces are `MissileDisaster.Core` and `MissileDisaster.Game`, and the log prefix is `[MissileDisaster] `.
- The test csproj links `..\..\src\MissileDisaster\Core\**\*.cs` automatically, so a new Core file is picked up without editing it.
- Bearings: **0 degrees is +Z, north, increasing clockwise**, so 90 is +X.
- The thread boundary is unchanged: flight and GameObjects on the main thread, impact damage on the simulation thread. This plan changes only what Missile interpolates, not where anything runs.
- Build with `powershell -ExecutionPolicy Bypass -File build.ps1`; test with `dotnet test tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj --nologo`.
- Commit by `git add`ing only the files concerned, leaving untracked `.blend` and `.mp3` files out. The local commit review hook may block on a priority-one finding.

---

## Files created and changed by this plan

| File | Kind | Responsibility |
|---|---|---|
| `src/MissileDisaster/Core/LaunchGeometry.cs` | new | a fixed bearing into an (X, Z) offset: the apex's horizontal position |
| `tests/.../LaunchGeometryTests.cs` | new | its tests |
| `src/MissileDisaster/Core/InterceptorTier.cs` | new | the layer data: each band, range, probability and cooldown |
| `tests/.../InterceptorTierTests.cs` | new | tests that the bands are contiguous and ordered |
| `src/MissileDisaster/Core/InterceptDecision.cs` | new | the engagement envelope plus the probability, with the random number injected |
| `tests/.../InterceptDecisionTests.cs` | new | its tests |
| `src/MissileDisaster/Game/ModConfig.cs` | changed | the flight constants move to the apex approach |
| `src/MissileDisaster/Game/Missile.cs` | changed | interpolates the descent from the apex to the impact |

---

## Task 1: LaunchGeometry (Core, test-first)

**Files:**
- Create: `src/MissileDisaster/Core/LaunchGeometry.cs`
- Test: `tests/MissileDisaster.Core.Tests/LaunchGeometryTests.cs`

**Interfaces:**
- Produces:
  - `struct MissileDisaster.Core.Offset2 { float X; float Z; }`
  - `Offset2 LaunchGeometry.BearingOffset(float bearingDeg, float horizontalDistance)` - 0 degrees is +Z and 90 is +X, increasing clockwise.

- [ ] **Step 1: write the failing tests.**

`tests/MissileDisaster.Core.Tests/LaunchGeometryTests.cs`:
```csharp
using MissileDisaster.Core;
using Xunit;

public class LaunchGeometryTests
{
    [Theory]
    [InlineData(0f, 100f, 0f, 100f)]     // north is +Z
    [InlineData(90f, 100f, 100f, 0f)]    // east is +X
    [InlineData(180f, 100f, 0f, -100f)]  // south is -Z
    [InlineData(270f, 100f, -100f, 0f)]  // west is -X
    public void BearingOffset_maps_compass_directions(float deg, float dist, float ex, float ez)
    {
        Offset2 o = LaunchGeometry.BearingOffset(deg, dist);
        Assert.Equal(ex, o.X, 3);
        Assert.Equal(ez, o.Z, 3);
    }

    [Theory]
    [InlineData(37f, 1234f)]
    [InlineData(315f, 2200f)]
    public void BearingOffset_preserves_horizontal_distance(float deg, float dist)
    {
        Offset2 o = LaunchGeometry.BearingOffset(deg, dist);
        float mag = (float)System.Math.Sqrt(o.X * o.X + o.Z * o.Z);
        Assert.Equal(dist, mag, 2);
    }
}
```

- [ ] **Step 2: confirm the tests fail.**

Run: `dotnet test tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj --nologo`
Expected: FAIL with a compile error, since `Offset2` and `LaunchGeometry` do not exist yet.

- [ ] **Step 3: implement.**

`src/MissileDisaster/Core/LaunchGeometry.cs`:
```csharp
namespace MissileDisaster.Core
{
    /// <summary>A horizontal bearing offset as (X, Z). No UnityEngine dependency.</summary>
    public struct Offset2
    {
        public float X;
        public float Z;
    }

    /// <summary>
    /// Works out the horizontal position of a trajectory's apex for a missile arriving from a
    /// fixed bearing. Bearings run clockwise, with 0 degrees as +Z (north) and 90 as +X (east).
    /// No UnityEngine dependency.
    /// </summary>
    public static class LaunchGeometry
    {
        public static Offset2 BearingOffset(float bearingDeg, float horizontalDistance)
        {
            double rad = bearingDeg * System.Math.PI / 180.0;
            return new Offset2
            {
                X = (float)(System.Math.Sin(rad) * horizontalDistance),
                Z = (float)(System.Math.Cos(rad) * horizontalDistance),
            };
        }
    }
}
```

- [ ] **Step 4: confirm the tests pass.**

Run: `dotnet test tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj --nologo`
Expected: PASS, the 6 new cases plus the 18 existing ones.

- [ ] **Step 5: Commit**

```bash
git add src/MissileDisaster/Core/LaunchGeometry.cs tests/MissileDisaster.Core.Tests/LaunchGeometryTests.cs
git commit -m "feat(core): LaunchGeometry, a fixed bearing to the apex horizontal offset (TDD)"
```

---

## Task 2: InterceptorTier (Core, test-first)

**Files:**
- Create: `src/MissileDisaster/Core/InterceptorTier.cs`
- Test: `tests/MissileDisaster.Core.Tests/InterceptorTierTests.cs`

**Interfaces:**
- Produces:
  - `enum MissileDisaster.Core.InterceptorKind { Arrow, Sam, Pac }`
  - `struct InterceptorTier { InterceptorKind Kind; float AltitudeMin; float AltitudeMax; float HorizontalRange; float InterceptChance; float CooldownSeconds; }`
  - `static class InterceptorTiers` with fields `Arrow`, `Sam`, `Pac` and `InterceptorTier[] Ordered`, from the highest band down.

- [ ] **Step 1: write the failing tests.**

`tests/MissileDisaster.Core.Tests/InterceptorTierTests.cs`:
```csharp
using MissileDisaster.Core;
using Xunit;

public class InterceptorTierTests
{
    [Fact]
    public void Ordered_is_highest_band_first()
    {
        var o = InterceptorTiers.Ordered;
        Assert.Equal(3, o.Length);
        Assert.Equal(InterceptorKind.Arrow, o[0].Kind);
        Assert.Equal(InterceptorKind.Sam, o[1].Kind);
        Assert.Equal(InterceptorKind.Pac, o[2].Kind);
        Assert.True(o[0].AltitudeMin > o[1].AltitudeMin);
        Assert.True(o[1].AltitudeMin > o[2].AltitudeMin);
    }

    [Fact]
    public void Bands_are_contiguous_and_start_at_ground()
    {
        Assert.Equal(0f, InterceptorTiers.Pac.AltitudeMin, 3);
        Assert.Equal(InterceptorTiers.Pac.AltitudeMax, InterceptorTiers.Sam.AltitudeMin, 3);
        Assert.Equal(InterceptorTiers.Sam.AltitudeMax, InterceptorTiers.Arrow.AltitudeMin, 3);
    }

    [Fact]
    public void All_tiers_have_valid_chance_range_and_positive_params()
    {
        foreach (var t in InterceptorTiers.Ordered)
        {
            Assert.InRange(t.InterceptChance, 0f, 1f);
            Assert.True(t.HorizontalRange > 0f);
            Assert.True(t.CooldownSeconds > 0f);
            Assert.True(t.AltitudeMax > t.AltitudeMin);
        }
    }
}
```

- [ ] **Step 2: confirm the tests fail.**

Run: `dotnet test tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj --nologo`
Expected: FAIL, with a compile error.

- [ ] **Step 3: implement.**

`src/MissileDisaster/Core/InterceptorTier.cs`:
```csharp
namespace MissileDisaster.Core
{
    public enum InterceptorKind { Arrow, Sam, Pac }

    /// <summary>An interception layer: the altitude band it covers, its horizontal range, its hit probability and its cooldown. No UnityEngine dependency.</summary>
    public struct InterceptorTier
    {
        public InterceptorKind Kind;
        public float AltitudeMin;
        public float AltitudeMax;
        public float HorizontalRange;
        public float InterceptChance; // 0..1
        public float CooldownSeconds;
    }

    /// <summary>Three layers from the top down: exo-atmospheric, high altitude, then terminal. The bands are contiguous from the ground up, and the figures are provisional, to be tuned in game.</summary>
    public static class InterceptorTiers
    {
        public static readonly InterceptorTier Pac = new InterceptorTier
        {
            Kind = InterceptorKind.Pac, AltitudeMin = 0f, AltitudeMax = 800f,
            HorizontalRange = 2000f, InterceptChance = 0.75f, CooldownSeconds = 4f
        };
        public static readonly InterceptorTier Sam = new InterceptorTier
        {
            Kind = InterceptorKind.Sam, AltitudeMin = 800f, AltitudeMax = 2500f,
            HorizontalRange = 4000f, InterceptChance = 0.6f, CooldownSeconds = 6f
        };
        public static readonly InterceptorTier Arrow = new InterceptorTier
        {
            Kind = InterceptorKind.Arrow, AltitudeMin = 2500f, AltitudeMax = 100000f,
            HorizontalRange = 6000f, InterceptChance = 0.5f, CooldownSeconds = 8f
        };

        /// <summary>The order interception is attempted in, from the highest band down.</summary>
        public static readonly InterceptorTier[] Ordered = { Arrow, Sam, Pac };
    }
}
```

- [ ] **Step 4: confirm the tests pass.**

Run: `dotnet test tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj --nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/MissileDisaster/Core/InterceptorTier.cs tests/MissileDisaster.Core.Tests/InterceptorTierTests.cs
git commit -m "feat(core): InterceptorTiers, the three interception layers (TDD)"
```

---

## Task 3: InterceptDecision (Core, test-first)

**Files:**
- Create: `src/MissileDisaster/Core/InterceptDecision.cs`
- Test: `tests/MissileDisaster.Core.Tests/InterceptDecisionTests.cs`

**Interfaces:**
- Consumes: `InterceptorTier`
- Produces:
  - `bool InterceptDecision.InEngagementZone(float missileAltitude, float horizontalDistance, InterceptorTier tier)`
  - `bool InterceptDecision.ShouldIntercept(float missileAltitude, float horizontalDistance, InterceptorTier tier, float roll)`, with the roll in [0,1) injected.

- [ ] **Step 1: write the failing tests.**

`tests/MissileDisaster.Core.Tests/InterceptDecisionTests.cs`:
```csharp
using MissileDisaster.Core;
using Xunit;

public class InterceptDecisionTests
{
    private static readonly InterceptorTier Sam = InterceptorTiers.Sam; // alt[800,2500) range 4000 chance 0.6

    [Theory]
    [InlineData(1500f, 1000f, true)]   // inside the band and in range
    [InlineData(800f, 1000f, true)]    // the lower bound, which is inclusive
    [InlineData(2500f, 1000f, false)]  // the upper bound, which is exclusive
    [InlineData(500f, 1000f, false)]   // below the band
    [InlineData(1500f, 4001f, false)]  // out of range
    [InlineData(1500f, 4000f, true)]   // exactly at the range limit, which is inclusive
    public void InEngagementZone_checks_band_and_range(float alt, float dist, bool expected)
    {
        Assert.Equal(expected, InterceptDecision.InEngagementZone(alt, dist, Sam));
    }

    [Theory]
    [InlineData(0.0f, true)]    // a roll under 0.6 intercepts
    [InlineData(0.59f, true)]
    [InlineData(0.6f, false)]   // a roll equal to the chance fails; only under it succeeds
    [InlineData(0.9f, false)]
    public void ShouldIntercept_rolls_within_zone(float roll, bool expected)
    {
        Assert.Equal(expected, InterceptDecision.ShouldIntercept(1500f, 1000f, Sam, roll));
    }

    [Fact]
    public void ShouldIntercept_false_outside_zone_regardless_of_roll()
    {
        Assert.False(InterceptDecision.ShouldIntercept(5000f, 1000f, Sam, 0.0f)); // outside the band
        Assert.False(InterceptDecision.ShouldIntercept(1500f, 9999f, Sam, 0.0f)); // out of range
    }
}
```

- [ ] **Step 2: confirm the tests fail.**

Run: `dotnet test tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj --nologo`
Expected: FAIL, with a compile error.

- [ ] **Step 3: implement.**

`src/MissileDisaster/Core/InterceptDecision.cs`:
```csharp
namespace MissileDisaster.Core
{
    /// <summary>
    /// Pure decision on whether an interception succeeds. The random number is injected as the
    /// roll argument so it can be tested. No UnityEngine dependency.
    /// altitude is the missile's height above the ground and horizontalDistance is how far away
    /// the interceptor building is.
    /// </summary>
    public static class InterceptDecision
    {
        public static bool InEngagementZone(float missileAltitude, float horizontalDistance, InterceptorTier tier)
        {
            return missileAltitude >= tier.AltitudeMin
                && missileAltitude < tier.AltitudeMax
                && horizontalDistance <= tier.HorizontalRange;
        }

        public static bool ShouldIntercept(float missileAltitude, float horizontalDistance, InterceptorTier tier, float roll)
        {
            return InEngagementZone(missileAltitude, horizontalDistance, tier)
                && roll < tier.InterceptChance;
        }
    }
}
```

- [ ] **Step 4: confirm the tests pass.**

Run: `dotnet test tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj --nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/MissileDisaster/Core/InterceptDecision.cs tests/MissileDisaster.Core.Tests/InterceptDecisionTests.cs
git commit -m "feat(core): InterceptDecision, the engagement envelope plus the probability (TDD)"
```

---

## Task 4: rework the incoming missile into an apex descent (ModConfig and Missile)

**Files:**
- Modify `src/MissileDisaster/Game/ModConfig.cs`, replacing the flight constants.
- Modify `src/MissileDisaster/Game/Missile.cs` to interpolate the descent from the apex to the impact.

**Interfaces:**
- Consumes `LaunchGeometry.BearingOffset`, `BallisticMath.AdvanceT` and `Lerp`, and the new `ModConfig` constants.
- Produces no API change: `Missile(target,type)`, `UpdateVisual(float)`, `Target`, `Spec` and `DestroyVisual` are all unchanged as far as `MissileManager` is concerned.

This is game DLL code with no unit tests; it is verified by the build succeeding and by checking it in game.

- [ ] **Step 1: replace the flight constants in ModConfig.**

Replace the flight block in `src/MissileDisaster/Game/ModConfig.cs` - the four constants `MissileSpeed`, `MissileArcHeight`, `MissileStartAltitude` and `MissileLaunchOffset` - with the following, keeping `MissileSpeed`:
```csharp
        // Flight, driven on the main thread by simulationTimeDelta.
        // The trajectory is the descending half only, from a high apex on a fixed bearing to the impact.
        public const float MissileSpeed = 900f;              // descent pace, in metres per second against the horizontal distance
        public const float IncomingBearingDegrees = 315f;    // bearing they arrive from, clockwise from north; 315 is north-west, and every missile shares it
        public const float ApexHorizontalOffset = 2200f;     // horizontal offset of the apex in metres; larger means a shallower angle
        public const float ApexAltitude = 4000f;             // height of the apex above the ground in metres; higher means a steeper dive from further up
```

- [ ] **Step 2: rewrite Missile for the apex descent.**

Replace the whole of `src/MissileDisaster/Game/Missile.cs` with:
```csharp
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// One missile in flight. Only the descending half of the trajectory is interpolated -
    /// from a high apex on a fixed bearing down to the impact - and all of it happens on the
    /// main thread; the simulation thread never touches this object.
    /// It is drawn as a simple sphere for now; the real model is a separate plan.
    /// </summary>
    public class Missile
    {
        private readonly Vector3 _apex;
        private readonly Vector3 _target;
        private readonly float _groundDistance;
        private readonly WarheadSpec _spec;
        private readonly GameObject _go;
        private float _t;

        public Vector3 Target => _target;
        public WarheadSpec Spec => _spec;

        public Missile(Vector3 target, WarheadType type)
        {
            _target = target;
            _spec = WarheadSpec.For(type);
            // It descends from a high apex on a fixed bearing. There is no ascent, so only the
            // terminal phase is drawn.
            Offset2 off = LaunchGeometry.BearingOffset(ModConfig.IncomingBearingDegrees, ModConfig.ApexHorizontalOffset);
            _apex = new Vector3(target.x + off.X, target.y + ModConfig.ApexAltitude, target.z + off.Z);
            float dx = target.x - _apex.x;
            float dz = target.z - _apex.z;
            _groundDistance = Mathf.Sqrt(dx * dx + dz * dz); // = ApexHorizontalOffset (>0)
            _t = 0f;

            _go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _go.transform.localScale = new Vector3(12f, 12f, 12f);
            var col = _go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            _go.transform.position = _apex;
        }

        /// <summary>
        /// Main thread. Interpolates the straight descent from the apex to the impact.
        /// Returning true means it landed on this frame. Queuing the damage and destroying the
        /// missile afterwards is MissileManager's job.
        /// </summary>
        public bool UpdateVisual(float simTimeDelta)
        {
            _t = BallisticMath.AdvanceT(_t, _groundDistance, ModConfig.MissileSpeed, simTimeDelta);
            float x = BallisticMath.Lerp(_apex.x, _target.x, _t);
            float y = BallisticMath.Lerp(_apex.y, _target.y, _t);
            float z = BallisticMath.Lerp(_apex.z, _target.z, _t);
            if (_go != null) _go.transform.position = new Vector3(x, y, z);
            return _t >= 1f;
        }

        /// <summary>Main thread. Destroys the missile's GameObject.</summary>
        public void DestroyVisual()
        {
            if (_go != null) Object.Destroy(_go);
        }
    }
}
```

Note that `BallisticMath.ArcHeightAt` goes unused here. It stays as a tested Core utility for tuning a curved descent later; keeping it is fine and it is not dead code.

- [ ] **Step 3: build and deploy.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds and the DLL is deployed.

- [ ] **Step 4: check how it looks in game (by hand, by the user).**

In Cities: Skylines, press the hotkey and click. Confirm that the sphere arrives **from the same bearing - north-west - starting at the high apex and drawing only the steep descending half**, that several launches all come from that direction, and that it follows the game speed and stops while paused.

- [ ] **Step 5: Commit**

```bash
git add src/MissileDisaster/Game/ModConfig.cs src/MissileDisaster/Game/Missile.cs
git commit -m "feat: incoming missiles now descend only, from a high apex on a fixed bearing"
```

---

## Definition of done

- Every Core test passes: the existing 18 plus roughly 6 for LaunchGeometry, 3 for InterceptorTier and 12 for InterceptDecision.
- The build and deployment succeed.
- The user confirms in game that it descends only, from high up and on a fixed bearing.

## Next (a separate plan, once model.blend is finished)

- Plan 2B: export and load the warhead, the ARROW, SAM and PAC models and the building meshes as OBJ, and give the incoming missile its real model and nose direction.
- Plan 2D: the three new buildings through `CustomBuildingFactory`, plus `InterceptorAI` and `InterceptorRegistry`, wiring the interception decision into `MissileManager` on the main thread.
- Plan 2E: the interceptors flying to the meeting point and the explosion.
