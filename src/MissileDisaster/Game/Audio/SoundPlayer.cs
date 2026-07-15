using System;
using UnityEngine;

namespace MissileDisaster.Game.Audio
{
    /// <summary>
    /// 読込済み AudioClip を指定ワールド座標で3D再生する（メインスレッド専用）。
    /// spatialBlend=1 の3D音源＋線形ロールオフで、リスナー(カメラ)からの距離で増幅/減衰する。
    /// 一時 GameObject を作って再生し、クリップ長経過後に破棄する。
    /// </summary>
    public static class SoundPlayer
    {
        public static void PlayAt(string clipName, Vector3 position, float volume, float minDistance, float maxDistance)
        {
            try
            {
                AudioClip clip = SoundLibrary.Get(clipName);
                if (clip == null) return; // 未読込/失敗時は無音（着弾等の処理は継続）

                var go = new GameObject("MissileDisasterSound_" + clipName);
                go.transform.position = position;
                var src = go.AddComponent<AudioSource>();
                src.clip = clip;
                src.volume = Mathf.Clamp01(volume);
                src.spatialBlend = 1f; // 完全3D
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = minDistance;
                src.maxDistance = maxDistance;
                src.dopplerLevel = 0f;
                src.playOnAwake = false;
                src.Play();

                UnityEngine.Object.Destroy(go, clip.length + 0.5f);
            }
            catch (Exception e)
            {
                ModConfig.LogError("SoundPlayer.PlayAt(" + clipName + ") error: " + e);
            }
        }
    }
}
