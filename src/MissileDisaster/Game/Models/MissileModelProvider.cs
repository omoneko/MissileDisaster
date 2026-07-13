using System;
using System.Collections.Generic;
using System.IO;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Models
{
    /// <summary>
    /// モデル GameObject 生成の単一窓口。Mod 配置フォルダの Models/&lt;name&gt;.obj(+.mtl) から
    /// 実行時にメッシュを構築してキャッシュし、要求ごとに新しいインスタンスを返す。
    /// Alien Invasion の ModelProvider を縮小移植（AssetBundle・デカール等は持ち込まない）。
    /// GameObject/Mesh/Material の生成を伴うため、必ずメインスレッドから呼ぶこと。
    /// </summary>
    public static class MissileModelProvider
    {
        private class BuiltModel
        {
            public Mesh Mesh;
            public Material[] Materials;
        }

        private static string _modDirectory;
        private static bool _initialized;
        private static readonly Dictionary<string, BuiltModel> _cache = new Dictionary<string, BuiltModel>();

        public static void Initialize(string modDirectory)
        {
            if (_initialized) return;
            _initialized = true;
            _modDirectory = modDirectory;
        }

        /// <summary>指定名モデルの新しいインスタンスを返す。生成できなければ null（呼び出し側でフォールバック）。</summary>
        public static GameObject CreateInstance(string name)
        {
            try
            {
                BuiltModel cached;
                if (!_cache.TryGetValue(name, out cached))
                {
                    cached = BuildFromObj(name);
                    if (cached != null) _cache[name] = cached;
                }

                if (cached != null)
                {
                    return InstantiateBuilt(name, cached);
                }

                return null;
            }
            catch (Exception e)
            {
                ModConfig.LogError("MissileModelProvider.CreateInstance(" + name + ") error: " + e);
                return null;
            }
        }

        private static GameObject InstantiateBuilt(string name, BuiltModel model)
        {
            try
            {
                var go = new GameObject("MissileDisaster_" + name);
                MeshFilter filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = model.Mesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = model.Materials;
                return go;
            }
            catch (Exception e)
            {
                ModConfig.LogError("MissileModelProvider.InstantiateBuilt(" + name + ") error: " + e);
                return null;
            }
        }

        private static BuiltModel BuildFromObj(string name)
        {
            try
            {
                if (string.IsNullOrEmpty(_modDirectory))
                {
                    ModConfig.LogError("MissileModelProvider.BuildFromObj: modDirectory 未初期化 (Initialize 未呼び出し)");
                    return null;
                }

                string modelsDir = Path.Combine(_modDirectory, ModConfig.ModelsFolderName);
                string objPath = Path.Combine(modelsDir, name + ".obj");
                if (!File.Exists(objPath))
                {
                    ModConfig.LogError("MissileModelProvider: OBJ が見つかりません path=" + objPath);
                    return null;
                }

                string objText = File.ReadAllText(objPath);
                ObjData data = ObjParser.Parse(objText);

                Dictionary<string, MtlColor> mtl = null;
                string mtlPath = Path.Combine(modelsDir, name + ".mtl");
                if (File.Exists(mtlPath))
                {
                    string mtlText = File.ReadAllText(mtlPath);
                    mtl = MtlParser.Parse(mtlText);
                }

                Mesh mesh;
                Material[] materials;
                if (!MissileMeshBuilder.TryBuild(data, mtl, ModConfig.ObjFallbackColor, out mesh, out materials))
                {
                    ModConfig.LogError("MissileModelProvider: OBJ からのメッシュ構築に失敗 name=" + name + " path=" + objPath);
                    return null;
                }

                ModConfig.Log("MissileModelProvider: OBJ からモデルを構築しました name=" + name);
                var built = new BuiltModel();
                built.Mesh = mesh;
                built.Materials = materials;
                return built;
            }
            catch (Exception e)
            {
                ModConfig.LogError("MissileModelProvider.BuildFromObj(" + name + ") error: " + e);
                return null;
            }
        }
    }
}
