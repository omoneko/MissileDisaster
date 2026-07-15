# 放射能汚染の本実装（原発MOD準拠・汚水処理場除染なし）計画

> ユーザー要望: 放射能汚染を NuclearMeltdown（原発MOD）の本格実装準拠で移植。
> **基本設定は原発MOD準拠**（濃度255・50年で期限切れ・自然減衰対策の維持reassert・セーブ永続）。
> **唯一の変更**: 汚水処理場（Water Treatment）による除染を無効化する（＝除染されない）。
> 汚染半径は本Mod既存の弾頭ベース（WarheadSpec.ContaminationRadius、地上核のみ>0）を使う。

## 移植する仕組み（除染を除く）

- ゾーン台帳（ContaminationManager）: AddZone/ReassertZone/ClearZone/RemoveZoneAt/Zones/ReplaceAll。
  着弾でゾーン追加→土壌汚染グリッド(NaturalResourceManager.m_pollution)へ書き込み。
- 維持（毎tick間引き, sim スレッド）: 期限(50年)切れは Clear＋除去、それ以外は ReassertZone で自然減衰を打ち消す。
  **除染判定(IsDecontaminationActive)・DecontaminateZone・ReducePollution は移植しない**（汚水処理場で除染されない）。
- セーブ永続（ISerializableData）: ゾーン台帳を byte[] 直列化して保存/復元。土壌汚染自体もゲームのセーブに含まれる。

## ファイル

Core（純粋・TDD）:
- `Core/ContaminationZone.cs`（新規・移植）: {CenterX,CenterZ,Radius,StartTicks}
- `Core/ContaminationClock.cs`（新規・移植）: HasExpired(start, now, years)
- `Core/ZoneSerializer.cs`（新規・移植）: Serialize/Deserialize（バージョン付き・破損時空）
- 既存: PollutionGrid, CellDose

Game:
- `Game/Contamination/ContaminationManager.cs`（書き換え）: 簡易Apply→台帳版。Maintain(nowTicks) で期限＋reassert（除染なし）。
  半径は MaxContaminationRadius でクランプ。radius<=0 は無視（空中爆発）。
- `Game/Contamination/PollutionField.cs`: ClearCell を追加（期限クリア用）。ApplyDose/Refresh は既存。
- `Game/Serialization/ContaminationDataExtension.cs`（新規）: OnSaveData/OnLoadData。
- `Game/Simulation/MissileThreadingExtension.cs`: OnAfterSimulationTick に汚染維持を追加（間引き）。
- `Game/Loading/MissileLoadingExtension.cs`: OnLevelUnloading で ContaminationManager.Reset()（ロード時は消さない=OnLoadDataを尊重）。
- `Game/ImpactResolver.cs`: Apply → AddZone(new ContaminationZone(x,z,radius,nowTicks))。
- `Game/ModConfig.cs`: ContaminationExpiryYears=50、ContaminationMaintainInterval（tick間引き）を追加。

## 性能上の注意

弾頭ベースの汚染半径は原発MOD(700m)より大きい（標準核で数km）。reassert を毎tickすると重いので
ContaminationMaintainInterval で十分間引く（自然減衰対策には数秒毎で足りる）。

## テスト

- `ContaminationClockTests`: 期限前=false / 期限後=true。
- `ZoneSerializerTests`: 往復一致 / バージョン不一致=空 / 破損=空。
- 実機: 地上核で汚染が残る、汚水処理場を建てても除染されない、セーブ/ロードで汚染ゾーンが復元、50年で消滅。

## 完了の定義

- Core テスト全緑。ビルド＆デプロイ成功。汚水処理場で除染されないことを実機確認。
