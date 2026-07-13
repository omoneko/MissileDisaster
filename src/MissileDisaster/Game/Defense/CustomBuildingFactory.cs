using System;
using MissileDisaster.Core;
using MissileDisaster.Game.Models;
using UnityEngine;

namespace MissileDisaster.Game.Defense
{
    /// <summary>
    /// 迎撃施設を「実行時クローン」で登録する。バニラ小型建物(既定: Wind Turbine)をテンプレとして
    /// クローンし、メッシュ/名前/AI だけ差し替える。m_generatedInfo・LOD・サムネイル atlas・footprint・
    /// マテリアルのシェーダーはテンプレ由来を継承する（これらは実行時再生成が壊れやすいため）。
    /// メインスレッド専用（OnLevelLoaded から冪等に呼ぶ）。
    ///
    /// 注意: これは半信頼な手法（コミュニティ調査結論）。S1 では PAC3 1種のみ登録し、実機で
    /// 「メニュー表示・設置・モデル表示・非クラッシュ」を確認してから S3 で3種へ一般化する。
    /// </summary>
    public static class CustomBuildingFactory
    {
        // クローンした BuildingInfo（GameObject は DontDestroyOnLoad で存続）。PrefabCollection は
        // レベルロード毎に作り直されるため、bool 一発ではなく毎ロードで「今のセッションに未登録なら再登録」する。
        private static BuildingInfo _info;

        /// <summary>PAC3 迎撃施設をビルドメニューへ登録する。クローンは初回のみ、登録は毎ロードで必要なら再実行（冪等）。</summary>
        public static void EnsureRegistered()
        {
            try
            {
                if (_info == null)
                {
                    BuildingInfo template = ResolveTemplate();
                    if (template == null)
                    {
                        ModConfig.LogError("CustomBuildingFactory: クローン元テンプレが見つかりません");
                        return;
                    }
                    _info = CloneBuilding(template, ModConfig.PacBuildingName,
                        ModConfig.PacBuildingModelName, InterceptorKind.Pac);
                    if (_info == null) return;
                    ModConfig.Log("CustomBuildingFactory: 迎撃施設をクローン生成 name=" + _info.name +
                        " template=" + template.name);
                }

                // 今のセッションの PrefabCollection に未登録なら（初回 or 別セーブ再ロード）登録する。
                if (PrefabCollection<BuildingInfo>.FindLoaded(_info.name) == null)
                {
                    RegisterPrefab(_info);
                    ModConfig.Log("CustomBuildingFactory: 迎撃施設を登録しました name=" + _info.name);
                }
            }
            catch (Exception e)
            {
                ModConfig.LogError("CustomBuildingFactory.EnsureRegistered error: " + e);
            }
        }

        /// <summary>テンプレ取得。既定名で見つからなければ、最小フットプリントの PowerPlantAI 建物を列挙で探す。</summary>
        private static BuildingInfo ResolveTemplate()
        {
            BuildingInfo byName = PrefabCollection<BuildingInfo>.FindLoaded(ModConfig.BuildingTemplateName);
            if (byName != null) return byName;

            BuildingInfo best = null;
            int bestCells = int.MaxValue;
            int count = PrefabCollection<BuildingInfo>.LoadedCount();
            for (int i = 0; i < count; i++)
            {
                BuildingInfo b = PrefabCollection<BuildingInfo>.GetLoaded((uint)i);
                if (b == null || b.m_buildingAI == null) continue;
                if (!(b.m_buildingAI is PowerPlantAI)) continue;
                int cells = Mathf.Max(1, b.m_cellWidth) * Mathf.Max(1, b.m_cellLength);
                if (cells < bestCells) { bestCells = cells; best = b; }
            }
            if (best != null) ModConfig.Log("CustomBuildingFactory: フォールバックテンプレ=" + best.name);
            return best;
        }

        private static BuildingInfo CloneBuilding(BuildingInfo template, string uniqueName, string modelName, InterceptorKind kind)
        {
            GameObject go = UnityEngine.Object.Instantiate(template.gameObject);
            go.name = uniqueName;
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.SetActive(false);

            BuildingInfo info = go.GetComponent<BuildingInfo>();
            info.name = uniqueName;                 // FindLoaded のキー。一意・不変。
            info.m_prefabInitialized = false;

            // --- メッシュ差し替え（マテリアルはテンプレのシェーダーを継承＝マゼンタ回避） ---
            Mesh mesh = MissileModelProvider.LoadMergedMesh(modelName);
            if (mesh != null)
            {
                info.m_mesh = mesh;
                info.m_lodMesh = mesh;              // null 厳禁
            }
            else
            {
                ModConfig.LogError("CustomBuildingFactory: モデル未取得のためテンプレのメッシュを使用 model=" + modelName);
            }
            if (template.m_material != null)
            {
                Material mat = new Material(template.m_material); // Custom/Buildings/Building シェーダー継承
                info.m_material = mat;
                info.m_lodMaterial = mat;
            }

            // footprint / generatedInfo / atlas / thumbnail / class / 配置 はテンプレ継承（壊れやすいため）
            info.m_placementStyle = ItemClass.Placement.Manual;

            // --- AI 差し替え（存在・電力・維持は PlayerBuildingAI 基底に委譲） ---
            BuildingAI oldAI = go.GetComponent<BuildingAI>();
            if (oldAI != null) UnityEngine.Object.DestroyImmediate(oldAI);
            InterceptorAI ai = go.AddComponent<InterceptorAI>();
            ai.Kind = kind;
            ai.m_info = info;
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
