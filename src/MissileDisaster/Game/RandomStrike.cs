using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// The random strike mode. StrikeScheduler, on the simulation thread, decides when a strike
    /// fires; this class does the actual launching on the main thread. The impact pattern -
    /// single, MIRV or random - comes from the settings.
    /// A MIRV launches every missile on the same frame, and since they all take the same time to
    /// fall, they land together.
    /// Targets are drawn by StrikeTargeting, which favours nuclear plants and interceptor sites,
    /// then transport hubs, then landmarks, then everything else. Each MIRV missile draws
    /// independently, so a salvo can hit several important sites at once. All main thread.
    /// </summary>
    public static class RandomStrike
    {
        private const int MirvMin = 3;          // fewest missiles in a MIRV salvo
        private const int MirvMax = 6;          // most missiles in a MIRV salvo; Random.Range excludes its upper bound, hence the +1 at the call site
        private const float MirvChance = 0.30f; // chance the random pattern picks MIRV; the other 70% is a single missile
        private const float FallbackRange = 4500f;

        /// <summary>Carries out a strike according to the AttackPattern setting. Main thread only.</summary>
        public static void FireStrike()
        {
            try
            {
                StrikeTargeting targeting = new StrikeTargeting();
                targeting.Scan(); // scan the buildings once and reuse it, even for a MIRV salvo

                int count = ResolveWarheadCount();
                for (int i = 0; i < count; i++)
                {
                    FireOne(targeting);
                }

                // Tell the player, once per city, what just hit them and where the switch is.
                UI.StrikeNotice.RandomStrikeLaunched();
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("RandomStrike.FireStrike error: " + e);
            }
        }

        /// <summary>How many missiles this strike launches: 1 for single, 3 to 6 for MIRV, and 70/30 between them for random.</summary>
        private static int ResolveWarheadCount()
        {
            int pattern = ModSettings.AttackPatternValue;
            bool mirv = pattern == 1 || (pattern == 2 && Random.value < MirvChance);
            return mirv ? Random.Range(MirvMin, MirvMax + 1) : 1;
        }

        /// <summary>Launches one missile, drawing a target by priority or falling back to a random position. Main thread only.</summary>
        private static void FireOne(StrikeTargeting targeting)
        {
            Vector3 target;
            if (targeting == null || !targeting.TryPick(out target))
            {
                float x = Random.Range(-FallbackRange, FallbackRange);
                float z = Random.Range(-FallbackRange, FallbackRange);
                target = new Vector3(x, 0f, z);
                target.y = TerrainManager.instance.SampleRawHeightSmoothWithWater(target, false, 0f);
            }

            WarheadType type = PickWarhead();
            float yield = type == WarheadType.Nuclear
                ? NuclearYields.Multiplier(NuclearYields.StandardKilotons)
                : ConventionalYields.Multiplier(ConventionalYields.ReferenceKilograms);

            MissileManager.Launch(target, type, yield, BurstType.Groundburst);
        }

        private static WarheadType PickWarhead()
        {
            int w = ModSettings.RandomWarhead != null ? ModSettings.RandomWarhead.value : 0;
            if (w >= 1 && w <= 5) return (WarheadType)(w - 1); // 1..5 → Conventional..Nuclear
            return (WarheadType)Random.Range(0, 5);            // the setting was 0, meaning pick at random
        }
    }
}
