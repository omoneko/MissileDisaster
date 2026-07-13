# ミサイル災害 Mod — 飛来弾道刷新＋3層迎撃防衛 設計書

- 日付: 2026-07-13
- 位置づけ: Phase 1（通常弾頭 MVP・master 済み）の上に積む増分。実機テストで得た改善要望＋③迎撃を前倒しで詳細化。
- ステータス: 設計確定（実装計画待ち）

## 1. 目的

実機で判明した飛来演出の改善と、3層迎撃防衛の導入。

1. 飛来ミサイルを **固定方位** から飛来させる（現状はランダム方位）。
2. **高高度から飛来**し、弾道のうち **降下枝（頂点→着弾）のみ描画**する。
3. **model.blend のモデル**を使用（球プレースホルダを置換）。
4. **ARROW（超高高度）/ SAM（高高度）/ PAC（終端）** の3層迎撃を、各1種の建物として導入。

## 2. 確定した設計方針（ブレインストーミングの結論）

| 項目 | 決定 |
|---|---|
| 飛来方位 | ワールド固定方位（定数）。全弾同じ向きから飛来 |
| 弾道・描画 | ロジックを「頂点(apex)→着弾」で t=0→1 と定義。apex を高高度・水平オフセット位置に置き、**降下枝のみ描画**（上昇は描かない） |
| 弾頭表示 | `弾道ミサイル弾頭` モデル（OBJ）。機首を進行方向へ向ける |
| 迎撃機構 | 高度帯レイヤー防衛（自動・確率）。ARROW最上→SAM→PAC終端の順に、担当高度帯∩水平射程で確率ロール。すり抜けたら着弾 |
| 迎撃演出 | 成功時、迎撃弾（ARROW/SAM/PACモデル）が建物→会合点へ飛翔→爆発フラッシュで両者消滅 |
| 建物 | コスト・電力ありの本格建物3種（バニラの電力消費建物を複製し `InterceptorAI` で挙動差し替え） |
| 迎撃判定スレッド | メインスレッド（飛来ミサイル位置が主管の側）で判定・解決。建物はレジストリに自己登録 |

## 3. model.blend のモデル在庫

| メッシュ名 | 用途 | 頂点数 |
|---|---|---|
| `弾道ミサイル弾頭` | 飛来ミサイル本体 | 161 |
| `ARROW` | 超高高度迎撃弾 | 769 |
| `SAM` | 高高度迎撃弾 | 1121 |
| `PAC` | 終端迎撃弾 | 1569 |

建物メッシュは含まれない → 迎撃建物はバニラ建物を複製し、ARROW/SAM/PAC モデルは**迎撃弾（飛翔体）**として使う。

## 4. アーキテクチャ（追加・変更ファイル）

```
src/MissileDisaster/
├── Core/
│   ├── BallisticMath.cs           # 変更: apex降下用ヘルパ追加（既存は不変）
│   ├── LaunchGeometry.cs          # 新規: 固定方位からの apex 位置算出（純粋・テスト可能）
│   └── InterceptDecision.cs       # 新規: 高度帯∩射程∩確率の迎撃判定（純粋・テスト可能）
├── Game/
│   ├── ModConfig.cs               # 変更: 方位/高度帯/迎撃パラメータ定数
│   ├── Missile.cs                 # 変更: apex降下・固定方位・モデル表示・機首向き・被迎撃
│   ├── MissileManager.cs          # 変更: 迎撃判定を飛翔ループ内(メイン)で実行
│   ├── Models/
│   │   ├── ModelLoader.cs         # 新規: OBJ/MTL 読込（Alien の ObjParser/MtlParser/ObjMeshBuilder 流用）
│   │   └── MissileModels.cs       # 新規: 4モデルのロード＆GameObject 生成facade
│   ├── Defense/
│   │   ├── InterceptorRegistry.cs # 新規: 稼働中迎撃建物の位置/帯/射程/確率/クールダウン（メイン読取）
│   │   ├── InterceptorAI.cs       # 新規: PlayerBuildingAI 派生。電力/維持＋レジストリ登録
│   │   ├── InterceptorTier.cs     # 新規: ARROW/SAM/PAC の帯・射程・確率定義
│   │   ├── InterceptorShot.cs     # 新規: 迎撃弾の飛翔体（建物→会合点→爆発）
│   │   └── CustomBuildingFactory.cs # 新規: バニラ電力建物を複製し3建物を登録
│   └── Effects/
│       └── InterceptFx.cs         # 新規: 会合点の爆発フラッシュ
├── Models/                        # OBJ/MTL 配置先（build.ps1 が配布）
tests/MissileDisaster.Core.Tests/
├── LaunchGeometryTests.cs         # 新規
└── InterceptDecisionTests.cs      # 新規
```

Alien Invasion からの流用: `ObjParser` / `MtlParser` / `ObjMeshBuilder`（OBJ 読込）、`RenderAssets`（シェーダ探索）、`Effects`（LineRenderer/フラッシュ）。

## 5. 機能別設計

### A. 飛来ミサイルの刷新

- **固定方位**: `ModConfig.IncomingBearingDegrees`（例: 315°=北西）。apex の水平オフセットはこの方位ベクトル×`ApexHorizontalOffset`。全弾同一方位。
- **高高度 apex 降下**: `_apex = target + bearingVec * ApexHorizontalOffset + up * ApexAltitude`（`ApexAltitude` は高め、例 4000）。`t=0` を apex、`t=1` を着弾とする**降下のみ**。放物線の弧は「降下枝」に限定（apex が最高点なので追加の弧成分は小さめ〜0にして急降下＋わずかな重力弧）。
  - `LaunchGeometry.ApexPosition(target, bearingDeg, horizOffset, altitude)` を純粋関数化（Core・テスト可能）。
- **描画範囲**: apex→着弾のみ生成・描画。上昇枝は存在しない（apex 始点）。「終端のみ描画・高高度から飛来」を満たす。
- **モデル**: 球を `弾道ミサイル弾頭` モデルに置換。**機首を速度ベクトル方向へ向ける**（`Quaternion.LookRotation(velocity)`）。
- **高度帯通過**: 降下中に ARROW帯→SAM帯→PAC帯を順に通過（帯境界は高度定数）。

### B. 3層迎撃防衛（ARROW / SAM / PAC）

- **建物3種**: `CustomBuildingFactory` がバニラの電力消費サービス建物を複製し、建設費・電力・維持費を継承。AI を `InterceptorAI` に差し替え、ARROW/SAM/PAC の3プレファブを登録。
- **`InterceptorAI : PlayerBuildingAI`**: バニラの電力/維持挙動は基底に委譲。稼働中（電力供給あり）は自身を `InterceptorRegistry` に登録、非稼働/破棄で解除。
- **`InterceptorTier`**（Core 定数）: 各層の `AltitudeMin/Max`・`HorizontalRange`・`InterceptChance`・`CooldownSeconds`。
  - ARROW=最上帯・広射程・低〜中確率、SAM=中帯・中確率、PAC=終端帯・高確率・狭め。バランスは定数で調整。
- **判定（メインスレッド）**: `MissileManager` の飛翔更新（メイン）で、各飛来ミサイルについて高い帯から順に登録建物を走査。ミサイルが「その建物の高度帯 ∩ 水平射程内」かつ建物クールダウン明けなら `InterceptDecision.ShouldIntercept(...)` で確率ロール。成功→被迎撃（着弾enqueueせず消滅）、建物クールダウン開始、迎撃演出を生成。
  - `InterceptDecision`（Core・テスト可能）: 高度帯判定・水平距離判定・確率の純粋ロジック（確率の乱数は引数注入でテスト可能に）。
- **演出**: 成功時 `InterceptorShot`（対応モデル）を建物位置から会合点へ上昇飛翔させ、会合で `InterceptFx` の爆発フラッシュ＋両者消滅。
- **すり抜け**: どの層でも迎撃されなければ既存 `ImpactResolver` で通常着弾。

### C. モデル読込パイプライン

- Blender MCP で model.blend の4メッシュを OBJ+MTL エクスポート（`bpy.ops.wm.obj_export`、各メッシュ個別ファイル）→ `src/MissileDisaster/Models/` に配置 → `build.ps1` が mod フォルダへ配布（Alien と同様）。
- 起動時 `MissileModels` が4モデルをロードしメッシュ/マテリアルをキャッシュ。飛来弾・各迎撃弾はここから GameObject を生成。ゲーム内スケールは定数で調整。
- CS Unity 5.6 のシェーダ制約は Alien の `RenderAssets` 流用で回避。

## 6. スレッド規律

- 飛来ミサイル（`_missiles`）・迎撃演出 GameObject・**迎撃判定/解決**は全てメインスレッド。
- `InterceptorRegistry` はメインスレッドが読む。建物 AI の登録/解除はメイン反映（`SimulationStep` は sim のため、登録はロック保護 or メインスレッド反映キュー経由）。
- 着弾ダメージ（`ImpactResolver`）は従来どおり sim スレッド（`MissileManager.UpdateSimulation` のキュー排出）。

## 7. テスト戦略

Core 純粋ロジックを xUnit でテスト:
- `LaunchGeometry`: apex 位置が方位・オフセット・高度どおり。着弾との水平距離＝horizOffset。
- `InterceptDecision`: 高度帯内/外、射程内/外、確率境界（乱数注入）で期待どおり true/false。
- 既存 `BallisticMath` 追加ヘルパのテスト。

ゲーム型依存（AI/建物/モデル/演出）は実機確認。

## 8. 実装順（各段が実機で確認できる単位＝別プランに分割）

- **Plan 2A（飛来刷新）**: 固定方位＋apex降下＋高高度化（モデルは球のまま）。実機で「同方向・高高度から降下枝のみ」を確認。
- **Plan 2B（モデル）**: model.blend 4メッシュを OBJ 化＋読込。飛来弾を実モデル化＋機首向き。
- **Plan 2C（迎撃Core）**: `LaunchGeometry`/`InterceptDecision`/`InterceptorTier`（TDD）。
- **Plan 2D（建物＋AI）**: `CustomBuildingFactory`＋`InterceptorAI`＋`InterceptorRegistry`。3建物設置→高度帯∩射程で被迎撃（演出は簡易フラッシュ）。
- **Plan 2E（迎撃演出）**: `InterceptorShot`（会合飛翔）＋`InterceptFx`（爆発）＋各モデル。

## 9. 未決事項・リスク

- バニラ建物の複製で電力/コストを継承する際、複製元の選定と `InterceptorAI` への差し替え手順の実機検証（Phase 1 の `CustomBuildingFactory` 方針を踏襲）。
- 高度帯境界・射程・確率・クールダウンはプレイしながら数値調整（バランス作業）。
- 降下枝の弧成分（急降下 vs ゆるい弧）は見た目調整。
- モデルのスケール・機首軸（モデルの前方がどの軸か）は実機で調整。
