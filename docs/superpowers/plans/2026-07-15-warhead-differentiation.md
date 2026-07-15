# 弾頭種別の作り分け 実装計画

> 現状: `WarheadType` は Conventional/Cluster/WhitePhosphorus/Thermobaric/Nuclear の5種が定義済みだが、
> `WarheadSpec.For` はすべて通常弾頭と同値を返し、着弾挙動が同一。本計画で5種を差別化する。
> **利用可能な着弾API**は `DisasterHelpers.MakeCrater` / `DisasterHelpers.DestroyStuff` のみ（火災の直接発火APIや
> 放射能汚染は無い／大規模移植になる）。よって差別化は「クレーター形状・破壊範囲・子弾の散布」で表現する。
> 放射能汚染（Nuclear）は `Contaminates` フラグのみ立て、実際の汚染グリッドは後続の専用フェーズに委ねる
> （ユーザーが今回選んだのは「種別の作り分け」であり「核＋ガイガー音」は別選択肢）。

## アーキテクチャ

- `Core/WarheadSpec.cs`（拡張・TDD）: 種別ごとの数値表を実装。フィールド追加:
  `Type`, `SubmunitionCount`（1=単一着弾, >1=子弾散布）, `SpreadRadius`（子弾散布半径）,
  `RaiseCraterEdges`（縁を持ち上げるか）, `Contaminates`（核のみ, 実汚染は後続）。既存3値は維持。
- `Core/SubmunitionScatter.cs`（新規・TDD）: 子弾散布点を**決定論的**に配置する純粋関数（乱数不使用＝再現可能）。
  向日葵配置(phyllotaxis): `angle=k*137.5°`, `r=SpreadRadius*sqrt((k+0.5)/count)`。`Offset2[]` を返す。
  count<=1 は原点1点。全点が SpreadRadius 内（数値安定のため半径は sqrt により均等分布）。
- `Game/ImpactResolver.cs`（改修）: `spec.SubmunitionCount<=1` は従来どおり単一着弾。>1 は散布点ごとに
  小クレーター＋範囲破壊を適用。`MakeCrater` の raiseEdges に `spec.RaiseCraterEdges` を渡す。
  Nuclear は `Contaminates` をログするのみ（実汚染は後続フェーズ）。sim スレッド契約は不変。

## 種別ごとの数値（ゲーム調整の暫定値, m）

| 種別 | Crater R/D | Destroy R | 子弾数 | 散布R | 縁上げ | 汚染 | 意図 |
|---|---|---|---|---|---|---|---|
| Conventional | 60 / 16 | 120 | 1 | 0 | no | no | 現状維持(基準) |
| Cluster | 18 / 5 | 45(各) | 9 | 160 | no | no | 広く浅い多点被害 |
| WhitePhosphorus | 10 / 3 | 40(各) | 12 | 140 | no | no | 焼夷弾の広域散布(火災は散点破壊で近似) |
| Thermobaric | 70 / 10 | 220 | 1 | 0 | yes | no | 過圧で建物を薙ぎ倒す最大破壊 |
| Nuclear | 150 / 40 | 380 | 1 | 0 | yes | yes | 巨大クレーター＋広域壊滅(＋汚染フラグ) |

## テスト戦略（TDD, RED→GREEN）

- `WarheadSpecTests`: 各種別が期待フィールドを返す／Conventional は従来値維持／Nuclear のみ Contaminates／
  Cluster・WP は SubmunitionCount>1 かつ SpreadRadius>0／Thermobaric・Nuclear は RaiseCraterEdges。
- `SubmunitionScatterTests`: count 一致／全点が SpreadRadius 以内／同入力で同出力(決定論)／count<=1 は原点1点／
  SpreadRadius=0 は全点原点。
- `ImpactResolver` は DisasterHelpers 依存のため実機確認（各種別で着弾を撃ち、被害形状の違いを目視）。

## 完了の定義

- Core テスト全緑（追加分含む）。ビルド＆デプロイ成功。
- 実機で5種の着弾差（単一大クレーター / 広域多点 / 過圧薙ぎ倒し / 巨大壊滅）が目視で確認できる。
- 火災の実発火・放射能汚染グリッドは本フェーズ対象外（後続フェーズ）と明記済み。
