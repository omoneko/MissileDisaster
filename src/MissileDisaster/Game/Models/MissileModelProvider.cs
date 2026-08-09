using System;
using System.Collections.Generic;
using System.IO;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Models
{
    /// <summary>
    /// Single entry point for creating model GameObjects. The mesh is built at runtime from
    /// Models/&lt;name&gt;.obj (and its .mtl) inside the mod folder, cached, and a fresh instance
    /// returned for each request.
    /// This is a cut-down port of Alien Invasion's ModelProvider, without the AssetBundle or the
    /// decal handling.
    /// It creates GameObjects, Meshes and Materials, so it must be called from the main thread.
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
        private static readonly Dictionary<string, Mesh> _meshCache = new Dictionary<string, Mesh>();
        private static readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();

        public static void Initialize(string modDirectory)
        {
            if (_initialized) return;
            _initialized = true;
            _modDirectory = modDirectory;
        }

        /// <summary>Loads Models/&lt;name&gt;.obj as a single-submesh Mesh, for a building's m_mesh. Null on failure. Cached.</summary>
        public static Mesh LoadMergedMesh(string name)
        {
            try
            {
                Mesh cached;
                if (_meshCache.TryGetValue(name, out cached)) return cached;

                ObjData data = LoadObjData(name);
                if (data == null) return null;

                Mesh mesh;
                if (!MissileMeshBuilder.TryBuildMergedMesh(data, out mesh))
                {
                    ModConfig.LogError("MissileModelProvider.LoadMergedMesh: failed to build the mesh, name=" + name);
                    return null;
                }
                _meshCache[name] = mesh;
                return mesh;
            }
            catch (Exception e)
            {
                ModConfig.LogError("MissileModelProvider.LoadMergedMesh(" + name + ") error: " + e);
                return null;
            }
        }

        private static ObjData LoadObjData(string name)
        {
            if (string.IsNullOrEmpty(_modDirectory))
            {
                ModConfig.LogError("MissileModelProvider.LoadObjData: modDirectory is not set");
                return null;
            }
            string objPath = Path.Combine(Path.Combine(_modDirectory, ModConfig.ModelsFolderName), name + ".obj");
            if (!File.Exists(objPath))
            {
                ModConfig.LogError("MissileModelProvider.LoadObjData: OBJ not found, path=" + objPath);
                return null;
            }
            return ObjParser.Parse(File.ReadAllText(objPath));
        }

        /// <summary>A new instance of the named model, or null if it could not be created, in which case the caller falls back.</summary>
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
                    ModConfig.LogError("MissileModelProvider.BuildFromObj: modDirectory is not set (Initialize was never called)");
                    return null;
                }

                string modelsDir = Path.Combine(_modDirectory, ModConfig.ModelsFolderName);
                string objPath = Path.Combine(modelsDir, name + ".obj");
                if (!File.Exists(objPath))
                {
                    ModConfig.LogError("MissileModelProvider: OBJ not found, path=" + objPath);
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
                    ModConfig.LogError("MissileModelProvider: failed to build the mesh from the OBJ, name=" + name + " path=" + objPath);
                    return null;
                }

                ApplyTextures(data, mtl, materials, modelsDir);

                ModConfig.Log("MissileModelProvider: built the model from its OBJ, name=" + name);
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

        /// <summary>
        /// Assigns each submesh's map_Kd texture to its material, when the MTL declared one. A
        /// missing or unreadable file just leaves the plain colour - the model stays usable.
        /// </summary>
        private static void ApplyTextures(ObjData data, Dictionary<string, MtlColor> mtl,
            Material[] materials, string modelsDir)
        {
            if (mtl == null || materials == null) return;
            for (int s = 0; s < data.Submeshes.Count && s < materials.Length; s++)
            {
                try
                {
                    if (materials[s] == null || data.Submeshes[s] == null) continue;
                    MtlColor entry;
                    if (!mtl.TryGetValue(data.Submeshes[s].Material ?? "", out entry)) continue;
                    if (entry == null || string.IsNullOrEmpty(entry.TextureFile)) continue;

                    Texture2D tex = LoadTexture(Path.Combine(modelsDir, entry.TextureFile));
                    if (tex == null) continue;
                    materials[s].mainTexture = tex;
                    if (materials[s].HasProperty("_MainTex")) materials[s].SetTexture("_MainTex", tex);
                }
                catch (Exception e)
                {
                    ModConfig.LogError("MissileModelProvider.ApplyTextures error: " + e);
                }
            }
        }

        /// <summary>Loads a PNG or JPG from disk into a Texture2D, cached by path. Null when the file is missing or does not decode.</summary>
        private static Texture2D LoadTexture(string path)
        {
            Texture2D cached;
            if (_textureCache.TryGetValue(path, out cached) && cached != null) return cached;
            try
            {
                if (!File.Exists(path))
                {
                    ModConfig.LogError("MissileModelProvider.LoadTexture: not found, path=" + path);
                    return null;
                }
                byte[] bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2);
                if (!tex.LoadImage(bytes)) // decodes PNG and JPG, resizing the texture to fit
                {
                    ModConfig.LogError("MissileModelProvider.LoadTexture: could not decode, path=" + path);
                    return null;
                }
                tex.wrapMode = TextureWrapMode.Clamp;
                _textureCache[path] = tex;
                return tex;
            }
            catch (Exception e)
            {
                ModConfig.LogError("MissileModelProvider.LoadTexture(" + path + ") error: " + e);
                return null;
            }
        }
    }
}
