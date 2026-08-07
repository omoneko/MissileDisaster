# 火災・放射能汚染の本実装 計画

> ユーザー選択: 「火災・放射能汚染の本実装」。焼夷系(白リン/サーモバリック)の実火災と、核の放射能汚染を実装する。
> 独立した2サブシステムのため **2段階**で進める（各段階でビルド→実機確認）。

## 重要な前提（調査結果）

- **火災は既存APIで実現できる**: `DisasterHelpers.DestroyStuff(seed, null, pos, preR, totalR, removeR, destMin, destMax, burnMin, burnMax)`
  の末尾2引数 `burnMin/burnMax` が「延焼帯」で、この範囲の建物は破壊ではなく**着火**する（現行ミサイルも
  `burnMin=r*0.3, burnMax=r*0.6` で既に少量着火している）。よって新しい火災システムは不要で、弾頭ごとに
  延焼半径を調整すればよい。`preR/totalR` は処理外周（= max(destMax, burnMax) にしないと外側が処理されない）。
- **放射能汚染は土壌汚染フィールド方式**: NuclearMeltdown が使う `NaturalResourceManager.m_naturalResources[i].m_pollution`
  （0-255）へ円形に書き込む。ゲームのセーブに自然に含まれ、汚染オーバーレイに可視化される。
  Core の座標計算(`PollutionGrid`)は純粋・テスト可能。適用は sim スレッド（NaturalResourceManager 書込み）。

## 段階1: 火災（焼夷差の作り込み）

- `Core/WarheadSpec.cs`: `BurnRadius`（延焼の外縁・m）を追加。値:
  通常72 / クラスター30 / 白リン90 / サーモバリック260 / 核420。白リンは破壊<<延焼（焼夷弾らしさ）。
- `Game/ImpactResolver.cs`: `ApplyBlast` の DestroyStuff 呼び出しを延焼帯対応に:
  `outer=max(destR, BurnRadius)`, `burnMin=min(destR*0.3, BurnRadius*0.5)`, `burnMax=BurnRadius`。
  通常弾の見た目は現行維持（BurnRadius=72 で従来と同値）。
- テスト: 白リンは BurnRadius>DestructionRadius（延焼が破壊を上回る）、サーモバリック>通常、核が最大、全値>=0。

## 段階2: 放射能汚染（核のみ）

- `Core/CellDose.cs`（新規・移植）: `{ int Index; byte Intensity; }`。
- `Core/PollutionGrid.cs`（新規・移植・TDD）: CellSize=33.75, Resolution=512, WorldToCell/CellIndex/CellsInRadius
  （中心 max→端 0 の線形減衰）。UnityEngine 非依存。
- `Game/Contamination/PollutionField.cs`（新規）: NaturalResourceManager への読み書き＋AreaModifiedB でテクスチャ更新。
- `Game/Contamination/ContaminationManager.cs`（新規・簡易版）: `Apply(centerX, centerZ, radius)` で汚染を書き込む。
  v1 は減衰/独自セーブを持たない（土壌汚染はゲームのセーブに含まれるため永続。減衰は後続で検討）。
- `Core/WarheadSpec.cs`: `ContaminationRadius`（核のみ>0, 例 460m）を追加。
- `Game/ImpactResolver.cs`: `spec.Contaminates` 時に `ContaminationManager.Apply(target.x, target.z, spec.ContaminationRadius)`
  を呼ぶ（sim スレッド）。既存の「ログのみ」を実適用に置換。
- テスト: PollutionGrid の座標/半径列挙/減衰/範囲外除外。WarheadSpec の ContaminationRadius（核>0・他=0）。

## Thread discipline (unchanged)

火災・汚染とも着弾処理の一部＝**sim スレッド**（ImpactResolver, DisasterHelpers/NaturalResourceManager と同じ側）。
飛翔・迎撃・GameObject 生成はメインのまま。境界は既存の `_impactQueue` のみで変更なし。

## 対象外（明記）

- ガイガーカウンター音（`freesound_...geiger...mp3`）は本計画対象外。汚染の可視/聴覚演出強化は後続。
- 汚染の時間減衰・独自セーブ台帳は v1 では持たない（NuclearMeltdown の除染ロジックは将来移植候補）。

## Definition of done

- Every Core test is green, including the new ones, and the build and deployment succeed.
- 実機: 白リン/サーモバリックで広範囲に火災、核で巨大火災＋汚染オーバーレイが残る。通常弾の見た目は不変。
