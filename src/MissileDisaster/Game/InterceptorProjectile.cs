using MissileDisaster.Core;
using MissileDisaster.Game.Effects;
using MissileDisaster.Game.Models;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// 発射器から飛来ミサイルへ向かう可視の迎撃ミサイル1発。すべてメインスレッドで進行する
    /// （sim スレッドは触れない）。獲物(prey)の現在位置を毎フレーム追尾し、到達したら解決する。
    /// isHit=true の弾が生存中の獲物へ到達すれば撃墜成立、false や獲物消失時は空振り(不発)。
    /// モデル(Models/&lt;name&gt;.obj、+Z=機首)を進行方向へ向ける。噴煙は破棄後も少しの間残す。
    /// </summary>
    public class InterceptorProjectile
    {
        private readonly GameObject _go;
        private readonly float _speed;
        private readonly bool _isHit;
        private Missile _prey;              // 追尾対象。撃墜/着弾で消えたら null 化され最後の既知点へ向かう
        private Vector3 _lastPreyPos;
        private float _elapsed;

        /// <summary>この迎撃弾が狙っている飛来ミサイル（撃墜処理用）。空振り・消失後は null。</summary>
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

        /// <summary>獲物が撃墜/着弾で消えた時、追尾を解いて最後の既知点へ向かわせる（メインスレッド）。</summary>
        public void ClearPrey()
        {
            if (_prey != null) _lastPreyPos = _prey.CurrentPosition;
            _prey = null;
        }

        /// <summary>
        /// メインスレッド。獲物へ向けて前進する。戻り値 true = 迎撃点に到達（解決すべき）。
        /// connectedHit = 命中確定弾が生存中の獲物へ到達した（＝撃墜成立）。point = 迎撃点。
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

        /// <summary>メインスレッド。迎撃弾 GameObject を破棄する。噴煙は切り離して寿命まで残す。</summary>
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

            // フォールバック（モデル読込不可時の小さな球）。
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.localScale = new Vector3(8f, 8f, 8f);
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
