using System;
using System.Collections.Generic;
using MissileDisaster.Core;
using MissileDisaster.Game.Models;
using UnityEngine;

namespace MissileDisaster.Game.Defense
{
    /// <summary>
    /// 迎撃施設3種を「実行時クローン」で登録する。災害サービスの設置建物をテンプレとしてクローンし
    /// （→ ビルドメニューの災害タブに入る）、メッシュ/名前/AI/マス/コスト/電力/水 を差し替える。
    /// m_generatedInfo・LOD・サムネイル atlas・シェーダーはテンプレ由来を継承（実行時再生成が壊れやすいため）。
    /// メインスレッド専用（OnLevelLoaded から冪等に呼ぶ）。
    ///
    /// 注意: 実行時 BuildingInfo クローンは半信頼な手法。実機で「災害タブ表示・設置・モデル表示・
    /// 非クラッシュ・セーブ再ロード耐性」を確認しながら進める。
    /// </summary>
    public static class CustomBuildingFactory
    {
        /// <summary>
        /// 1施設ぶんの仕様。迎撃高度: Aegis(Arrow帯)>THAAD(Sam帯)>PAC3(Pac帯)。
        /// レーダーサイトは迎撃せず、稼働中に迎撃確率へ SupportMultiplier を掛ける支援施設。
        /// </summary>
        private struct BuildingSpec
        {
            public string Name;     // 一意・セーブ間不変の prefab 名
            public string Model;    // Models/<Model>.obj
            public int CellW;
            public int CellL;
            public int Cost;        // 建設費(₡)
            public int PowerKw;     // 電力消費(kW)
            public int WaterM3;     // 水消費(m^3)
            public int Upkeep;      // 維持費
            public InterceptorKind Kind; // 迎撃層(高度帯)。IsRadar=true のときは未使用。
            public bool IsRadar;    // 支援(レーダー)施設か
            public float SupportMultiplier; // レーダーの迎撃確率倍率(既定 1)
        }

        // Kind の高度帯: Arrow=最高(超高高度), Sam=中(高高度), Pac=終端。
        private static readonly BuildingSpec[] Specs =
        {
            new BuildingSpec { Name = "MissileDisaster_PAC3",        Model = "Building_PAC3",      CellW = 3, CellL = 4, Cost = 320000, PowerKw = 100, WaterM3 =   0, Upkeep =  800, Kind = InterceptorKind.Pac,   IsRadar = false, SupportMultiplier = 1f },
            new BuildingSpec { Name = "MissileDisaster_THAAD",       Model = "Building_THAAD",     CellW = 5, CellL = 5, Cost = 400000, PowerKw = 480, WaterM3 = 240, Upkeep = 2000, Kind = InterceptorKind.Sam,   IsRadar = false, SupportMultiplier = 1f },
            new BuildingSpec { Name = "MissileDisaster_AegisAshore", Model = "Building_Aegis",     CellW = 6, CellL = 6, Cost = 600000, PowerKw = 480, WaterM3 = 240, Upkeep = 3000, Kind = InterceptorKind.Arrow, IsRadar = false, SupportMultiplier = 1f },
            new BuildingSpec { Name = "MissileDisaster_RadarSite",   Model = "Building_RadarSite", CellW = 6, CellL = 6, Cost = 500000, PowerKw = 600, WaterM3 = 240, Upkeep = 2500, Kind = InterceptorKind.Pac,   IsRadar = true,  SupportMultiplier = 1.5f },
        };

        // クローンした BuildingInfo（GameObject は DontDestroyOnLoad で存続）。PrefabCollection は
        // レベルロード毎に作り直されるため、クローンは初回のみ・登録は毎ロードで未登録時に再実行する。
        private static readonly Dictionary<string, BuildingInfo> _infos = new Dictionary<string, BuildingInfo>();
        private static BuildingInfo _template;

        /// <summary>迎撃施設をビルドメニュー(災害タブ)へ登録する（冪等）。</summary>
        public static void EnsureRegistered()
        {
            try
            {
                bool anyNew = false;
                for (int i = 0; i < Specs.Length; i++)
                {
                    if (EnsureOne(Specs[i])) anyNew = true;
                }
                // OnLevelLoaded 時点では toolbar パネルが未生成(実測 DisastersGroupPanel 数=0)なので、
                // 即時 refresh では反映できない。パネル生成後に PumpPanelRefresh() で反映する。
                if (anyNew) { _needPanelRefresh = true; _refreshTick = 0; _refreshScans = 0; }
            }
            catch (Exception e)
            {
                ModConfig.LogError("CustomBuildingFactory.EnsureRegistered error: " + e);
            }
        }

        private static bool _needPanelRefresh;
        private static int _refreshTick;
        private static int _refreshScans;

        /// <summary>
        /// メインスレッドから毎フレーム呼ぶ。災害タブのパネルが生成されたら RefreshPanel して新規建物
        /// ボタンを反映する（OnLevelLoaded では未生成のため遅延実行）。約0.5秒毎に走査し、見つかれば1回で終了。
        /// </summary>
        public static void PumpPanelRefresh()
        {
            if (!_needPanelRefresh) return;
            _refreshTick++;
            if (_refreshTick % 30 != 0) return; // 走査は間引く（FindObjectsOfTypeAll は重い）

            try
            {
                DisastersGroupPanel[] panels = Resources.FindObjectsOfTypeAll<DisastersGroupPanel>();
                if (panels != null && panels.Length > 0)
                {
                    for (int i = 0; i < panels.Length; i++)
                    {
                        if (panels[i] != null) panels[i].RefreshPanel();
                    }
                    _needPanelRefresh = false;
                    ModConfig.Log("CustomBuildingFactory: 災害パネル再生成 panels=" + panels.Length + " scans=" + _refreshScans);
                    return;
                }

                if (++_refreshScans > 120) // ~60秒探して諦める
                {
                    _needPanelRefresh = false;
                    ModConfig.LogError("CustomBuildingFactory: DisastersGroupPanel が生成されず、パネル反映を断念");
                }
            }
            catch (Exception e)
            {
                _needPanelRefresh = false;
                ModConfig.LogError("CustomBuildingFactory.PumpPanelRefresh error: " + e);
            }
        }

        /// <summary>1施設を確保・登録する。今回のセッションで新規登録したら true。</summary>
        private static bool EnsureOne(BuildingSpec spec)
        {
            BuildingInfo info;
            if (!_infos.TryGetValue(spec.Name, out info) || info == null)
            {
                BuildingInfo template = ResolveTemplate();
                if (template == null)
                {
                    ModConfig.LogError("CustomBuildingFactory: クローン元テンプレが見つかりません spec=" + spec.Name);
                    return false;
                }
                info = CloneBuilding(template, spec);
                if (info == null) return false;
                _infos[spec.Name] = info;
                ModConfig.Log("CustomBuildingFactory: クローン生成 name=" + info.name + " template=" + template.name);
            }

            // 今のセッションの PrefabCollection に未登録なら（初回 or 別セーブ再ロード）登録する。
            if (PrefabCollection<BuildingInfo>.FindLoaded(info.name) == null)
            {
                RegisterPrefab(info);
                ModConfig.Log("CustomBuildingFactory: 登録 name=" + info.name + " | " + Diag(info));
                LogRegistrationState(info); // 一覧に本当に載っているかの決定的確認
                return true;
            }
            return false;
        }

        /// <summary>登録後、ツールバーが走査する GetLoaded 集合に本当に入っているかを確認する（登録 vs フィルタの切り分け）。</summary>
        private static void LogRegistrationState(BuildingInfo info)
        {
            try
            {
                bool found = PrefabCollection<BuildingInfo>.FindLoaded(info.name) != null;
                bool inLoaded = false; int idx = -1;
                int count = PrefabCollection<BuildingInfo>.LoadedCount();
                for (int i = 0; i < count; i++)
                {
                    if (PrefabCollection<BuildingInfo>.GetLoaded((uint)i) == info) { inLoaded = true; idx = i; break; }
                }
                ModConfig.Log("  postReg name=" + info.name + " FindLoaded=" + found +
                    " inGetLoaded=" + inLoaded + " idx=" + idx + " prefabDataIndex=" + info.m_prefabDataIndex +
                    " loadedCount=" + count);
            }
            catch (Exception e)
            {
                ModConfig.LogError("LogRegistrationState error: " + e);
            }
        }

        /// <summary>トグルバー表示フィルタに効くフィールドを1行で出す診断。</summary>
        private static string Diag(BuildingInfo b)
        {
            if (b == null) return "null";
            string cls = b.m_class != null
                ? b.m_class.m_service + "/" + b.m_class.m_subService + "/L" + (int)b.m_class.m_level
                : "?";
            return "UICat='" + b.category + "' avail=" + b.m_availableIn +
                " place=" + b.m_placementStyle + "/" + b.m_placementMode +
                " class=" + cls + " thumb='" + b.m_Thumbnail + "' atlas=" + (b.m_Atlas != null ? "set" : "null") +
                " cells=" + b.m_cellWidth + "x" + b.m_cellLength + " unlockMs=" + (b.m_UnlockMilestone != null ? "set" : "null");
        }

        /// <summary>
        /// テンプレ取得。災害サービスかつ「地上設置(OnGround/OnTerrain)」の最小建物を優先する
        /// （水上ブイ=Tsunami Warning Buoy 等は地上建物に不適なので除外）。無ければ地上不問の災害建物、
        /// 既定名、最後に最小の PowerPlantAI へフォールバック。
        /// </summary>
        // タブに確実に写っている（メニュー表示が確実な）災害建物を優先テンプレにする。名前はパッチ差異があるため列挙も併用。
        private static readonly string[] PreferredTemplates =
        {
            "Radar Tower", "Space Radar", "Weather Radar", "Doppler Radar", "Earthquake Sensor",
        };

        private static BuildingInfo ResolveTemplate()
        {
            if (_template != null) return _template;

            // 0) 既知の「メニューに写る」災害建物を名前で優先取得。
            for (int i = 0; i < PreferredTemplates.Length; i++)
            {
                BuildingInfo pref = PrefabCollection<BuildingInfo>.FindLoaded(PreferredTemplates[i]);
                if (pref != null && pref.m_class != null && pref.m_class.m_service == ItemClass.Service.Disaster)
                {
                    _template = pref;
                    ModConfig.Log("CustomBuildingFactory: 優先テンプレ=" + pref.name + " | " + Diag(pref));
                    return _template;
                }
            }

            BuildingInfo groundDisaster = null; int groundCells = int.MaxValue;
            BuildingInfo anyDisaster = null; int anyCells = int.MaxValue;
            BuildingInfo smallestPower = null; int powerCells = int.MaxValue;
            var disasterNames = new System.Text.StringBuilder();

            int count = PrefabCollection<BuildingInfo>.LoadedCount();
            for (int i = 0; i < count; i++)
            {
                BuildingInfo b = PrefabCollection<BuildingInfo>.GetLoaded((uint)i);
                if (b == null || b.m_buildingAI == null || b.m_class == null) continue;
                if (b.m_placementStyle != ItemClass.Placement.Manual) continue;
                int cells = Mathf.Max(1, b.m_cellWidth) * Mathf.Max(1, b.m_cellLength);

                if (b.m_class.m_service == ItemClass.Service.Disaster)
                {
                    disasterNames.Append('[').Append(b.name).Append(":").Append(b.category).Append(']');
                    if (cells < anyCells) { anyCells = cells; anyDisaster = b; }
                    bool ground = b.m_placementMode == BuildingInfo.PlacementMode.OnGround
                        || b.m_placementMode == BuildingInfo.PlacementMode.OnTerrain;
                    if (ground && cells < groundCells) { groundCells = cells; groundDisaster = b; }
                }
                if (b.m_buildingAI is PowerPlantAI && cells < powerCells) { powerCells = cells; smallestPower = b; }
            }

            ModConfig.Log("CustomBuildingFactory: 災害建物候補=" + disasterNames);

            _template = groundDisaster ?? anyDisaster
                ?? PrefabCollection<BuildingInfo>.FindLoaded(ModConfig.FallbackBuildingTemplateName)
                ?? smallestPower;

            if (_template != null)
                ModConfig.Log("CustomBuildingFactory: テンプレ=" + _template.name + " | " + Diag(_template));
            else
                ModConfig.LogError("CustomBuildingFactory: テンプレ候補が見つかりません");
            return _template;
        }

        private static BuildingInfo CloneBuilding(BuildingInfo template, BuildingSpec spec)
        {
            GameObject go = UnityEngine.Object.Instantiate(template.gameObject);
            go.name = spec.Name;
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.SetActive(false);

            BuildingInfo info = go.GetComponent<BuildingInfo>();
            info.name = spec.Name;                  // FindLoaded のキー。一意・不変。
            info.m_prefabInitialized = false;

            // --- メッシュ差し替え（マテリアルはテンプレのシェーダーを継承＝マゼンタ回避） ---
            Mesh mesh = MissileModelProvider.LoadMergedMesh(spec.Model);
            if (mesh != null)
            {
                info.m_mesh = mesh;
                info.m_lodMesh = mesh;              // null 厳禁
            }
            else
            {
                ModConfig.LogError("CustomBuildingFactory: モデル未取得のためテンプレのメッシュを使用 model=" + spec.Model);
            }
            if (template.m_material != null)
            {
                Material mat = new Material(template.m_material); // Custom/Buildings/Building シェーダー継承
                info.m_material = mat;
                info.m_lodMaterial = mat;
            }

            // 敷地サイズ（マス）。土台の見た目はテンプレ由来だがサイズだけ仕様に合わせる。
            info.m_cellWidth = spec.CellW;
            info.m_cellLength = spec.CellL;
            info.m_placementStyle = ItemClass.Placement.Manual;
            info.m_placementMode = BuildingInfo.PlacementMode.OnGround; // 地上設置(テンプレが水上でも上書き)
            // m_UnlockMilestone/availableIn/UICategory/class/atlas/thumbnail はテンプレ継承。
            // ※ m_UnlockMilestone を null にするとパネル populate が NRE でこの建物をスキップし、
            //   タブに出なくなる（診断で clone だけ unlockMs=null かつ非表示だった）。テンプレ値を維持する。

            // --- AI 差し替え（存在・電力・維持・コストは PlayerBuildingAI に設定） ---
            BuildingAI oldAI = go.GetComponent<BuildingAI>();
            if (oldAI != null) UnityEngine.Object.DestroyImmediate(oldAI);
            InterceptorAI ai = go.AddComponent<InterceptorAI>();
            ai.Kind = spec.Kind;
            ai.IsRadar = spec.IsRadar;
            ai.SupportMultiplier = spec.SupportMultiplier;
            ai.m_info = info;
            ai.m_constructionCost = spec.Cost;
            ai.m_maintenanceCost = spec.Upkeep;
            ai.m_electricityConsumption = spec.PowerKw;
            ai.m_waterConsumption = spec.WaterM3;
            info.m_buildingAI = ai;

            return info;
        }

        private static void RegisterPrefab(BuildingInfo info)
        {
            info.m_prefabInitialized = false; // 別セッション再登録でも初期化し直す
            info.m_prefabDataIndex = -1;
            PrefabCollection<BuildingInfo>.InitializePrefabs("MissileDisaster", new[] { info }, null);
            PrefabCollection<BuildingInfo>.BindPrefabs();
            info.RefreshLevelOfDetail();
            info.gameObject.SetActive(true);
        }
    }
}
