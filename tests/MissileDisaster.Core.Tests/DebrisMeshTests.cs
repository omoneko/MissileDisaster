using MissileDisaster.Core;
using Xunit;

/// <summary>
/// The chunk geometry, checked against the props the game already ships. The reference is
/// rock_small_01..04's LOD meshes, read out of the game's own assets: 31-58 triangles, 38-59
/// vertices, about 4.0 x 1.8 x 2.6 m. A chunk that drifts far from that stops reading as rubble.
/// </summary>
public class DebrisMeshTests
{
    private static void Bounds(DebrisMeshData m, out float w, out float h, out float d)
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        for (int i = 0; i < m.VertexCount; i++)
        {
            float x = m.Positions[i * 3], y = m.Positions[i * 3 + 1], z = m.Positions[i * 3 + 2];
            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (y < minY) minY = y; if (y > maxY) maxY = y;
            if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
        }
        w = maxX - minX; h = maxY - minY; d = maxZ - minZ;
    }

    [Fact]
    public void The_poly_count_sits_in_the_reference_bracket()
    {
        DebrisMeshData m = DebrisMesh.Build(1, 4f);
        // The game's rock LODs run 31-58 triangles and 38-59 vertices. Being in the same league
        // is the point: a hundred chunks in the air must not cost more than the rocks do.
        Assert.InRange(m.TriangleCount, 30, 90);
        Assert.InRange(m.VertexCount, 30, 70);
    }

    [Fact]
    public void A_chunk_lies_flat_the_way_rubble_does()
    {
        // 4.0 x 1.8 x 2.6 on the reference props: wider than deep, much wider than tall. A cube
        // tumbling through the air reads as dice, not as masonry.
        float w, h, d;
        Bounds(DebrisMesh.Build(3, 4f), out w, out h, out d);
        Assert.True(h < w * 0.75f, $"flatter than it is wide (w={w:F2} h={h:F2})");
        Assert.True(d < w, $"deeper than tall but narrower than wide (d={d:F2} w={w:F2})");
        Assert.True(d > h, $"and not a slab on its edge (d={d:F2} h={h:F2})");
    }

    [Fact]
    public void The_longest_axis_matches_the_size_it_was_asked_for()
    {
        // The effect scales chunks by their size in metres, so a unit chunk has to actually be
        // that unit - within the jaggedness, which pushes vertices in and out by design.
        float w, h, d;
        Bounds(DebrisMesh.Build(2, 10f), out w, out h, out d);
        Assert.InRange(w, 10f * (1f - DebrisMesh.Jaggedness), 10f * (1f + DebrisMesh.Jaggedness));
    }

    [Fact]
    public void Every_variant_is_a_different_shape()
    {
        // Two identical chunks side by side in a burst is the thing that gives procedural
        // geometry away.
        for (int a = 0; a < DebrisMesh.Variants; a++)
        {
            for (int b = a + 1; b < DebrisMesh.Variants; b++)
            {
                DebrisMeshData ma = DebrisMesh.Build(a, 4f), mb = DebrisMesh.Build(b, 4f);
                bool same = true;
                for (int i = 0; i < ma.Positions.Length && same; i++)
                {
                    if (System.Math.Abs(ma.Positions[i] - mb.Positions[i]) > 0.0001f) same = false;
                }
                Assert.False(same, $"variants {a} and {b} are the same shape");
            }
        }
    }

    [Fact]
    public void The_same_seed_always_builds_the_same_chunk()
    {
        DebrisMeshData a = DebrisMesh.Build(7, 4f), b = DebrisMesh.Build(7, 4f);
        Assert.Equal(a.Positions, b.Positions);
        Assert.Equal(a.Triangles, b.Triangles);
    }

    [Fact]
    public void It_is_jagged_rather_than_smooth()
    {
        // Measure how far the vertices sit from a perfect hull: a smooth ball would have them
        // all at the same relative distance, and would read as a pebble.
        DebrisMeshData m = DebrisMesh.Build(5, 4f);
        float min = float.MaxValue, max = 0f;
        for (int i = 1; i < m.VertexCount - 1; i++) // skip the poles, which sit on the axis
        {
            float x = m.Positions[i * 3], y = m.Positions[i * 3 + 1] / DebrisMesh.HeightRatio,
                  z = m.Positions[i * 3 + 2] / DebrisMesh.DepthRatio;
            float r = (float)System.Math.Sqrt(x * x + y * y + z * z);
            if (r < min) min = r;
            if (r > max) max = r;
        }
        Assert.True(max > min * 1.4f, $"the hull is visibly broken up ({min:F2}..{max:F2})");
    }

    [Fact]
    public void Every_triangle_indexes_a_real_vertex()
    {
        // A stray index is an exception inside Unity's mesh builder, not a visual bug.
        for (int seed = 0; seed < DebrisMesh.Variants; seed++)
        {
            DebrisMeshData m = DebrisMesh.Build(seed, 4f);
            Assert.Equal(0, m.Triangles.Length % 3);
            foreach (int i in m.Triangles) Assert.InRange(i, 0, m.VertexCount - 1);
        }
    }

    [Fact]
    public void Every_vertex_is_used_by_at_least_one_face()
    {
        DebrisMeshData m = DebrisMesh.Build(0, 4f);
        var used = new bool[m.VertexCount];
        foreach (int i in m.Triangles) used[i] = true;
        for (int i = 0; i < used.Length; i++) Assert.True(used[i], $"vertex {i} is orphaned");
    }

    [Fact]
    public void A_nonsense_size_still_builds_something()
    {
        DebrisMeshData m = DebrisMesh.Build(1, 0f);
        Assert.True(m.VertexCount > 0 && m.TriangleCount > 0);
    }
}
