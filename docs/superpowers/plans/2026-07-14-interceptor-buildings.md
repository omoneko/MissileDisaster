# 迎撃施設フェーズ 実装計画（正規建物・実行時クローン）

> 設計書 `2026-07-13-flight-and-defense-design.md` の 2B(建物メッシュ)＋2D(建物+AI+レジストリ+判定)＋2E(迎撃弾+爆発) を実装する。
> ユーザー決定: 施設は **正規建物方式（実行時クローン）**。順序=飛来弾(済)→施設。PAC施設メッシュ=PAC3。

## 方針と既知のリスク（重要）

CS初代で**コードのみの正規 BuildingInfo 生成は「半信頼」**（コミュニティ調査結論）。壊れやすいのは `m_generatedInfo` / LOD / サムネイルatlas。確実に近づける唯一の道は:
- **同フットプリントのバニラ建物をテンプレとしてクローン**（例: `Wind Turbine`＝`PowerPlantAI`。小型・電力・維持費あり）。
- **メッシュ/マテリアル/名前/コスト/AI だけ差し替え**、`m_generatedInfo`/atlas/thumbnail/**シェーダー**はテンプレ由来を継承。
- 名前は**セーフ&セーブ間で不変**、登録は `InitializePrefabs`+`BindPrefabs`、`OnLevelLoaded` で**冪等**に。
- マテリアルはテンプレの `m_material` を複製（`Custom/Buildings/Building` シェーダー）→ マゼンタ回避。

**だから最初のサブ増分 S1 を「歩く骨格」にして、実機で建物が出る/置ける/クラッシュしないを先に確認する。** S1 が通らなければ方式を再検討（ツール設置サイト方式へ退避、or アセットバンドル）。

## スレッド規律

- 建物 AI の `SimulationStep` は **sim スレッド**。迎撃判定は **メインスレッド**（`MissileManager.UpdateVisual`、飛来弾 GameObject と同じ側）。
- 従って **InterceptorRegistry はメインスレッドが `BuildingManager` を間引き走査して更新**する（sim スレッドから登録しない＝ロック不要）。稼働中(建設完了・電力OK)の迎撃建物だけを列挙。

---

## S1: 歩く骨格 — 建物を1つ設置できる（PAC3, デリスク最優先）

**Files:**
- Blender 書き出し: `src/MissileDisaster/Models/Building_PAC3.obj`(+.mtl)（直立=up_axis Y、原点・接地）
- Create: `src/MissileDisaster/Game/Defense/InterceptorAI.cs`（`PlayerBuildingAI` 派生の最小AI。`InterceptorKind` を保持）
- Create: `src/MissileDisaster/Game/Defense/CustomBuildingFactory.cs`（テンプレ列挙→クローン→差し替え→登録）
- Modify: `src/MissileDisaster/Game/ModConfig.cs`（建物定数: テンプレ候補名・建物名・コスト・維持費・モデル名・スケール）
- Modify: `src/MissileDisaster/Game/Loading/MissileLoadingExtension.cs`（`OnLevelLoaded` で `CustomBuildingFactory.EnsureRegistered()` を冪等呼び出し、gated LoadMode）

**CustomBuildingFactory.EnsureRegistered():**
1. 冪等ガード（登録済みなら return）。
2. テンプレ取得: `PrefabCollection<BuildingInfo>.FindLoaded("Wind Turbine")`。null なら loaded を列挙して `PowerPlantAI` かつ最小 cell の建物を選ぶ（フォールバック）。
3. `Object.Instantiate(template.gameObject)`→`DontDestroyOnLoad`→`SetActive(false)`。
4. `info.name` を一意・不変("MissileDisaster_PAC3")。`m_prefabInitialized=false`。
5. メッシュ/マテリアル: `MissileModelProvider.CreateInstance("Building_PAC3")` で得た Mesh/Material を使う…**が建物は Custom/Buildings/Building シェーダーが要る**ため、マテリアルは `new Material(template.m_material)` を複製し `mainTexture` に我々のメッシュ色を持たせる（テクスチャ無しなら色のみ）。`m_mesh`/`m_lodMesh`=我々, `m_material`/`m_lodMaterial`=複製。
   - `m_generatedInfo`/`m_cellWidth`/`m_cellLength`/`m_Atlas`/`m_Thumbnail`/`m_class`/`m_collisionHeight` は**テンプレ継承**。`m_placementStyle=Manual`。
6. AI 差し替え: 既存 `BuildingAI` を `DestroyImmediate`→`AddComponent<InterceptorAI>()`。`ai.m_info=info; info.m_buildingAI=ai;` コスト/維持費を AI に設定。`InterceptorKind=Pac`。
7. 登録: `info.m_prefabDataIndex=-1; PrefabCollection<BuildingInfo>.InitializePrefabs("MissileDisaster", info, null); BindPrefabs(); info.RefreshLevelOfDetail(); go.SetActive(true);`
8. メニューに出ない場合（遅延登録）は該当 `GeneratedScrollPanel.RefreshPanel()`（電力タブ）。ログで確認。

**InterceptorAI:** `PlayerBuildingAI` 派生。`public InterceptorKind Kind;`。S1 では挙動は基底委譲のみ（存在・電力・維持）。将来 S2 でレジストリ用の状態参照に使う。

**検証（ユーザー・実機）:** 電力タブに建物が出る→設置できる→**PAC3モデルが表示**（マゼンタでない）→電力/コスト動作→クラッシュ無し。ログに登録メッセージ。**ここが通ってから S2 以降へ。**

---

## S2: 迎撃判定の配線（検出→ミサイル消滅＋簡易フラッシュ）

**Files:** Create `Game/Defense/InterceptorRegistry.cs`; Modify `MissileManager.cs`, `Missile.cs`(現在位置プロパティ), `Simulation/MissileThreadingExtension.cs`（レジストリ更新駆動）, `Effects/`（簡易フラッシュ）。

- `InterceptorRegistry`（メインスレッド専用）: `Refresh()` が `BuildingManager.instance.m_buildings` を間引き走査し、`InterceptorAI` かつ稼働中(完了・電力OK)の建物の {位置, Kind→InterceptorTier, クールダウン残} を収集。`TryConsume(...)` でクールダウン管理。
- `MissileManager.UpdateVisual`（メイン）: 各飛来弾について高い帯から順に、圏内(高度帯∩水平射程)＆クールダウン明けの建物で `InterceptDecision.ShouldIntercept(alt, dist, tier, roll)`（roll=`SimulationManager.instance.m_randomizer`…はsim。メインは `UnityEngine.Random`）。成功→弾を消滅（着弾 enqueue せず DestroyVisual）、建物クールダウン開始、`Effects.PlayInterceptFlash(交会点)`。
- 弾の高度=`pos.y - Target.y`、水平距離=建物との XZ 距離。
- **検証:** 建物設置→発射→時々迎撃(消滅+閃光)・時々すり抜け。帯/確率は定数調整。

---

## S3: 3施設化（ARROW/SM/PAC 各1建物）

- Blender: `Building_VLS_ARROW.obj` / `Building_VLS_SM.obj` 追加書き出し。
- `CustomBuildingFactory` を3種登録に一般化（Kind→モデル名/建物名/コスト/テンプレ）。各 `InterceptorAI.Kind` を設定。
- `InterceptorRegistry` は Kind→`InterceptorTiers` で帯・射程・確率・CD を引く（既存 Core を使用）。
- **検証:** 3種を設置、高度帯で担当が分かれて迎撃（ARROW=超高高度→SAM→PAC=終端）。

---

## S4: 迎撃弾の飛翔＋爆発演出

- Create `Game/Defense/InterceptorShot.cs`（迎撃弾モデル `Interceptor_ARROW/SM/PAC` を建物→交会点へ上昇飛翔、機首+Z を進行方向へ＝既存 LookRotation 手法）。
- Create `Game/Effects/InterceptFx.cs`（交会点で爆発フラッシュ＋両者消滅）。`MissileTrail` の資産解決を流用可。
- 迎撃成功時 S2 の簡易フラッシュを InterceptorShot＋InterceptFx に置換。
- **検証:** 迎撃時、施設から迎撃弾が上がり交会点で爆発、飛来弾が消える。

---

## Definition of done

- S1: 建物が設置でき、モデル表示・電力・非クラッシュ（実機・ユーザー）。
- S2–S4: 3施設で高度帯別に確率迎撃、迎撃弾飛翔＋爆発、すり抜けは通常着弾。Core テスト維持。
- 各サブ増分ごとに ビルド＆デプロイ→レビュー→通常コミット（Codexフック尊重、`--no-verify` 禁止）。

## Risksと退避

- S1 で建物が出ない/クラッシュ/マゼンタ/サムネ壊れ → 早期フェーズ登録(PrefabHook/Harmony)へ、それでも駄目ならツール設置サイト方式 or アセットバンドルへ退避（ユーザーへ相談）。
