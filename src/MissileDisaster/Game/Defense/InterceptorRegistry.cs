using System.Collections.Generic;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Defense
{
    /// <summary>
    /// Detects the interceptor sites (PAC3, THAAD, Aegis) and the supporting radar that have
    /// been built, by name, and resolves interceptions against incoming missiles.
    /// <b>All of it is main thread only</b>, the same side as MissileManager.UpdateVisual.
    ///
    /// - The buildings are rescanned every InterceptorScanIntervalFrames rather than every
    ///   frame, since walking all of BuildingManager is expensive.
    /// - Cooldowns tick down every frame and are carried across a rescan by building ID.
    /// - Whether an interception succeeds is decided by Core's InterceptDecision and
    ///   InterceptorTiers, with the probability multiplied while a radar is operating.
    /// - Cost, power and water are left entirely to the asset's own settings; nothing here
    ///   overrides them.
    /// </summary>
    public static class InterceptorRegistry
    {
        private struct Interceptor
        {
            public ushort Id;
            public Vector3 Position;
            public InterceptorTier Tier;
            public float Cooldown; // seconds; it cannot engage while this is above zero
        }

        private static readonly List<Interceptor> _interceptors = new List<Interceptor>();
        private static bool _radarActive;
        private static int _framesSinceScan = int.MaxValue; // forces a scan on the very first tick
        private static int _lastLoggedActive = -1;
        private static int _lastLoggedInactive = -1;
        private static bool _lastLoggedRadar;

        /// <summary>Main thread only. Ticks the cooldowns down and rescans the buildings on its interval.</summary>
        public static void Tick(float deltaSeconds)
        {
            for (int i = 0; i < _interceptors.Count; i++)
            {
                Interceptor it = _interceptors[i];
                if (it.Cooldown > 0f)
                {
                    it.Cooldown = Mathf.Max(0f, it.Cooldown - deltaSeconds);
                    _interceptors[i] = it;
                }
            }

            if (_framesSinceScan >= ModConfig.InterceptorScanIntervalFrames)
            {
                _framesSinceScan = 0;
                Scan();
            }
            else
            {
                _framesSinceScan++;
            }
        }

        /// <summary>
        /// Main thread only. Against one incoming missile, fires exactly one launcher that is
        /// in range and off cooldown, working from the highest layer down.
        /// Firing always spends the cooldown, so an engagement is one round. Whether it hits is
        /// a single draw against the single-shot kill probability.
        /// Returns true if a launcher fired, with launcherPosition where it fired from, kind
        /// being the layer and isHit whether it will connect; false if nothing could fire.
        /// altitude is missilePos.y - targetGround.y, which works because the dive is steep
        /// enough that the ground under the missile is about the height of the impact point.
        /// </summary>
        public static bool TryEngage(Vector3 missilePos, Vector3 targetGround,
            out Vector3 launcherPosition, out InterceptorKind kind, out bool isHit)
        {
            launcherPosition = missilePos;
            kind = InterceptorKind.Pac;
            isHit = false;
            if (_interceptors.Count == 0) return false;

            float altitude = missilePos.y - targetGround.y;
            if (altitude < 0f) altitude = 0f;
            float multiplier = _radarActive ? ModConfig.RadarSupportMultiplier : 1f;

            InterceptorTier[] ordered = InterceptorTiers.Ordered; // Arrow→Sam→Pac
            for (int t = 0; t < ordered.Length; t++)
            {
                InterceptorKind tierKind = ordered[t].Kind;
                for (int i = 0; i < _interceptors.Count; i++)
                {
                    Interceptor it = _interceptors[i];
                    if (it.Tier.Kind != tierKind || it.Cooldown > 0f) continue;

                    float dx = it.Position.x - missilePos.x;
                    float dz = it.Position.z - missilePos.z;
                    float horizontalDistance = Mathf.Sqrt(dx * dx + dz * dz);
                    if (!InterceptDecision.InEngagementZone(altitude, horizontalDistance, it.Tier)) continue;

                    // Fire: one launcher, one round, spending its cooldown. Whether it hits is
                    // a single draw against the single-shot kill probability.
                    it.Cooldown = it.Tier.CooldownSeconds;
                    _interceptors[i] = it;
                    float chance = Mathf.Clamp01(it.Tier.InterceptChance * multiplier);
                    isHit = Random.value < chance;
                    launcherPosition = it.Position;
                    kind = tierKind;
                    ModConfig.Log("Interceptor fired: " + tierKind + " " + (isHit ? "HIT" : "MISS")
                        + " (alt=" + Mathf.RoundToInt(altitude) + "m, dist=" + Mathf.RoundToInt(horizontalDistance)
                        + "m, Pk=" + chance.ToString("0.00") + ", radar=" + _radarActive + ")");
                    return true;
                }
            }
            return false;
        }

        /// <summary>Main thread only. Discards the tracked state, on a level change.</summary>
        public static void Reset()
        {
            _interceptors.Clear();
            _radarActive = false;
            _framesSinceScan = int.MaxValue;
            _lastLoggedActive = -1;
            _lastLoggedInactive = -1;
            _lastLoggedRadar = false;
        }

        /// <summary>Walks BuildingManager and picks up the operating interceptor sites and radars whose names match.</summary>
        private static void Scan()
        {
            BuildingManager bm = BuildingManager.instance;
            if (bm == null) return;
            Building[] buffer = bm.m_buildings.m_buffer;
            if (buffer == null) return;

            // Keep the previous state by ID, so cooldowns carry across the rescan.
            Dictionary<ushort, float> priorCooldowns = null;
            if (_interceptors.Count > 0)
            {
                priorCooldowns = new Dictionary<ushort, float>(_interceptors.Count);
                for (int i = 0; i < _interceptors.Count; i++)
                {
                    priorCooldowns[_interceptors[i].Id] = _interceptors[i].Cooldown;
                }
            }

            _interceptors.Clear();
            bool radar = false;
            int inactiveMatches = 0; // matched by name but unfinished or destroyed; for diagnostics
            Building.Flags firstInactiveFlags = 0; // flags of the first such building, to explain why
            string firstInactiveName = null;

            for (int i = 1; i < buffer.Length; i++)
            {
                Building b = buffer[i];
                Building.Flags flags = b.m_flags;
                if ((flags & Building.Flags.Created) == 0) continue;

                BuildingInfo info = b.Info;
                string name = info != null ? info.name : null;
                if (string.IsNullOrEmpty(name)) continue;

                bool isRadar = InterceptorNameMatcher.IsRadar(name);
                InterceptorKind kind;
                bool isInterceptor = InterceptorNameMatcher.TryMatchTier(name, out kind);
                if (!isRadar && !isInterceptor) continue;

                if (!IsOperational(flags))
                {
                    if (inactiveMatches == 0) { firstInactiveFlags = flags; firstInactiveName = name; }
                    inactiveMatches++;
                    continue;
                }

                if (isRadar)
                {
                    radar = true;
                    continue;
                }

                float cooldown = 0f;
                if (priorCooldowns != null) priorCooldowns.TryGetValue((ushort)i, out cooldown);

                _interceptors.Add(new Interceptor
                {
                    Id = (ushort)i,
                    Position = b.m_position,
                    Tier = TierFor(kind),
                    Cooldown = cooldown,
                });
            }

            _radarActive = radar;
            LogChangesIfAny(_interceptors.Count, inactiveMatches, radar, firstInactiveFlags, firstInactiveName);
        }

        /// <summary>Logs only when what was detected changes, which makes it useful in game without flooding the log on every scan.</summary>
        private static void LogChangesIfAny(int active, int inactive, bool radar,
            Building.Flags firstInactiveFlags, string firstInactiveName)
        {
            if (active == _lastLoggedActive && inactive == _lastLoggedInactive && radar == _lastLoggedRadar) return;
            _lastLoggedActive = active;
            _lastLoggedInactive = inactive;
            _lastLoggedRadar = radar;
            string msg = "Interceptors detected: active=" + active + ", radar=" + radar;
            if (inactive > 0)
            {
                // Log the first such building's flags, to explain why it is not operating.
                msg += ", matched by name but not operating=" + inactive
                    + " [e.g. '" + firstInactiveName + "' flags=" + firstInactiveFlags + "]";
            }
            ModConfig.Log(msg);
        }

        /// <summary>
        /// Whether a building is operating: created, completed and not destroyed.
        /// Note that some custom assets never get Building.Flags.Active even when they are
        /// powered. Requiring Active would stop interception working at all, so the test is
        /// Completed plus not destroyed.
        /// </summary>
        private static bool IsOperational(Building.Flags flags)
        {
            if ((flags & Building.Flags.Created) == 0) return false;
            if ((flags & Building.Flags.Completed) == 0) return false;
            const Building.Flags dead = Building.Flags.Abandoned | Building.Flags.BurnedDown
                | Building.Flags.Collapsed | Building.Flags.Deleted;
            return (flags & dead) == 0;
        }

        private static InterceptorTier TierFor(InterceptorKind kind)
        {
            switch (kind)
            {
                case InterceptorKind.Arrow: return InterceptorTiers.Arrow;
                case InterceptorKind.Sam: return InterceptorTiers.Sam;
                default: return InterceptorTiers.Pac;
            }
        }
    }
}
