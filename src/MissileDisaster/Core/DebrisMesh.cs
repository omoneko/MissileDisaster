using System;

namespace MissileDisaster.Core
{
    /// <summary>The geometry of one chunk: positions as flat xyz triples, and triangle indices.</summary>
    public class DebrisMeshData
    {
        public float[] Positions;   // x,y,z per vertex
        public int[] Triangles;     // three indices per face, wound clockwise for Unity

        public int VertexCount { get { return Positions.Length / 3; } }
        public int TriangleCount { get { return Triangles.Length / 3; } }
    }

    /// <summary>
    /// Builds the little slabs of masonry a blast throws, procedurally. Pure, with no
    /// UnityEngine dependency, so the shape can be measured in a test rather than squinted at.
    ///
    /// A round soft billboard tinted brown is not rubble, and drawing it that way is what the
    /// first attempt did. What rubble is, in this game, was measured off the props the game
    /// already ships - rock_small_01..04's LOD meshes in the game's own assets:
    ///
    ///     31-58 triangles, 38-59 vertices
    ///     about 4.0 m x 1.8 m x 2.6 m - wider than deep, and much wider than tall
    ///
    /// That last part is the whole character of the thing. Rubble lies flat; a cube tumbling
    /// through the air reads as dice. So the chunks here are irregular polyhedra squashed to
    /// roughly those proportions, at roughly that poly count - a shape built to the same brief
    /// as the game's own, not a copy of one.
    ///
    /// The construction is a deformed icosphere-ish hull: a ring-and-pole lattice with every
    /// vertex pushed in or out by a seeded hash, so no two chunks are alike and none of them is
    /// smooth. Flat-shaded by the renderer, the facets catch the light separately, which is what
    /// sells them as broken concrete rather than as pebbles.
    /// </summary>
    public static class DebrisMesh
    {
        /// <summary>Rings of vertices between the poles. Four rings, two poles gives 4*8+2 = 34 vertices and 64 faces - the reference bracket.</summary>
        public const int Rings = 4;
        public const int Segments = 8;

        /// <summary>The reference proportions, from the game's own rock LODs: 4.0 x 1.8 x 2.6 normalised on the longest axis.</summary>
        public const float HeightRatio = 0.45f;   // 1.8 / 4.0
        public const float DepthRatio = 0.65f;    // 2.6 / 4.0

        /// <summary>How far a vertex may be pushed from the base hull, as a fraction of the radius. This is what makes it broken rather than round.</summary>
        public const float Jaggedness = 0.34f;

        /// <summary>
        /// How much of a vertex's push is shared with its neighbours around the ring.
        ///
        /// Pushing each vertex independently gives needles: one vertex flung out between two
        /// that were not makes a spike, and a chunk covered in them reads as a caltrop rather
        /// than as masonry. Blending each push with the two beside it turns those spikes into
        /// broad faces - which is what the reference props are, a few big flat planes meeting at
        /// hard edges.
        /// </summary>
        public const float Smoothing = 0.5f;

        /// <summary>How many different chunks to build. Enough that a burst never shows two of the same rubble side by side.</summary>
        public const int Variants = 4;

        /// <summary>
        /// One chunk, sized so its longest axis is unitSize. Deterministic in the seed: the same
        /// seed always builds the same chunk, which is what lets the shipped shapes be tested.
        /// </summary>
        public static DebrisMeshData Build(int seed, float unitSize)
        {
            if (unitSize <= 0f) unitSize = 1f;
            int vertexCount = Rings * Segments + 2;
            var pos = new float[vertexCount * 3];

            float half = unitSize * 0.5f;
            int v = 0;

            // Bottom pole, then the rings, then the top pole.
            Place(pos, ref v, 0f, -half * HeightRatio, 0f);

            for (int r = 0; r < Rings; r++)
            {
                // Latitude from just above the bottom pole to just below the top one.
                double phi = Math.PI * (r + 1) / (Rings + 1);
                double ringRadius = Math.Sin(phi);
                double y = Math.Cos(phi);

                for (int s = 0; s < Segments; s++)
                {
                    double theta = 2.0 * Math.PI * s / Segments;
                    // Every vertex gets its own push, smoothed against its neighbours so the
                    // hull comes out as broad faces rather than as spikes.
                    float push = 1f + (Displacement(seed, r, s) - 0.5f) * 2f * Jaggedness;
                    float x = (float)(Math.Cos(theta) * ringRadius) * half * push;
                    float yy = (float)y * half * HeightRatio * push;
                    float z = (float)(Math.Sin(theta) * ringRadius) * half * DepthRatio * push;
                    Place(pos, ref v, x, yy, z);
                }
            }

            Place(pos, ref v, 0f, half * HeightRatio, 0f);

            return new DebrisMeshData { Positions = pos, Triangles = BuildTriangles() };
        }

        /// <summary>
        /// One vertex's displacement, its own roll blended with the two beside it on the ring.
        /// </summary>
        public static float Displacement(int seed, int ring, int segment)
        {
            int prev = (segment + Segments - 1) % Segments;
            int next = (segment + 1) % Segments;
            float here = Hash01(seed, ring * Segments + segment);
            float before = Hash01(seed, ring * Segments + prev);
            float after = Hash01(seed, ring * Segments + next);
            float neighbours = (before + after) * 0.5f;
            return here * (1f - Smoothing) + neighbours * Smoothing;
        }

        private static void Place(float[] pos, ref int v, float x, float y, float z)
        {
            pos[v * 3] = x;
            pos[v * 3 + 1] = y;
            pos[v * 3 + 2] = z;
            v++;
        }

        /// <summary>
        /// The faces: a fan around the bottom pole, quads between each pair of rings, a fan
        /// around the top. Wound so the outward face is the visible one in Unity's clockwise
        /// convention.
        /// </summary>
        private static int[] BuildTriangles()
        {
            int bottom = 0;
            int top = Rings * Segments + 1;
            var tris = new System.Collections.Generic.List<int>(Segments * (Rings + 1) * 6);

            // Bottom fan. Ring 0 starts at index 1.
            for (int s = 0; s < Segments; s++)
            {
                int a = 1 + s;
                int b = 1 + (s + 1) % Segments;
                tris.Add(bottom); tris.Add(a); tris.Add(b);
            }

            // The bands between consecutive rings, two triangles per quad.
            for (int r = 0; r < Rings - 1; r++)
            {
                int rowA = 1 + r * Segments;
                int rowB = 1 + (r + 1) * Segments;
                for (int s = 0; s < Segments; s++)
                {
                    int s1 = (s + 1) % Segments;
                    int a = rowA + s, b = rowA + s1;
                    int c = rowB + s, d = rowB + s1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            // Top fan, off the last ring.
            int last = 1 + (Rings - 1) * Segments;
            for (int s = 0; s < Segments; s++)
            {
                int a = last + s;
                int b = last + (s + 1) % Segments;
                tris.Add(top); tris.Add(b); tris.Add(a);
            }

            return tris.ToArray();
        }

        /// <summary>A deterministic 0..1 from the seed and the vertex, so a shape is reproducible forever.</summary>
        public static float Hash01(int seed, int index)
        {
            unchecked
            {
                uint h = (uint)(seed * 668265263 + index * 374761393 + 1274126177);
                h ^= h >> 13;
                h *= 1911520717u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
