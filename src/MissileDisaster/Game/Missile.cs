using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// 飛翔中の 1 発。位置補間はすべてメインスレッドで行う（sim スレッドはこのオブジェクトに触れない）。
    /// 可視表現は Phase 1 では簡易プリミティブ（球）。後続 Phase でトレイル/モデルに差し替え。
    /// </summary>
    public class Missile
    {
        private readonly Vector3 _start;
        private readonly Vector3 _target;
        private readonly float _groundDistance;
        private readonly WarheadSpec _spec;
        private readonly GameObject _go;
        private float _t;

        public Vector3 Target => _target;
        public WarheadSpec Spec => _spec;

        public Missile(Vector3 target, WarheadType type)
        {
            _target = target;
            _spec = WarheadSpec.For(type);
            // 発射点はターゲットから水平にオフセットした高所にする。
            // 真上（オフセット0）だと地表投影距離が0になり、AdvanceT のゼロ距離ガードで
            // t が初フレームに即1へ跳ね、ミサイルが飛ばず即着弾してしまう。
            // 水平オフセットを与えることで斜めに飛来する放物線の弧になり、迎撃(後続Phase)の
            // 飛行フェーズも成立する。方向はメインスレッドセーフな UnityEngine.Random で毎回ランダム。
            float ang = Random.Range(0f, 2f * Mathf.PI);
            float ox = Mathf.Cos(ang) * ModConfig.MissileLaunchOffset;
            float oz = Mathf.Sin(ang) * ModConfig.MissileLaunchOffset;
            _start = new Vector3(target.x + ox, target.y + ModConfig.MissileStartAltitude, target.z + oz);
            float dx = target.x - _start.x;
            float dz = target.z - _start.z;
            _groundDistance = Mathf.Sqrt(dx * dx + dz * dz); // = MissileLaunchOffset (>0)
            _t = 0f;

            _go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _go.transform.localScale = new Vector3(12f, 12f, 12f);
            var col = _go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            _go.transform.position = _start;
        }

        /// <summary>
        /// メインスレッド。位置を進める。戻り値 true = このフレームで着弾（t&gt;=1 到達）。
        /// 着弾後の処理（ダメージの enqueue と GameObject 破棄）は MissileManager 側が行う。
        /// </summary>
        public bool UpdateVisual(float simTimeDelta)
        {
            _t = BallisticMath.AdvanceT(_t, _groundDistance, ModConfig.MissileSpeed, simTimeDelta);
            float x = BallisticMath.Lerp(_start.x, _target.x, _t);
            float z = BallisticMath.Lerp(_start.z, _target.z, _t);
            float y = BallisticMath.Lerp(_start.y, _target.y, _t) + BallisticMath.ArcHeightAt(_t, ModConfig.MissileArcHeight);
            if (_go != null) _go.transform.position = new Vector3(x, y, z);
            return _t >= 1f;
        }

        /// <summary>メインスレッド。飛翔体 GameObject を破棄する。</summary>
        public void DestroyVisual()
        {
            if (_go != null) Object.Destroy(_go);
        }
    }
}
