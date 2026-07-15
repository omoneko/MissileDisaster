# 弾頭選択UI＋核威力プリセット 実装計画

> ユーザー決定: テンキー選択は「テスト用」。実装は**UIで操作**できるようにする。
> 核の出力調整は**方式3（発射時の威力プリセット選択）**。戦術/標準/戦略のプリセットで核の効果半径を一括スケール。

## アーキテクチャ

- `Core/NuclearYield.cs`（新規・TDD）: 威力プリセット（Tactical=20kt / Standard=150kt / Strategic=1000kt）。
  効果スケール係数は爆風半径 ∝ 威力^(1/3) に倣い `Multiplier(kt)=cbrt(kt/150)`（Standard=1.0, Tactical≈0.51, Strategic≈1.87）。
  UnityEngine 非依存・純粋。
- `Core/WarheadSpec.cs`: `Scaled(float m)` を追加（Crater/Destruction/Burn/Contamination 半径を m 倍した**新しい struct** を返す＝不変）。
- `Game/Missile.cs` / `Game/MissileManager.cs`: `Launch` を `(target, type, nuclearYieldMultiplier)` に。
  核のみ `spec = WarheadSpec.For(type).Scaled(mult)`。Missile はスケール済み spec を受け取る。
- `Game/UI/MissilePanel.cs`（新規）: UIView 直下の常設パネル。弾頭5種ボタン＋核威力3プリセットボタン＋
  「照準（発射）」ボタン。選択をハイライト。AlienInvasion.InvasionUI のパターン（`ButtonMenu` スプライト、
  eventClick、レベルロードで生成/破棄）を踏襲。選択は `MissileTool.CurrentWarhead` / `CurrentNuclearYield` に反映。
- `Game/UI/MissileTool.cs`: テンキー選択とOnToolGUIラベルを**撤去**。発射時に選択中の弾頭＋核威力を使用。
- `Game/Loading/MissileLoadingExtension.cs`: OnLevelLoaded で `MissilePanel.Create()`、OnLevelUnloading で `Destroy()`
  （静的状態をレベルまたぎで残さない）。
- `Game/ImpactResolver.cs`: クレーター半径/深さに上限（`CraterRadiusMax`/`CraterDepthMax`）を設け、
  戦略核でも地形を過剰破壊しない（NuclearMeltdown と同方針）。
- `Game/ModConfig.cs`: パネル寸法・位置・クレーター上限定数を追加。

## テスト戦略（TDD）

- `NuclearYieldTests`: Standard の係数=1.0、Tactical<1<Strategic、単調増加、正値、Multiplier(kt) の cbrt 関係。
- `WarheadSpecTests`: `Scaled(1)` は不変、`Scaled(2)` で各半径2倍・SubmunitionCount/フラグ/Type 不変、元 struct 不変。
- UI（UIPanel/UIButton）は実機確認（パネル表示、弾頭/威力選択→発射で反映、レベル再ロードで残留しない）。

## スレッド規律（不変）

UI はメインスレッド。着弾（威力反映済み spec の解決）は従来どおり sim スレッド。境界は `_impactQueue` のみ。

## 完了の定義

- Core テスト全緑（追加分含む）。ビルド＆デプロイ成功。テンキー選択は撤去済み。
- 実機: パネルで弾頭と核威力を選び、照準→クリックで発射。核は威力プリセットで規模が変わる。
  レベル再ロードでパネルが二重化・残留しない。
