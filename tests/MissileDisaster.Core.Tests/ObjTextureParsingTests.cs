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

}
