namespace MissileDisaster.Game
{
    /// <summary>
    /// 核弾頭の着弾点を「外部Mod」がリフレクションで読めるように公開する疎結合ビーコン。
    /// （例: Alien Invasion Mod が本ビーコンを読み、核直撃を受けたトライポッドを転倒・消滅させる隠し要素。）
    /// 両Modは相互にDLL参照しないため、外部Modは型名 "MissileDisaster.Game.NuclearImpactBeacon" を
    /// AppDomain から探し、下記2メンバをリフレクションで呼ぶ。片方のMODしか無くても安全に無視される。
    ///
    /// リフレクション契約（外部Modが依存する。シグネチャの互換を壊さないこと）:
    ///   public static long CurrentId { get; }   // 直近に発行した着弾ID（0=まだ無し）。安価な「新着有無」判定用。
    ///   public static float[] Snapshot();        // 直近の核着弾を新しい順に {id, x, z} の三つ組で返す（最大 Capacity 件）。
    ///
    /// 直近 Capacity 件をリングバッファで保持し、各件に単調増加ID（1始まり）を付ける。読み手は
    /// 「前回見たIDより大きい件だけ」を新規着弾として処理する。座標は着弾点の水平位置(x,z)のみ
    /// （半径判定は読み手側が自分の設定で行う）。
    ///
    /// スレッド: Publish は着弾確定時（メインスレッド, MissileManager.UpdateVisual）から呼ぶ。
    /// 読み手も現状メインスレッドだが、将来の誤用に備えロックで保護する。
    /// </summary>
    public static class NuclearImpactBeacon
    {
        private const int Capacity = 16;

        private struct Entry { public long Id; public float X; public float Z; }

        private static readonly Entry[] _ring = new Entry[Capacity];
        private static int _count;   // 保持件数(<=Capacity)
        private static int _head;    // 次に書き込むリング位置
        private static long _lastId; // 直近に発行したID
        private static readonly object _lock = new object();

        /// <summary>直近に発行した着弾ID（0=まだ無し）。読み手が Snapshot を呼ぶ前の安価な新着判定に使う。</summary>
        public static long CurrentId
        {
            get { lock (_lock) { return _lastId; } }
        }

        /// <summary>核着弾点を1件公開する。メインスレッド（着弾確定時）から呼ぶ。</summary>
        public static void Publish(float x, float z)
        {
            lock (_lock)
            {
                _lastId++;
                _ring[_head] = new Entry { Id = _lastId, X = x, Z = z };
                _head = (_head + 1) % Capacity;
                if (_count < Capacity) _count++;
            }
        }

        /// <summary>直近の核着弾を新しい順に {id, x, z} の三つ組で返す（最大 Capacity 件）。無ければ空配列。</summary>
        public static float[] Snapshot()
        {
            lock (_lock)
            {
                float[] result = new float[_count * 3];
                int idx = _head - 1; // _head の1つ手前が最新
                for (int i = 0; i < _count; i++)
                {
                    if (idx < 0) idx += Capacity;
                    Entry e = _ring[idx];
                    result[i * 3] = e.Id;
                    result[i * 3 + 1] = e.X;
                    result[i * 3 + 2] = e.Z;
                    idx--;
                }
                return result;
            }
        }

        /// <summary>レベル切替時のリセット。古い着弾を新レベルへ持ち越さない。メインスレッド。</summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _count = 0;
                _head = 0;
                _lastId = 0;
            }
        }
    }
}
