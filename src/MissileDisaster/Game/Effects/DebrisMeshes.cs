using System;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// The chunk meshes and the material they are drawn with. Main thread only.
    ///
    /// These are real geometry, not billboards. A particle system in Mesh render mode draws one
    /// of these per particle, lit by the scene like anything else in the city, which is the only
    /// way rubble reads as solid: a flat sprite always betrays itself the moment the camera
    /// moves, and the round soft one this replaced never read as masonry at any angle.
    ///
    /// The cache is guarded on the objects rather than on a flag, because Unity destroys them
    /// when a city unloads and leaves references that compare equal to null - a flag would leave
    /// every strike afterwards throwing invisible rubble.
    /// </summary>
    public static class DebrisMeshes
    {
        // Concrete. Rough, not shiny: rubble has no gloss, and a specular highlight on a
        // tumbling chunk reads as plastic.
        private static readonly Color ChunkColour = new Color(0.44f, 0.41f, 0.38f, 1f);
        private const float Smoothness = 0.05f;
        private const float Metallic = 0f;

        private static Mesh[] _meshes;
        private static Material _material;

        /// <summary>The chunk shapes, one metre on their longest axis so the particle size scales them directly.</summary>
        public static Mesh[] Chunks
        {
            get
            {
                Ensure();
                return _meshes;
            }
        }

        /// <summary>An opaque, lit, matte material - the chunks are objects, not smoke.</summary>
        public static Material ChunkMaterial
        {
            get
            {
                Ensure();
                return _material;
            }
        }

        private static void Ensure()
        {
            if (_meshes == null || _meshes.Length == 0 || _meshes[0] == null) _meshes = BuildMeshes();
            if (_material == null) _material = BuildMaterial();
        }

        private static Mesh[] BuildMeshes()
        {
            var meshes = new Mesh[DebrisMesh.Variants];
            for (int i = 0; i < meshes.Length; i++)
            {
                DebrisMeshData data = DebrisMesh.Build(i, 1f);

                var verts = new Vector3[data.VertexCount];
                for (int v = 0; v < verts.Length; v++)
                {
                    verts[v] = new Vector3(data.Positions[v * 3], data.Positions[v * 3 + 1],
                        data.Positions[v * 3 + 2]);
                }

                var mesh = new Mesh();
                mesh.name = "MissileDisaster_DebrisChunk" + i;
                mesh.vertices = verts;
                mesh.triangles = data.Triangles;
                // Smoothed normals would round the facets off; the whole point of the jagged
                // hull is that each face catches the light on its own. RecalculateNormals gives
                // shared normals, so the facets are split by duplicating the vertices first.
                Flatten(mesh);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                meshes[i] = mesh;
            }
            return meshes;
        }

        /// <summary>
        /// Splits every triangle onto its own vertices, so RecalculateNormals gives each face a
        /// single flat normal instead of averaging them into a smooth ball.
        /// </summary>
        private static void Flatten(Mesh mesh)
        {
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;
            var flatVerts = new Vector3[tris.Length];
            var flatTris = new int[tris.Length];
            for (int i = 0; i < tris.Length; i++)
            {
                flatVerts[i] = verts[tris[i]];
                flatTris[i] = i;
            }
            mesh.Clear();
            mesh.vertices = flatVerts;
            mesh.triangles = flatTris;
        }

        private static Material BuildMaterial()
        {
            // Borrowing one of the game's own materials would leave this invisible on a renderer
            // the mod created - a lesson this project has already paid for - so the material is
            // built here from a shader that exists.
            Shader shader = RenderAssets.FindFirst("Standard", "Legacy Shaders/Diffuse", "Diffuse");
            if (shader == null) shader = RenderAssets.FindLoadedContaining(new[] { "loading" }, "standard", "diffuse");
            if (shader == null) return null;

            var mat = new Material(shader);
            mat.color = ChunkColour;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", ChunkColour);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", Metallic);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", Smoothness);
            return mat;
        }
    }
}
