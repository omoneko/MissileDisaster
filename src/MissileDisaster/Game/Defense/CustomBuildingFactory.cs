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
        /// <summary>1施設ぶんの仕様。迎撃高度: Aegis(Arrow帯)>THAAD(Sam帯)>PAC3(Pac帯)。</summary>
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
            public InterceptorKind Kind; // 迎撃層(高度帯)

            public BuildingSpec(string name, string model, int cw, int cl, int cost, int power, int water, int upkeep, InterceptorKind kind)
            {
                Name = name; Model = model; CellW = cw; CellL = cl;
                Cost = cost; PowerKw = power; WaterM3 = water; Upkeep = upkeep; Kind = kind;
            }
        }

        // Kind の高度帯: Arrow=最高(超高高度), Sam=中(高高度), Pac=終端。
        private static readonly BuildingSpec[] Specs =
        {
            new BuildingSpec("MissileDisaster_PAC3",       "Building_PAC3",  3, 4, 320000, 100,   0,  800, InterceptorKind.Pac),
            new BuildingSpec("MissileDisaster_THAAD",      "Building_THAAD", 5, 5, 400000, 480, 240, 2000, InterceptorKind.Sam),
            new BuildingSpec("MissileDisaster_AegisAshore","Building_Aegis", 6, 6, 600000, 480, 240, 3000, InterceptorKind.Arrow),
        };

        // クローンした BuildingInfo（GameObject は DontDestroyOnLoad で存続）。PrefabCollection は
        // レベルロード毎に作り直されるため、クローンは初回のみ・登録は毎ロードで未登録時に再実行する。
        private static readonly Dictionary<string, BuildingInfo> _infos = new Dictionary<string, BuildingInfo>();
        private static BuildingInfo _template;

        /// <summary>迎撃施設3種をビルドメニューへ登録する（冪等）。</summary>
        public static void EnsureRegistered()
        {
            try
            {
                for (int i = 0; i < Specs.Length; i++)
                {
                    EnsureOne(Specs[i]);
                }
            }
            catch (Exception e)
            {
                ModConfig.LogError("CustomBuildingFactory.EnsureRegistered error: " + e);
            }
        }

        private static void EnsureOne(BuildingSpec spec)
        {
            BuildingInfo info;
            if (!_infos.TryGetValue(spec.Name, out info) || info == null)
            {
                BuildingInfo template = ResolveTemplate();
                if (template == null)
                {
                    ModConfig.LogError("CustomBuildingFactory: クローン元テンプレが見つかりません spec=" + spec.Name);
                    return;
                }
                info = CloneBuilding(template, spec);
                if (info == null) return;
                _infos[spec.Name] = info;
                ModConfig.Log("CustomBuildingFactory: クローン生成 name=" + info.name + " template=" + template.name);
            }

            // 今のセッションの PrefabCollection に未登録なら（初回 or 別セーブ再ロード）登録する。
            if (PrefabCollection<BuildingInfo>.FindLoaded(info.name) == null)
            {
                RegisterPrefab(info);
                ModConfig.Log("CustomBuildingFactory: 登録 name=" + info.name);
            }
        }

        /// <summary>
        /// テンプレ取得。まず災害サービスの設置建物（→災害タブ）を探す。無ければ既定名、
        /// それも無ければ最小の PowerPlantAI 建物へフォールバック（DLC 無しなど）。
        /// </summary>
        private static BuildingInfo ResolveTemplate()
        {
            if (_template != null) return _template;

            BuildingInfo disaster = null;
            BuildingInfo smallestPower = null;
            int disasterCells = int.MaxValue;
            int powerCells = int.MaxValue;

            int count = PrefabCollection<BuildingInfo>.LoadedCount();
            for (int i = 0; i < count; i++)
            {
                BuildingInfo b = PrefabCollection<BuildingInfo>.GetLoaded((uint)i);
                if (b == null || b.m_buildingAI == null || b.m_class == null) continue;
                if (b.m_placementStyle != ItemClass.Placement.Manual) continue;
                int cells = Mathf.Max(1, b.m_cellWidth) * Mathf.Max(1, b.m_cellLength);

                if (b.m_class.m_service == ItemClass.Service.Disaster && cells < disasterCells)
                {
                    disasterCells = cells; disaster = b;
                }
                if (b.m_buildingAI is PowerPlantAI && cells < powerCells)
                {
                    powerCells = cells; smallestPower = b;
                }
            }

            if (disaster != null) { _template = disaster; ModConfig.Log("CustomBuildingFactory: 災害テンプレ=" + disaster.name); return _template; }

            BuildingInfo byName = PrefabCollection<BuildingInfo>.FindLoaded(ModConfig.FallbackBuildingTemplateName);
            if (byName != null) { _template = byName; ModConfig.Log("CustomBuildingFactory: 既定テンプレ=" + byName.name + "（災害建物が見つからず）"); return _template; }

            _template = smallestPower;
            if (_template != null) ModConfig.Log("CustomBuildingFactory: フォールバックテンプレ=" + _template.name);
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
            // m_generatedInfo / atlas / thumbnail / m_class(=災害タブ) はテンプレ継承。

            // --- AI 差し替え（存在・電力・維持・コストは PlayerBuildingAI に設定） ---
            BuildingAI oldAI = go.GetComponent<BuildingAI>();
            if (oldAI != null) UnityEngine.Object.DestroyImmediate(oldAI);
            InterceptorAI ai = go.AddComponent<InterceptorAI>();
            ai.Kind = spec.Kind;
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
