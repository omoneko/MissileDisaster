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
                ApplyBlast(target, spec.CraterRadius, spec.CraterDepth, spec.DestructionRadius, spec.BurnRadius, spec.RaiseCraterEdges);
            }
            else
            {
                // Scattering warheads - cluster and white phosphorus - do smaller damage at
                // each submunition point, placed deterministically.
                Offset2[] offsets = SubmunitionScatter.Offsets(spec.SubmunitionCount, spec.SpreadRadius);
                for (int i = 0; i < offsets.Length; i++)
                {
                    Vector3 p = new Vector3(target.x + offsets[i].X, target.y, target.z + offsets[i].Z);
                    ApplyBlast(p, spec.CraterRadius, spec.CraterDepth, spec.DestructionRadius, spec.BurnRadius, spec.RaiseCraterEdges);
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

        /// <summary>Applies one detonation's crater, area destruction and fires. Simulation thread.</summary>
        private static void ApplyBlast(Vector3 pos, float craterRadius, float craterDepth,
            float destructionRadius, float burnRadius, bool raiseEdges)
        {
            // WithBurst sets craterRadius to 0 for an airburst, which is how ground and air
            // bursts are told apart here.
            bool groundburst = craterRadius > 0f;

            // Only a groundburst leaves a crater, dug with the game's own MakeCrater. It is
            // capped so that even a strategic warhead does not wreck the terrain.
            if (groundburst)
            {
                float cRadius = craterRadius > ModConfig.CraterRadiusMax ? ModConfig.CraterRadiusMax : craterRadius;
                float cDepth = craterDepth > ModConfig.CraterDepthMax ? ModConfig.CraterDepthMax : craterDepth;
                DisasterHelpers.MakeCrater(new Vector2(pos.x, pos.z), cRadius, cDepth, raiseEdges);
            }

            // Area destruction and fires. The last two arguments to DestroyStuff, burnMin and
            // burnMax, are the burn band, within which buildings catch fire.
            // preRadius and totalRadius are the outer bound of the whole operation, so they take
            // the larger of destMax and burnMax - passing anything smaller is the known trap
            // where the outer area is never scanned.
            int seed = (int)SimulationManager.instance.m_randomizer.Int32(1000000u);
            // A high-yield warhead's real radii exceed the map, so they are capped to keep
            // DestroyStuff from an extreme scan.
            float destMax = destructionRadius > ModConfig.MaxEffectRadius ? ModConfig.MaxEffectRadius : destructionRadius;
            float burnMax = burnRadius > ModConfig.MaxEffectRadius ? ModConfig.MaxEffectRadius : burnRadius;
            float outer = destMax > burnMax ? destMax : burnMax;
            // Falloff in concentric rings: everything within core is destroyed, and from core
            // out to destMax the probability drops off, with destMin equal to core.
            float core = destMax * ModConfig.DestructionCoreFraction;
            // Inside removeRadius everything goes, foundations included - roads, bridges and
            // footings alike.
            //  - Groundburst: the core becomes the crater and is removed entirely, while further
            //    out only buildings are destroyed, by chance.
            //  - Airburst: removeRadius is 0, so nothing is removed. Buildings collapse, but the
            //    footings, roads, water pipes and metro tunnels survive.
            float removeRadius = groundburst ? core : 0f;
            float destMin = core;
            float burnMin = core;
            if (burnMin > burnMax * 0.5f) burnMin = burnMax * 0.5f; // hold the inner edge back so the burn band cannot invert
            DisasterHelpers.DestroyStuff(seed, null, pos, outer, outer, removeRadius, destMin, destMax, burnMin, burnMax);
        }
    }
}
