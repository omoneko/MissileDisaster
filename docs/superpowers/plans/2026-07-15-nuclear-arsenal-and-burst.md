# 核兵器カタログ＋kt入力＋空中/地上爆発 実装計画

> 参照: ja.wikipedia.org/wiki/核兵器一覧, nukesimulator.com/ja。
> ユーザー要望: (1) 代表的核兵器10種から選択、(2) kt単位の数値入力で出力指定、(3) 空中爆発/地上爆発の選択。
> 従来の3プリセット(戦術/標準/戦略)はカタログ＋数値入力に置換する。

## 代表核兵器カタログ（10種・kt昇順・出典の代表値）

| # | 名称 | 威力 | 備考 |
|---|---|---|---|
| 1 | リトルボーイ | 15 kt | 広島 |
| 2 | ファットマン | 22 kt | 長崎 |
| 3 | トリニティ | 25 kt | 世界初の核実験 |
| 4 | W87 | 300 kt | ミニットマンIII |
| 5 | B61 | 340 kt | 可変・最大 |
| 6 | W88 | 475 kt | トライデントII |
| 7 | B83 | 1,200 kt | 米現役最大級 |
| 8 | アイビー・マイク | 10,400 kt | 初の水爆 |
| 9 | キャッスル・ブラボー | 15,000 kt | 米最大の核実験 |
| 10 | ツァーリ・ボンバ | 50,000 kt | 史上最大 |

## 空中爆発 / 地上爆発（物理準拠）

- **地上爆発(Groundburst)**: クレーターあり＋放射性降下物(汚染)あり。従来の核挙動。
- **空中爆発(Airburst)**: クレーター無し・汚染ほぼ無し。ただし爆風/熱線で**破壊・延焼が広がる**（×AirBurstBlastFactor≈1.35）。
  広島/長崎は空中爆発で被害面積を最大化した。核以外の弾頭にも適用可（空中=クレーター無し＋広域、地上=クレーター有り）。

## Architecture

- `Core/NuclearWeapons.cs`（新規・TDD）: `NuclearWeapon{Name,Kilotons}` と `Catalog`(10種, kt昇順)。純粋。
- `Core/NuclearYield.cs`（整理）: 3プリセット enum を撤去し、`NuclearYields.Multiplier(int kt)`＋`StandardKilotons` のみ残す
  （kt→スケール係数 = cbrt(kt/150)）。数値入力・カタログ選択の双方がこの1関数を使う。
- `Core/BurstType.cs`（新規）: enum `{ Airburst, Groundburst }`。
- `Core/WarheadSpec.cs`: `WithBurst(BurstType)` を追加（Airburst はクレーター/汚染を0にし破壊・延焼を×1.35した**新struct**）。不変。
- `Game/UI/MissileTool.cs`: `CurrentYieldKilotons(int=150)`, `CurrentBurst(BurstType=Groundburst)`。発射時に
  `Multiplier(kt)` と `burst` を使用。
- `Game/MissileManager.cs`: `Launch(target, type, yieldMultiplier, burst)`。spec=For(type)→核なら Scaled(mult)→WithBurst(burst)。
- `Game/UI/MissilePanel.cs`: 核威力セクションを刷新。
  - 核兵器カタログ(10種)を UIDropDown で選択（選ぶと kt を反映）。
  - kt 数値入力（UITextField, 整数, 1以上）。手入力が最優先。
  - 空中/地上を2ボタンでトグル（ハイライト）。全弾頭に効く。
  - 現在の kt/種別/爆発高度を表示。

## Testing (test-first)

- `NuclearWeaponsTests`: 10件・全 kt>0・名称非空・kt昇順・既知値(リトルボーイ15/ツァーリ50000)。
- `NuclearYieldTests`: `Multiplier(150)=1`, cbrt 関係, 単調増加, 正値（enumテストは撤去）。
- `WarheadSpecTests`: `WithBurst(Ground)` 不変、`WithBurst(Air)` はクレーター0・汚染0・破壊/延焼増、元 struct 不変。
- UI(UIDropDown/UITextField/UIButton) は実機確認。

## Definition of done

- Core テスト全緑。ビルド＆デプロイ成功。
- 実機: カタログ選択 or kt手入力で威力可変、空中/地上で挙動差（クレーター有無・汚染有無・破壊広がり）が出る。
