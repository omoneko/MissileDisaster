# ランダム攻撃：災害頻度連動＋MIRV 同時着弾 設計

作成日: 2026-07-19
対象: Missile Disaster mod（Cities: Skylines 2015 / Unity 5.6）
ブランチ: feature/phase2-flight-intercept-core

## 1. 目的 / 背景

現在のランダム攻撃は「実時間の固定間隔で 1 発ずつ」発射する暫定実装で、次の2点をユーザー要望どおりに作り替える。

1. **頻度モデル** … バニラ自然災害の発生頻度に連動させ、他の自然災害が発生するたびにミサイル用カウンターをリセットする（ミサイルは災害の“合間”に発生）。
2. **着弾パターン** … 現行の 1 発ずつに加え、複数弾を同時着弾させる **MIRV** パターンを追加する。

## 2. 現状（作り替え前）

- `Game/Simulation/MissileThreadingExtension.cs`：`OnUpdate` で `_randomTimer += realTimeDelta`、`>= RandomInterval`（既定180秒）で `RandomStrike.Fire()` を1回呼ぶ。実時間ベース／他災害と無関係／1発のみ。
- `Game/RandomStrike.cs`：`Fire()` は1発だけ発射（建物 or ランダム座標）。
- `Game/ModSettings.cs`：`RandomEnabled`(0/1)、`RandomIntervalSeconds`(既定180)、`RandomWarhead`(0=ランダム,1..5=固定)。

## 3. 技術的前提（実機DLLで確認済み）

- `DisasterManager` の以下フィールドはすべて **public**（Assembly-CSharp 参照済みのため直接読める。**Harmony 不要**）。
  - `m_randomDisastersProbability : float` … マップ/難易度由来の自然災害発生頻度。
  - `m_randomDisasterCooldown : int` … 次の災害までのカウンター（災害発生でリセットされる）。
  - `m_disasterCount : int` / `m_disasters : FastList<DisasterData>` … 発生中の災害の実数・実体。
- **飛翔時間は全弾一定**：`Missile` の `_groundDistance` は `ModConfig.ApexHorizontalOffset`（const 2200m）に一致し、落下高度は `ApexAltitude`（const 4000m）、速度は `MissileSpeed`（const 900）。方位も全弾共通（`IncomingBearingDegrees` const 315）。よって目標座標に依らず着弾までの所要時間は同一。→ **同一フレームで発射すれば着弾も同時**（同期処理は不要）。

## 4. 設計

### 4.1 頻度モデル：バニラ災害と枠を共有

**アプローチ**：`DisasterManager.instance` をポーリング（読み取りのみ）。却下案＝Harmony フック（不要な複雑さ／競合）、本物の DisasterInfo 登録（ミサイルは災害型でない・DLC 依存・過大）。

**スケジューラの純粋ロジック**を `Core/StrikeScheduler.cs`（UnityEngine 非依存）に分離し、xUnit でテストする。ゲーム時間（`SimulationManager.instance.m_currentGameTime`）で駆動し、速度・一時停止に自然連動する。

状態（すべてシミュレーションスレッドのみが触る）：
- `double _countdownDays` … 次のミサイル攻撃までのゲーム内日数。
- `int _lastDisasterCount` … 前回観測した `m_disasterCount`。
- `bool _initialized`。

メソッド（テスト用に依存を注入）：
```
// 1シミュレーションティックごとに呼ぶ。true = 今tick発火。
bool Advance(
    double gameDaysDelta,   // 前回からのゲーム内経過日数
    int    disasterCount,   // 現在の m_disasterCount
    float  probability,     // 現在の m_randomDisastersProbability (>=0)
    double freqMultiplier,  // 設定（0.25..3.0）
    double rng)             // [0,1) 区間乱数（間隔ばらつき用・注入）
```

ロジック：
1. 初回：`_lastDisasterCount = disasterCount; _countdownDays = NextInterval(...); _initialized = true; return false;`
2. `disasterCount > _lastDisasterCount`（**他の自然災害が発生**）：`_countdownDays = NextInterval(...);` にリセットし `_lastDisasterCount = disasterCount; return false;`
3. `disasterCount < _lastDisasterCount`（災害が消滅）：`_lastDisasterCount = disasterCount;`（リセットせず継続）。
4. それ以外：`_countdownDays -= gameDaysDelta; if (_countdownDays <= 0) { _countdownDays = NextInterval(...); return true; } return false;`

`NextInterval(probability, freqMultiplier, rng)`（正規化版・実装準拠）：
- `pf = ProbabilityFactor(probability)` … `probability / RefProbability` を `[ProbFactorMin, ProbFactorMax]` にクランプ。probability≈0（**災害無効マップ**）でも `ProbFactorMin` 止まりで有限間隔になり機能が死なない。
- `mean = BaseIntervalDays / (freqMultiplier * pf)`（頻度 ×↑ で間隔 ↓、災害頻度 ↑ で間隔 ↓）。
- `interval = mean * (0.5 + clamp01(rng))`（[0.5×,1.5×] の自然なばらつき）。
- `Clamp(interval, MinIntervalDays, MaxIntervalDays)`。

定数（`Core` 内に定義。実機観察で微調整。純粋ロジックなので数値変更はテスト不要）：
- `BaseIntervalDays = 20`：`freqMultiplier=1`・`probability=RefProbability` のときのミサイル基準間隔（ゲーム内日）。主要な調整ノブ。
- `RefProbability = 0.05`：想定される標準的な `m_randomDisastersProbability`。実機値がずれても `ProbabilityFactor` のクランプで間隔が暴れない。
- `ProbFactorMin = 0.25` / `ProbFactorMax = 4.0` / `MinIntervalDays = 2` / `MaxIntervalDays = 365` / `Epsilon`。

注：`m_randomDisastersProbability` の絶対スケールは非公開のため、`probability` を `RefProbability` で正規化して用いる。「×1＝バニラと厳密に同一頻度」ではなく「**バニラ災害頻度に比例**」する設計（実機で `BaseIntervalDays`／`RefProbability` を校正）。

**自己トリガーの扱い**：ミサイル着弾が `DisasterHelpers` 経由で `m_disasterCount` を増やす可能性がある。増えても発火直後に `_countdownDays` は既にリセット済みで、次tickで“他災害発生”として再リセットされるだけなので無害（二重発火にはならない）。実装時に挙動を確認して本節に追記する。

### 4.2 着弾パターン：Single / MIRV / Random

`RandomStrike` を拡張。発火の「タイミング」は 4.1 のスケジューラが決め、「パターン」は発火時に `AttackPattern` 設定で分岐する。

- **Single**（既定）＝1発（現行踏襲）。
- **MIRV**＝**3〜6発**（`UnityEngine.Random.Range(3,7)`）を**同一フレームで発射**（＝同時着弾）。各弾は**街中の別々の建物/地点**を独立に抽選（既存 `TryRandomBuilding` を弾ごとに呼ぶ＝市街地に散開）。
- **Random**＝毎回抽選。単発多め・時々 MIRV（**70% Single / 30% MIRV**）。

**弾頭**：各弾で既存 `PickWarhead()` を呼ぶ。Warhead=Random(0) なら弾ごとに個別抽選、固定(1..5)なら全弾同type。burst は現行どおり `BurstType.Groundburst` 固定。

`RandomStrike` の公開API：
```
static void FireStrike();   // AttackPattern 設定を見て Single / MIRV / Random を実行
static void FireOne();      // 1発（内部・既存 Fire 相当）
```

### 4.3 スレッド境界

- **シミュレーションスレッド**（`OnAfterSimulationTick`）：`m_currentGameTime.Ticks` の差分から `gameDaysDelta` を算出し、`DisasterManager` を読んで `StrikeScheduler.Advance(...)` を進める。`true` なら発火要求フラグを立てる（ロック保護の bool）。
- **メインスレッド**（`OnUpdate`）：発火要求フラグが立っていればクリアして `RandomStrike.FireStrike()` を実行（GameObject 生成・弾頭抽選・目標選定はメインスレッド）。
- `RandomEnabled` が false のときはスケジューラを進めず（`_initialized=false` へリセット）、フラグも立てない。

### 4.4 設定UI（`Game/Mod.cs` `OnSettingsUI`）

「Random missile strikes」グループ：
- `Enable random strikes`（既存・既定 OFF）。
- ~~`Interval between strikes (seconds)`~~ を撤去し **`Strike frequency (× natural disaster rate)`** スライダー（0.25〜3.0, step 0.25, 既定 1.0）。
- **`Attack pattern`** ドロップダウン（**Single / MIRV / Random**）← 新規。既定 **Single**。
- `Warhead`（既存）。

### 4.5 設定の永続化（`Game/ModSettings.cs`）

- `RandomIntervalSeconds`（廃止）。旧キーは設定ファイルに残っても無害。
- 追加 `StrikeFrequencyPct : SavedInt`（25〜300, 既定 100。`freqMultiplier = value / 100.0`）。SavedFloat を避け SavedInt を percent 保存。
- 追加 `AttackPattern : SavedInt`（0=Single,1=MIRV,2=Random, 既定 0）。
- 既存 `RandomEnabled`, `RandomWarhead` は据え置き。
- 参照ヘルパ：`StrikeFrequency`（double, =Pct/100）、`AttackPatternValue`（int）。

## 5. 変更ファイル

- 新規 `Core/StrikeScheduler.cs` … 純粋ロジック（Advance/NextInterval）。
- 新規 `tests/.../StrikeSchedulerTests.cs` … xUnit。
- `Game/ModSettings.cs` … RandomIntervalSeconds 撤去、StrikeFrequencyPct / AttackPattern 追加。
- `Game/Simulation/MissileThreadingExtension.cs` … 実時間タイマー撤去。sim tick で DisasterManager 連動スケジューラ駆動＋発火フラグ。OnUpdate はフラグ消化のみ。
- `Game/RandomStrike.cs` … FireStrike（パターン分岐）／FireOne、MIRV 複数発射。
- `Game/Mod.cs` … UI 差し替え（Strike frequency スライダー・Attack pattern ドロップダウン）。

## 6. テスト

`StrikeScheduler`（純粋）を xUnit で網羅：
- 初回呼び出しは発火しない・`_lastDisasterCount` を初期化する。
- `disasterCount` 増加でカウントダウンがリセットされ発火しない（他災害でリセット）。
- `disasterCount` 減少ではリセットしない。
- 経過日数の累積で `_countdownDays<=0` に達したら発火し、間隔が再設定される。
- `freqMultiplier` 増加で平均間隔が短くなる（3×は1×の約1/3）。
- `probability` 増加で平均間隔が短くなる。
- `probability≈0` で `DefaultProbability` にフォールバックし有限間隔になる。
- 間隔が `[Min,Max]` にクランプされる。
- `rng` 0 と ~1 で `[0.5×,1.5×]` の範囲になる。

MIRV の同時着弾・スレッド境界・UI は実機確認（自動テスト対象外）。

## 7. 非対象（YAGNI）

- MIRV の弾着時刻の明示同期（飛翔時間一定のため不要）。
- burst 種別のランダム化。
- MIRV 発数のスライダー化（当面 3〜6 固定レンジ）。
- 本物の DisasterInfo としての登録・災害パネル表示。

## 8. 互換性・公開

- セーブデータ形式に変更なし（設定ファイルのキー追加のみ、後方互換）。
- 実装後、実機で頻度・MIRV 同時着弾・オプションUIを確認 → Workshop を UPDATE で公開。
