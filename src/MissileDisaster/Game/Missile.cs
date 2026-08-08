using MissileDisaster.Core;
using MissileDisaster.Game.Effects;
using MissileDisaster.Game.Models;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// One missile in flight. Only the descending half of the trajectory is interpolated -
    /// from an apex at high altitude on a fixed bearing, straight down to the detonation - and
    /// all of it happens on the main thread; the simulation thread never touches this object.
    /// It is drawn as a warhead model (Models/IncomingWarhead.obj), falling back to a sphere if
    /// that cannot be loaded, with the nose - the model's +Z - pointed along the flight path.
    /// There are two points of interest, and they only coincide for a groundburst. Target is the
    /// spot on the ground the damage is applied to, and DetonationPosition is where the warhead
    /// actually goes off: directly above the target, at the spec's burst altitude, for an
    /// airburst. The flight stops at the detonation point, so an airburst never reaches the
    /// ground and its fireball is left hanging in the air over the target.
    /// </summary>
    public class Missile
    {
        private readonly Vector3 _apex;
        private readonly Vector3 _target;
        private readonly Vector3 _detonation;
        private readonly float _groundDistance;
        private readonly WarheadSpec _spec;
        private readonly GameObject _go;
        private float _t;
        private bool _doomed;

        public Vector3 Target => _target;
        public WarheadSpec Spec => _spec;

        /// <summary>Where the warhead goes off: the target itself for a groundburst, or the point at the burst altitude above it for an airburst. The explosion and its sound are placed here.</summary>
        public Vector3 DetonationPosition => _detonation;

        /// <summary>Main thread. The missile's current world position, used to resolve interceptions. Once the GameObject is destroyed this returns the detonation point.</summary>
        public Vector3 CurrentPosition => _go != null ? _go.transform.position : _detonation;

        /// <summary>Where it was launched from, the high-altitude apex. Used to place the launch sound in 3D.</summary>
        public Vector3 LaunchPosition => _apex;

        /// <summary>Whether an interceptor is confirmed to hit it. Such a missile is not engaged again and does no damage when it lands.</summary>
        public bool Doomed => _doomed;

        /// <summary>Marks a missile an interceptor is confirmed to hit. Main thread.</summary>
        public void MarkDoomed() { _doomed = true; }

        public Missile(Vector3 target, WarheadSpec spec)
        {
            _target = target;
            _spec = spec;

            // An airburst detonates above the target rather than on it. The altitude is capped
            // so that the warhead always has some of the descent left to fall.
            float burstAltitude = spec.Airburst
                ? Mathf.Clamp(spec.BurstAltitude, 0f, ModConfig.MaxBurstAltitude)
                : 0f;
            _detonation = new Vector3(target.x, target.y + burstAltitude, target.z);

            // It descends from an apex at high altitude on a fixed bearing. There is no ascent
            // to draw; only the terminal phase exists.
            Offset2 off = LaunchGeometry.BearingOffset(ModConfig.IncomingBearingDegrees, ModConfig.ApexHorizontalOffset);
            _apex = new Vector3(target.x + off.X, target.y + ModConfig.ApexAltitude, target.z + off.Z);
            float dx = target.x - _apex.x;
            float dz = target.z - _apex.z;
            _groundDistance = Mathf.Sqrt(dx * dx + dz * dz); // = ApexHorizontalOffset (>0)
            _t = 0f;

            _go = CreateVisual();
            _go.transform.position = _apex;
            // The descent is a straight line, so the heading never changes: the nose (+Z) is
            // pointed along it exactly once.
            Vector3 velocity = _detonation - _apex;
            if (velocity.sqrMagnitude > 1e-6f)
            {
                _go.transform.rotation = Quaternion.LookRotation(velocity);
            }
            // Add the meteor-like burning trail of sparks and smoke. It is in world space, so
            // it is left behind as the missile passes.
            MissileTrail.Attach(_go);
        }

        /// <summary>Creates the warhead model, falling back to a sphere if it cannot be loaded. The collider is not needed and is destroyed.</summary>
        private static GameObject CreateVisual()
        {
            GameObject go = MissileModelProvider.CreateInstance(ModConfig.IncomingMissileModelName);
            if (go != null)
            {
                go.transform.localScale = new Vector3(
                    ModConfig.IncomingMissileScale, ModConfig.IncomingMissileScale, ModConfig.IncomingMissileScale);
                return go;
            }

            // Fallback: a plain sphere.
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.localScale = new Vector3(6f, 6f, 6f); // the fallback size when the model could not be loaded
            Collider col = sphere.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            return sphere;
        }

        /// <summary>
        /// Main thread. Interpolates the straight descent from the apex to the detonation point,
        /// which is the target for a groundburst and the point above it for an airburst.
        /// Returning true means it went off on this frame. Queuing the damage and destroying the
        /// missile afterwards is MissileManager's job.
        /// </summary>
        public bool UpdateVisual(float simTimeDelta)
        {
            _t = BallisticMath.AdvanceT(_t, _groundDistance, ModConfig.MissileSpeed, simTimeDelta);
            float x = BallisticMath.Lerp(_apex.x, _detonation.x, _t);
            float y = BallisticMath.Lerp(_apex.y, _detonation.y, _t);
            float z = BallisticMath.Lerp(_apex.z, _detonation.z, _t);
            if (_go != null) _go.transform.position = new Vector3(x, y, z);
            return _t >= 1f;
        }

        /// <summary>Main thread. Destroys the missile's GameObject, detaching the trail so it burns out over its remaining lifetime.</summary>
        public void DestroyVisual()
        {
            if (_go == null) return;
            DetachAndFadeTrail(_go);
            Object.Destroy(_go);
        }

        /// <summary>
        /// On impact, the trail's ParticleSystem is detached from the missile - keeping its
        /// world position - and only new emission is stopped, so the existing sparks and smoke
        /// drift out over their remaining lifetime before being destroyed. Destroying it with
        /// the missile would make the whole wake vanish instantly.
        /// </summary>
        private static void DetachAndFadeTrail(GameObject missile)
        {
            ParticleSystem[] systems = missile.GetComponentsInChildren<ParticleSystem>();
            float life = Mathf.Max(ModConfig.TrailFireLifetime, ModConfig.TrailSmokeLifetime);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null) continue;
                ps.transform.SetParent(null, true); // detach it so destroying the parent cannot take it too, keeping its world position
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); // stop emitting, but keep simulating the particles already out
                Object.Destroy(ps.gameObject, life + 0.1f);
            }
        }
    }
}
