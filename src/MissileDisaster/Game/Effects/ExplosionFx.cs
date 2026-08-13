using System;
using ColossalFramework;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// Plays the game's meteor impact effect on impact, sized to the yield. Main thread only,
    /// called from the impact side of MissileManager.UpdateVisual. A scattering warhead fires one
    /// per submunition. Without the meteor effect - that is, without the Natural Disasters DLC -
    /// it falls back to this mod's own particle fireball.
    /// It dispatches through EffectManager the same way NuclearMeltdown.Game.MeltdownEffect
    /// does, but the size comes from the SpawnArea's radius rather than from the magnitude: see
    /// MissileDisaster.Core.ExplosionScale for why the magnitude cannot do it.
    /// A nuclear detonation does not use the vanilla effect at all - NuclearMushroomFx draws the
    /// fireball and the cloud to real figures, which nothing in the base game can be stretched to
    /// - and every warhead sends a blast front out across the ground through ShockWaveFx.
    /// </summary>
    public static class ExplosionFx
    {
        private static EffectInfo _meteorEffect;
        private static bool _searched;

        /// <summary>
        /// Plays the explosion. center is where the warhead actually went off - on the ground for
        /// a groundburst, up at the burst altitude for an airburst - and groundZero is the spot
        /// below it that takes the damage. The fireball goes where it detonated; a mushroom cloud
        /// always rises from the ground, however high above it the warhead burst.
        /// Main thread. A failure here does not stop the impact resolving.
        /// </summary>
        public static void Play(Vector3 center, Vector3 groundZero, WarheadSpec spec)
        {
            try
            {
                EffectInfo effect = ResolveMeteorEffect();
                // Two different sizes, deliberately. The fireball is what the charge looks like;
                // the blast front is what it damages. They used to be the same number, which made
                // every conventional fireball as wide as its destruction radius.
                float fireball = ExplosionScale.FireballRadius(spec);
                float blast = ExplosionScale.BlastRadius(spec);

                if (spec.Type == WarheadType.Nuclear)
                {
                    // A nuclear detonation is entirely this mod's own, with or without the DLC:
                    // the fireball where it burst and the cloud rising from the ground, both
                    // built to real figures. The vanilla effect has no size of its own to speak
                    // of and cannot be stretched over the kilometres involved.
                    // Because it skips the vanilla effect it also skips that effect's LightEffect,
                    // so the flash it would have carried is played here instead.
                    NuclearMushroomFx.Play(groundZero, center, spec.YieldKilotons, spec.Airburst);
                    DetonationFlashFx.Play(center, Mathf.RoundToInt(spec.YieldKilotons),
                        NuclearCloudDisplay.For(spec.YieldKilotons).FireballRadius);
                    ShockWaveFx.Play(groundZero, spec.DestructionRadius);
                    return;
                }

                // The blast front, out across the ground, whatever the warhead. A scattering
                // warhead gets one from the middle of the pattern rather than one per bomblet.
                ShockWaveFx.Play(groundZero, spec.SubmunitionCount > 1
                    ? Mathf.Max(spec.SpreadRadius, blast) : blast);

                if (effect == null)
                {
                    ExplosionFallback.Play(center, fireball);
                    return;
                }

                if (spec.SubmunitionCount > 1)
                {
                    // Scattering warhead: a smaller effect at each submunition point. The
                    // fireball figure is already per submunition, not for the whole load.
                    Offset2[] offs = SubmunitionScatter.Offsets(spec.SubmunitionCount, spec.SpreadRadius);
                    for (int i = 0; i < offs.Length; i++)
                    {
                        Dispatch(effect, new Vector3(center.x + offs[i].X, center.y, center.z + offs[i].Z),
                            fireball, ExplosionScale.SubmunitionParticlesPerSecond);
                    }
                    return;
                }

                // A single detonation, conventional or thermobaric: one explosion at the
                // detonation point, sized to the yield.
                Dispatch(effect, center, fireball, ExplosionScale.SingleParticlesPerSecond);
            }
            catch (Exception e)
            {
                ModConfig.LogError("ExplosionFx.Play error: " + e);
            }
        }

        /// <summary>
        /// Dispatches the vanilla effect so that it covers visualRadius on the ground. The disc
        /// the particles are spawned over is what makes the effect large; the magnitude is solved
        /// from it to hold the emission near a fixed particle budget, since it is a density.
        /// </summary>
        private static void Dispatch(EffectInfo effect, Vector3 pos, float visualRadius, float particlesPerSecond)
        {
            float spawnRadius = ExplosionScale.SpawnRadius(visualRadius);
            float magnitude = ExplosionScale.Magnitude(spawnRadius, particlesPerSecond);
            var area = new EffectInfo.SpawnArea(pos, Vector3.up, spawnRadius);
            Singleton<EffectManager>.instance.DispatchEffect(
                effect, default(InstanceID), area, Vector3.zero, 0f, magnitude,
                Singleton<VehicleManager>.instance.m_audioGroup);
        }

        /// <summary>
        /// Resolves the meteor impact effect. A meteor is really a MeteorAI carried by a
        /// VehicleInfo, and its m_impactEffect is the impact effect. The search runs once and is
        /// cached; without the DLC the result is null.
        /// </summary>
        private static EffectInfo ResolveMeteorEffect()
        {
            if (_searched) return _meteorEffect;
            _searched = true;
            try
            {
                int count = PrefabCollection<VehicleInfo>.LoadedCount();
                for (int i = 0; i < count; i++)
                {
                    VehicleInfo info = PrefabCollection<VehicleInfo>.GetLoaded((uint)i);
                    if (info == null) continue;
                    MeteorAI ai = info.m_vehicleAI as MeteorAI;
                    if (ai != null && ai.m_impactEffect != null)
                    {
                        _meteorEffect = ai.m_impactEffect;
                        ModConfig.Log("ExplosionFx: found the meteor impact effect");
                        return _meteorEffect;
                    }
                }
                ModConfig.Log("ExplosionFx: no meteor effect, probably no DLC - falling back to the simple fireball");
            }
            catch (Exception e)
            {
                ModConfig.LogError("ExplosionFx.ResolveMeteorEffect error: " + e);
            }
            return _meteorEffect;
        }
    }
}
