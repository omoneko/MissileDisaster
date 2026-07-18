using System;
using ColossalFramework;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Effects
{
    /// <summary>
    /// 着弾時にバニラの隕石(メテオ)着弾エフェクトを爆発規模に合わせて発火する。メインスレッド専用
    /// （MissileManager.UpdateVisual の着弾側から呼ぶ）。爆発が大きいほど多数の隕石エフェクトを
    /// 破壊半径内に散布して面で見せる。子弾散布弾は散布点ごとに発火する。
    /// メテオエフェクト(Natural Disasters DLC)が無い環境では簡易パーティクル火球にフォールバックする。
    /// NuclearMeltdown.Game.MeltdownEffect の EffectManager 発火パターンを踏襲。
    /// </summary>
    public static class ExplosionFx
    {
        private static EffectInfo _meteorEffect;
        private static bool _searched;

        /// <summary>着弾点で爆発エフェクトを発火する（メインスレッド）。失敗しても着弾処理は続行。</summary>
        public static void Play(Vector3 center, WarheadSpec spec)
        {
            try
            {
                EffectInfo effect = ResolveMeteorEffect();
                float radius = Mathf.Max(spec.DestructionRadius, spec.BurnRadius * 0.5f, 30f);

                if (spec.Type == WarheadType.Nuclear)
                {
                    if (effect != null)
                    {
                        // DLCあり: 隕石(メテオ)着弾エフェクト＝あの大きなキノコ状の雲を破壊半径連動でスケール表示。
                        float nukeScale = Mathf.Clamp(radius / 60f, 12f, ModConfig.NuclearExplosionScaleMax);
                        Dispatch(effect, center, nukeScale);
                    }
                    else
                    {
                        // DLCなし: 自作の白煙キノコ雲を規模連動で生成（滞留・成層圏での傘状展開を再現）。
                        NuclearMushroomFx.Play(center, radius);
                    }
                    return;
                }

                if (effect == null)
                {
                    ExplosionFallback.Play(center, radius);
                    return;
                }

                if (spec.SubmunitionCount > 1)
                {
                    // 子弾散布: 散布点ごとに小さめのエフェクト。
                    Offset2[] offs = SubmunitionScatter.Offsets(spec.SubmunitionCount, spec.SpreadRadius);
                    float s = Mathf.Clamp(spec.DestructionRadius / 24f, 0.75f, 2.5f);
                    for (int i = 0; i < offs.Length; i++)
                    {
                        Dispatch(effect, new Vector3(center.x + offs[i].X, center.y, center.z + offs[i].Z), s);
                    }
                    return;
                }

                // 単一着弾（通常弾・サーモバリック）: 着弾点に単一の爆発エフェクト（規模連動スケール）。
                float singleScale = Mathf.Clamp(radius / 50f, 2f, ModConfig.ExplosionBloomScaleMax);
                Dispatch(effect, center, singleScale);
            }
            catch (Exception e)
            {
                ModConfig.LogError("ExplosionFx.Play error: " + e);
            }
        }

        private static void Dispatch(EffectInfo effect, Vector3 pos, float scale)
        {
            var area = new EffectInfo.SpawnArea(pos, Vector3.up, 0f);
            Singleton<EffectManager>.instance.DispatchEffect(
                effect, default(InstanceID), area, Vector3.zero, 0f, scale,
                Singleton<VehicleManager>.instance.m_audioGroup);
        }

        /// <summary>
        /// 隕石(メテオ)着弾エフェクトを解決する。メテオの実体は VehicleInfo に載る MeteorAI で、
        /// その m_impactEffect が着弾エフェクト。初回のみ走査してキャッシュする（DLC 無しなら null）。
        /// </summary>
        private static EffectInfo ResolveMeteorEffect()
        {
            if (_searched) return _meteorEffect;
            _searched = true;
            try
            {
                int count = PrefabCollection<VehicleInfo>.LoadedCount();
                for (int i = 0; i < count; i++)
                {
                    VehicleInfo info = PrefabCollection<VehicleInfo>.GetLoaded((uint)i);
                    if (info == null) continue;
                    MeteorAI ai = info.m_vehicleAI as MeteorAI;
                    if (ai != null && ai.m_impactEffect != null)
                    {
                        _meteorEffect = ai.m_impactEffect;
                        ModConfig.Log("ExplosionFx: 隕石着弾エフェクトを取得しました");
                        return _meteorEffect;
                    }
                }
                ModConfig.Log("ExplosionFx: 隕石エフェクト無し(DLC非所持?) — 簡易火球にフォールバック");
            }
            catch (Exception e)
            {
                ModConfig.LogError("ExplosionFx.ResolveMeteorEffect error: " + e);
            }
            return _meteorEffect;
        }
    }
}
