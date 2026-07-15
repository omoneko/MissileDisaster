using MissileDisaster.Core;
using Xunit;

public class NuclearWeaponsTests
{
    [Fact]
    public void Catalog_has_ten_representative_weapons()
    {
        Assert.Equal(10, NuclearWeapons.Catalog.Length);
    }

    [Fact]
    public void Every_weapon_has_a_name_and_positive_yield()
    {
        foreach (NuclearWeapon w in NuclearWeapons.Catalog)
        {
            Assert.False(string.IsNullOrEmpty(w.Name), "名称は非空");
            Assert.True(w.Kilotons > 0, $"{w.Name} の威力は正");
        }
    }

    [Fact]
    public void Catalog_is_sorted_ascending_by_yield()
    {
        NuclearWeapon[] c = NuclearWeapons.Catalog;
        for (int i = 1; i < c.Length; i++)
        {
            Assert.True(c[i].Kilotons >= c[i - 1].Kilotons, "kt昇順");
        }
    }

    [Fact]
    public void Catalog_includes_known_extremes()
    {
        // 最小級=リトルボーイ(15kt前後)、最大=ツァーリ・ボンバ(50000kt)。
        Assert.Equal(15, NuclearWeapons.Catalog[0].Kilotons);
        Assert.Equal(50000, NuclearWeapons.Catalog[NuclearWeapons.Catalog.Length - 1].Kilotons);
    }
}
