namespace MissileDisaster.Game
{
    /// <summary>
    /// 全弾種の着弾を「外部Mod」がリフレクションで読めるように公開する疎結合ビーコン
    /// （NuclearImpactBeaconの汎用版。CS:WARFRONTが軍事ユニットへの被害判定に使う——
    /// ユーザーのWorkshopコメント対応「ミサイル災害でユニットが死なない」の修正）。
    /// 両Modは相互にDLL参照しないため、外部Modは型名 "MissileDisaster.Game.ImpactBeacon" を
    /// AppDomainから探し、下記2メンバをリフレクションで呼ぶ。片方のMODしか無くても安全に無視される。
    ///
    /// リフレクション契約（外部Modが依存する。シグネチャの互換を壊さないこと）:
    ///   public static long CurrentId();   // 直近に発行した着弾ID（0=まだ無し）。安価な「新着有無」判定用。
    ///   public static float[] Snapshot(); // 直近の着弾を新しい順に
    ///                                     // {id, x, z, destructionRadius, burnRadius, isNuclear(0/1)}
    ///                                     // の6つ組で返す（最大Capacity件）。
    /// IDはプロセス中単調増加（Resetでも巻き戻さない＝読み手の既読ID方式が破綻しない）。
    ///
    /// スレッド: Publishは着弾確定時（メインスレッド, MissileManager.UpdateVisual）から。
    /// 読み手（CS:WARFRONT）はsimスレッドから呼ぶため、全公開メンバをロックで保護する。
    /// </summary>
    public static class ImpactBeacon
    {
        private const int Capacity = 16;
        private const int Stride = 6;

        private struct Entry
        {
            public long Id;
            public float X, Z, DestructionRadius, BurnRadius;
            public bool IsNuclear;
        }

        private static readonly Entry[] _ring = new Entry[Capacity];
        private static int _count;
        private static int _head;
        private static long _lastId;
        private static readonly object _lock = new object();

        /// <summary>直近に発行した着弾ID（0=まだ無し）。</summary>
        public static long CurrentId()
        {
            lock (_lock) { return _lastId; }
        }

        /// <summary>着弾を1件公開する（メインスレッド、着弾確定時）。</summary>
        public static void Publish(float x, float z, float destructionRadius, float burnRadius, bool isNuclear)
        {
            lock (_lock)
            {
                _lastId++;
                _ring[_head] = new Entry
                {
                    Id = _lastId, X = x, Z = z,
                    DestructionRadius = destructionRadius, BurnRadius = burnRadius, IsNuclear = isNuclear
                };
                _head = (_head + 1) % Capacity;
                if (_count < Capacity) _count++;
            }
        }

        /// <summary>直近の着弾を新しい順に{id, x, z, destructionRadius, burnRadius, isNuclear}で返す。</summary>
        public static float[] Snapshot()
        {
            lock (_lock)
            {
                float[] result = new float[_count * Stride];
                int idx = _head - 1;
                for (int i = 0; i < _count; i++)
                {
                    if (idx < 0) idx += Capacity;
                    Entry e = _ring[idx];
                    int o = i * Stride;
                    result[o] = e.Id;
                    result[o + 1] = e.X;
                    result[o + 2] = e.Z;
                    result[o + 3] = e.DestructionRadius;
                    result[o + 4] = e.BurnRadius;
                    result[o + 5] = e.IsNuclear ? 1f : 0f;
                    idx--;
                }
                return result;
            }
        }

        /// <summary>レベル切替時のリセット。件数のみ消し、IDは巻き戻さない（読み手の既読ID方式のため）。</summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _count = 0;
                _head = 0;
            }
        }
    }
}
