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
    /// The nuclear mushroom cloud is always this mod's own NuclearMushroomFx, with or without the
    /// DLC, because it is the only one whose canopy can be built to the destruction radius; the
    /// vanilla effect is used for the flash at the point the warhead went off.
    /// </summary>
    public static class ExplosionFx
    {
        // The nuclear fireball against what the weapon destroys. At 150 kt the fireball is about
        // 500 m across and the 5 psi contour 3.7 km, so the ball is roughly a seventh of it.
        private const float NuclearFireballFraction = 0.15f;

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
                // The size of the fireball follows the yield: the spec's radii have already been
                // scaled by the charge, and ExplosionScale turns them into the effect scale.
                float radius = ExplosionScale.VisualRadius(spec);

                if (spec.Type == WarheadType.Nuclear)
                {
                    // The cloud is always this mod's own, raised from ground zero with a canopy as
                    // wide as the destruction radius. The vanilla effect cannot be stretched to
                    // kilometres, so it is not asked to be the cloud.
                    NuclearMushroomFx.Play(groundZero, spec.DestructionRadius);
                    // The flash goes where the warhead actually went off - up in the air for an
                    // airburst - and is the size of the fireball, not of the destruction.
                    float fireball = spec.DestructionRadius * NuclearFireballFraction;
                    if (effect != null)
                    {
                        Dispatch(effect, center, fireball, ExplosionScale.NuclearParticlesPerSecond);
                    }
                    else if (spec.Airburst)
                    {
                        // Without the DLC a groundburst already has the cloud's own fireball at
                        // this point, so only an airburst needs one adding in the air.
                        ExplosionFallback.Play(center, fireball);
                    }
                    return;
                }

                if (effect == null)
                {
                    ExplosionFallback.Play(center, radius);
                    return;
                }

                if (spec.SubmunitionCount > 1)
                {
                    // Scattering warhead: a smaller effect at each submunition point.
                    Offset2[] offs = SubmunitionScatter.Offsets(spec.SubmunitionCount, spec.SpreadRadius);
                    for (int i = 0; i < offs.Length; i++)
                    {
                        Dispatch(effect, new Vector3(center.x + offs[i].X, center.y, center.z + offs[i].Z),
                            radius, ExplosionScale.SubmunitionParticlesPerSecond);
                    }
                    return;
                }

                // A single detonation, conventional or thermobaric: one explosion at the
                // detonation point, sized to the yield.
                Dispatch(effect, center, radius, ExplosionScale.SingleParticlesPerSecond);
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
