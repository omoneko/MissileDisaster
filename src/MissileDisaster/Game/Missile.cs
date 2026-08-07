using MissileDisaster.Core;
using MissileDisaster.Game.Effects;
using MissileDisaster.Game.Models;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// One missile in flight. Only the descending half of the trajectory is interpolated -
    /// from an apex at high altitude on a fixed bearing, straight down to the impact - and all
    /// of it happens on the main thread; the simulation thread never touches this object.
    /// It is drawn as a warhead model (Models/IncomingWarhead.obj), falling back to a sphere if
    /// that cannot be loaded, with the nose - the model's +Z - pointed along the flight path.
    /// </summary>
    public class Missile
    {
        private readonly Vector3 _apex;
        private readonly Vector3 _target;
        private readonly float _groundDistance;
        private readonly WarheadSpec _spec;
        private readonly GameObject _go;
        private float _t;
        private bool _doomed;

        public Vector3 Target => _target;
        public WarheadSpec Spec => _spec;

        /// <summary>Main thread. The missile's current world position, used to resolve interceptions. Once the GameObject is destroyed this returns the impact point.</summary>
        public Vector3 CurrentPosition => _go != null ? _go.transform.position : _target;

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
            Vector3 velocity = _target - _apex;
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
        /// Main thread. Interpolates the straight descent from the apex to the impact.
        /// Returning true means it landed on this frame. Queuing the damage and destroying the
        /// missile afterwards is MissileManager's job.
        /// </summary>
        public bool UpdateVisual(float simTimeDelta)
        {
            _t = BallisticMath.AdvanceT(_t, _groundDistance, ModConfig.MissileSpeed, simTimeDelta);
            float x = BallisticMath.Lerp(_apex.x, _target.x, _t);
            float y = BallisticMath.Lerp(_apex.y, _target.y, _t);
            float z = BallisticMath.Lerp(_apex.z, _target.z, _t);
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
