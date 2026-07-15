using System.Collections.Generic;
using MissileDisaster.Core;
using Xunit;

public class ZoneSerializerTests
{
    [Fact]
    public void Roundtrip_preserves_zones()
    {
        var zones = new List<ContaminationZone>
        {
            new ContaminationZone(100.5f, -200.25f, 460f, 1234567890L, 255f),
            new ContaminationZone(-50f, 75f, 5300f, 987654321L, 128.5f), // 除染途中の端数濃度も保持
        };
        byte[] bytes = ZoneSerializer.Serialize(zones);
        List<ContaminationZone> back = ZoneSerializer.Deserialize(bytes);

        Assert.Equal(zones.Count, back.Count);
        for (int i = 0; i < zones.Count; i++)
        {
            Assert.Equal(zones[i].CenterX, back[i].CenterX, 3);
            Assert.Equal(zones[i].CenterZ, back[i].CenterZ, 3);
            Assert.Equal(zones[i].Radius, back[i].Radius, 3);
            Assert.Equal(zones[i].StartTicks, back[i].StartTicks);
            Assert.Equal(zones[i].Intensity, back[i].Intensity, 3);
        }
    }

    [Fact]
    public void Empty_list_roundtrips_to_empty()
    {
        byte[] bytes = ZoneSerializer.Serialize(new List<ContaminationZone>());
        Assert.Empty(ZoneSerializer.Deserialize(bytes));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 9, 9, 9 })]
    public void Corrupt_or_short_data_yields_empty(byte[] data)
    {
        Assert.Empty(ZoneSerializer.Deserialize(data));
    }
}
