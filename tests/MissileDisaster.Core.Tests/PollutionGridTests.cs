using System.Collections.Generic;
using MissileDisaster.Core;
using Xunit;

public class PollutionGridTests
{
    [Fact]
    public void WorldToCell_center_is_middle_cell()
    {
        Assert.Equal(256, PollutionGrid.WorldToCell(0f));
    }

    [Theory]
    [InlineData(-9999999f, 0)]
    [InlineData(9999999f, 511)]
    public void WorldToCell_clamps_to_grid(float world, int expected)
    {
        Assert.Equal(expected, PollutionGrid.WorldToCell(world));
    }

    [Fact]
    public void CellIndex_is_row_major()
    {
        Assert.Equal(2 * PollutionGrid.Resolution + 3, PollutionGrid.CellIndex(3, 2));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-5f)]
    public void CellsInRadius_empty_for_nonpositive_radius(float radius)
    {
        Assert.Empty(PollutionGrid.CellsInRadius(0f, 0f, radius, 255));
    }

    [Fact]
    public void CellsInRadius_returns_cells_with_valid_intensities()
    {
        List<CellDose> doses = PollutionGrid.CellsInRadius(0f, 0f, 200f, 255);
        Assert.NotEmpty(doses);
        foreach (CellDose d in doses)
        {
            Assert.True(d.Index >= 0);
            Assert.InRange(d.Intensity, (byte)0, (byte)255);
        }
    }

    [Fact]
    public void CellsInRadius_center_cell_gets_max_intensity()
    {
        int centerIndex = PollutionGrid.CellIndex(PollutionGrid.WorldToCell(0f), PollutionGrid.WorldToCell(0f));
        List<CellDose> doses = PollutionGrid.CellsInRadius(0f, 0f, 200f, 255);
        byte centerIntensity = 0;
        bool found = false;
        foreach (CellDose d in doses)
        {
            if (d.Index == centerIndex) { centerIntensity = d.Intensity; found = true; }
        }
        Assert.True(found, "the centre cell is included");
        Assert.Equal(255, centerIntensity);
    }

    [Fact]
    public void CellsInRadius_larger_radius_covers_more_cells()
    {
        int small = PollutionGrid.CellsInRadius(0f, 0f, 100f, 255).Count;
        int large = PollutionGrid.CellsInRadius(0f, 0f, 400f, 255).Count;
        Assert.True(large > small, "a larger radius covers more cells");
    }

    [Fact]
    public void CellsInRadius_has_falloff_below_max_somewhere()
    {
        List<CellDose> doses = PollutionGrid.CellsInRadius(0f, 0f, 300f, 255);
        bool anyBelowMax = false;
        foreach (CellDose d in doses) if (d.Intensity < 255) { anyBelowMax = true; break; }
        Assert.True(anyBelowMax, "some cells have fallen off towards the edge");
    }
}
