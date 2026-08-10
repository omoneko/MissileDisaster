using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// Resolves the damage of an impact. DisasterHelpers is contracted to the simulation
    /// thread, so this must only be called from MissileManager.UpdateSimulation.
    /// </summary>
    public static class ImpactResolver
    {
        public static void Resolve(Vector3 target, WarheadSpec spec)
        {
            if (spec.SubmunitionCount <= 1)
            {
                // A single detonation: conventional, thermobaric or nuclear.
                ApplyBlast(target, spec);
            }
            else
            {
                // Scattering warheads - cluster and white phosphorus - do smaller damage at
                // each submunition point, placed deterministically.
                Offset2[] offsets = SubmunitionScatter.Offsets(spec.SubmunitionCount, spec.SpreadRadius);
                for (int i = 0; i < offsets.Length; i++)
                {
                    Vector3 p = new Vector3(target.x + offsets[i].X, target.y, target.z + offsets[i].Z);
                    ApplyBlast(p, spec);
                }
            }

            if (spec.Contaminates)
            {
                // Add the radioactive contamination zone: tracked in the ledger, lifting after
                // 50 years, and not removed by anything else. Simulation thread.
                long nowTicks = SimulationManager.instance.m_currentGameTime.Ticks;
                Contamination.ContaminationManager.AddZone(
                    new MissileDisaster.Core.ContaminationZone(target.x, target.z, spec.ContaminationRadius, nowTicks));
            }

            ModConfig.Log("Impact resolved: " + spec.Type + " x" + spec.SubmunitionCount + " at " + target);
        }

        /// <summary>Applies one detonation's crater, area destruction and fires, always on the ground below the warhead. Simulation thread.</summary>
        private static void ApplyBlast(Vector3 pos, WarheadSpec spec)
        {
            // A warhead digs a crater only if it goes off on the ground and has the punch to
            // move earth at all - an incendiary such as white phosphorus does not.
            bool craters = !spec.Airburst && spec.CraterRadius > 0f;

            // The crater is dug with the game's own MakeCrater, held under a soft ceiling so that
            // even a strategic warhead does not wreck the terrain - while a bigger one still
            // leaves a bigger hole than a smaller one, which a hard cap did not.
            float cRadius = EffectCeiling.Soft(spec.CraterRadius, ModConfig.CraterRadiusKnee, ModConfig.CraterRadiusMax);
            if (craters)
            {
                float cDepth = EffectCeiling.Soft(spec.CraterDepth, ModConfig.CraterDepthKnee, ModConfig.CraterDepthMax);
                DisasterHelpers.MakeCrater(new Vector2(pos.x, pos.z), cRadius, cDepth, spec.RaiseCraterEdges);
            }

            // Area destruction and fires. The last two arguments to DestroyStuff, burnMin and
            // burnMax, are the burn band, within which buildings catch fire.
            // preRadius and totalRadius are the outer bound of the whole operation, so they take
            // the larger of destMax and burnMax - passing anything smaller is the known trap
            // where the outer area is never scanned.
            int seed = (int)SimulationManager.instance.m_randomizer.Int32(1000000u);
            // A high-yield warhead's real radii exceed the map, so they are held under a ceiling
            // to keep DestroyStuff from an extreme scan. It is the map's diagonal, so a warhead
            // that really does reach across the whole map is allowed to.
            float destMax = spec.DestructionRadius > ModConfig.MaxEffectRadius ? ModConfig.MaxEffectRadius : spec.DestructionRadius;
            float burnMax = spec.BurnRadius > ModConfig.MaxEffectRadius ? ModConfig.MaxEffectRadius : spec.BurnRadius;
            float outer = destMax > burnMax ? destMax : burnMax;
            // Falloff in concentric rings: everything within core is destroyed, and from core
            // out to destMax the probability drops off, with destMin equal to core.
            float core = destMax * ModConfig.DestructionCoreFraction;
            // Inside removeRadius everything goes, foundations included - roads, bridges and
            // footings alike. It is tied to the crater, so only a warhead that actually digs one
            // takes the ground with it.
            //  - Groundburst: the core becomes the crater and is removed entirely, while further
            //    out only buildings are destroyed, by chance.
            //  - Airburst, or an incendiary that leaves no crater: removeRadius is 0, so nothing
            //    is removed. Buildings collapse or burn, but the footings, roads, water pipes and
            //    metro tunnels survive.
            // It never falls short of the crater itself: the ground inside the bowl has been
            // excavated, so a road left standing over it would hang in mid-air.
            float removeRadius = 0f;
            if (craters) removeRadius = core > cRadius ? core : cRadius;
            float destMin = core;
            float burnMin = core;
            if (burnMin > burnMax * 0.5f) burnMin = burnMax * 0.5f; // hold the inner edge back so the burn band cannot invert
            DisasterHelpers.DestroyStuff(seed, null, pos, outer, outer, removeRadius, destMin, destMax, burnMin, burnMax);
        }
    }
}
