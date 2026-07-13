using System.Collections.Generic;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// 発射・追跡の静的コーディネータ。
    /// スレッド境界（重要）:
    ///  - _missiles（飛翔中リスト）はメインスレッドのみが触る（Launch/UpdateVisual/Reset）。
    ///    sim スレッドはこのリストに一切アクセスしない。
    ///  - 着弾ダメージは DisasterHelpers を使うため sim スレッドで実行が必要。そこでメインスレッドは
    ///    着弾時に ImpactJob（座標＋弾頭スペックの値）を _impactQueue に積み、sim スレッド
    ///    （UpdateSimulation）はロック下でキューを排出して解決する。
    ///  これにより List&lt;Missile&gt; をスレッド跨ぎで共有せず、境界はロック保護した小さな値キューのみになる。
    /// </summary>
    public static class MissileManager
    {
        private struct ImpactJob
        {
            public Vector3 Target;
            public WarheadSpec Spec;
        }

        private static readonly List<Missile> _missiles = new List<Missile>();        // メインスレッド専用
        private static readonly List<ImpactJob> _impactQueue = new List<ImpactJob>();  // 受け渡し(ロック保護)
        private static readonly object _impactLock = new object();

        /// <summary>メインスレッドから読む。</summary>
        public static bool HasActive => _missiles.Count > 0;

        /// <summary>メインスレッド専用。</summary>
        public static void Launch(Vector3 target, WarheadType type)
        {
            _missiles.Add(new Missile(target, type));
            ModConfig.Log("Missile launched at " + target + " (" + type + ")");
        }

        /// <summary>メインスレッド専用。飛翔を進め、着弾したものはダメージを enqueue して破棄・除去。</summary>
        public static void UpdateVisual(float simTimeDelta)
        {
            for (int i = _missiles.Count - 1; i >= 0; i--)
            {
                Missile m = _missiles[i];
                bool impacted = m.UpdateVisual(simTimeDelta);
                if (impacted)
                {
                    lock (_impactLock)
                    {
                        _impactQueue.Add(new ImpactJob { Target = m.Target, Spec = m.Spec });
                    }
                    m.DestroyVisual();
                    _missiles.RemoveAt(i);
                }
            }
        }

        /// <summary>シミュレーションスレッド専用。着弾キューを排出し DisasterHelpers で解決する。</summary>
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

        /// <summary>メインスレッド専用。全飛翔体を破棄し、キューも空にする。</summary>
        public static void Reset()
        {
            for (int i = 0; i < _missiles.Count; i++) _missiles[i].DestroyVisual();
            _missiles.Clear();
            lock (_impactLock) { _impactQueue.Clear(); }
        }
    }
}
