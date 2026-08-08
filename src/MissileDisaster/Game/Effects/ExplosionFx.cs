using System;
using ColossalFramework;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// Plays the game's meteor impact effect on impact, scaled to the size of the explosion.
    /// Main thread only, called from the impact side of MissileManager.UpdateVisual.
    /// The larger the explosion, the more copies of the effect are scattered across the
    /// destruction radius, so it reads as an area rather than a point. A scattering warhead
    /// fires one per submunition.
    /// Without the meteor effect - that is, without the Natural Disasters DLC - it falls back to
    /// a simple particle fireball.
    /// It dispatches through EffectManager the same way NuclearMeltdown.Game.MeltdownEffect
    /// does.
    /// </summary>
    public static class ExplosionFx
    {
        private static EffectInfo _meteorEffect;
        private static bool _searched;

        /// <summary>Plays the explosion at the detonation point - on the ground for a groundburst, up at the burst altitude for an airburst. Main thread. A failure here does not stop the impact resolving.</summary>
        public static void Play(Vector3 center, WarheadSpec spec)
        {
            try
            {
                EffectInfo effect = ResolveMeteorEffect();
                // The size of the fireball follows the yield: the spec's radii have already been
                // scaled by the charge, and ExplosionScale turns them into the effect scale.
                float radius = ExplosionScale.VisualRadius(spec);

                if (spec.Type == WarheadType.Nuclear)
                {
                    if (effect != null)
                    {
                        // With the DLC: the meteor impact effect, that large mushroom-shaped
                        // cloud, scaled with the destruction radius.
                        Dispatch(effect, center, ExplosionScale.ForNuclear(spec));
                    }
                    else
                    {
                        // Without it: this mod's own white mushroom cloud, scaled the same way,
                        // which reproduces the lingering column and the canopy spreading out at
                        // the top.
                        NuclearMushroomFx.Play(center, radius);
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
                    float s = ExplosionScale.ForSubmunition(spec);
                    for (int i = 0; i < offs.Length; i++)
                    {
                        Dispatch(effect, new Vector3(center.x + offs[i].X, center.y, center.z + offs[i].Z), s);
                    }
                    return;
                }

                // A single detonation, conventional or thermobaric: one explosion at the
                // detonation point, scaled with the yield.
                Dispatch(effect, center, ExplosionScale.ForSingle(spec));
            }
            catch (Exception e)
            {
                ModConfig.LogError("ExplosionFx.Play error: " + e);
            }
        }

        private static void Dispatch(EffectInfo effect, Vector3 pos, float scale)
        {
            var area = new EffectInfo.SpawnArea(pos, Vector3.up, 0f);
            Singleton<EffectManager>.instance.DispatchEffect(
                effect, default(InstanceID), area, Vector3.zero, 0f, scale,
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
