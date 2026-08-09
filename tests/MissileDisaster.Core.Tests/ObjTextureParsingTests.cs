using MissileDisaster.Core;
using Xunit;

/// <summary>
/// The texture path through the OBJ/MTL parsers: vt and vn lines, the aligned-by-order
/// convention the cloud model converter emits, and map_Kd. Kept separate from the original
/// parser tests because these cover a capability the missile models never used.
/// </summary>
public class ObjTextureParsingTests
{
    private const string AlignedObj =
        "v 1.0 2.0 3.0\n" +
        "v 4.0 5.0 6.0\n" +
        "v 7.0 8.0 9.0\n" +
        "vt 0.1 0.2\n" +
        "vt 0.3 0.4\n" +
        "vt 0.5 0.6\n" +
        "vn 1.0 0.0 0.0\n" +
        "vn 0.0 1.0 0.0\n" +
        "vn 0.0 0.0 1.0\n" +
        "f 1/1/1 2/2/2 3/3/3\n";

    [Fact]
    public void Aligned_uvs_and_normals_are_kept_and_flagged()
    {
        var data = ObjParser.Parse(AlignedObj);
        Assert.True(data.HasAlignedUVs);
        Assert.True(data.HasAlignedNormals);
        Assert.Equal(0.1f, data.UVs[0], 3);
        Assert.Equal(0.2f, data.UVs[1], 3);
        Assert.Equal(0.6f, data.UVs[5], 3);
    }

    [Fact]
    public void Normal_x_is_mirrored_like_the_positions()
    {
        // Positions get -x for the right-to-left-handed flip; a normal that did not follow
        // would light the model inside out.
        var data = ObjParser.Parse(AlignedObj);
        Assert.Equal(-1.0f, data.Normals[0], 3);
        Assert.Equal(1.0f, data.Normals[4], 3);
        Assert.Equal(1.0f, data.Normals[8], 3);
    }

    [Fact]
    public void A_blender_style_export_with_unaligned_counts_is_not_flagged()
    {
        // Two vts for three vertices: the counts do not line up, so the UVs must not be
        // applied by position index - that would texture the model with the wrong corners.
        string obj =
            "v 1 0 0\nv 0 1 0\nv 0 0 1\n" +
            "vt 0.1 0.2\nvt 0.3 0.4\n" +
            "f 1/1 2/2 3/1\n";
        var data = ObjParser.Parse(obj);
        Assert.False(data.HasAlignedUVs);
        Assert.False(data.HasAlignedNormals);
        Assert.Equal(3, data.VertexCount); // and the geometry itself still parses
    }

    [Fact]
    public void An_obj_with_no_vt_keeps_the_old_behaviour()
    {
        var data = ObjParser.Parse("v 1 0 0\nv 0 1 0\nv 0 0 1\nf 1 2 3\n");
        Assert.False(data.HasAlignedUVs);
        Assert.False(data.HasAlignedNormals);
        Assert.Empty(data.UVs);
    }

    [Fact]
    public void Map_Kd_is_read_off_the_material()
    {
        var mtl = MtlParser.Parse("newmtl MyClouds\nKd 1.0 1.0 1.0\nmap_Kd MushroomCloud.png\n");
        Assert.Equal("MushroomCloud.png", mtl["MyClouds"].TextureFile);
    }

    [Fact]
    public void A_material_without_map_Kd_has_no_texture()
    {
        var mtl = MtlParser.Parse("newmtl Paint\nKd 0.5 0.5 0.5\n");
        Assert.Null(mtl["Paint"].TextureFile);
    }

    [Fact]
    public void The_shipped_cloud_model_meets_the_aligned_convention()
    {
        // The real file, as generated: one vt and one vn per v, in order, so the mesh builder
        // can texture it. If a re-export breaks the convention this is the test that says so.
        string path = System.IO.Path.Combine(FindModelsDir(), "MushroomCloud.obj");
        var data = ObjParser.Parse(System.IO.File.ReadAllText(path));
        Assert.True(data.VertexCount > 500, "the generated sculpt has real detail");
        Assert.True(data.HasAlignedUVs, "one vt per v, in vertex order");
        Assert.True(data.HasAlignedNormals, "one vn per v, in vertex order");
        Assert.Single(data.Submeshes);

        // Normalised: base at y=0, height 1. The game scales it straight to metres.
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < data.VertexCount; i++)
        {
            float y = data.Positions[i * 3 + 1];
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }
        Assert.InRange(minY, -0.001f, 0.001f);
        Assert.InRange(maxY, 0.999f, 1.001f);
    }

    private static string FindModelsDir()
    {
        // Walks up from the test bin folder to the repo root, which keeps the test independent
        // of where the runner was started.
        var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = System.IO.Path.Combine(dir.FullName, "src", "MissileDisaster", "Models");
            if (System.IO.Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new System.IO.DirectoryNotFoundException("src/MissileDisaster/Models not found above " + System.AppContext.BaseDirectory);
    }
}
