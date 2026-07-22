using System.Collections.Generic;
using MissileDisaster.Core;
using MissileDisaster.Game.Audio;
using MissileDisaster.Game.Defense;
using MissileDisaster.Game.Effects;
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

        private static readonly List<Missile> _missiles = new List<Missile>();                    // メインスレッド専用
        private static readonly List<InterceptorProjectile> _interceptors = new List<InterceptorProjectile>(); // メインスレッド専用
        private static readonly List<ImpactJob> _impactQueue = new List<ImpactJob>();  // 受け渡し(ロック保護)
        private static readonly object _impactLock = new object();

        /// <summary>メインスレッドから読む。</summary>
        public static bool HasActive => _missiles.Count > 0;

        /// <summary>
        /// メインスレッド専用。出力係数(yieldMultiplier)で効果半径をスケールし、爆発高度(burst)を反映した spec で発射する。
        /// 係数は呼び出し側が弾頭に応じて算出（核=kt由来、非核=kg由来）。空中爆発はクレーター/汚染を無くし破壊・延焼を広げる。
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

            // 発射音（launcher2/launcher7 をランダム）。発射地点(apex)から距離で増減する3D音。
            string launcher = UnityEngine.Random.value < 0.5f ? SoundLibrary.Launcher2 : SoundLibrary.Launcher7;
            SoundPlayer.PlayAt(launcher, missile.LaunchPosition, ModConfig.SoundVolumeNormal,
                ModConfig.SoundLaunchMinDistance, ModConfig.SoundLaunchMaxDistance);

            ModConfig.Log("Missile launched at " + target + " (" + type + ", " + burst
                + ", x" + yieldMultiplier.ToString("0.00") + ")");
        }

        /// <summary>
        /// メインスレッド専用。迎撃施設の走査/クールダウン、迎撃ミサイルの飛翔と解決、飛来弾の飛翔と交戦を進める。
        /// 着弾（未撃墜）弾はダメージを enqueue して破棄。命中確定弾は着弾させずダメージも出さない。
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
                    // 命中確定(Doomed)弾は迎撃済み扱い＝ダメージ・爆発なし。未撃墜のみ着弾ダメージ＋爆発エフェクト。
                    if (!m.Doomed)
                    {
                        ExplosionFx.Play(m.Target, m.Spec); // 隕石着弾エフェクト（規模連動・メインスレッド）
                        PlayImpactSound(m.Target, m.Spec);  // 爆発音（核は atomic_bomb を2倍音量）
                        // 核着弾点を疎結合ビーコンへ公開（外部Mod連携の隠し要素: Alien のトライポッド直撃転倒）。
                        if (m.Spec.Type == WarheadType.Nuclear)
                        {
                            NuclearImpactBeacon.Publish(m.Target.x, m.Target.z);
                        }
                        lock (_impactLock)
                        {
                            _impactQueue.Add(new ImpactJob { Target = m.Target, Spec = m.Spec });
                        }
                    }
                    RemoveMissile(i, m);
                    continue;
                }

                if (m.Doomed) continue; // 迎撃弾が向かっている最中。再交戦しない。

                // 交戦: 圏内で待機中の発射器が1基だけ実弾を発射する。命中確定なら弾に印を付ける。
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

        /// <summary>メインスレッド専用。迎撃ミサイルを前進させ、迎撃点到達で解決（撃墜=閃光、空振り=不発煙）。</summary>
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
                    if (idx >= 0) RemoveMissile(idx, prey); // 撃墜: ダメージ無しで消滅＋他の追尾弾の参照解除
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

        /// <summary>着弾音（メインスレッド）。核は atomic_bomb を2倍音量＋広い可聴範囲、他は explosion1。</summary>
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

        /// <summary>メインスレッド専用。飛来弾を破棄・除去し、それを追尾中の迎撃弾の参照を解く。</summary>
        private static void RemoveMissile(int index, Missile m)
        {
            m.DestroyVisual();
            _missiles.RemoveAt(index);
            for (int j = 0; j < _interceptors.Count; j++)
            {
                if (_interceptors[j].Prey == m) _interceptors[j].ClearPrey();
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

        /// <summary>メインスレッド専用。全飛翔体（飛来弾・迎撃弾）を破棄し、キューも空にする。</summary>
        public static void Reset()
        {
            for (int i = 0; i < _missiles.Count; i++) _missiles[i].DestroyVisual();
            _missiles.Clear();
            for (int j = 0; j < _interceptors.Count; j++) _interceptors[j].Destroy();
            _interceptors.Clear();
            lock (_impactLock) { _impactQueue.Clear(); }
            NuclearImpactBeacon.Reset(); // 核着弾ビーコンも空にする（レベル切替で持ち越さない）
        }
    }
}
