using System;
using ColossalFramework;
using ColossalFramework.Math;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// Sets the trees inside a blast's thermal ring alight. Simulation thread only - it writes
    /// to TreeManager, and TreeManager.BurnTree touches DisasterManager and InstanceManager as
    /// it goes.
    ///
    /// This is a Natural Disasters feature and degrades to nothing without it: BurnTree's first
    /// act is to check LoadingManager.SupportsExpansion(NaturalDisasters) and return false if it
    /// is missing (verified in the IL, not assumed). On a base-game install the walk still runs
    /// and simply lights nothing, so the mod's "no DLC required" promise holds - burning trees
    /// are an extra for owners rather than a requirement.
    ///
    /// Trees are found through the game's own 32 m grid rather than by scanning all 262,144 of
    /// them: only the cells the reach actually covers are walked, and each holds a linked list
    /// through TreeInstance.m_nextGridTree.
    /// </summary>
    public static class TreeFires
    {
        // The grid's own geometry, from TreeManager's constants. Named here so the arithmetic
        // below reads as what it is rather than as magic numbers.
        private const float CellSize = 32f;                 // TREEGRID_CELL_SIZE
        private const int Resolution = 540;                 // TREEGRID_RESOLUTION
        private const int HalfResolution = Resolution / 2;   // the grid is centred on the origin

        /// <summary>
        /// Lights trees around pos, out to the reach TreeIgnition derives from the burn radius.
        /// Never throws: a failure here must not cost the strike its damage.
        /// </summary>
        public static void Ignite(Vector3 pos, float burnRadius, ref Randomizer randomizer)
        {
            float reach = TreeIgnition.Reach(burnRadius);
            if (reach <= 0f) return;

            try
            {
                TreeManager tm = Singleton<TreeManager>.instance;
                if (tm == null || tm.m_treeGrid == null || tm.m_trees == null) return;

                // Two passes over the same cells. The first counts what is there, because the
                // per-tree probability depends on how many candidates exist - a lone copse
                // should burn entirely, a dense forest should be sampled - and that cannot be
                // known until the walk has looked.
                int candidates = Count(tm, pos, reach);
                if (candidates <= 0) return;
                float density = TreeIgnition.Density(candidates);
                int budget = TreeIgnition.Budget(candidates);

                int lit = Light(tm, pos, reach, density, budget, ref randomizer);
                if (lit > 0) ModConfig.Log("tree fires: lit " + lit + " of " + candidates + " within " + reach.ToString("0") + " m");
            }
            catch (Exception e)
            {
                ModConfig.LogError("TreeFires.Ignite error: " + e);
            }
        }

        private static int Count(TreeManager tm, Vector3 pos, float reach)
        {
            int found = 0;
            int minX, minZ, maxX, maxZ;
            CellRange(pos, reach, out minX, out minZ, out maxX, out maxZ);
            float reachSqr = reach * reach;

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    uint tree = tm.m_treeGrid[z * Resolution + x];
                    int guard = 0;
                    while (tree != 0u && guard++ < 65536)
                    {
                        if (Burnable(tm, tree, pos, reachSqr)) found++;
                        tree = tm.m_trees.m_buffer[tree].m_nextGridTree;
                    }
                }
            }
            return found;
        }

        private static int Light(TreeManager tm, Vector3 pos, float reach, float density, int budget,
            ref Randomizer randomizer)
        {
            int lit = 0;
            int minX, minZ, maxX, maxZ;
            CellRange(pos, reach, out minX, out minZ, out maxX, out maxZ);
            float reachSqr = reach * reach;

            for (int z = minZ; z <= maxZ && lit < budget; z++)
            {
                for (int x = minX; x <= maxX && lit < budget; x++)
                {
                    uint tree = tm.m_treeGrid[z * Resolution + x];
                    int guard = 0;
                    while (tree != 0u && guard++ < 65536 && lit < budget)
                    {
                        uint next = tm.m_trees.m_buffer[tree].m_nextGridTree; // read before the flags change under us
                        if (Burnable(tm, tree, pos, reachSqr))
                        {
                            Vector3 p = tm.m_trees.m_buffer[tree].Position;
                            float dx = p.x - pos.x, dz = p.z - pos.z;
                            float distance = Mathf.Sqrt(dx * dx + dz * dz);
                            float chance = TreeIgnition.Chance(distance, reach) * density;
                            if (Roll(ref randomizer, chance) && tm.BurnTree(tree, null, 0)) lit++;
                        }
                        tree = next;
                    }
                }
            }
            return lit;
        }

        /// <summary>A tree that exists, is not already alight, and stands inside the reach.</summary>
        private static bool Burnable(TreeManager tm, uint tree, Vector3 pos, float reachSqr)
        {
            TreeInstance.Flags flags = (TreeInstance.Flags)tm.m_trees.m_buffer[tree].m_flags;
            if ((flags & TreeInstance.Flags.Created) == TreeInstance.Flags.None) return false;
            if ((flags & TreeInstance.Flags.Burning) != TreeInstance.Flags.None) return false;
            if ((flags & TreeInstance.Flags.Deleted) != TreeInstance.Flags.None) return false;

            Vector3 p = tm.m_trees.m_buffer[tree].Position;
            float dx = p.x - pos.x, dz = p.z - pos.z;
            return dx * dx + dz * dz <= reachSqr;
        }

        /// <summary>The grid cells the reach covers, clamped to the grid.</summary>
        private static void CellRange(Vector3 pos, float reach, out int minX, out int minZ, out int maxX, out int maxZ)
        {
            minX = Cell(pos.x - reach);
            minZ = Cell(pos.z - reach);
            maxX = Cell(pos.x + reach);
            maxZ = Cell(pos.z + reach);
        }

        private static int Cell(float world)
        {
            int c = Mathf.FloorToInt(world / CellSize + HalfResolution);
            if (c < 0) return 0;
            return c > Resolution - 1 ? Resolution - 1 : c;
        }

        /// <summary>
        /// A weighted coin from the simulation's own deterministic randomizer, so a strike plays
        /// out the same way on a reload and nothing here can desync a save.
        /// </summary>
        private static bool Roll(ref Randomizer randomizer, float chance)
        {
            if (chance <= 0f) return false;
            if (chance >= 1f) return true;
            return randomizer.Int32(1000u) < (int)(chance * 1000f);
        }
    }
}
