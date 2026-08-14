using ColossalFramework;
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

            SplashWater(pos, spec);
            StartBlackRain(pos, spec);
        }

        /// <summary>
        /// Brings the black rain down after a nuclear detonation: soot and fallout scavenged out
        /// of the column by the water the fireball condensed, falling dirty enough to mark what
        /// it lands on.
        /// <para>
        /// The game does none of this by itself - its weather simulation never reads pollution -
        /// so rain a player sees after a strike is the ordinary cycle and a coincidence. This is
        /// the deliberate version. The ground stain is cosmetic; the fallout that actually does
        /// something is the ground pollution applied above.
        /// </para>
        /// Simulation thread, like the rest of this class.
        /// </summary>
        private static void StartBlackRain(Vector3 pos, WarheadSpec spec)
        {
            try
            {
                if (spec.Type != WarheadType.Nuclear) return;
                if (ModSettings.BlackRain == null || ModSettings.BlackRain.value == 0) return;
                // A coin toss, on the simulation thread's own randomizer so that reloading the
                // save and replaying the strike gives the same weather.
                int roll = (int)SimulationManager.instance.m_randomizer.Int32(100u);
                if (!BlackRain.FallsThisTime(spec.YieldKilotons, roll))
                {
                    ModConfig.Log("No black rain this time (roll " + roll + " of "
                        + BlackRain.ChancePercent + ").");
                    return;
                }

                float rainSeconds = BlackRain.RainSeconds(spec.YieldKilotons);
                BlackRainController.Begin(rainSeconds);

                // Sized from the FIRES, not the fallout: the soot that makes the rain black comes
                // off the burning city. That is also why an airburst gets one - Hiroshima was an
                // airburst at 600 m, and its black rain is the case this is modelled on.
                float stainRadius = BlackRain.StainRadius(spec.BurnRadius);
                if (stainRadius <= 0f) return;

                // Downwind, because the column is carried before it comes down. The Hiroshima
                // rain fell to the north and west of the hypocentre, not in a ring around it.
                float windX, windZ;
                WindDirection(out windX, out windZ);
                float cx, cz;
                BlackRain.Centre(pos.x, pos.z, stainRadius, windX, windZ, out cx, out cz);

                // Drawing is main-thread work, so it is handed over rather than done here.
                float stainSeconds = BlackRain.StainSeconds(rainSeconds);
                BlackRainQueue.Enqueue(new Vector3(cx, pos.y, cz), stainRadius, stainSeconds,
                    windX, windZ);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("ImpactResolver.StartBlackRain error: " + e);
            }
        }

        /// <summary>
        /// The unit vector the wind is blowing towards. WeatherManager.m_windDirection is an
        /// angle in degrees; a missing manager or a still day falls back to north, so the stain
        /// is offset consistently rather than jumping back to a centred disc.
        /// </summary>
        private static void WindDirection(out float x, out float z)
        {
            x = 0f;
            z = 1f;
            try
            {
                WeatherManager wm = Singleton<WeatherManager>.instance;
                if (wm == null) return;
                float radians = wm.m_windDirection * Mathf.Deg2Rad;
                x = Mathf.Sin(radians);
                z = Mathf.Cos(radians);
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("ImpactResolver.WindDirection error: " + e);
            }
        }

        /// <summary>
        /// Presses the water surface down under the burst, if it went off over or beside water.
        /// The game's water simulation takes it from there: the cavity rebounds and the wave
        /// radiates outward on its own, which is both the real behaviour and far better than
        /// anything hand-rolled would be.
        /// <para>
        /// DisasterHelpers.SplashWater is the same call the vanilla meteor and earthquake make,
        /// so it is contracted to the simulation thread exactly like MakeCrater and DestroyStuff
        /// above - which is where this already is.
        /// </para>
        /// </summary>
        private static void SplashWater(Vector3 pos, WarheadSpec spec)
        {
            try
            {
                // The fireball is what couples the energy into the water, so it sizes the splash.
                // Nuclear builds its fireball from the yield rather than carrying a figure on the
                // spec - see WarheadSpec.FireballRadius.
                float fireball = spec.Type == WarheadType.Nuclear
                    ? NuclearCloudDisplay.For(spec.YieldKilotons).FireballRadius
                    : spec.FireballRadius;
                if (!WaterSplash.Displaces(fireball)) return;

                var flat = new Vector2(pos.x, pos.z);
                if (!TerrainManager.instance.HasWater(flat)) return;

                float radius = WaterSplash.Radius(fireball);
                float depth = WaterSplash.Depth(fireball);
                DisasterHelpers.SplashWater(flat, radius, depth);
                ModConfig.Log("Water displaced at " + flat + ": radius " + radius.ToString("0")
                    + " m, pressed down " + depth.ToString("0.0") + " m");
            }
            catch (System.Exception e)
            {
                // The water is decoration; losing it must never cost the impact its damage.
                ModConfig.LogError("ImpactResolver.SplashWater error: " + e);
            }
        }
    }
}
