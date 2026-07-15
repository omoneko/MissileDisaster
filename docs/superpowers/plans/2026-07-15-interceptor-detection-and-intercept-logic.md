# 迎撃施設「検出＋迎撃ロジック」実装計画（アセット方式・後継）

> 前提の転換: 迎撃施設は Asset Editor で作成した**正規アセット**（PAC3/THAAD/Aegis/Radar、命名統一済み）として
> ゲームに存在するようになった。実行時クローン方式（`CustomBuildingFactory`/`InterceptorAI`）は完全に不要になり撤去する。
> ユーザー決定: **コスト/電力/水/維持費はアセット側の設定をそのまま使う**（Mod は上書きしない）。
> Mod の役割は「設置された建物を**名前で検出**し、**迎撃ロジックを実行する**」ことに限定する。

## アーキテクチャ

- `Core/InterceptorNameMatcher.cs`（新規・TDD）: 建物名キーワード判定（UnityEngine非依存）。`NuclearMeltdown.Core.NuclearNameMatcher` と同一パターン。
  - PAC3→`InterceptorKind.Pac`、THAAD→`Sam`、Aegis/イージス→`Arrow`、Radar/レーダー→支援(IsRadar)。
- `Game/Defense/InterceptorRegistry.cs`（新規）: メインスレッド専用。`BuildingManager` を間引き走査（~1秒毎）し、名前一致した**稼働中**建物を追跡。クールダウンは毎フレーム減算。`TryIntercept(missilePos, targetGroundPos, out interceptPoint)` で高い帯から順に交戦圏＋確率判定（既存Core `InterceptDecision`/`InterceptorTiers`を使用）。レーダー稼働中は確率×1.5。
- `Game/Effects/InterceptFx.cs`（新規）: 迎撃成功時の簡易閃光（バニラ爆発エフェクトを流用、Alien `Effects.PlayImpactBurst` と同パターン）。
- `Game/Missile.cs`: `CurrentPosition` を公開（迎撃判定用）。
- `Game/MissileManager.cs`: `UpdateVisual` 内で `InterceptorRegistry.Tick`→各弾の迎撃判定→成功時は着弾enqueueせず消滅＋閃光。
- `Game/Simulation/MissileThreadingExtension.cs`: Ctrl+1..4 ホットキー・`PumpPanelRefresh` 呼び出しを削除（Asset Editor化で不要になった暫定コード）。
- `Game/Loading/MissileLoadingExtension.cs`: `CustomBuildingFactory.EnsureRegistered()` 呼び出しを削除。`InterceptorRegistry.Reset()` を load/unload で呼ぶ（`MissileManager.Reset()` と同じ静的状態衛生パターン）。
- **削除**: `Game/Defense/CustomBuildingFactory.cs`、`Game/Defense/InterceptorAI.cs`。
- `ModConfig.cs`: `FallbackBuildingTemplateName` 等クローン専用定数を削除。`RadarSupportMultiplier`/`InterceptorScanIntervalFrames`/`InterceptFlashMagnitude` を追加。

## スレッド規律（不変）

迎撃判定・建物走査・クールダウン管理は**すべてメインスレッド**（`MissileManager.UpdateVisual` と同じ側、飛来ミサイルGameObjectと同じ）。着弾ダメージ解決は従来どおり sim スレッド。両者の接点は既存の `_impactQueue`（ロック保護）のみで変更なし。

## テスト戦略

`InterceptorNameMatcher` を Core xUnit でテスト（大文字小文字無視・Workshop風の`ID.名前_Data`接頭辞/接尾辞混在・非該当建物）。`InterceptorRegistry`/`InterceptFx` はゲーム型依存のため実機確認（ビルド成功＋実際に施設を配置してミサイルを迎撃できるか）。

## 完了の定義

- Core テスト全緑（追加分含む）。ビルド＆デプロイ成功。
- 実機で PAC3/THAAD/Aegis を設置→飛来ミサイルが高度帯に応じて確率で迎撃され消滅＋閃光、すり抜けは通常着弾。
- レーダーサイト設置＋稼働で迎撃確率が体感的に上がる（数値検証は次段階の実機フィードバックで調整）。
