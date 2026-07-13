# ミサイル災害 Mod — Phase 1 実装計画（基盤＋通常弾頭 1 発）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** クリックした地点へ通常弾頭ミサイルを 1 発、放物線で飛翔させ、着弾でクレーター＋範囲破壊を起こす最小構成を作る。

**Architecture:** 新規・独立 Mod `MissileDisaster`。Alien Invasion の「メインスレッド＝Transform 補間（`simulationTimeDelta` 駆動）／シミュレーションスレッド＝`DisasterHelpers` で着弾ダメージ」という二段構えを踏襲。純粋数学は `Core`（UnityEngine 非依存・float のみ）に置き xUnit でテスト。ゲーム型依存部は実機確認。

**Tech Stack:** C# (LangVersion 7.3)、mod 本体は .NET Framework 3.5（Cities: Skylines / Unity 5.6）、テストは net8.0 + xUnit。参照 DLL は CS インストール配下の `Cities_Data\Managed`。

## Global Constraints

- Mod 本体ターゲット: `TargetFrameworkVersion=v3.5`、`LangVersion=7.3`。UnityEngine 4.5+ API 禁止（`IReadOnlyList` 等不可、`string.IndexOf(string, StringComparison)` は可）。
- `Core/**/*.cs` は UnityEngine を参照しない（float/組込型のみ）。テストプロジェクトが net8.0 で同ソースをリンクするため。
- CS Managed DLL パス: `C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed`（`ManagedDLLPath` プロパティで一元化）。
- スレッド境界厳守: GameObject/Transform/Effects/状態書込み＝メインスレッド、`DisasterHelpers`/汚染書込み＝シミュレーションスレッド。`ThreadingExtensionBase.OnAfterSimulationTick` から Transform を触らない。
- テスト構成は既存 2 Mod と同一（テスト csproj が `..\..\src\MissileDisaster\Core\**\*.cs` を `LinkBase="Core"` でリンク）。
- ログ接頭辞: `[MissileDisaster] `。
- 名前空間ルート: `MissileDisaster`（`MissileDisaster.Core` / `MissileDisaster.Game` / `MissileDisaster.Game.Simulation` / `MissileDisaster.Game.UI`）。
- Mod デプロイ先: `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\MissileDisaster`。

---

## ファイル構成（Phase 1 で作成するファイル）

| ファイル | 責務 |
|---|---|
| `MissileDisaster.sln` | ソリューション（mod + テスト） |
| `src/MissileDisaster/MissileDisaster.csproj` | mod 本体プロジェクト（v3.5・CS DLL 参照） |
| `src/MissileDisaster/Properties/AssemblyInfo.cs` | アセンブリ情報 |
| `src/MissileDisaster/Core/BallisticMath.cs` | 放物線・補間・進行の純粋数学 |
| `src/MissileDisaster/Core/WarheadType.cs` | 弾頭種別 enum（Phase 1 は Conventional のみ使用） |
| `src/MissileDisaster/Core/WarheadSpec.cs` | 弾頭別パラメータ表（Phase 1 は Conventional の係数） |
| `src/MissileDisaster/Game/ModConfig.cs` | 定数・ログ |
| `src/MissileDisaster/Game/Missile.cs` | 1 発の状態＋飛翔補間＋着弾 |
| `src/MissileDisaster/Game/MissileManager.cs` | 発射・追跡（メイン＝飛翔／sim＝着弾） |
| `src/MissileDisaster/Game/ImpactResolver.cs` | クレーター＋範囲破壊（sim スレッド） |
| `src/MissileDisaster/Game/UI/MissileTool.cs` | 着弾点クリック指定ツール |
| `src/MissileDisaster/Game/Simulation/MissileThreadingExtension.cs` | 発動・進行・着弾の駆動 |
| `src/MissileDisaster/Game/Mod.cs` | IUserMod エントリ |
| `tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj` | テストプロジェクト |
| `tests/MissileDisaster.Core.Tests/BallisticMathTests.cs` | BallisticMath のテスト |
| `build.ps1` | ビルド＆デプロイ |

---

## Task 1: プロジェクト基盤（コンパイルが通る空 Mod ＋テスト土台）

**Files:**
- Create: `src/MissileDisaster/MissileDisaster.csproj`
- Create: `src/MissileDisaster/Properties/AssemblyInfo.cs`
- Create: `src/MissileDisaster/Game/ModConfig.cs`
- Create: `src/MissileDisaster/Game/Mod.cs`
- Create: `tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj`
- Create: `MissileDisaster.sln`
- Create: `build.ps1`

**Interfaces:**
- Produces: `MissileDisaster.Game.ModConfig.Log(string)` / `LogError(string)` / `LogPrefix`。IUserMod `MissileDisaster.Game.Mod`。

- [ ] **Step 1: mod 本体 csproj を作成**

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

- [ ] **Step 2: AssemblyInfo を作成**

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

- [ ] **Step 3: ModConfig を作成（Phase 1 の定数）**

`src/MissileDisaster/Game/ModConfig.cs`:
```csharp
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>Mod 全体の定数と共通ログ。</summary>
    public static class ModConfig
    {
        public const string LogPrefix = "[MissileDisaster] ";

        // 手動発射ツールを起動するキー（Alien の F7 と衝突しないよう F9）。
        public const KeyCode ManualTriggerKey = KeyCode.F9;

        // 飛翔（メインスレッドで simulationTimeDelta 駆動）。
        public const float MissileSpeed = 900f;   // 地表投影距離に対する m/秒 相当
        public const float MissileArcHeight = 700f; // 放物線の頂点高さ（m）
        public const float MissileStartAltitude = 1200f; // 発射点の高さ

        // 着弾（通常弾頭・sim スレッドで DisasterHelpers を呼ぶ）。
        public const float SinkholeRadius = 60f;
        public const float SinkholeDepth = 16f;
        public const float DestructionRadius = 120f;

        public static void Log(string msg) { Debug.Log(LogPrefix + msg); }
        public static void LogError(string msg) { Debug.LogError(LogPrefix + msg); }
    }
}
```

- [ ] **Step 4: IUserMod エントリを作成**

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

- [ ] **Step 5: テストプロジェクトを作成**

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
    <!-- Core の実ソースを直接コンパイルしてテスト（別ビルド不要） -->
    <Compile Include="..\..\src\MissileDisaster\Core\**\*.cs" LinkBase="Core" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6: ソリューションを作成しプロジェクトを追加**

Run:
```bash
cd "C:/Users/omone/Desktop/G/ミサイル災害プロジェクト"
dotnet new sln -n MissileDisaster
dotnet sln add tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj
```
Expected: sln 作成、テストプロジェクト追加成功（mod 本体 v3.5 は `dotnet sln add` すると SDK 差異の警告が出るため sln には追加せず、`build.ps1`（msbuild）でビルドする）。

- [ ] **Step 7: build.ps1 を作成**

`build.ps1`:
```powershell
$ErrorActionPreference = "Stop"
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild が見つかりません" }

& $msbuild "src\MissileDisaster\MissileDisaster.csproj" /t:Restore,Build /p:Configuration=Release /v:minimal
if ($LASTEXITCODE -ne 0) { throw "ビルド失敗" }

$dll = "src\MissileDisaster\bin\Release\MissileDisaster.dll"
$modDir = Join-Path $env:LOCALAPPDATA "Colossal Order\Cities_Skylines\Addons\Mods\MissileDisaster"
New-Item -ItemType Directory -Force -Path $modDir | Out-Null
Copy-Item $dll $modDir -Force
Write-Host "配置完了: $modDir"
```

- [ ] **Step 8: mod 本体がコンパイルできることを確認**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: `MissileDisaster -> ...\bin\Release\MissileDisaster.dll` と「配置完了」。エラー無し。

- [ ] **Step 9: テストプロジェクトがビルド/実行できることを確認（テスト 0 件）**

Run: `dotnet test tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj --nologo`
Expected: ビルド成功、合計 0 件（Core にまだテスト対象が無い）。

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "chore: MissileDisaster プロジェクト基盤 (空Mod+テスト土台)"
```

---

## Task 2: BallisticMath（Core・TDD）

**Files:**
- Create: `src/MissileDisaster/Core/BallisticMath.cs`
- Test: `tests/MissileDisaster.Core.Tests/BallisticMathTests.cs`

**Interfaces:**
- Produces:
  - `float BallisticMath.Clamp01(float t)`
  - `float BallisticMath.Lerp(float a, float b, float t)` — t を 0..1 にクランプして補間
  - `float BallisticMath.ArcHeightAt(float t, float arcHeight)` — 放物線の高さ成分。t=0,1 で 0、t=0.5 で arcHeight
  - `float BallisticMath.AdvanceT(float t, float groundDistance, float speed, float dt)` — 進行度 t を速度・経過時間で進める（距離 0 でも例外を出さない）

- [ ] **Step 1: 失敗するテストを書く**

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
    [InlineData(0f, 100f, 2f, 100f)]   // クランプされる
    public void Lerp_interpolates_and_clamps(float a, float b, float t, float expected)
    {
        Assert.Equal(expected, BallisticMath.Lerp(a, b, t), 4);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(1f, 0f)]
    [InlineData(0.5f, 700f)]  // 頂点で arcHeight
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
        // 距離1000, 速度500, dt=1 → +0.5
        Assert.Equal(0.5f, BallisticMath.AdvanceT(0f, 1000f, 500f, 1f), 4);
    }

    [Fact]
    public void AdvanceT_handles_zero_distance_without_divide_by_zero()
    {
        float result = BallisticMath.AdvanceT(0.4f, 0f, 500f, 1f);
        Assert.True(result >= 1f); // 距離0なら即着弾扱い(=1到達)
    }
}
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `dotnet test tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj --nologo`
Expected: コンパイルエラー（`BallisticMath` 未定義）で FAIL。

- [ ] **Step 3: 最小実装を書く**

`src/MissileDisaster/Core/BallisticMath.cs`:
```csharp
namespace MissileDisaster.Core
{
    /// <summary>
    /// ミサイル飛翔（放物線）の純粋数学。UnityEngine 非依存（float のみ）で単体テスト可能。
    /// ゲーム側は x/z を Lerp、y を Lerp + ArcHeightAt で合成して Vector3 を作る。
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

        /// <summary>放物線の高さ成分。t=0,1 で 0、t=0.5 で arcHeight。</summary>
        public static float ArcHeightAt(float t, float arcHeight)
        {
            t = Clamp01(t);
            return arcHeight * 4f * t * (1f - t);
        }

        /// <summary>進行度 t を「地表投影距離 groundDistance を speed で進む」ぶんだけ加算。</summary>
        public static float AdvanceT(float t, float groundDistance, float speed, float dt)
        {
            if (groundDistance <= 0.0001f) return 1f; // 距離0は即着弾扱い
            return t + (speed * dt) / groundDistance;
        }
    }
}
```

- [ ] **Step 4: テストが通ることを確認**

Run: `dotnet test tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj --nologo`
Expected: PASS（全 15 ケース合格）。

- [ ] **Step 5: Commit**

```bash
git add src/MissileDisaster/Core/BallisticMath.cs tests/MissileDisaster.Core.Tests/BallisticMathTests.cs
git commit -m "feat(core): 放物線飛翔の純粋数学 BallisticMath (TDD)"
```

---

## Task 3: WarheadType / WarheadSpec（Core・TDD、Phase 1 は Conventional）

**Files:**
- Create: `src/MissileDisaster/Core/WarheadType.cs`
- Create: `src/MissileDisaster/Core/WarheadSpec.cs`
- Test: `tests/MissileDisaster.Core.Tests/WarheadSpecTests.cs`

**Interfaces:**
- Produces:
  - `enum MissileDisaster.Core.WarheadType { Conventional, Cluster, WhitePhosphorus, Thermobaric, Nuclear }`
  - `struct WarheadSpec { float CraterRadius; float CraterDepth; float DestructionRadius; bool Contaminates; }`
  - `WarheadSpec WarheadSpec.For(WarheadType type)` — Phase 1 は Conventional のみ実値、他は Conventional と同値の暫定（後続 Phase で差別化）

- [ ] **Step 1: 失敗するテストを書く**

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

- [ ] **Step 2: テストが失敗することを確認**

Run: `dotnet test tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj --nologo`
Expected: コンパイルエラー（`WarheadType`/`WarheadSpec` 未定義）で FAIL。

- [ ] **Step 3: 最小実装を書く**

`src/MissileDisaster/Core/WarheadType.cs`:
```csharp
namespace MissileDisaster.Core
{
    /// <summary>弾頭種別。Phase 1 は Conventional のみ挙動を実装する。</summary>
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
    /// 弾頭ごとの着弾パラメータ（UnityEngine 非依存の数値表）。
    /// Phase 1 は Conventional のみ実値。他種別は暫定で Conventional と同値を返し、
    /// 後続 Phase（弾頭分岐・核）で差別化する。
    /// </summary>
    public struct WarheadSpec
    {
        public float CraterRadius;
        public float CraterDepth;
        public float DestructionRadius;
        public bool Contaminates;

        public static WarheadSpec For(WarheadType type)
        {
            // Phase 1: すべて通常弾頭相当（後続 Phase で type ごとに分岐）。
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

- [ ] **Step 4: テストが通ることを確認**

Run: `dotnet test tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj --nologo`
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add src/MissileDisaster/Core/WarheadType.cs src/MissileDisaster/Core/WarheadSpec.cs tests/MissileDisaster.Core.Tests/WarheadSpecTests.cs
git commit -m "feat(core): WarheadType/WarheadSpec (Phase1=Conventional)"
```

---

## Task 4: ImpactResolver（sim スレッドのクレーター＋破壊）

**Files:**
- Create: `src/MissileDisaster/Game/ImpactResolver.cs`

**Interfaces:**
- Consumes: `MissileDisaster.Core.WarheadSpec`
- Produces: `void ImpactResolver.Resolve(UnityEngine.Vector3 target, WarheadSpec spec)` — シミュレーションスレッド専用。`DisasterHelpers.MakeCrater` と `DisasterHelpers.DestroyStuff` を 1 回ずつ呼ぶ。

- [ ] **Step 1: 実装を書く（Alien の ResolveBombardDamage を通常弾頭向けに簡約）**

`src/MissileDisaster/Game/ImpactResolver.cs`:
```csharp
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// 着弾ダメージ解決。DisasterHelpers はシミュレーションスレッドから呼ぶ契約のため、
    /// このメソッドは MissileManager.UpdateSimulation（sim スレッド）からのみ呼ぶこと。
    /// </summary>
    public static class ImpactResolver
    {
        public static void Resolve(Vector3 target, WarheadSpec spec)
        {
            // クレーター（バニラ SinkholeAI と同じ MakeCrater 呼び出し。1 回だけ）。
            DisasterHelpers.MakeCrater(new Vector2(target.x, target.z), spec.CraterRadius, spec.CraterDepth, false);

            // 範囲破壊。preRadius=totalRadius にする（0 だと何も壊れない既知の罠を回避）。
            int seed = (int)SimulationManager.instance.m_randomizer.Int32(1000000u);
            float r = spec.DestructionRadius;
            DisasterHelpers.DestroyStuff(seed, null, target, r, r, 0f, r * 0.5f, r, r * 0.3f, r * 0.6f);

            ModConfig.Log("Impact resolved (crater+destruction) at " + target);
        }
    }
}
```

- [ ] **Step 2: コンパイル確認**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: ビルド成功（`DisasterHelpers.MakeCrater`/`DestroyStuff` が解決される）。

- [ ] **Step 3: Commit**

```bash
git add src/MissileDisaster/Game/ImpactResolver.cs
git commit -m "feat: 着弾クレーター+範囲破壊 ImpactResolver (simスレッド)"
```

---

## Task 5: Missile / MissileManager（飛翔＝メイン、着弾＝sim）

**Files:**
- Modify: `src/MissileDisaster/Game/ModConfig.cs`（`MissileLaunchOffset` 定数を追加）
- Create: `src/MissileDisaster/Game/Missile.cs`
- Create: `src/MissileDisaster/Game/MissileManager.cs`

**Interfaces:**
- Consumes: `BallisticMath`、`WarheadType`、`WarheadSpec.For`、`ImpactResolver.Resolve`、`ModConfig`
- Produces:
  - `MissileManager.Launch(Vector3 target, WarheadType type)` — メインスレッド。発射点を target から水平 `MissileLaunchOffset`・高さ `MissileStartAltitude` オフセットした位置に生成。
  - `MissileManager.UpdateVisual(float simTimeDelta)` — メインスレッド。飛翔体の位置を進める。
  - `MissileManager.UpdateSimulation()` — sim スレッド。着弾保留中のミサイルのダメージを解決。
  - `bool MissileManager.HasActive { get; }`

- [ ] **Step 0: ModConfig に発射オフセット定数を追加**

`src/MissileDisaster/Game/ModConfig.cs` の飛翔ブロック（`MissileStartAltitude` の行の直後）に 1 行追加する:
```csharp
        public const float MissileLaunchOffset = 1500f; // 発射点をターゲットから水平にずらす距離(m)。放物線の弧を成立させる
```

- [ ] **Step 1: Missile を実装**

`src/MissileDisaster/Game/Missile.cs`:
```csharp
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// 飛翔中の 1 発。位置補間はすべてメインスレッドで行う（sim スレッドはこのオブジェクトに触れない）。
    /// 可視表現は Phase 1 では簡易プリミティブ（球）。後続 Phase でトレイル/モデルに差し替え。
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
            // 発射点はターゲットから水平にオフセットした高所にする。
            // 真上（オフセット0）だと地表投影距離が0になり、AdvanceT のゼロ距離ガードで
            // t が初フレームに即1へ跳ね、ミサイルが飛ばず即着弾してしまう。
            // 水平オフセットを与えることで斜めに飛来する放物線の弧になり、迎撃(後続Phase)の
            // 飛行フェーズも成立する。方向はメインスレッドセーフな UnityEngine.Random で毎回ランダム。
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
        /// メインスレッド。位置を進める。戻り値 true = このフレームで着弾（t&gt;=1 到達）。
        /// 着弾後の処理（ダメージの enqueue と GameObject 破棄）は MissileManager 側が行う。
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

        /// <summary>メインスレッド。飛翔体 GameObject を破棄する。</summary>
        public void DestroyVisual()
        {
            if (_go != null) Object.Destroy(_go);
        }
    }
}
```

- [ ] **Step 2: MissileManager を実装**

`src/MissileDisaster/Game/MissileManager.cs`:
```csharp
using System.Collections.Generic;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// 発射・追跡の静的コーディネータ。
    /// スレッド境界（重要）:
    ///  - _missiles（飛翔中リスト）はメインスレッドのみが触る（Launch/UpdateVisual/Reset）。
    ///    sim スレッドはこのリストに一切アクセスしない。
    ///  - 着弾ダメージは DisasterHelpers を使うため sim スレッドで実行が必要。そこでメインスレッドは
    ///    着弾時に ImpactJob（座標＋弾頭スペックの値）を _impactQueue に積み、sim スレッド
    ///    （UpdateSimulation）はロック下でキューを排出して解決する。
    ///  これにより List&lt;Missile&gt; をスレッド跨ぎで共有せず、境界はロック保護した小さな値キューのみになる。
    /// </summary>
    public static class MissileManager
    {
        private struct ImpactJob
        {
            public Vector3 Target;
            public WarheadSpec Spec;
        }

        private static readonly List<Missile> _missiles = new List<Missile>();        // メインスレッド専用
        private static readonly List<ImpactJob> _impactQueue = new List<ImpactJob>();  // 受け渡し(ロック保護)
        private static readonly object _impactLock = new object();

        /// <summary>メインスレッドから読む。</summary>
        public static bool HasActive => _missiles.Count > 0;

        /// <summary>メインスレッド専用。</summary>
        public static void Launch(Vector3 target, WarheadType type)
        {
            _missiles.Add(new Missile(target, type));
            ModConfig.Log("Missile launched at " + target + " (" + type + ")");
        }

        /// <summary>メインスレッド専用。飛翔を進め、着弾したものはダメージを enqueue して破棄・除去。</summary>
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

        /// <summary>シミュレーションスレッド専用。着弾キューを排出し DisasterHelpers で解決する。</summary>
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

        /// <summary>メインスレッド専用。全飛翔体を破棄し、キューも空にする。</summary>
        public static void Reset()
        {
            for (int i = 0; i < _missiles.Count; i++) _missiles[i].DestroyVisual();
            _missiles.Clear();
            lock (_impactLock) { _impactQueue.Clear(); }
        }
    }
}
```

- [ ] **Step 3: コンパイル確認**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: ビルド成功。

- [ ] **Step 4: Commit**

```bash
git add src/MissileDisaster/Game/Missile.cs src/MissileDisaster/Game/MissileManager.cs
git commit -m "feat: Missile/MissileManager (飛翔=メイン, 着弾=sim)"
```

---

## Task 6: MissileTool ＋ ThreadingExtension ＋ Mod 登録（実機で発射）

**Files:**
- Create: `src/MissileDisaster/Game/UI/MissileTool.cs`
- Create: `src/MissileDisaster/Game/Simulation/MissileThreadingExtension.cs`
- Modify: `src/MissileDisaster/Game/Mod.cs`

**Interfaces:**
- Consumes: `MissileManager.Launch/UpdateVisual/UpdateSimulation`、`ModConfig.ManualTriggerKey`、`WarheadType`
- Produces: `MissileTool`（`ToolBase`）、`MissileThreadingExtension`（`ThreadingExtensionBase`）。`Mod` は `IUserModThreading` ではなく、CS の自動検出で `ThreadingExtensionBase` を拾わせる（Alien と同じく別クラスで実装）。

- [ ] **Step 1: 着弾点クリックツールを実装（Alien の配置ツールを踏襲）**

`src/MissileDisaster/Game/UI/MissileTool.cs`:
```csharp
using ColossalFramework;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.UI
{
    /// <summary>
    /// 着弾点をクリック指定するツール。バニラ災害と同じ「狙って左クリックで確定」。
    /// ToolBase のライフサイクルはメイン/レンダースレッドなので Launch を直接呼んでよい。
    /// </summary>
    public class MissileTool : ToolBase
    {
        // Phase 1 は通常弾頭固定。後続 Phase で選択 UI から差し替える。
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

- [ ] **Step 2: ThreadingExtension を実装（F9 でツール起動、飛翔=メイン、着弾=sim）**

`src/MissileDisaster/Game/Simulation/MissileThreadingExtension.cs`:
```csharp
using ICities;
using UnityEngine;

namespace MissileDisaster.Game.Simulation
{
    /// <summary>
    /// 発動・進行・着弾を駆動する。
    /// OnUpdate（メイン）: F9 でツール起動、飛翔を simulationTimeDelta で進める（速度連動・一時停止で凍結）。
    /// OnAfterSimulationTick（sim）: 着弾ダメージ解決のみ（DisasterHelpers）。
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

- [ ] **Step 3: Mod.cs はそのまま（説明文で F9 を案内済み）確認のみ**

`src/MissileDisaster/Game/Mod.cs` は Task 1 の内容で変更不要。CS は同一アセンブリ内の `ThreadingExtensionBase` 派生（`MissileThreadingExtension`）と `ToolBase` 派生（`MissileTool`）を自動的に検出・利用する。

- [ ] **Step 4: ビルド＆デプロイ**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: ビルド成功、`...\Addons\Mods\MissileDisaster` に DLL 配置。

- [ ] **Step 5: 実機で動作確認（手動）**

手順:
1. Cities: Skylines 起動 → Content Manager → Mods で「Missile Disaster」を有効化。
2. 任意の街をロード。
3. `F9` を押す → カーソルにオレンジの着弾円が出る。
4. 地面をクリック → 着弾点の真上からミサイル（球）が放物線を描いて落下 → 着弾でクレーター＋周囲の建物破壊。
5. ゲーム速度 2x/3x で飛翔が速くなること、一時停止で飛翔が止まることを確認。
6. ログ（`%LOCALAPPDATA%\...\Cities_Skylines\output_log.txt` 等）に `[MissileDisaster] Missile launched ...` → `Impact resolved ...` が出ることを確認。

Expected: クリック地点にクレーターと破壊が発生。速度連動・一時停止凍結が効く。

- [ ] **Step 6: Commit**

```bash
git add src/MissileDisaster/Game/UI/MissileTool.cs src/MissileDisaster/Game/Simulation/MissileThreadingExtension.cs src/MissileDisaster/Game/Mod.cs
git commit -m "feat: 着弾点クリックツール+F9駆動 (通常弾頭MVP完成)"
```

---

## Phase 1 完了の定義（Definition of Done）

- `dotnet test` が全 Core テスト合格（BallisticMath / WarheadSpec）。
- `build.ps1` がビルド＆デプロイ成功。
- 実機で「F9 → クリック → 放物線飛翔 → 着弾クレーター＋破壊」が動作し、ゲーム速度連動・一時停止凍結が効く。

## 次フェーズ予告（別計画で作成）

- Phase 2: 弾頭分岐（クラスター分裂／白リン延焼／気化爆弾の広域破壊）＋ WarheadSpec 差別化。
- Phase 3: 核プリセット（NukeScaling/NukePresets）＋放射能（RadiationManager/RadiationGrid/RadDecontaminationAI）＋ガイガー音。
- Phase 4: バラージ 1〜200（BarrageScheduler＋負荷分散）＋単一/ミックストグル。
- Phase 5: 迎撃施設（InterceptResolver＋MissileDefenseAI＋CustomBuildingFactory）。
