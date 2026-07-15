using System.Collections.Generic;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.Defense
{
    /// <summary>
    /// 設置済みの迎撃施設（PAC3/THAAD/Aegis）と支援施設（レーダー）を名前で検出し、飛来ミサイルの
    /// 迎撃を判定する。<b>すべてメインスレッド専用</b>（MissileManager.UpdateVisual と同じ側）。
    ///
    /// - 建物走査は毎フレームではなく InterceptorScanIntervalFrames 間隔で間引く（BuildingManager 全走査は重い）。
    /// - クールダウンは毎 Tick 減算。再走査を跨いでも建物 ID で引き継ぐ。
    /// - 迎撃可否は既存 Core（InterceptDecision / InterceptorTiers）で判定。レーダー稼働中は確率×倍率。
    /// - コスト/電力/水はアセット側の設定に委ねる。ここでは一切上書きしない。
    /// </summary>
    public static class InterceptorRegistry
    {
        private struct Interceptor
        {
            public ushort Id;
            public Vector3 Position;
            public InterceptorTier Tier;
            public float Cooldown; // 秒。>0 の間は交戦不可
        }

        private static readonly List<Interceptor> _interceptors = new List<Interceptor>();
        private static bool _radarActive;
        private static int _framesSinceScan = int.MaxValue; // 初回 Tick で即走査
        private static int _lastLoggedActive = -1;
        private static int _lastLoggedInactive = -1;
        private static bool _lastLoggedRadar;

        /// <summary>メインスレッド専用。クールダウン減算と、間引かれた建物再走査を行う。</summary>
        public static void Tick(float deltaSeconds)
        {
            for (int i = 0; i < _interceptors.Count; i++)
            {
                Interceptor it = _interceptors[i];
                if (it.Cooldown > 0f)
                {
                    it.Cooldown = Mathf.Max(0f, it.Cooldown - deltaSeconds);
                    _interceptors[i] = it;
                }
            }

            if (_framesSinceScan >= ModConfig.InterceptorScanIntervalFrames)
            {
                _framesSinceScan = 0;
                Scan();
            }
            else
            {
                _framesSinceScan++;
            }
        }

        /// <summary>
        /// メインスレッド専用。飛来ミサイル 1 発に対し、交戦圏内で待機中の発射器を「高い層から1基だけ」発射させる。
        /// 発射は必ずクールダウンを消費する（＝1交戦につき1発）。命中可否(isHit)は single-shot Pk の 1 回抽選で決まる。
        /// 発射したら true（launcherPosition=発射位置, kind=層, isHit=命中確定か）。撃てる発射器が無ければ false。
        /// altitude は missilePos.y - targetGround.y（急降下なのでミサイル直下の地面高≒着弾地点の高さ）。
        /// </summary>
        public static bool TryEngage(Vector3 missilePos, Vector3 targetGround,
            out Vector3 launcherPosition, out InterceptorKind kind, out bool isHit)
        {
            launcherPosition = missilePos;
            kind = InterceptorKind.Pac;
            isHit = false;
            if (_interceptors.Count == 0) return false;

            float altitude = missilePos.y - targetGround.y;
            if (altitude < 0f) altitude = 0f;
            float multiplier = _radarActive ? ModConfig.RadarSupportMultiplier : 1f;

            InterceptorTier[] ordered = InterceptorTiers.Ordered; // Arrow→Sam→Pac
            for (int t = 0; t < ordered.Length; t++)
            {
                InterceptorKind tierKind = ordered[t].Kind;
                for (int i = 0; i < _interceptors.Count; i++)
                {
                    Interceptor it = _interceptors[i];
                    if (it.Tier.Kind != tierKind || it.Cooldown > 0f) continue;

                    float dx = it.Position.x - missilePos.x;
                    float dz = it.Position.z - missilePos.z;
                    float horizontalDistance = Mathf.Sqrt(dx * dx + dz * dz);
                    if (!InterceptDecision.InEngagementZone(altitude, horizontalDistance, it.Tier)) continue;

                    // 発射: 1基が1発だけ撃つ（クールダウン消費）。命中は single-shot Pk で1回だけ抽選。
                    it.Cooldown = it.Tier.CooldownSeconds;
                    _interceptors[i] = it;
                    float chance = Mathf.Clamp01(it.Tier.InterceptChance * multiplier);
                    isHit = Random.value < chance;
                    launcherPosition = it.Position;
                    kind = tierKind;
                    ModConfig.Log("Interceptor fired: " + tierKind + " " + (isHit ? "HIT" : "MISS")
                        + " (alt=" + Mathf.RoundToInt(altitude) + "m, dist=" + Mathf.RoundToInt(horizontalDistance)
                        + "m, Pk=" + chance.ToString("0.00") + ", radar=" + _radarActive + ")");
                    return true;
                }
            }
            return false;
        }

        /// <summary>メインスレッド専用。追跡状態を破棄する（レベル切替時）。</summary>
        public static void Reset()
        {
            _interceptors.Clear();
            _radarActive = false;
            _framesSinceScan = int.MaxValue;
            _lastLoggedActive = -1;
            _lastLoggedInactive = -1;
            _lastLoggedRadar = false;
        }

        /// <summary>BuildingManager を走査し、名前一致した稼働中の迎撃施設/レーダーを取り込む。</summary>
        private static void Scan()
        {
            BuildingManager bm = BuildingManager.instance;
            if (bm == null) return;
            Building[] buffer = bm.m_buildings.m_buffer;
            if (buffer == null) return;

            // 再走査を跨いでクールダウンを引き継ぐため、旧状態を ID で退避。
            Dictionary<ushort, float> priorCooldowns = null;
            if (_interceptors.Count > 0)
            {
                priorCooldowns = new Dictionary<ushort, float>(_interceptors.Count);
                for (int i = 0; i < _interceptors.Count; i++)
                {
                    priorCooldowns[_interceptors[i].Id] = _interceptors[i].Cooldown;
                }
            }

            _interceptors.Clear();
            bool radar = false;
            int inactiveMatches = 0; // 名前一致したが未完成/破壊済み（診断用）
            Building.Flags firstInactiveFlags = 0; // 最初の非稼働建物のフラグ（原因診断用）
            string firstInactiveName = null;

            for (int i = 1; i < buffer.Length; i++)
            {
                Building b = buffer[i];
                Building.Flags flags = b.m_flags;
                if ((flags & Building.Flags.Created) == 0) continue;

                BuildingInfo info = b.Info;
                string name = info != null ? info.name : null;
                if (string.IsNullOrEmpty(name)) continue;

                bool isRadar = InterceptorNameMatcher.IsRadar(name);
                InterceptorKind kind;
                bool isInterceptor = InterceptorNameMatcher.TryMatchTier(name, out kind);
                if (!isRadar && !isInterceptor) continue;

                if (!IsOperational(flags))
                {
                    if (inactiveMatches == 0) { firstInactiveFlags = flags; firstInactiveName = name; }
                    inactiveMatches++;
                    continue;
                }

                if (isRadar)
                {
                    radar = true;
                    continue;
                }

                float cooldown = 0f;
                if (priorCooldowns != null) priorCooldowns.TryGetValue((ushort)i, out cooldown);

                _interceptors.Add(new Interceptor
                {
                    Id = (ushort)i,
                    Position = b.m_position,
                    Tier = TierFor(kind),
                    Cooldown = cooldown,
                });
            }

            _radarActive = radar;
            LogChangesIfAny(_interceptors.Count, inactiveMatches, radar, firstInactiveFlags, firstInactiveName);
        }

        /// <summary>検出状況が前回から変わった時だけログを出す（実機での検出確認・毎走査のスパム防止）。</summary>
        private static void LogChangesIfAny(int active, int inactive, bool radar,
            Building.Flags firstInactiveFlags, string firstInactiveName)
        {
            if (active == _lastLoggedActive && inactive == _lastLoggedInactive && radar == _lastLoggedRadar) return;
            _lastLoggedActive = active;
            _lastLoggedInactive = inactive;
            _lastLoggedRadar = radar;
            string msg = "Interceptors detected: active=" + active + ", radar=" + radar;
            if (inactive > 0)
            {
                // 非稼働の原因を特定するため、最初の該当建物のフラグを出力する。
                msg += ", 名前一致だが非稼働=" + inactive
                    + " [例 '" + firstInactiveName + "' flags=" + firstInactiveFlags + "]";
            }
            ModConfig.Log(msg);
        }

        /// <summary>
        /// 建物が「稼働中」か（生成済み・完成・破壊されていない）。
        /// 注: 一部のカスタムアセットは有電力でも Building.Flags.Active が立たない。Active 必須にすると
        /// 迎撃が一切発動しないため要件から外し、Completed（完成済み）＋非破壊のみを条件とする。
        /// </summary>
        private static bool IsOperational(Building.Flags flags)
        {
            if ((flags & Building.Flags.Created) == 0) return false;
            if ((flags & Building.Flags.Completed) == 0) return false;
            const Building.Flags dead = Building.Flags.Abandoned | Building.Flags.BurnedDown
                | Building.Flags.Collapsed | Building.Flags.Deleted;
            return (flags & dead) == 0;
        }

        private static InterceptorTier TierFor(InterceptorKind kind)
        {
            switch (kind)
            {
                case InterceptorKind.Arrow: return InterceptorTiers.Arrow;
                case InterceptorKind.Sam: return InterceptorTiers.Sam;
                default: return InterceptorTiers.Pac;
            }
        }
    }
}
