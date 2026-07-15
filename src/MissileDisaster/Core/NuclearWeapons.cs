namespace MissileDisaster.Core
{
    /// <summary>代表的な核兵器1種（名称と威力kt）。UnityEngine 非依存。</summary>
    public struct NuclearWeapon
    {
        public string Name;
        public int Kilotons;

        public NuclearWeapon(string name, int kilotons)
        {
            Name = name;
            Kilotons = kilotons;
        }
    }

    /// <summary>
    /// 代表的な核兵器10種のカタログ（ja.wikipedia「核兵器一覧」等の公表代表値）。kt昇順。
    /// UI の選択肢に使う。任意 kt は数値入力で指定でき、双方 NuclearYields.Multiplier(kt) で係数化する。
    /// </summary>
    public static class NuclearWeapons
    {
        public static readonly NuclearWeapon[] Catalog =
        {
            new NuclearWeapon("Little Boy (Hiroshima)", 15),
            new NuclearWeapon("Fat Man (Nagasaki)", 22),
            new NuclearWeapon("Trinity (first test)", 25),
            new NuclearWeapon("W87 (Minuteman III)", 300),
            new NuclearWeapon("B61 (variable, max)", 340),
            new NuclearWeapon("W88 (Trident II)", 475),
            new NuclearWeapon("B83 (largest US active)", 1200),
            new NuclearWeapon("Ivy Mike (first H-bomb)", 10400),
            new NuclearWeapon("Castle Bravo", 15000),
            new NuclearWeapon("Tsar Bomba", 50000),
        };
    }
}
