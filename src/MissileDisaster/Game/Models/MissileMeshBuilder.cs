using System;
using System.Collections.Generic;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Models
{
    /// <summary>
    /// Core が解析した ObjData/MtlColor から、実行時に Unity の Mesh/Material を構築する。
    /// Alien Invasion の ObjMeshBuilder を縮小移植したもの（夜間発光・透過登録は持ち込まない）。
    /// Mesh/Material/Shader の生成は Unity のメインスレッドでのみ許可されるため、必ず
    /// メインスレッド（GameObject を生成する箇所と同じスレッド）から呼ぶこと。
    /// </summary>
    public static class MissileMeshBuilder
    {
        public static bool TryBuild(ObjData obj, Dictionary<string, MtlColor> mtl, Color fallbackColor, out Mesh mesh, out Material[] materials)
        {
            mesh = null;
            materials = null;

            try
            {
                if (obj == null || obj.Positions == null || obj.Submeshes == null) return false;
                int vertexCount = obj.VertexCount;
                if (vertexCount <= 0 || obj.Submeshes.Count == 0) return false;

                var vertices = new Vector3[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                {
                    vertices[i] = new Vector3(
                        obj.Positions[i * 3],
                        obj.Positions[i * 3 + 1],
                        obj.Positions[i * 3 + 2]);
                }

                var builtMesh = new Mesh();
                builtMesh.vertices = vertices;
                builtMesh.subMeshCount = obj.Submeshes.Count;

                var mats = new Material[obj.Submeshes.Count];

                for (int s = 0; s < obj.Submeshes.Count; s++)
                {
                    ObjSubmesh sub = obj.Submeshes[s];
                    List<int> validTriangles = FilterValidTriangles(sub != null ? sub.Triangles : null, vertexCount);
                    builtMesh.SetTriangles(validTriangles, s);
                    mats[s] = BuildMaterial(sub != null ? sub.Material : null, mtl, fallbackColor);
                }

                builtMesh.RecalculateNormals();
                builtMesh.RecalculateBounds();

                mesh = builtMesh;
                materials = mats;
                return true;
            }
            catch (Exception e)
            {
                ModConfig.LogError("MissileMeshBuilder.TryBuild error: " + e);
                mesh = null;
                materials = null;
                return false;
            }
        }

        /// <summary>
        /// 全サブメッシュの三角形を単一サブメッシュへ統合した Mesh を構築する（マテリアル1枚運用）。
        /// CS の建物レンダラは m_mesh + 単一 m_material を素直に描くため、建物用途に使う。
        /// </summary>
        public static bool TryBuildMergedMesh(ObjData obj, out Mesh mesh)
        {
            mesh = null;
            try
            {
                if (obj == null || obj.Positions == null || obj.Submeshes == null) return false;
                int vertexCount = obj.VertexCount;
                if (vertexCount <= 0) return false;

                var vertices = new Vector3[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                {
                    vertices[i] = new Vector3(
                        obj.Positions[i * 3],
                        obj.Positions[i * 3 + 1],
                        obj.Positions[i * 3 + 2]);
                }

                var allTris = new List<int>();
                for (int s = 0; s < obj.Submeshes.Count; s++)
                {
                    ObjSubmesh sub = obj.Submeshes[s];
                    allTris.AddRange(FilterValidTriangles(sub != null ? sub.Triangles : null, vertexCount));
                }
                if (allTris.Count == 0) return false;

                var built = new Mesh();
                built.vertices = vertices;
                built.subMeshCount = 1;
                built.SetTriangles(allTris, 0);
                built.RecalculateNormals();
                built.RecalculateBounds();

                mesh = built;
                return true;
            }
            catch (Exception e)
            {
                ModConfig.LogError("MissileMeshBuilder.TryBuildMergedMesh error: " + e);
                mesh = null;
                return false;
            }
        }

        /// <summary>破損/範囲外インデックスの三角形を除去する。Unity の SetTriangles は範囲外
        /// インデックスがあると例外を投げるため、必ずこのフィルタを通してから渡す。</summary>
        private static List<int> FilterValidTriangles(List<int> triangles, int vertexCount)
        {
            if (triangles == null || triangles.Count == 0) return new List<int>();

            var valid = new List<int>(triangles.Count);
            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                if (a < 0 || a >= vertexCount) continue;
                if (b < 0 || b >= vertexCount) continue;
                if (c < 0 || c >= vertexCount) continue;

                valid.Add(a);
                valid.Add(b);
                valid.Add(c);
            }
            return valid;
        }

        private static Material BuildMaterial(string materialName, Dictionary<string, MtlColor> mtl, Color fallbackColor)
        {
            Material mat = CreateBaseMaterial();
            if (mat == null) return null;

            try
            {
                float r = fallbackColor.r, g = fallbackColor.g, b = fallbackColor.b;

                MtlColor found;
                if (mtl != null && !string.IsNullOrEmpty(materialName) && mtl.TryGetValue(materialName, out found) && found != null)
                {
                    r = found.R;
                    g = found.G;
                    b = found.B;
                }

                Color color = new Color(r, g, b, 1f);
                mat.color = color;
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", ModConfig.ObjMetallic);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", ModConfig.ObjGlossiness);
            }
            catch (Exception e)
            {
                ModConfig.LogError("MissileMeshBuilder.BuildMaterial error: " + e);
            }

            return mat;
        }

        private static Material CreateBaseMaterial()
        {
            try
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
                if (shader == null) shader = Shader.Find("Diffuse");
                if (shader == null) return null;
                return new Material(shader);
            }
            catch (Exception e)
            {
                ModConfig.LogError("MissileMeshBuilder.CreateBaseMaterial error: " + e);
                return null;
            }
        }
    }
}
