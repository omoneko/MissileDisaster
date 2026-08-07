# Missile Disaster mod - Phase 1 implementation plan (the foundation plus one conventional warhead)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** the smallest thing that works - one conventional missile flying a parabola to the point you clicked, leaving a crater and destroying the area around it.

**Architecture:** a new, self-contained mod, `MissileDisaster`, following Alien Invasion's two-tier split - Transform interpolation on the main thread driven by `simulationTimeDelta`, and impact damage through `DisasterHelpers` on the simulation thread. The pure maths lives in `Core` with no UnityEngine dependency, floats only, and is covered by xUnit; anything depending on game types is verified in game.

**Tech stack:** C# with LangVersion 7.3; the mod targets .NET Framework 3.5 (Cities: Skylines on Unity 5.6) and the tests net8.0 with xUnit. The referenced DLLs come from `Cities_Data\Managed` in the game's installation.

## Global Constraints

- The mod targets `TargetFrameworkVersion=v3.5` with `LangVersion=7.3`. No .NET 4.5 or later APIs - `IReadOnlyList` and the like are out, though `string.IndexOf(string, StringComparison)` is fine.
- `Core/**/*.cs` never references UnityEngine, using floats and built-in types only, because the test project links the same sources under net8.0.
- The managed DLL path is kept in one place, the `ManagedDLLPath` property, pointing at the game's `Cities_Data\Managed`.
- The thread boundary is absolute: GameObjects, Transforms, effects and writing state on the main thread; `DisasterHelpers` and the contamination writes on the simulation thread. Never touch a Transform from `ThreadingExtensionBase.OnAfterSimulationTick`.
- The test layout matches the two existing mods: the test csproj links `..\..\src\MissileDisaster\Core\**\*.cs` with `LinkBase="Core"`.
- The log prefix is `[MissileDisaster] `.
- The root namespace is `MissileDisaster`, with `MissileDisaster.Core`, `MissileDisaster.Game`, `MissileDisaster.Game.Simulation` and `MissileDisaster.Game.UI`.
- The mod deploys to `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\MissileDisaster`.

---

## Files created in Phase 1

| File | Responsibility |
|---|---|
| `MissileDisaster.sln` | the solution, holding the mod and the tests |
| `src/MissileDisaster/MissileDisaster.csproj` | the mod project, targeting v3.5 and referencing the CS DLLs |
| `src/MissileDisaster/Properties/AssemblyInfo.cs` | the assembly information |
| `src/MissileDisaster/Core/BallisticMath.cs` | the pure maths of the parabola, the interpolation and the progress |
| `src/MissileDisaster/Core/WarheadType.cs` | the warhead enum; Phase 1 uses Conventional only |
| `src/MissileDisaster/Core/WarheadSpec.cs` | the parameter table per warhead; Phase 1 has the conventional figures |
| `src/MissileDisaster/Game/ModConfig.cs` | the constants and logging |
| `src/MissileDisaster/Game/Missile.cs` | one missile: its state, its flight interpolation and its impact |
| `src/MissileDisaster/Game/MissileManager.cs` | launching and tracking - flight on the main thread, impact on the simulation thread |
| `src/MissileDisaster/Game/ImpactResolver.cs` | the crater and the area destruction, on the simulation thread |
| `src/MissileDisaster/Game/UI/MissileTool.cs` | the tool for clicking where it should land |
| `src/MissileDisaster/Game/Simulation/MissileThreadingExtension.cs` | drives the triggering, the flight and the impact |
| `src/MissileDisaster/Game/Mod.cs` | the IUserMod entry point |
| `tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj` | the test project |
| `tests/MissileDisaster.Core.Tests/BallisticMathTests.cs` | the BallisticMath tests |
| `build.ps1` | builds and deploys |

---

## Task 1: the project foundation (an empty mod that compiles, plus the test scaffolding)

**Files:**
- Create: `src/MissileDisaster/MissileDisaster.csproj`
- Create: `src/MissileDisaster/Properties/AssemblyInfo.cs`
- Create: `src/MissileDisaster/Game/ModConfig.cs`
- Create: `src/MissileDisaster/Game/Mod.cs`
- Create: `tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj`
- Create: `MissileDisaster.sln`
- Create: `build.ps1`

**Interfaces:**
- Produces `MissileDisaster.Game.ModConfig.Log(string)`, `LogError(string)` and `LogPrefix`, plus the IUserMod `MissileDisaster.Game.Mod`.

- [ ] **Step 1: create the mod csproj.**

`src/MissileDisaster/MissileDisaster.csproj`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildToolsPath)\Microsoft.Common.props" Condition="Exists('$(MSBuildToolsPath)\Microsoft.Common.props')" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Release</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <ProjectGuid>{D3A1B3D0-0000-4000-8000-000000000010}</ProjectGuid>
    <OutputType>Library</OutputType>
    <RootNamespace>MissileDisaster</RootNamespace>
    <AssemblyName>MissileDisaster</AssemblyName>
    <TargetFrameworkVersion>v3.5</TargetFrameworkVersion>
    <LangVersion>7.3</LangVersion>
    <FileAlignment>512</FileAlignment>
    <ManagedDLLPath>C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed</ManagedDLLPath>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)' == 'Release' ">
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <Optimize>true</Optimize>
    <DebugType>pdbonly</DebugType>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="ICities">
      <HintPath>$(ManagedDLLPath)\ICities.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="Assembly-CSharp">
      <HintPath>$(ManagedDLLPath)\Assembly-CSharp.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="ColossalManaged">
      <HintPath>$(ManagedDLLPath)\ColossalManaged.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="UnityEngine">
      <HintPath>$(ManagedDLLPath)\UnityEngine.dll</HintPath>
      <Private>False</Private>
    </Reference>
  </ItemGroup>
  <ItemGroup>
    <Compile Include="Core\**\*.cs" />
    <Compile Include="Game\**\*.cs" />
    <Compile Include="Properties\AssemblyInfo.cs" />
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
```

- [ ] **Step 2: create AssemblyInfo.**

`src/MissileDisaster/Properties/AssemblyInfo.cs`:
```csharp
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("MissileDisaster")]
[assembly: AssemblyProduct("MissileDisaster")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: ComVisible(false)]
[assembly: Guid("d3a1b3d0-0000-4000-8000-000000000010")]
```

- [ ] **Step 3: create ModConfig with the Phase 1 constants.**

`src/MissileDisaster/Game/ModConfig.cs`:
```csharp
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>Mod-wide constants and shared logging.</summary>
    public static class ModConfig
    {
        public const string LogPrefix = "[MissileDisaster] ";

        // Hotkey that opens the manual launch tool; F9, to avoid Alien's F7.
        public const KeyCode ManualTriggerKey = KeyCode.F9;

        // Flight, driven on the main thread by simulationTimeDelta.
        public const float MissileSpeed = 900f;   // metres per second against the horizontal distance
        public const float MissileArcHeight = 700f; // height of the parabola's apex (m)
        public const float MissileStartAltitude = 1200f; // height of the launch point

        // Impact of a conventional warhead; DisasterHelpers is called on the simulation thread.
        public const float SinkholeRadius = 60f;
        public const float SinkholeDepth = 16f;
        public const float DestructionRadius = 120f;

        public static void Log(string msg) { Debug.Log(LogPrefix + msg); }
        public static void LogError(string msg) { Debug.LogError(LogPrefix + msg); }
    }
}
```

- [ ] **Step 4: create the IUserMod entry point.**

`src/MissileDisaster/Game/Mod.cs`:
```csharp
using ICities;

namespace MissileDisaster.Game
{
    public class Mod : IUserMod
    {
        public string Name => "Missile Disaster";
        public string Description =>
            "Launch missiles (conventional now; more warheads coming) at any spot. " +
            "Press F9 or use the button, then click a target.";
    }
}
```

- [ ] **Step 5: create the test project.**

`tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>disable</Nullable>
    <LangVersion>7.3</LangVersion>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <!-- Compile the real Core sources straight into the test assembly, so no separate build is needed -->
    <Compile Include="..\..\src\MissileDisaster\Core\**\*.cs" LinkBase="Core" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6: create the solution and add the projects.**

Run:
```bash
cd <repository root>
dotnet new sln -n MissileDisaster
dotnet sln add tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj
```
Expected: the sln is created and the test project added. The v3.5 mod project is deliberately left out of the sln, since `dotnet sln add` warns about the SDK difference; it is built with `build.ps1` through msbuild instead.

- [ ] **Step 7: create build.ps1.**

`build.ps1`:
```powershell
$ErrorActionPreference = "Stop"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild not found" }

& $msbuild "src\MissileDisaster\MissileDisaster.csproj" /t:Restore,Build /p:Configuration=Release /v:minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$dll = "src\MissileDisaster\bin\Release\MissileDisaster.dll"
$modDir = Join-Path $env:LOCALAPPDATA "Colossal Order\Cities_Skylines\Addons\Mods\MissileDisaster"
New-Item -ItemType Directory -Force -Path $modDir | Out-Null
Copy-Item $dll $modDir -Force
Write-Host "Deploy complete: $modDir"
```

- [ ] **Step 8: confirm the mod compiles.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: `MissileDisaster -> ...\bin\Release\MissileDisaster.dll` followed by the deployment message, with no errors.

- [ ] **Step 9: confirm the test project builds and runs, with zero tests.**

Run: `dotnet test tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj --nologo`
Expected: the build succeeds with a total of 0 tests, since Core has nothing to test yet.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "chore: scaffold the MissileDisaster project (empty mod plus test scaffolding)"
```

---

## Task 2: BallisticMath (Core, test-first)

**Files:**
- Create: `src/MissileDisaster/Core/BallisticMath.cs`
- Test: `tests/MissileDisaster.Core.Tests/BallisticMathTests.cs`

**Interfaces:**
- Produces:
  - `float BallisticMath.Clamp01(float t)`
  - `float BallisticMath.Lerp(float a, float b, float t)` - interpolates, clamping t into 0..1
  - `float BallisticMath.ArcHeightAt(float t, float arcHeight)` - the height component of the arc: 0 at t=0 and t=1, and arcHeight at t=0.5
  - `float BallisticMath.AdvanceT(float t, float groundDistance, float speed, float dt)` - advances t by the speed and the elapsed time, without throwing at zero distance

- [ ] **Step 1: write the failing tests.**

`tests/MissileDisaster.Core.Tests/BallisticMathTests.cs`:
```csharp
using MissileDisaster.Core;
using Xunit;

public class BallisticMathTests
{
    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(1f, 1f)]
    [InlineData(2f, 1f)]
    public void Clamp01_clamps_to_unit_range(float input, float expected)
    {
        Assert.Equal(expected, BallisticMath.Clamp01(input), 5);
    }

    [Theory]
    [InlineData(0f, 100f, 0f, 0f)]
    [InlineData(0f, 100f, 1f, 100f)]
    [InlineData(0f, 100f, 0.25f, 25f)]
    [InlineData(0f, 100f, 2f, 100f)]   // clamped
    public void Lerp_interpolates_and_clamps(float a, float b, float t, float expected)
    {
        Assert.Equal(expected, BallisticMath.Lerp(a, b, t), 4);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(1f, 0f)]
    [InlineData(0.5f, 700f)]  // arcHeight at the apex
    public void ArcHeightAt_is_zero_at_ends_and_peaks_at_mid(float t, float expected)
    {
        Assert.Equal(expected, BallisticMath.ArcHeightAt(t, 700f), 3);
    }

    [Fact]
    public void ArcHeightAt_is_symmetric()
    {
        Assert.Equal(BallisticMath.ArcHeightAt(0.25f, 700f),
                     BallisticMath.ArcHeightAt(0.75f, 700f), 4);
    }

    [Fact]
    public void AdvanceT_progresses_by_speed_over_distance()
    {
        // A distance of 1000 at a speed of 500 over dt=1 advances t by 0.5.
        Assert.Equal(0.5f, BallisticMath.AdvanceT(0f, 1000f, 500f, 1f), 4);
    }

    [Fact]
    public void AdvanceT_handles_zero_distance_without_divide_by_zero()
    {
        float result = BallisticMath.AdvanceT(0.4f, 0f, 500f, 1f);
        Assert.True(result >= 1f); // zero distance counts as an immediate impact, so t reaches 1
    }
}
```

- [ ] **Step 2: confirm the tests fail.**

Run: `dotnet test tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj --nologo`
Expected: FAIL with a compile error, since `BallisticMath` does not exist yet.

- [ ] **Step 3: write the minimum implementation.**

`src/MissileDisaster/Core/BallisticMath.cs`:
```csharp
namespace MissileDisaster.Core
{
    /// <summary>
    /// Pure maths for a missile's parabolic flight. No UnityEngine dependency - floats only -
    /// so it is unit testable. The Game layer builds the Vector3 by lerping x and z, and
    /// composing y from a lerp plus ArcHeightAt.
    /// </summary>
    public static class BallisticMath
    {
        public static float Clamp01(float t)
        {
            if (t < 0f) return 0f;
            if (t > 1f) return 1f;
            return t;
        }

        public static float Lerp(float a, float b, float t)
        {
            t = Clamp01(t);
            return a + (b - a) * t;
        }

        /// <summary>The height component of the arc: 0 at t=0 and t=1, and arcHeight at t=0.5.</summary>
        public static float ArcHeightAt(float t, float arcHeight)
        {
            t = Clamp01(t);
            return arcHeight * 4f * t * (1f - t);
        }

        /// <summary>Advances t by however far speed carries it along groundDistance, the distance projected on the ground.</summary>
        public static float AdvanceT(float t, float groundDistance, float speed, float dt)
        {
            if (groundDistance <= 0.0001f) return 1f; // zero distance counts as an immediate impact
            return t + (speed * dt) / groundDistance;
        }
    }
}
```

- [ ] **Step 4: confirm the tests pass.**

Run: `dotnet test tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj --nologo`
Expected: PASS, all 15 cases.

- [ ] **Step 5: Commit**

```bash
git add src/MissileDisaster/Core/BallisticMath.cs tests/MissileDisaster.Core.Tests/BallisticMathTests.cs
git commit -m "feat(core): BallisticMath, the pure maths of the parabolic flight (TDD)"
```

---

## Task 3: WarheadType and WarheadSpec (Core, test-first; Phase 1 covers Conventional)

**Files:**
- Create: `src/MissileDisaster/Core/WarheadType.cs`
- Create: `src/MissileDisaster/Core/WarheadSpec.cs`
- Test: `tests/MissileDisaster.Core.Tests/WarheadSpecTests.cs`

**Interfaces:**
- Produces:
  - `enum MissileDisaster.Core.WarheadType { Conventional, Cluster, WhitePhosphorus, Thermobaric, Nuclear }`
  - `struct WarheadSpec { float CraterRadius; float CraterDepth; float DestructionRadius; bool Contaminates; }`
  - `WarheadSpec WarheadSpec.For(WarheadType type)` - only Conventional has real figures in Phase 1; the rest provisionally return the same, and a later phase differentiates them

- [ ] **Step 1: write the failing tests.**

`tests/MissileDisaster.Core.Tests/WarheadSpecTests.cs`:
```csharp
using MissileDisaster.Core;
using Xunit;

public class WarheadSpecTests
{
    [Fact]
    public void Conventional_has_positive_crater_and_destruction()
    {
        var spec = WarheadSpec.For(WarheadType.Conventional);
        Assert.True(spec.CraterRadius > 0f);
        Assert.True(spec.CraterDepth > 0f);
        Assert.True(spec.DestructionRadius > 0f);
    }

    [Fact]
    public void Conventional_does_not_contaminate()
    {
        Assert.False(WarheadSpec.For(WarheadType.Conventional).Contaminates);
    }

    [Fact]
    public void Every_warhead_type_has_a_spec()
    {
        foreach (WarheadType t in System.Enum.GetValues(typeof(WarheadType)))
        {
            var spec = WarheadSpec.For(t);
            Assert.True(spec.DestructionRadius > 0f, $"{t} must have destruction radius");
        }
    }
}
```

- [ ] **Step 2: confirm the tests fail.**

Run: `dotnet test tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj --nologo`
Expected: FAIL with a compile error, since `WarheadType` and `WarheadSpec` do not exist yet.

- [ ] **Step 3: write the minimum implementation.**

`src/MissileDisaster/Core/WarheadType.cs`:
```csharp
namespace MissileDisaster.Core
{
    /// <summary>The kinds of warhead. Phase 1 implements the behaviour of Conventional only.</summary>
    public enum WarheadType
    {
        Conventional,
        Cluster,
        WhitePhosphorus,
        Thermobaric,
        Nuclear,
    }
}
```

`src/MissileDisaster/Core/WarheadSpec.cs`:
```csharp
namespace MissileDisaster.Core
{
    /// <summary>
    /// Impact parameters per warhead, as a plain table of numbers with no UnityEngine
    /// dependency.
    /// Only Conventional has real figures in Phase 1; the others provisionally return the same,
    /// and a later phase - the warhead types and the nuclear one - differentiates them.
    /// </summary>
    public struct WarheadSpec
    {
        public float CraterRadius;
        public float CraterDepth;
        public float DestructionRadius;
        public bool Contaminates;

        public static WarheadSpec For(WarheadType type)
        {
            // Phase 1: everything behaves as a conventional warhead; a later phase branches on the type.
            return new WarheadSpec
            {
                CraterRadius = 60f,
                CraterDepth = 16f,
                DestructionRadius = 120f,
                Contaminates = false,
            };
        }
    }
}
```

- [ ] **Step 4: confirm the tests pass.**

Run: `dotnet test tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj --nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/MissileDisaster/Core/WarheadType.cs src/MissileDisaster/Core/WarheadSpec.cs tests/MissileDisaster.Core.Tests/WarheadSpecTests.cs
git commit -m "feat(core): WarheadType/WarheadSpec (Phase1=Conventional)"
```

---

## Task 4: ImpactResolver (the crater and the destruction, on the simulation thread)

**Files:**
- Create: `src/MissileDisaster/Game/ImpactResolver.cs`

**Interfaces:**
- Consumes: `MissileDisaster.Core.WarheadSpec`
- Produces `void ImpactResolver.Resolve(UnityEngine.Vector3 target, WarheadSpec spec)`, simulation thread only, calling `DisasterHelpers.MakeCrater` and `DisasterHelpers.DestroyStuff` once each.

- [ ] **Step 1: write the implementation**, a simplification of Alien's ResolveBombardDamage for a conventional warhead.

`src/MissileDisaster/Game/ImpactResolver.cs`:
```csharp
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// Resolves the damage of an impact. DisasterHelpers is contracted to the simulation
    /// thread, so this must only be called from MissileManager.UpdateSimulation.
    /// </summary>
    public static class ImpactResolver
    {
        public static void Resolve(Vector3 target, WarheadSpec spec)
        {
            // The crater, using the same MakeCrater call as the vanilla SinkholeAI, exactly once.
            DisasterHelpers.MakeCrater(new Vector2(target.x, target.z), spec.CraterRadius, spec.CraterDepth, false);

            // Area destruction. preRadius has to equal totalRadius; passing 0 is the known trap where nothing is destroyed.
            int seed = (int)SimulationManager.instance.m_randomizer.Int32(1000000u);
            float r = spec.DestructionRadius;
            DisasterHelpers.DestroyStuff(seed, null, target, r, r, 0f, r * 0.5f, r, r * 0.3f, r * 0.6f);

            ModConfig.Log("Impact resolved (crater+destruction) at " + target);
        }
    }
}
```

- [ ] **Step 2: confirm it compiles.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds, with `DisasterHelpers.MakeCrater` and `DestroyStuff` resolving.

- [ ] **Step 3: Commit**

```bash
git add src/MissileDisaster/Game/ImpactResolver.cs
git commit -m "feat: ImpactResolver, the impact crater and area destruction (simulation thread)"
```

---

## Task 5: Missile and MissileManager (flight on the main thread, impact on the simulation thread)

**Files:**
- Modify `src/MissileDisaster/Game/ModConfig.cs` to add the `MissileLaunchOffset` constant.
- Create: `src/MissileDisaster/Game/Missile.cs`
- Create: `src/MissileDisaster/Game/MissileManager.cs`

**Interfaces:**
- Consumes `BallisticMath`, `WarheadType`, `WarheadSpec.For`, `ImpactResolver.Resolve` and `ModConfig`
- Produces:
  - `MissileManager.Launch(Vector3 target, WarheadType type)` - main thread. Creates the missile at a launch point offset from the target by `MissileLaunchOffset` horizontally and `MissileStartAltitude` vertically.
  - `MissileManager.UpdateVisual(float simTimeDelta)` - main thread. Advances the missiles' positions.
  - `MissileManager.UpdateSimulation()` - simulation thread. Resolves the damage of the missiles waiting to land.
  - `bool MissileManager.HasActive { get; }`

- [ ] **Step 0: add the launch offset constant to ModConfig.**

Add one line to the flight block in `src/MissileDisaster/Game/ModConfig.cs`, just after `MissileStartAltitude`:
```csharp
        public const float MissileLaunchOffset = 1500f; // how far the launch point is offset horizontally from the target (m), which is what gives the parabola its arc
```

- [ ] **Step 1: implement Missile.**

`src/MissileDisaster/Game/Missile.cs`:
```csharp
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// One missile in flight. All of the position interpolation happens on the main thread; the
    /// simulation thread never touches this object.
    /// It is drawn as a simple sphere in Phase 1; a later phase replaces that with a trail and a
    /// model.
    /// </summary>
    public class Missile
    {
        private readonly Vector3 _start;
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
            // The launch point is high above and offset horizontally from the target.
            // Directly overhead, with no offset, the horizontal distance is zero, AdvanceT's
            // zero-distance guard jumps t straight to 1 on the first frame, and the missile
            // lands without ever flying.
            // The horizontal offset gives it a parabolic arc coming in at an angle, which also
            // gives the interception in a later phase a flight to work with. The direction is
            // drawn each time from UnityEngine.Random, which is main-thread safe.
            float ang = Random.Range(0f, 2f * Mathf.PI);
            float ox = Mathf.Cos(ang) * ModConfig.MissileLaunchOffset;
            float oz = Mathf.Sin(ang) * ModConfig.MissileLaunchOffset;
            _start = new Vector3(target.x + ox, target.y + ModConfig.MissileStartAltitude, target.z + oz);
            float dx = target.x - _start.x;
            float dz = target.z - _start.z;
            _groundDistance = Mathf.Sqrt(dx * dx + dz * dz); // = MissileLaunchOffset (>0)
            _t = 0f;

            _go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _go.transform.localScale = new Vector3(12f, 12f, 12f);
            var col = _go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            _go.transform.position = _start;
        }

        /// <summary>
        /// Main thread. Advances the position; returning true means it landed on this frame.
        /// Queuing the damage and destroying the GameObject afterwards is MissileManager's job.
        /// </summary>
        public bool UpdateVisual(float simTimeDelta)
        {
            _t = BallisticMath.AdvanceT(_t, _groundDistance, ModConfig.MissileSpeed, simTimeDelta);
            float x = BallisticMath.Lerp(_start.x, _target.x, _t);
            float z = BallisticMath.Lerp(_start.z, _target.z, _t);
            float y = BallisticMath.Lerp(_start.y, _target.y, _t) + BallisticMath.ArcHeightAt(_t, ModConfig.MissileArcHeight);
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

- [ ] **Step 2: implement MissileManager.**

`src/MissileDisaster/Game/MissileManager.cs`:
```csharp
using System.Collections.Generic;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// Static coordinator for launching and tracking missiles.
    /// The thread boundary matters here:
    ///  - _missiles, the list of missiles in flight, is touched by the main thread alone,
    ///    through Launch, UpdateVisual and Reset. The simulation thread never reads it.
    ///  - Impact damage goes through DisasterHelpers and therefore has to run on the simulation
    ///    thread. So on impact the main thread pushes an ImpactJob - the position plus the
    ///    warhead spec, all plain values - onto _impactQueue, and the simulation thread drains
    ///    and resolves that queue under a lock in UpdateSimulation.
    ///  The upshot is that List&lt;Missile&gt; is never shared across threads: the only thing that
    ///  crosses the boundary is a small, lock-protected queue of values.
    /// </summary>
    public static class MissileManager
    {
        private struct ImpactJob
        {
            public Vector3 Target;
            public WarheadSpec Spec;
        }

        private static readonly List<Missile> _missiles = new List<Missile>();        // main thread only
        private static readonly List<ImpactJob> _impactQueue = new List<ImpactJob>();  // crosses threads, lock-protected
        private static readonly object _impactLock = new object();

        /// <summary>Read from the main thread.</summary>
        public static bool HasActive => _missiles.Count > 0;

        /// <summary>Main thread only.</summary>
        public static void Launch(Vector3 target, WarheadType type)
        {
            _missiles.Add(new Missile(target, type));
            ModConfig.Log("Missile launched at " + target + " (" + type + ")");
        }

        /// <summary>Main thread only. Advances the flight; anything that lands has its damage queued and is then destroyed and removed.</summary>
        public static void UpdateVisual(float simTimeDelta)
        {
            for (int i = _missiles.Count - 1; i >= 0; i--)
            {
                Missile m = _missiles[i];
                bool impacted = m.UpdateVisual(simTimeDelta);
                if (impacted)
                {
                    lock (_impactLock)
                    {
                        _impactQueue.Add(new ImpactJob { Target = m.Target, Spec = m.Spec });
                    }
                    m.DestroyVisual();
                    _missiles.RemoveAt(i);
                }
            }
        }

        /// <summary>Simulation thread only. Drains the impact queue and resolves it through DisasterHelpers.</summary>
        public static void UpdateSimulation()
        {
            List<ImpactJob> jobs = null;
            lock (_impactLock)
            {
                if (_impactQueue.Count > 0)
                {
                    jobs = new List<ImpactJob>(_impactQueue);
                    _impactQueue.Clear();
                }
            }
            if (jobs == null) return;
            for (int i = 0; i < jobs.Count; i++)
            {
                ImpactResolver.Resolve(jobs[i].Target, jobs[i].Spec);
            }
        }

        /// <summary>Main thread only. Destroys every missile in flight and empties the queue.</summary>
        public static void Reset()
        {
            for (int i = 0; i < _missiles.Count; i++) _missiles[i].DestroyVisual();
            _missiles.Clear();
            lock (_impactLock) { _impactQueue.Clear(); }
        }
    }
}
```

- [ ] **Step 3: confirm it compiles.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/MissileDisaster/Game/Missile.cs src/MissileDisaster/Game/MissileManager.cs
git commit -m "feat: Missile and MissileManager (flight on main, impact on sim)"
```

---

## Task 6: MissileTool, the ThreadingExtension and registering the mod - launching in game

**Files:**
- Create: `src/MissileDisaster/Game/UI/MissileTool.cs`
- Create: `src/MissileDisaster/Game/Simulation/MissileThreadingExtension.cs`
- Modify: `src/MissileDisaster/Game/Mod.cs`

**Interfaces:**
- Consumes `MissileManager.Launch`, `UpdateVisual` and `UpdateSimulation`, `ModConfig.ManualTriggerKey` and `WarheadType`
- Produces `MissileTool` (a `ToolBase`) and `MissileThreadingExtension` (a `ThreadingExtensionBase`). `Mod` does not implement any threading interface; CS discovers the `ThreadingExtensionBase` on its own, exactly as in Alien, which keeps it in its own class.

- [ ] **Step 1: implement the click-to-place tool**, following Alien's placement tool.

`src/MissileDisaster/Game/UI/MissileTool.cs`:
```csharp
using ColossalFramework;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.UI
{
    /// <summary>
    /// The tool for clicking where the missile should land, with the same feel as the vanilla
    /// disasters: aim, then left click to confirm.
    /// A ToolBase lifecycle runs on the main/render thread, so calling Launch directly from here
    /// is safe.
    /// </summary>
    public class MissileTool : ToolBase
    {
        // Phase 1 is fixed to the conventional warhead; a later phase takes it from the selection UI.
        public WarheadType SelectedWarhead = WarheadType.Conventional;

        private Vector3 m_cachedPosition;
        private bool m_placementValid;
        private Ray m_mouseRay;
        private float m_mouseRayLength;
        private bool m_mouseRayValid;

        protected override void OnToolLateUpdate()
        {
            m_mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            m_mouseRayLength = Camera.main.farClipPlane;
            m_mouseRayValid = !m_toolController.IsInsideUI && Cursor.visible;
        }

        public override void SimulationStep()
        {
            if (m_mouseRayValid)
            {
                RaycastInput input = new RaycastInput(m_mouseRay, m_mouseRayLength);
                RaycastOutput output;
                if (RayCast(input, out output))
                {
                    output.m_hitPos.y = Singleton<TerrainManager>.instance.SampleRawHeightSmoothWithWater(output.m_hitPos, false, 0f);
                    m_cachedPosition = output.m_hitPos;
                    m_placementValid = true;
                    return;
                }
            }
            m_placementValid = false;
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            if (!m_placementValid) return;
            Color color = new Color(1f, 0.4f, 0.1f, 0.6f);
            Singleton<RenderManager>.instance.OverlayEffect.DrawCircle(
                cameraInfo, color, m_cachedPosition, 100f,
                m_cachedPosition.y - 100f, m_cachedPosition.y + 100f, false, true);
        }

        protected override void OnToolGUI(Event e)
        {
            if (m_toolController.IsInsideUI) return;
            if (e.type != EventType.MouseDown || e.button != 0 || !m_placementValid) return;
            try
            {
                MissileManager.Launch(m_cachedPosition, SelectedWarhead);
            }
            catch (System.Exception ex)
            {
                ModConfig.LogError("MissileTool.OnToolGUI error: " + ex);
            }
            finally
            {
                ToolsModifierControl.SetTool<DefaultTool>();
            }
        }
    }
}
```

- [ ] **Step 2: implement the ThreadingExtension** - the hotkey opens the tool, the flight runs on the main thread and the impact on the simulation thread.

`src/MissileDisaster/Game/Simulation/MissileThreadingExtension.cs`:
```csharp
using ICities;
using UnityEngine;

namespace MissileDisaster.Game.Simulation
{
    /// <summary>
    /// Drives triggering, flight and impact.
    /// OnUpdate, on the main thread, opens the tool on the hotkey and advances the flight by
    /// simulationTimeDelta, so it follows the game speed and freezes while paused.
    /// OnAfterSimulationTick, on the simulation thread, does nothing but resolve the impact
    /// damage through DisasterHelpers.
    /// </summary>
    public class MissileThreadingExtension : ThreadingExtensionBase
    {
        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            try
            {
                if (Input.GetKeyDown(ModConfig.ManualTriggerKey))
                {
                    ToolsModifierControl.SetTool<MissileDisaster.Game.UI.MissileTool>();
                }

                bool paused = SimulationManager.instance.SimulationPaused;
                if (!paused)
                {
                    MissileManager.UpdateVisual(simulationTimeDelta);
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("OnUpdate error: " + e);
            }
        }

        public override void OnAfterSimulationTick()
        {
            try
            {
                MissileManager.UpdateSimulation();
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("OnAfterSimulationTick error: " + e);
            }
        }
    }
}
```

- [ ] **Step 3: leave Mod.cs alone** - its description already mentions the hotkey. Just check it.

`src/MissileDisaster/Game/Mod.cs` needs no change from Task 1. CS finds and uses the `ThreadingExtensionBase` subclass (`MissileThreadingExtension`) and the `ToolBase` subclass (`MissileTool`) in the same assembly on its own.

- [ ] **Step 4: build and deploy.**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: the build succeeds and the DLL is deployed to `...\Addons\Mods\MissileDisaster`.

- [ ] **Step 5: check it works in game, by hand.**

Steps:
1. Start Cities: Skylines and enable "Missile Disaster" under Content Manager -> Mods.
2. Load any city.
3. Press `F9`; an orange impact circle follows the cursor.
4. Click the ground; the missile - a sphere - falls along a parabola from above the impact point, leaving a crater and destroying the buildings around it.
5. Confirm the flight speeds up at 2x and 3x, and stops while paused.
6. Confirm the log contains `[MissileDisaster] Missile launched ...` followed by `Impact resolved ...`.

Expected: a crater and destruction where you clicked, following the game speed and freezing while paused.

- [ ] **Step 6: Commit**

```bash
git add src/MissileDisaster/Game/UI/MissileTool.cs src/MissileDisaster/Game/Simulation/MissileThreadingExtension.cs src/MissileDisaster/Game/Mod.cs
git commit -m "feat: the click-to-place tool and its hotkey (the conventional warhead MVP)"
```

---

## Phase 1 definition of done

- `dotnet test` passes every Core test, for BallisticMath and WarheadSpec.
- `build.ps1` builds and deploys successfully.
- In game, the hotkey then a click produces the parabolic flight and the impact crater and destruction, following the game speed and freezing while paused.

## The phases after this (planned separately)

- Phase 2: the warhead types - the cluster split, the white phosphorus fires and the thermobaric's wide destruction - and differentiating WarheadSpec.
- Phase 3: the nuclear presets (NukeScaling and NukePresets), the radioactivity (RadiationManager, RadiationGrid and RadDecontaminationAI) and the Geiger sound.
- Phase 4: the 1 to 200 barrage (BarrageScheduler plus spreading the load) and the single-or-mixed toggle.
- Phase 5: the interceptor sites (InterceptResolver, MissileDefenseAI and CustomBuildingFactory).
