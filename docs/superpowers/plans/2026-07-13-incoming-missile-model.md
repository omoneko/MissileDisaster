# 飛来ミサイル刷新（apex降下＋実モデル＋Z+機首向き）実装計画

> 増分: 設計書 `2026-07-13-flight-and-defense-design.md` の **2A（飛来刷新）＋2B（飛来弾のみモデル化）** を統合。
> ユーザー決定: 進め方=「飛来弾→施設の順」、PAC施設=PAC3。本計画は飛来弾のみ（施設は次計画）。

**Goal:** 飛来ミサイルを (1) 固定方位・高高度 apex からの降下枝のみ、(2) `弾道ミサイル弾頭` の実モデル表示、(3) 機首(モデル+Z)を進行方向へ向ける、へ刷新。実機で確認できる最小単位。

**Architecture:** Phase 1 の上に積む。Alien Invasion の OBJ 読込パイプライン（`ObjParser`/`ObjData`/`MtlParser` = Core 純粋、`ObjMeshBuilder`/`ModelProvider` = Game）を**縮小移植**（AssetBundle/デカール/夜間発光は持ち込まない）。`Missile` を apex 降下＋モデル生成＋`LookRotation` 機首向きへ変更。main/sim スレッド境界は不変。

**Global Constraints:**
- Mod 本体 `v3.5` / `LangVersion 7.3`。`Core/**/*.cs` は UnityEngine 非依存。
- 方位規約: 0°=+Z(北), 90°=+X, 時計回り。
- モデル軸: Blender +Z=機首。OBJ を up_axis=Z/forward_axis=Y で書き出し済み → `ObjParser` は X 反転のみ → Unity ローカル +Z=機首。`Quaternion.LookRotation(velocity)` で機首が進行方向へ向く。
- 資産は既に配置済み: `src/MissileDisaster/Models/IncomingWarhead.obj`(+`.mtl`)（161v/318tri/2mat, z −1.117..1.0）。
- コミットは該当ファイルのみ `git add`（`.blend`/`.mp3` 等の未追跡物は含めない）。Codex フックは尊重（`--no-verify` 禁止）。

---

## ファイル構成

| File | Kind | Responsibility |
|---|---|---|
| `Core/ObjData.cs` | 新規(移植) | OBJ 中間表現（`ObjData`/`ObjSubmesh`） |
| `Core/ObjParser.cs` | 新規(移植) | OBJ テキスト解析（X反転＋巻き順反転） |
| `Core/MtlParser.cs` | 新規(移植) | MTL 解析（`MtlColor`, Kd/d） |
| `tests/.../ObjParserTests.cs` | 新規(移植) | パーサのテスト |
| `Game/Models/MissileMeshBuilder.cs` | 新規 | `ObjData`+MTL→`Mesh`/`Material[]`（Standard, 発光なし） |
| `Game/Models/MissileModelProvider.cs` | 新規 | `Models/<name>.obj` から GameObject 生成＋キャッシュ |
| `Game/ModConfig.cs` | 変更 | 飛翔定数を apex 方式へ＋モデル定数追加 |
| `Game/Missile.cs` | 変更 | apex 降下＋実モデル＋機首向き（球フォールバック） |
| `Game/Mod.cs` | 変更 | `OnEnabled` で modPath 取得→`MissileModelProvider.Initialize` |
| `build.ps1` | 変更 | `Models/*.obj,*.mtl` を mod フォルダへ配布 |

---

## Task A: OBJ/MTL パーサを Core へ移植（TDD）

Alien の `Core/ObjParser.cs`・`Core/ObjData.cs`・`Core/MtlParser.cs` を `MissileDisaster.Core` 名前空間へコピー（ロジック不変）。Alien の `ObjParserTests.cs` を名前空間だけ差し替えて移植。テスト csproj は `Core/**/*.cs` を自動リンクするので追記不要。
- 検証: `dotnet test` が全緑（既存38＋移植分）。

## Task B: モデル生成（Game）

- `MissileMeshBuilder.TryBuild(ObjData, Dictionary<string,MtlColor>, Color fallback, out Mesh, out Material[])`: Alien `ObjMeshBuilder` の縮小版。`FilterValidTriangles`＋Standard シェーダ＋Kd 色＋metallic/gloss。**`EmissionController` 参照・透過登録は削除**。
- `MissileModelProvider`: `Initialize(modDir)` / `CreateInstance(name)`。`Models/<name>.obj`(+`.mtl`) を Core パーサ→`MissileMeshBuilder`→`Mesh`/`Material[]` をキャッシュし、`GameObject`(MeshFilter+MeshRenderer) を返す。無ければ null（呼び出し側フォールバック）。AssetBundle/デカールは持ち込まない。全てメインスレッド。

## Task C: ModConfig 刷新

飛翔ブロック（`MissileSpeed`/`MissileArcHeight`/`MissileStartAltitude`/`MissileLaunchOffset`）を置換:
```csharp
public const float MissileSpeed = 900f;            // 降下ペース(水平投影 m/秒 相当)
public const float IncomingBearingDegrees = 315f;  // 飛来方位(0=北,時計回り)。全弾同一。315=北西
public const float ApexHorizontalOffset = 2200f;   // apex 水平オフセット(m)
public const float ApexAltitude = 4000f;           // apex 対地高度(m)
```
モデル定数を追加:
```csharp
public const string ModelsFolderName = "Models";
public const string IncomingMissileModelName = "IncomingWarhead";
public const float IncomingMissileScale = 18f;     // モデル ~2m → 実機 ~38m。実機で調整
public const float ObjMetallic = 0.6f;
public const float ObjGlossiness = 0.5f;
public static readonly Color ObjFallbackColor = new Color(0.25f, 0.25f, 0.25f, 1f);
```

## Task D: Missile を apex 降下＋実モデル＋機首向きへ

- `_apex = target + BearingOffset(IncomingBearingDegrees, ApexHorizontalOffset) + up*ApexAltitude`。降下枝のみ（上昇なし）。
- `_groundDistance = |target - apex|(水平)`（= ApexHorizontalOffset > 0）。
- 表示: `MissileModelProvider.CreateInstance(IncomingMissileModelName)`。null なら球フォールバック（Phase1 同様）。`localScale = IncomingMissileScale`。Collider は破棄。
- `UpdateVisual`: `AdvanceT`＋直線 `Lerp`（`ArcHeightAt` 不使用）。位置更新に加え、`Vector3 vel = (_target - _apex)`（一定）で `transform.rotation = Quaternion.LookRotation(vel)` を設定し機首(+Z)を進行方向へ。`return _t >= 1f`。
- `MissileManager` から見た API（`Missile(target,type)`/`UpdateVisual`/`Target`/`Spec`/`DestroyVisual`）は不変。

## Task E: Mod.OnEnabled で初期化

`Mod.cs` に `OnEnabled()` 追加: `Singleton<PluginManager>.instance.FindPluginInfo(Assembly.GetExecutingAssembly())` → `info.modPath` → `MissileModelProvider.Initialize(info.modPath)`。try/catch＋LogError。参照: `System.Reflection`, `ColossalFramework`(Singleton), `ColossalFramework.Plugins`(PluginManager)（csproj は ColossalManaged 参照済み）。

## Task F: build.ps1 配布

DLL コピー後に `Models` フォルダを `$modDir\Models` へコピー（`*.obj`,`*.mtl`）。

## Task G: ビルド＆実機確認（ユーザー）

`build.ps1` 成功→CS で F9→クリック。**北西・高高度から降下枝のみ**で飛来、球でなく弾頭モデルが**機首を進行方向へ**向けて着弾すること、複数発が同方位から来ることを確認。スケール/方位/軸は実機で微調整。

## Definition of done

- Core テスト全緑（移植パーサ含む）。ビルド＆デプロイ成功。実機で「固定方位・高高度・降下枝のみ・実モデル・機首向き」を確認（ユーザー）。

## 次計画（施設）

PAC3/VLS＿ARROW/VLS＿SM の新規建物3種＋`InterceptorAI`＋`InterceptorRegistry`＋迎撃判定配線（設計書 2D）＋迎撃弾 ARROW/SM/パトリオット の会合飛翔＋爆発（2E）。
