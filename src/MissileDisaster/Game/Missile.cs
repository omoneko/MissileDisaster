using MissileDisaster.Core;
using MissileDisaster.Game.Effects;
using MissileDisaster.Game.Models;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// 飛翔中の 1 発。固定方位・高高度の apex(頂点)から着弾までの「降下枝のみ」を、
    /// すべてメインスレッドで直線補間する（sim スレッドはこのオブジェクトに触れない）。
    /// 可視表現は弾頭モデル（Models/IncomingWarhead.obj）。読込不可時は球へフォールバックする。
    /// 機首（モデルの +Z）は進行方向へ向ける。
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

        /// <summary>メインスレッド。飛翔体の現在ワールド座標（迎撃判定用）。GameObject 破棄後は着弾点を返す。</summary>
        public Vector3 CurrentPosition => _go != null ? _go.transform.position : _target;

        /// <summary>発射地点（高高度の apex）。発射音の3D音源位置に使う。</summary>
        public Vector3 LaunchPosition => _apex;

        /// <summary>迎撃(命中確定)済みか。true の弾は再交戦せず、着弾してもダメージを発生させない。</summary>
        public bool Doomed => _doomed;

        /// <summary>迎撃ミサイルの命中が確定した弾に印を付ける（メインスレッド）。</summary>
        public void MarkDoomed() { _doomed = true; }

        public Missile(Vector3 target, WarheadSpec spec)
        {
            _target = target;
            _spec = spec;

            // 固定方位・高高度の apex から降下する。上昇枝は存在しない(= 終端のみ描画)。
            Offset2 off = LaunchGeometry.BearingOffset(ModConfig.IncomingBearingDegrees, ModConfig.ApexHorizontalOffset);
            _apex = new Vector3(target.x + off.X, target.y + ModConfig.ApexAltitude, target.z + off.Z);
            float dx = target.x - _apex.x;
            float dz = target.z - _apex.z;
            _groundDistance = Mathf.Sqrt(dx * dx + dz * dz); // = ApexHorizontalOffset (>0)
            _t = 0f;

            _go = CreateVisual();
            _go.transform.position = _apex;
            // 直線降下なので進行方向は一定。機首(+Z)を進行方向へ一度だけ向ける。
            Vector3 velocity = _target - _apex;
            if (velocity.sqrMagnitude > 1e-6f)
            {
                _go.transform.rotation = Quaternion.LookRotation(velocity);
            }
            // 隕石風の燃焼トレイル（火の粉＋煙）を付与。ワールド空間なので後方に航跡を残す。
            MissileTrail.Attach(_go);
        }

        /// <summary>弾頭モデルを生成。読込不可なら球へフォールバック。Collider は不要なので破棄。</summary>
        private static GameObject CreateVisual()
        {
            GameObject go = MissileModelProvider.CreateInstance(ModConfig.IncomingMissileModelName);
            if (go != null)
            {
                go.transform.localScale = new Vector3(
                    ModConfig.IncomingMissileScale, ModConfig.IncomingMissileScale, ModConfig.IncomingMissileScale);
                return go;
            }

            // フォールバック（Phase 1 と同じ球）。
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.localScale = new Vector3(12f, 12f, 12f);
            Collider col = sphere.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            return sphere;
        }

        /// <summary>
        /// メインスレッド。apex→着弾を直線降下で補間する。戻り値 true = このフレームで着弾(t&gt;=1)。
        /// 着弾後の処理(ダメージ enqueue と破棄)は MissileManager 側が行う。
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

        /// <summary>メインスレッド。飛翔体 GameObject を破棄する。トレイルは切り離して残り寿命まで燃やし切る。</summary>
        public void DestroyVisual()
        {
            if (_go == null) return;
            DetachAndFadeTrail(_go);
            Object.Destroy(_go);
        }

        /// <summary>
        /// 着弾時、トレイルの ParticleSystem を弾体から切り離し（ワールド位置維持）、新規放出だけ止めて
        /// 既存の火の粉/煙は残り寿命まで漂わせてから破棄する。弾体ごと即破棄すると航跡が一瞬で消えるため。
        /// </summary>
        private static void DetachAndFadeTrail(GameObject missile)
        {
            ParticleSystem[] systems = missile.GetComponentsInChildren<ParticleSystem>();
            float life = Mathf.Max(ModConfig.TrailFireLifetime, ModConfig.TrailSmokeLifetime);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null) continue;
                ps.transform.SetParent(null, true); // 親破棄に巻き込まれないよう独立させる(ワールド位置維持)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); // 放出停止・既存粒子は継続シミュレート
                Object.Destroy(ps.gameObject, life + 0.1f);
            }
        }
    }
}
