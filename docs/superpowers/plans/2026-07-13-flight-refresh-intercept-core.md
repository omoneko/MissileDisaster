# 飛来刷新＋迎撃Coreロジック 実装計画（モデル非依存・先行）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 飛来ミサイルを固定方位・高高度 apex からの降下枝のみに刷新（モデルは球のまま）し、3層迎撃の純粋判定ロジック（高度帯・射程・確率）をテスト付きで用意する。建物・実モデルは別プラン（model.blend 完成後）。

**Architecture:** Phase 1 の上に積む。`Core` に UnityEngine 非依存の純粋関数（`LaunchGeometry`/`InterceptorTiers`/`InterceptDecision`）を追加し xUnit でテスト。`Missile` は apex→着弾の降下補間へ変更（既存の main/sim スレッド境界は不変）。

**Tech Stack:** C# 7.3 / .NET 3.5（mod 本体）、net8.0 + xUnit（テスト）。CS DLL 参照は既存。

## Global Constraints

- Mod 本体 `TargetFrameworkVersion=v3.5`、`LangVersion=7.3`。.NET 4.5+ API 禁止。
- `Core/**/*.cs` は UnityEngine 非依存（`Mathf` 不可 → `System.Math` を使い float へキャスト）。float/組込型のみ。
- 名前空間 `MissileDisaster.Core` / `MissileDisaster.Game`。ログ接頭辞 `[MissileDisaster] `。
- テスト構成: テスト csproj が `..\..\src\MissileDisaster\Core\**\*.cs` を自動リンク（新規 Core ファイルは追記不要で拾われる）。
- 方位規約: **0°=+Z(北)、時計回りに増加**（90°=+X)。
- スレッド境界不変: 飛翔/GameObject はメイン、着弾ダメージは sim。本プランは Missile の補間内容のみ変更し境界は変えない。
- ビルド: `powershell -ExecutionPolicy Bypass -File build.ps1`。テスト: `dotnet test tests/MissileDisaster.Core.Tests/MissileDisaster.Core.Tests.csproj --nologo`。
- コミット時は該当ファイルのみ `git add`（未追跡の .blend/.mp3 は含めない）。ローカル Codex コミットレビューフックが P1 でブロックし得る。

---

## ファイル構成（本プランで作成・変更）

| File | Kind | Responsibility |
|---|---|---|
| `src/MissileDisaster/Core/LaunchGeometry.cs` | 新規 | 固定方位→(X,Z)オフセット（apex 水平位置） |
| `tests/.../LaunchGeometryTests.cs` | 新規 | 上のテスト |
| `src/MissileDisaster/Core/InterceptorTier.cs` | 新規 | 迎撃層データ（ARROW/SAM/PAC の帯・射程・確率・CD） |
| `tests/.../InterceptorTierTests.cs` | 新規 | 帯の連続性・順序テスト |
| `src/MissileDisaster/Core/InterceptDecision.cs` | 新規 | 交戦圏判定＋確率（乱数注入） |
| `tests/.../InterceptDecisionTests.cs` | 新規 | 上のテスト |
| `src/MissileDisaster/Game/ModConfig.cs` | 変更 | 飛翔定数を apex 方式へ差し替え |
| `src/MissileDisaster/Game/Missile.cs` | 変更 | apex→着弾の降下補間へ |

---

## Task 1: LaunchGeometry（Core・TDD）

**Files:**
- Create: `src/MissileDisaster/Core/LaunchGeometry.cs`
- Test: `tests/MissileDisaster.Core.Tests/LaunchGeometryTests.cs`

**Interfaces:**
- Produces:
  - `struct MissileDisaster.Core.Offset2 { float X; float Z; }`
  - `Offset2 LaunchGeometry.BearingOffset(float bearingDeg, float horizontalDistance)` — 0°=+Z, 90°=+X, 時計回り。

- [ ] **Step 1: write the failing tests.**

`tests/MissileDisaster.Core.Tests/LaunchGeometryTests.cs`:
```csharp
using MissileDisaster.Core;
using Xunit;

public class LaunchGeometryTests
{
    [Theory]
    [InlineData(0f, 100f, 0f, 100f)]     // 北=+Z
    [InlineData(90f, 100f, 100f, 0f)]    // 東=+X
    [InlineData(180f, 100f, 0f, -100f)]  // 南=-Z
    [InlineData(270f, 100f, -100f, 0f)]  // 西=-X
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
Expected: コンパイルエラー（`Offset2`/`LaunchGeometry` 未定義）で FAIL。

- [ ] **Step 3: implement.**

`src/MissileDisaster/Core/LaunchGeometry.cs`:
```csharp
namespace MissileDisaster.Core
{
    /// <summary>水平方位オフセット(X,Z)。UnityEngine 非依存。</summary>
    public struct Offset2
    {
        public float X;
        public float Z;
    }

    /// <summary>
    /// 固定方位から飛来する弾道の apex(頂点)水平位置を算出する純粋ロジック。
    /// 方位規約: 0°=+Z(北), 90°=+X(東), 時計回りに増加。UnityEngine 非依存。
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
Expected: PASS（新規6ケース＋既存18ケース）。

- [ ] **Step 5: Commit**

```bash
git add src/MissileDisaster/Core/LaunchGeometry.cs tests/MissileDisaster.Core.Tests/LaunchGeometryTests.cs
git commit -m "feat(core): 固定方位→apex水平オフセット LaunchGeometry (TDD)"
```

---

## Task 2: InterceptorTier（Core・TDD）

**Files:**
- Create: `src/MissileDisaster/Core/InterceptorTier.cs`
- Test: `tests/MissileDisaster.Core.Tests/InterceptorTierTests.cs`

**Interfaces:**
- Produces:
  - `enum MissileDisaster.Core.InterceptorKind { Arrow, Sam, Pac }`
  - `struct InterceptorTier { InterceptorKind Kind; float AltitudeMin; float AltitudeMax; float HorizontalRange; float InterceptChance; float CooldownSeconds; }`
  - `static class InterceptorTiers` with fields `Arrow`, `Sam`, `Pac` and `InterceptorTier[] Ordered`（高い帯から順）。

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

    /// <summary>迎撃層の担当高度帯・水平射程・迎撃確率・クールダウン。UnityEngine 非依存。</summary>
    public struct InterceptorTier
    {
        public InterceptorKind Kind;
        public float AltitudeMin;
        public float AltitudeMax;
        public float HorizontalRange;
        public float InterceptChance; // 0..1
        public float CooldownSeconds;
    }

    /// <summary>ARROW(超高高度)→SAM(高高度)→PAC(終端)の3層。帯は地面から連続。数値は暫定(実機調整)。</summary>
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

        /// <summary>迎撃試行順(高い帯から)。</summary>
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
git commit -m "feat(core): 迎撃3層データ InterceptorTiers (ARROW/SAM/PAC, TDD)"
```

---

## Task 3: InterceptDecision（Core・TDD）

**Files:**
- Create: `src/MissileDisaster/Core/InterceptDecision.cs`
- Test: `tests/MissileDisaster.Core.Tests/InterceptDecisionTests.cs`

**Interfaces:**
- Consumes: `InterceptorTier`
- Produces:
  - `bool InterceptDecision.InEngagementZone(float missileAltitude, float horizontalDistance, InterceptorTier tier)`
  - `bool InterceptDecision.ShouldIntercept(float missileAltitude, float horizontalDistance, InterceptorTier tier, float roll)` — roll(0..1) を注入。

- [ ] **Step 1: write the failing tests.**

`tests/MissileDisaster.Core.Tests/InterceptDecisionTests.cs`:
```csharp
using MissileDisaster.Core;
using Xunit;

public class InterceptDecisionTests
{
    private static readonly InterceptorTier Sam = InterceptorTiers.Sam; // alt[800,2500) range 4000 chance 0.6

    [Theory]
    [InlineData(1500f, 1000f, true)]   // 帯内・射程内
    [InlineData(800f, 1000f, true)]    // 下端(含む)
    [InlineData(2500f, 1000f, false)]  // 上端(含まない)
    [InlineData(500f, 1000f, false)]   // 帯下
    [InlineData(1500f, 4001f, false)]  // 射程外
    [InlineData(1500f, 4000f, true)]   // 射程端(含む)
    public void InEngagementZone_checks_band_and_range(float alt, float dist, bool expected)
    {
        Assert.Equal(expected, InterceptDecision.InEngagementZone(alt, dist, Sam));
    }

    [Theory]
    [InlineData(0.0f, true)]    // roll < 0.6 → 迎撃
    [InlineData(0.59f, true)]
    [InlineData(0.6f, false)]   // roll == chance → 失敗(未満のみ成功)
    [InlineData(0.9f, false)]
    public void ShouldIntercept_rolls_within_zone(float roll, bool expected)
    {
        Assert.Equal(expected, InterceptDecision.ShouldIntercept(1500f, 1000f, Sam, roll));
    }

    [Fact]
    public void ShouldIntercept_false_outside_zone_regardless_of_roll()
    {
        Assert.False(InterceptDecision.ShouldIntercept(5000f, 1000f, Sam, 0.0f)); // 帯外
        Assert.False(InterceptDecision.ShouldIntercept(1500f, 9999f, Sam, 0.0f)); // 射程外
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
    /// 迎撃可否の純粋判定。乱数は引数(roll)注入でテスト可能に。UnityEngine 非依存。
    /// altitude はミサイルの対地高度、horizontalDistance は迎撃建物までの水平距離。
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
git commit -m "feat(core): 交戦圏+確率の迎撃判定 InterceptDecision (TDD)"
```

---

## Task 4: 飛来ミサイルを apex 降下へ刷新（ModConfig＋Missile）

**Files:**
- Modify: `src/MissileDisaster/Game/ModConfig.cs`（飛翔定数を差し替え）
- Modify: `src/MissileDisaster/Game/Missile.cs`（apex→着弾の降下補間へ）

**Interfaces:**
- Consumes: `LaunchGeometry.BearingOffset`、`BallisticMath.AdvanceT/Lerp`、新 `ModConfig` 定数。
- Produces: 変更なし（`MissileManager` から見た `Missile(target,type)` / `UpdateVisual(float)` / `Target` / `Spec` / `DestroyVisual` は不変）。

ゲーム DLL コード（ユニットテスト無し）。検証はビルド成功＋実機確認。

- [ ] **Step 1: ModConfig の飛翔定数を差し替える**

`src/MissileDisaster/Game/ModConfig.cs` の飛翔ブロック（`MissileSpeed` / `MissileArcHeight` / `MissileStartAltitude` / `MissileLaunchOffset` の4定数）を、以下へ置き換える（`MissileSpeed` は残す）:
```csharp
        // Flight, driven on the main thread by simulationTimeDelta.
        // 弾道は固定方位・高高度の apex(頂点)から着弾までの「降下枝のみ」。
        public const float MissileSpeed = 900f;              // 降下ペース(水平投影距離に対する m/秒 相当)
        public const float IncomingBearingDegrees = 315f;    // 飛来方位(0=北,時計回り)。全弾同一方位。315=北西
        public const float ApexHorizontalOffset = 2200f;     // apex の水平オフセット(m)。大きいほど浅い角度
        public const float ApexAltitude = 4000f;             // apex の対地高度(m)。高いほど急角度で高高度から飛来
```

- [ ] **Step 2: Missile を apex 降下へ書き換える**

`src/MissileDisaster/Game/Missile.cs` の全内容を以下へ置き換える:
```csharp
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// 飛翔中の 1 発。固定方位・高高度の apex(頂点)から着弾までの「降下枝のみ」を、
    /// すべてメインスレッドで補間する（sim スレッドはこのオブジェクトに触れない）。
    /// 可視表現は本プランでは簡易プリミティブ（球）。実モデル化は別プラン。
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
            // 固定方位・高高度の apex から降下する。上昇枝は存在しない(=終端のみ描画)。
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
        /// メインスレッド。apex→着弾を直線降下で補間する。戻り値 true = このフレームで着弾(t&gt;=1)。
        /// 着弾後の処理(ダメージ enqueue と破棄)は MissileManager 側が行う。
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

注: `BallisticMath.ArcHeightAt` は本プランでは未使用になる（テスト済み Core ユーティリティとして残置。将来の曲線降下チューニング用）。dead code ではなく保持で問題ない。

- [ ] **Step 3: ビルド＆デプロイ**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: ビルド成功、DLL 配置。

- [ ] **Step 4: 実機で見た目確認（手動・ユーザー）**

Cities: Skylines で F9 → クリック。ミサイル(球)が **同一方位（北西）から高高度 apex を起点に、急角度の降下枝のみ** を描いて着弾すること、複数撃っても全部同じ方向から来ること、速度連動・一時停止を確認。

- [ ] **Step 5: Commit**

```bash
git add src/MissileDisaster/Game/ModConfig.cs src/MissileDisaster/Game/Missile.cs
git commit -m "feat: 飛来ミサイルを固定方位・高高度apexの降下枝のみに刷新"
```

---

## Definition of done

- 全 Core テスト合格（既存18＋新規: LaunchGeometry6/InterceptorTier3/InterceptDecision12 目安）。
- ビルド＆デプロイ成功。
- 実機で「固定方位・高高度から降下枝のみ」を確認（ユーザー）。

## 次（model.blend 完成後の別プラン）

- Plan 2B: 弾頭＋ARROW/SAM/PAC＋建物メッシュの OBJ 化・読込、飛来弾の実モデル化＋機首向き。
- Plan 2D: 新規建物3種（`CustomBuildingFactory`）＋`InterceptorAI`＋`InterceptorRegistry`、迎撃判定を `MissileManager`（メイン）へ配線。
- Plan 2E: 迎撃弾の会合飛翔＋爆発演出。
