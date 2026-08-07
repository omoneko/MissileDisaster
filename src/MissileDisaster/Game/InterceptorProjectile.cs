using MissileDisaster.Core;
using MissileDisaster.Game.Effects;
using MissileDisaster.Game.Models;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// One visible interceptor, flying from its launcher towards an incoming missile.
    /// Everything happens on the main thread; the simulation thread never touches it. It
    /// follows its prey's current position every frame and resolves on arrival.
    /// If it was going to hit and the prey is still there when it arrives, the missile is shot
    /// down; if it was not, or the prey has already gone, it simply fizzles.
    /// The model (Models/&lt;name&gt;.obj, +Z as the nose) points along the flight path, and its
    /// exhaust lingers for a moment after it is destroyed.
    /// </summary>
    public class InterceptorProjectile
    {
        private readonly GameObject _go;
        private readonly float _speed;
        private readonly bool _isHit;
        private Missile _prey;              // what it is chasing; cleared once that missile is gone, after which it flies on to the last known point
        private Vector3 _lastPreyPos;
        private float _elapsed;

        /// <summary>The incoming missile this interceptor is after. Null once it has fizzled or the target has gone.</summary>
        public Missile Prey => _prey;

        public InterceptorProjectile(Vector3 origin, Missile prey, InterceptorKind kind, bool isHit)
        {
            _prey = prey;
            _isHit = isHit;
            _speed = SpeedFor(kind);
            _lastPreyPos = prey != null ? prey.CurrentPosition : origin;

            _go = CreateVisual(kind);
            _go.transform.position = origin;
            AimAt(_lastPreyPos);
            InterceptorTrail.Attach(_go);
        }

        /// <summary>Releases the chase when the target is gone, sending it on to the last known point. Main thread.</summary>
        public void ClearPrey()
        {
            if (_prey != null) _lastPreyPos = _prey.CurrentPosition;
            _prey = null;
        }

        /// <summary>
        /// Main thread. Advances towards the target. Returning true means it reached the
        /// intercept point and should be resolved. connectedHit means it was going to hit and
        /// the target was still there, i.e. a kill, and point is where the interception
        /// happened.
        /// </summary>
        public bool Update(float deltaSeconds, out bool connectedHit, out Vector3 point)
        {
            _elapsed += deltaSeconds;

            Vector3 aim = _prey != null ? _prey.CurrentPosition : _lastPreyPos;
            _lastPreyPos = aim;

            Vector3 pos = _go != null ? _go.transform.position : aim;
            Vector3 delta = aim - pos;
            float dist = delta.magnitude;
            float step = _speed * deltaSeconds;

            bool reached = dist <= Mathf.Max(step, ModConfig.InterceptorCatchRadius);
            bool timedOut = _elapsed >= ModConfig.InterceptorMaxFlightSeconds;

            if (reached || timedOut)
            {
                point = reached ? aim : pos;
                connectedHit = reached && _isHit && _prey != null;
                return true;
            }

            if (_go != null)
            {
                Vector3 next = pos + delta / dist * step;
                _go.transform.position = next;
                AimAt(aim);
            }
            point = pos;
            connectedHit = false;
            return false;
        }

        /// <summary>Main thread. Destroys the interceptor's GameObject, detaching the exhaust so it lasts out its lifetime.</summary>
        public void Destroy()
        {
            if (_go == null) return;
            InterceptorTrail.DetachAndLinger(_go);
            Object.Destroy(_go);
        }

        private void AimAt(Vector3 aim)
        {
            if (_go == null) return;
            Vector3 dir = aim - _go.transform.position;
            if (dir.sqrMagnitude > 1e-6f) _go.transform.rotation = Quaternion.LookRotation(dir);
        }

        private static GameObject CreateVisual(InterceptorKind kind)
        {
            GameObject go = MissileModelProvider.CreateInstance(ModelFor(kind));
            if (go != null)
            {
                float s = ModConfig.InterceptorModelScale;
                go.transform.localScale = new Vector3(s, s, s);
                return go;
            }

            // Fallback: a small sphere, when the model could not be loaded.
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.localScale = new Vector3(4f, 4f, 4f);
            Collider col = sphere.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            return sphere;
        }

        private static string ModelFor(InterceptorKind kind)
        {
            switch (kind)
            {
                case InterceptorKind.Arrow: return ModConfig.InterceptorModelArrow;
                case InterceptorKind.Sam: return ModConfig.InterceptorModelThaad;
                default: return ModConfig.InterceptorModelPac;
            }
        }

        private static float SpeedFor(InterceptorKind kind)
        {
            switch (kind)
            {
                case InterceptorKind.Arrow: return ModConfig.InterceptorSpeedArrow;
                case InterceptorKind.Sam: return ModConfig.InterceptorSpeedThaad;
                default: return ModConfig.InterceptorSpeedPac;
            }
        }
    }
}
