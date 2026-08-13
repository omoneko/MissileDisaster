using System;
using ColossalFramework;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// Draws the explosion on impact, sized to the yield. Main thread only, called from the
    /// impact side of MissileManager.UpdateVisual. A scattering warhead draws one per submunition.
    ///
    /// <para>
    /// Every fireball here is the mod's own. The vanilla meteor impact effect used to draw the
    /// non-nuclear ones, and it was dropped because its size cannot be controlled:
    /// DispatchEffect offers a SpawnArea radius and a magnitude, the magnitude is a particle
    /// density, and the size of an individual flame lives in the effect prefab - a shared game
    /// asset. Shrinking the disc for a small warhead therefore gave fewer meteor-sized flames
    /// rather than a smaller explosion. See MissileDisaster.Core.ExplosionScale for the IL this
    /// was read from. Losing the vanilla effect also loses the LightEffect it carried, so the
    /// flash is drawn here too, by DetonationFlashFx.
    /// </para>
    ///
    /// A nuclear detonation is drawn by NuclearMushroomFx, to real figures, and every warhead
    /// sends a blast front out across the ground through ShockWaveFx.
    /// </summary>
    public static class ExplosionFx
    {
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
                    DetonationFlashFx.PlayNuclear(center, Mathf.RoundToInt(spec.YieldKilotons),
                        NuclearCloudDisplay.For(spec.YieldKilotons).FireballRadius);
                    ShockWaveFx.Play(groundZero, spec.DestructionRadius);
                    return;
                }

                // The blast front, out across the ground, whatever the warhead. A scattering
                // warhead gets one from the middle of the pattern rather than one per bomblet.
                ShockWaveFx.Play(groundZero, spec.SubmunitionCount > 1
                    ? Mathf.Max(spec.SpreadRadius, blast) : blast);

                if (spec.SubmunitionCount > 1)
                {
                    // Scattering warhead: a fireball at each submunition point. The figure is
                    // already per submunition, not for the whole load.
                    Offset2[] offs = SubmunitionScatter.Offsets(spec.SubmunitionCount, spec.SpreadRadius);
                    for (int i = 0; i < offs.Length; i++)
                    {
                        ExplosionFallback.Play(
                            new Vector3(center.x + offs[i].X, center.y, center.z + offs[i].Z), fireball);
                    }
                    // One flash for the pattern rather than fourteen stacked on top of each other,
                    // and no mushroom: scattered bomblets throw up a haze, not a column.
                    DetonationFlashFx.PlayConventional(center, Mathf.Max(fireball, spec.SpreadRadius * 0.25f));
                    return;
                }

                // A single detonation, conventional or thermobaric.
                ExplosionFallback.Play(center, fireball);
                DetonationFlashFx.PlayConventional(center, fireball);
                // The column of dirt and smoke it lifts. It rises from the ground however high
                // the warhead burst, the same way the nuclear cloud does.
                SmallMushroomFx.Play(groundZero, fireball);
            }
            catch (Exception e)
            {
                ModConfig.LogError("ExplosionFx.Play error: " + e);
            }
        }
    }
}
