using MissileDisaster.Core;

namespace MissileDisaster.Game.Contamination
{
    /// <summary>
    /// Wrapper for writing to NaturalResourceManager's ground pollution cells. The pollution is
    /// part of the game's own save and shows up on its pollution overlay. Writes must come from
    /// the simulation thread, the same one that resolves impacts.
    /// Ported, in part, from NuclearMeltdown.Game.PollutionField.
    /// </summary>
    public static class PollutionField
    {
        /// <summary>Raises a cell to at least the given dose, leaving it alone if it is already higher. True if it actually changed.</summary>
        public static bool ApplyDose(CellDose dose)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (dose.Index < 0 || dose.Index >= arr.Length) return false;
            if (arr[dose.Index].m_pollution < dose.Intensity)
            {
                arr[dose.Index].m_pollution = dose.Intensity;
                return true;
            }
            return false;
        }

        /// <summary>Overwrites a cell with dose.Intensity, used to lower it during decontamination. True if it actually changed.</summary>
        public static bool SetDose(CellDose dose)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (dose.Index < 0 || dose.Index >= arr.Length) return false;
            if (arr[dose.Index].m_pollution != dose.Intensity)
            {
                arr[dose.Index].m_pollution = dose.Intensity;
                return true;
            }
            return false;
        }

        /// <summary>Clears a cell to zero, used when a zone expires. True if it actually changed.</summary>
        public static bool ClearCell(int index)
        {
            var arr = NaturalResourceManager.instance.m_naturalResources;
            if (index < 0 || index >= arr.Length) return false;
            if (arr[index].m_pollution != 0)
            {
                arr[index].m_pollution = 0;
                return true;
            }
            return false;
        }

        /// <summary>Refreshes the pollution texture over the given range of cells.</summary>
        public static void Refresh(int minX, int minZ, int maxX, int maxZ)
        {
            NaturalResourceManager.instance.AreaModifiedB(minX, minZ, maxX, maxZ);
        }
    }
}
