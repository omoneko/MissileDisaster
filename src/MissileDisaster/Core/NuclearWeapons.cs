namespace MissileDisaster.Core
{
    /// <summary>One well-known nuclear weapon: its name and its yield in kilotons. No UnityEngine dependency.</summary>
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
    /// A catalogue of ten well-known nuclear weapons, using widely published yields, in
    /// ascending order of kilotons. These are the options offered in the UI; any other yield can
    /// be typed in directly, and both paths go through NuclearYields.Multiplier(kt).
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
