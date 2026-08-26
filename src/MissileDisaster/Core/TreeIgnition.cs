using System;

namespace MissileDisaster.Core
{
    /// <summary>
    /// Decides which trees a blast sets alight, and how thoroughly. Pure, with no UnityEngine
    /// dependency, so the budget and the falloff can be tested rather than eyeballed.
    ///
    /// Two things have to be true at once. Physically, everything inside the thermal radius
    /// catches: at Hiroshima the fires started well beyond the blast damage, and a forest inside
    /// that ring goes up. Practically, a 150 kt burn radius covers 5.8 km, which on a wooded map
    /// is tens of thousands of trees - and the game carries every burning one in
    /// TreeManager.m_burningTrees, ticking it every frame. Igniting them all would be the last
    /// thing the save ever did.
    ///
    /// So the ignition is a budget spread over the burn area, weighted towards the middle: near
    /// ground zero almost every tree the walk reaches is lit, and the chance tapers to nothing at
    /// the edge. What the player sees is a forest fire that is fiercest at the centre and peters
    /// out - which is what a real one does - drawn with a few hundred trees rather than a few
    /// hundred thousand.
    /// </summary>
    public static class TreeIgnition
    {
        /// <summary>
        /// The most trees one detonation may set alight. The game's own forest fire runs in the
        /// low hundreds; this sits in the same range so a strike costs the simulation about what
        /// a natural disaster does.
        /// </summary>
        public const int MaxTrees = 320;

        /// <summary>
        /// How far out the fires reach, against the warhead's burn radius. Short of the full
        /// radius: the outer fringe of a thermal ring is scattered ignition, and stretching the
        /// budget over it would thin the middle - where the fire should be solid - to nothing.
        /// </summary>
        public const float ReachFraction = 0.62f;

        /// <summary>Inside this fraction of the reach, every tree the walk finds is lit.</summary>
        public const float CoreFraction = 0.25f;

        /// <summary>How sharply the chance falls off from the core to the edge. Above 1 it holds up through the middle and drops late.</summary>
        public const float FalloffPower = 1.6f;

        /// <summary>
        /// How far from the impact trees are considered at all, in metres. Zero or less - an
        /// airburst high enough to have no burn radius, say - means no walk at all.
        /// </summary>
        public static float Reach(float burnRadius)
        {
            if (burnRadius <= 0f) return 0f;
            return burnRadius * ReachFraction;
        }

        /// <summary>
        /// The chance a tree at this distance catches, before the budget is applied. 1 inside the
        /// core, tapering to 0 at the reach, and 0 beyond it.
        /// </summary>
        public static float Chance(float distance, float reach)
        {
            if (reach <= 0f) return 0f;
            if (distance <= 0f) return 1f;
            if (distance >= reach) return 0f;
            float core = reach * CoreFraction;
            if (distance <= core) return 1f;
            float t = (distance - core) / (reach - core); // 0 at the core's edge, 1 at the reach
            float remaining = 1f - t;
            return (float)Math.Pow(remaining, FalloffPower);
        }

        /// <summary>
        /// How many trees a walk should be allowed to light, given how many it is going to find.
        /// The budget is the ceiling; a small copse under a small warhead simply burns entirely.
        /// </summary>
        public static int Budget(int treesInReach)
        {
            if (treesInReach <= 0) return 0;
            return treesInReach < MaxTrees ? treesInReach : MaxTrees;
        }

        /// <summary>
        /// The share of candidates that may be taken, so a dense forest under a big warhead
        /// spends the budget evenly across the area instead of emptying it on the first cells the
        /// walk happens to reach. Combined with Chance, this is the per-tree probability.
        /// </summary>
        public static float Density(int treesInReach)
        {
            if (treesInReach <= 0) return 0f;
            if (treesInReach <= MaxTrees) return 1f;
            return MaxTrees / (float)treesInReach;
        }
    }
}
