# ミサイル災害 Mod 設計書 (Cities: Skylines 無印)

- 日付: 2026-07-13
- 対象ゲーム: Cities: Skylines（初代 / 無印）
- 実装基盤: C# + Harmony、既存の災害システム（`DisasterManager` / `MeteorStrike` / `DisasterHelpers`）応用
- ステータス: 設計確定（実装計画待ち）

## 1. 目的とスコープ

指定箇所・都市全域へミサイルを投射できる災害 Mod。5 機能を実装する。

1. 指定箇所へ任意弾頭（通常/クラスター/白リン/気化爆弾/核）を投射
2. 都市全域ランダムに任意数（1〜200 発）を発射するバラージ災害
3. ミサイルを迎撃する防衛施設
4. 核弾頭は出力プリセット（代表 6 種）から選択
5. 核弾頭時は放射能汚染（範囲内住民は即病気）を発生

### 確定した設計方針（ブレインストーミングの結論）

- **プロジェクト構成**: 新規・独立 Mod `MissileDisaster`。Alien Invasion / NuclearMeltdown からコード流用。3 Mod は独立共存、Workshop も個別公開。
- **飛翔・迎撃**: 放物線飛翔＋飛行中迎撃（防衛施設が飛行中ミサイルを検知して確率撃墜）。
- **核出力換算**: 立方根則（半径 ∝ yield^(1/3)）＋上限クランプ。
- **放射能**: 病気＋時間で致死・長期汚染。**独立概念として新設**し、**既存の下水/水処理施設では除染不可**。**専用の除染施設のみ**が除去できる。
- **バラージ弾頭**: 「単一弾頭」/「ミックス（ランダム割当）」をトグルで切替。

## 2. アーキテクチャと流用マップ

```
MissileDisaster/
├── src/MissileDisaster/
│   ├── Core/                         # ゲーム型に依存しない純粋ロジック（xUnit でテスト）
│   │   ├── BallisticMath.cs          # 新規: 放物線座標・飛行時間
│   │   ├── NukeScaling.cs            # 新規: 立方根則＋クランプ (yield→半径)
│   │   ├── NukePresets.cs            # 新規: 核 6 種プリセット表
│   │   ├── WarheadSpec.cs            # 新規: 弾頭ごとのパラメータ表
│   │   ├── InterceptResolver.cs      # 新規: 迎撃確率判定（弾頭別）
│   │   └── BarrageScheduler.cs       # 新規: 発数クランプ(1-200)＋分散発射計画
│   ├── Game/
│   │   ├── Mod.cs                    # Alien Mod.cs を土台
│   │   ├── ModConfig.cs              # 両 Mod 流用（定数集約）
│   │   ├── MissileManager.cs         # 新規: 飛翔体の生成・追跡・着弾（Alien InvasionManager 応用）
│   │   ├── Missile.cs                # 新規: 1 発の状態＋Transform 補間（Alien Invasion.cs 応用）
│   │   ├── Warheads/
│   │   │   ├── IWarhead.cs
│   │   │   ├── ConventionalWarhead.cs
│   │   │   ├── ClusterWarhead.cs
│   │   │   ├── WhitePhosphorusWarhead.cs
│   │   │   ├── ThermobaricWarhead.cs
│   │   │   └── NuclearWarhead.cs
│   │   ├── ImpactResolver.cs         # Alien の MakeCrater/DestroyStuff/延焼 を流用
│   │   ├── Radiation/
│   │   │   ├── RadiationManager.cs   # 新規: 放射能汚染（独立概念）
│   │   │   ├── RadiationGrid.cs      # 新規: 汚染グリッド（Nuclear の実装パターン流用）
│   │   │   └── RadiationSickness.cs  # 新規: 範囲内住民を病気→致死（Nuclear の健康低下流用）
│   │   ├── Buildings/
│   │   │   ├── CustomBuildingFactory.cs   # 新規: バニラ建物を複製して 2 施設を登録
│   │   │   ├── MissileDefenseAI.cs        # 新規: 迎撃施設 AI
│   │   │   └── RadDecontaminationAI.cs    # 新規: 除染施設 AI
│   │   ├── UI/MissileTool.cs         # Alien の着弾点クリックツール流用
│   │   └── Simulation/MissileThreadingExtension.cs  # Alien の simTimeDelta 駆動流用
│   └── Effects/                      # Alien の LineRenderer/エフェクト資産流用
└── tests/MissileDisaster.Core.Tests/ # 両 Mod 同様の xUnit 構成
```

### 流用元の対応

| 必要機能 | 流用元 | 既存実装 |
|---|---|---|
| 着弾点クリック指定ツール | Alien Invasion | `ToolBase` 派生＋クリック→座標 |
| クレーター＋範囲破壊＋延焼 | Alien Invasion | `DisasterHelpers.MakeCrater/DestroyStuff` |
| 複数同時・速度連動・一時停止 | Alien Invasion | スロット配列＋`simulationTimeDelta` |
| 放物線飛翔＋トレイル/エフェクト | Alien Invasion | Transform 補間・LineRenderer |
| 汚染グリッドの構造・走査パターン | NuclearMeltdown | `ContaminationManager` の実装様式 |
| 住民の健康低下 | NuclearMeltdown | 健康値操作ロジック |

### スレッド規律（Alien と同一）

- メインスレッド: GameObject / Transform / Effects / 状態書込み
- sim スレッド: `RadiationManager` のグリッド処理のみ
- 飛翔・迎撃・着弾は全て `simulationTimeDelta` 駆動 → ゲーム速度連動＋一時停止で凍結。

## 3. 機能別設計

### ① 弾頭 5 種の着弾挙動

`WarheadSpec`（Core・数値表）＋各 `Warheads/*.cs`（着弾演出）で分離。基準値は Alien のクレーター/破壊/延焼を係数で差別化。

| 弾頭 | クレーター | 範囲破壊 | 延焼 | 放射能 | 特徴 |
|---|---|---|---|---|---|
| 通常 Conventional | 中 | 中 | 小 | なし | 単発の素直な着弾。基準 |
| クラスター Cluster | 極小×多数 | 広く薄い | 中 | なし | 着弾前に空中で子弾 N 発に分裂→広域散布。分裂前に迎撃しないと防げない |
| 白リン WhitePhosphorus | ほぼ無 | 小 | 極大・持続 | なし | 焼夷特化。範囲内建物を長時間炎上 |
| 気化爆弾 Thermobaric | 浅い | 極大 | 大 | なし | 過圧で広範囲を薙ぎ倒す。深い穴は掘らない |
| 核 Nuclear | 最大 | 最大 | 大 | あり | 出力プリセット連動。①④⑤ が全部乗る |

- 共通 IF: `IWarhead.Detonate(Vector3 pos, WarheadSpec spec)`。
- クラスターのみ飛翔中の分裂点 `splitAltitude` を持ち、`MissileManager` が分裂を扱う。

### ② バラージ災害（1〜200 発）

- 発動: UI（ボタン/キー）＋任意でランダム自然発生。
- `BarrageScheduler.Plan(count, mode)`（Core・テスト可能）で発数を 1〜200 にクランプし、数フレームに分散した発射計画へ変換。
- 弾頭: トグルで「単一弾頭」/「ミックス（ランダム割当、核の混入比率を調整可）」。
- **負荷対策（実装の山場）**:
  - 同時飛翔数に上限。超過分はキュー。
  - 飛翔中は軽量表現。
  - 着弾/爆発エフェクトはプール再利用。
  - 着弾処理もフレーム分散し sim スレッドを詰まらせない。

### ③ 迎撃施設（防衛）

- プロップ建物＋`MissileDefenseAI : PlayerBuildingAI`。
- パラメータ: `range` / `interceptChance` / `cooldown`（ammo 等は入れずシンプル）。
- `SimulationStep` で射程内の飛行中ミサイルを探索→クールダウン明けなら `InterceptResolver` で確率判定→成功で撃墜エフェクト＋除去。
- 迎撃確率は弾頭別（Core・テスト可能）: 核＝低め。クラスターは分裂前のみ有効。

### ④ 核出力プリセット（代表 6 種）

出力レンジが広く分布する代表 6 種。`NukeScaling.BlastRadius(yieldKt, scale) = scale * yieldKt^(1/3)`、`Mathf.Min` で上限クランプ（マップを覆わない）。クレーター/破壊/汚染半径すべてこの半径へスケール（各々に上限）。核選択時のみ UI にプリセット選択を表示。

| # | 名称 | 出力 |
|---|---|---|
| 1 | Little Boy（広島） | 16 kt |
| 2 | Fat Man（長崎） | 21 kt |
| 3 | W53 / B53 | 9 Mt |
| 4 | Mk-41 (B41) | 25 Mt |
| 5 | Tsar Bomba（実験値） | 50 Mt |
| 6 | Tsar Bomba（設計値） | 100 Mt |

（出力値は参考: 核兵器一覧。ゲーム内は立方根則＋クランプで圧縮する）

### ⑤ 放射能汚染（独立概念＋専用除染施設）

- **新概念** `RadiationManager` / `RadiationGrid`。NuclearMeltdown からはグリッドの構造・走査パターンのみ流用し、**「Water Treatment を検出して除染」ロジックは使わない**。既存の下水/水処理施設では放射能は一切除去されない。
- 被害: 範囲内住民は即病気 → 滞在で健康漸減 → 時間経過で死亡。汚染は長期残留し緩やかに自然減衰（核出力でスケール／定数調整）。核弾頭のみ発生。
- **除去は新設の専用施設のみ**: `RadDecontaminationAI : PlayerBuildingAI` を持つプロップ建物「除染施設 Decontamination Facility」。稼働中、射程内の `RadiationGrid` セルを徐々に低減。これ以外（自然減衰を除く）では汚染は消えない。
- 地面オーバーレイ（緑/紫の半透明）で汚染を可視化。

### カスタム建物の生成（③＋⑤ 共通）

- 新規プロップ建物が 2 種（迎撃施設・除染施設）。
- コードのみで追加するため、`CustomBuildingFactory` でバニラ建物の `BuildingInfo` を複製し、AI とメッシュ/名称を差し替えて登録する方式。

## 4. テスト戦略

純粋ロジックは xUnit でテスト（両 Mod と同構成、`Core/**/*.cs` をテストプロジェクトへリンク）:

- `BallisticMath`: 放物線の端点一致・頂点高さ・t=0/1 の座標。
- `NukeScaling`: 立方根の単調増加・クランプ上限・相対比。
- `NukePresets`: 6 種の定義・出力値。
- `WarheadSpec`: 弾頭別係数の妥当性。
- `InterceptResolver`: 弾頭別確率・クラスター分裂後は迎撃不可。
- `BarrageScheduler`: 1〜200 クランプ・分散計画の発数一致。

ゲーム型依存（AI/Manager/Tool）は実機動作確認で担保。

## 5. 実装順（MVP 優先）

各段が「動く単位」になるよう段階的に組む。

1. **通常弾頭 1 発**: クリック→放物線飛翔→着弾クレーター＋破壊（Alien 流用で土台完成）。
2. 弾頭分岐（クラスター/白リン/気化）。
3. 核プリセット＋放射能（独立概念の新設＋専用除染施設）。
4. バラージ 1〜200＋負荷分散。
5. 迎撃施設（最も複雑、最後）。

## 6. 未決事項・リスク

- カスタム建物 2 種をコードのみで安定生成できるか（`BuildingInfo` 複製方式の検証が必要）。
- 200 発バラージの sim スレッド負荷。分散パラメータは実機で調整。
- 核 6 種の `scale`・クランプ上限はプレイしながら数値調整（バランス作業）。
- 放射能の致死速度・自然減衰レートは定数調整前提。
