using System.Collections.Generic;

namespace MissileDisaster.Core
{
    /// <summary>The triangles of a single material, as read from an OBJ.</summary>
    public class ObjSubmesh
    {
        /// <summary>The usemtl name. Faces appearing before the first usemtl get "".</summary>
        public string Material;

        /// <summary>0-based vertex position indices; three per triangle, in Unity's winding order.</summary>
        public List<int> Triangles;

        public ObjSubmesh()
        {
            Material = "";
            Triangles = new List<int>();
        }
    }

    /// <summary>The parsed contents of a Blender-exported OBJ, as a Unity-independent intermediate.</summary>
    public class ObjData
    {
        /// <summary>Every vertex's x, y and z flattened into one list, with X already mirrored.</summary>
        public List<float> Positions;

        /// <summary>
        /// Every vt's u and v flattened into one list. Faces do not carry vt indices through this
        /// parser; UVs are used only when the file has exactly one vt per v, in the same order -
        /// which is the convention tools/cloud-model/generate.py emits. HasAlignedUVs says whether
        /// this file met it.
        /// </summary>
        public List<float> UVs;

        /// <summary>Every vn's x, y and z flattened, X mirrored like the positions. Same aligned-by-order convention as the UVs.</summary>
        public List<float> Normals;

        public List<ObjSubmesh> Submeshes;

        public ObjData()
        {
            Positions = new List<float>();
            UVs = new List<float>();
            Normals = new List<float>();
            Submeshes = new List<ObjSubmesh>();
        }

        public int VertexCount { get { return Positions.Count / 3; } }

        /// <summary>Whether the file carried one vt per vertex, in vertex order, so the UVs can be applied by position index.</summary>
        public bool HasAlignedUVs { get { return VertexCount > 0 && UVs.Count == VertexCount * 2; } }

        /// <summary>Whether the file carried one vn per vertex, in vertex order, so the normals can be applied by position index.</summary>
        public bool HasAlignedNormals { get { return VertexCount > 0 && Normals.Count == VertexCount * 3; } }
    }
}
