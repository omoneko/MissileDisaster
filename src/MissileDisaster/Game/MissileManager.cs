using System.Collections.Generic;
using MissileDisaster.Core;
using MissileDisaster.Game.Audio;
using MissileDisaster.Game.Defense;
using MissileDisaster.Game.Effects;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// Static coordinator for launching and tracking missiles.
    /// The thread boundary matters here:
    ///  - _missiles, the list of missiles in flight, is touched by the main thread alone,
    ///    through Launch, UpdateVisual and Reset. The simulation thread never reads it.
    ///  - Impact damage goes through DisasterHelpers and therefore has to run on the simulation
    ///    thread. So on impact the main thread pushes an ImpactJob - the position plus the
    ///    warhead spec, all plain values - onto _impactQueue, and the simulation thread drains
    ///    and resolves that queue under a lock in UpdateSimulation.
    ///  The upshot is that List&lt;Missile&gt; is never shared across threads: the only thing that
    ///  crosses the boundary is a small, lock-protected queue of values.
    /// </summary>
    public static class MissileManager
    {
        private struct ImpactJob
        {
            public Vector3 Target;
            public WarheadSpec Spec;
        }

        private static readonly List<Missile> _missiles = new List<Missile>();                    // main thread only
        private static readonly List<InterceptorProjectile> _interceptors = new List<InterceptorProjectile>(); // main thread only
        private static readonly List<ImpactJob> _impactQueue = new List<ImpactJob>();  // crosses threads, lock-protected
        private static readonly object _impactLock = new object();

        /// <summary>Read from the main thread.</summary>
        public static bool HasActive => _missiles.Count > 0;

        /// <summary>
        /// Main thread only. Launches with a spec whose effect radii are scaled by
        /// yieldMultiplier and adjusted for the burst height. The caller works the multiplier
        /// out from the warhead - from kilotons for a nuclear one, from kilograms otherwise.
        /// An airburst removes the crater and the contamination and widens the destruction and
        /// the fires.
        /// </summary>
        public static void Launch(Vector3 target, WarheadType type, float yieldMultiplier, BurstType burst)
        {
            WarheadSpec spec = WarheadSpec.For(type);
            if (yieldMultiplier > 0f)
            {
                spec = spec.Scaled(yieldMultiplier);
            }
            spec = spec.WithBurst(burst);
            Missile missile = new Missile(target, spec);
            _missiles.Add(missile);

            // The launch sound, chosen at random between two samples. It is 3D, placed at the
            // apex it was launched from, so it fades with distance.
            string launcher = UnityEngine.Random.value < 0.5f ? SoundLibrary.Launcher2 : SoundLibrary.Launcher7;
            SoundPlayer.PlayAt(launcher, missile.LaunchPosition, ModConfig.SoundVolumeNormal,
                ModConfig.SoundLaunchMinDistance, ModConfig.SoundLaunchMaxDistance);

            ModConfig.Log("Missile launched at " + target + " (" + type + ", " + burst
                + ", x" + yieldMultiplier.ToString("0.00") + ")");
        }

        /// <summary>
        /// Main thread only. Advances the interceptor site scan and cooldowns, the interceptor
        /// missiles and their resolution, and the incoming missiles and their engagements.
        /// A missile that lands without being shot down has its damage queued and is destroyed;
        /// one an interceptor is confirmed to hit never lands and does no damage.
        /// </summary>
        public static void UpdateVisual(float simTimeDelta)
        {
            InterceptorRegistry.Tick(simTimeDelta);
            UpdateInterceptors(simTimeDelta);

            for (int i = _missiles.Count - 1; i >= 0; i--)
            {
                Missile m = _missiles[i];
                bool impacted = m.UpdateVisual(simTimeDelta);
                if (impacted)
                {
                    // A doomed missile counts as already intercepted: no damage and no
                    // explosion. Only one that got through gets both.
                    if (!m.Doomed)
                    {
                        ExplosionFx.Play(m.Target, m.Spec); // the impact effect, scaled with the yield. Main thread.
                        PlayImpactSound(m.Target, m.Spec);  // the blast; a nuclear one is twice as loud
                        // Publish the nuclear impact to the loosely coupled beacon, which is
                        // what lets Alien Invasion topple a tripod caught in a direct hit.
                        if (m.Spec.Type == WarheadType.Nuclear)
                        {
                            NuclearImpactBeacon.Publish(m.Target.x, m.Target.z);
                        }
                        // Publish every impact to the general beacon, which CS:WARFRONT reads to
                        // damage military units.
                        ImpactBeacon.Publish(m.Target.x, m.Target.z,
                            m.Spec.DestructionRadius, m.Spec.BurnRadius, m.Spec.Type == WarheadType.Nuclear);
                        lock (_impactLock)
                        {
                            _impactQueue.Add(new ImpactJob { Target = m.Target, Spec = m.Spec });
                        }
                    }
                    RemoveMissile(i, m);
                    continue;
                }

                if (m.Doomed) continue; // an interceptor is already on its way; do not engage again

                // Engagement: exactly one launcher in range and off cooldown fires a real
                // round. If it is going to hit, the missile is marked.
                Vector3 launcher;
                InterceptorKind kind;
                bool isHit;
                if (InterceptorRegistry.TryEngage(m.CurrentPosition, m.Target, out launcher, out kind, out isHit))
                {
                    if (isHit) m.MarkDoomed();
                    _interceptors.Add(new InterceptorProjectile(launcher, m, kind, isHit));
                }
            }
        }

        /// <summary>Main thread only. Advances the interceptors and resolves them on reaching the intercept point: a flash for a kill, a puff of smoke for a miss.</summary>
        private static void UpdateInterceptors(float simTimeDelta)
        {
            for (int j = _interceptors.Count - 1; j >= 0; j--)
            {
                InterceptorProjectile p = _interceptors[j];
                bool connectedHit;
                Vector3 point;
                if (!p.Update(simTimeDelta, out connectedHit, out point)) continue;

                if (connectedHit && p.Prey != null)
                {
                    Missile prey = p.Prey;
                    int idx = _missiles.IndexOf(prey);
                    if (idx >= 0) RemoveMissile(idx, prey); // shot down: it vanishes without damage, and any other interceptor chasing it is released
                    InterceptFx.PlayFlash(point);
                    SoundPlayer.PlayAt(SoundLibrary.Intercept, point, ModConfig.SoundVolumeNormal,
                        ModConfig.SoundInterceptMinDistance, ModConfig.SoundInterceptMaxDistance);
                }
                else
                {
                    InterceptFx.PlayFizzle(point);
                }

                p.Destroy();
                _interceptors.RemoveAt(j);
            }
        }

        /// <summary>The impact sound, on the main thread. A nuclear blast is twice as loud and audible much further away than the others.</summary>
        private static void PlayImpactSound(Vector3 target, WarheadSpec spec)
        {
            if (spec.Type == WarheadType.Nuclear)
            {
                SoundPlayer.PlayAt(SoundLibrary.Nuclear, target, ModConfig.SoundVolumeNuclear,
                    ModConfig.SoundNuclearMinDistance, ModConfig.SoundNuclearMaxDistance);
            }
            else
            {
                SoundPlayer.PlayAt(SoundLibrary.Explosion, target, ModConfig.SoundVolumeNormal,
                    ModConfig.SoundExplosionMinDistance, ModConfig.SoundExplosionMaxDistance);
            }
        }

        /// <summary>Main thread only. Destroys and removes an incoming missile, releasing any interceptor chasing it.</summary>
        private static void RemoveMissile(int index, Missile m)
        {
            m.DestroyVisual();
            _missiles.RemoveAt(index);
            for (int j = 0; j < _interceptors.Count; j++)
            {
                if (_interceptors[j].Prey == m) _interceptors[j].ClearPrey();
            }
        }

        /// <summary>Simulation thread only. Drains the impact queue and resolves it through DisasterHelpers.</summary>
        public static void UpdateSimulation()
        {
            List<ImpactJob> jobs = null;
            lock (_impactLock)
            {
                if (_impactQueue.Count > 0)
                {
                    jobs = new List<ImpactJob>(_impactQueue);
                    _impactQueue.Clear();
                }
            }
            if (jobs == null) return;
            for (int i = 0; i < jobs.Count; i++)
            {
                ImpactResolver.Resolve(jobs[i].Target, jobs[i].Spec);
            }
        }

        /// <summary>Main thread only. Destroys every missile and interceptor in flight and empties the queue.</summary>
        public static void Reset()
        {
            for (int i = 0; i < _missiles.Count; i++) _missiles[i].DestroyVisual();
            _missiles.Clear();
            for (int j = 0; j < _interceptors.Count; j++) _interceptors[j].Destroy();
            _interceptors.Clear();
            lock (_impactLock) { _impactQueue.Clear(); }
            NuclearImpactBeacon.Reset(); // clear the nuclear beacon too, so nothing carries into the next level
            ImpactBeacon.Reset(); // and the general beacon likewise
        }
    }
}
