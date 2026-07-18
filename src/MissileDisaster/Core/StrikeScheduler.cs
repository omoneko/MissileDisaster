namespace MissileDisaster.Core
{
    /// <summary>
    /// ランダム攻撃の発火スケジューラ（純粋・UnityEngine 非依存）。
    /// バニラ自然災害の頻度(probability)に比例した間隔でミサイル攻撃を発火し、
    /// 他の自然災害が発生(disasterCount の増加)するたびにカウントダウンをリセットする
    /// ＝ミサイルは災害の“合間”に発生する。ゲーム内時間(日)で駆動する。
    ///
    /// probability(=DisasterManager.m_randomDisastersProbability)の絶対スケールは非公開のため、
    /// 基準値 RefProbability で正規化し係数を [ProbFactorMin, ProbFactorMax] にクランプして用いる。
    /// これにより実機値が想定と多少ずれても間隔が暴れない。BaseIntervalDays が主要な調整ノブ。
    /// </summary>
    public sealed class StrikeScheduler
    {
        // 調整用定数（実機で校正。純粋ロジックのため数値変更はテスト対象外）。
        public const double BaseIntervalDays = 20.0;  // 既定の災害頻度・freq×1 でのミサイル基準間隔(ゲーム内日)
        public const double RefProbability = 0.05;    // 想定される標準的な m_randomDisastersProbability
        public const double ProbFactorMin = 0.25;     // probability 係数の下限（災害無効マップ等のフォールバック）
        public const double ProbFactorMax = 4.0;      // 上限
        public const double MinIntervalDays = 2.0;
        public const double MaxIntervalDays = 365.0;
        public const double Epsilon = 1e-6;

        private double _countdownDays;
        private int _lastDisasterCount;
        private bool _initialized;

        /// <summary>次の攻撃までの残りゲーム内日数（監視用）。</summary>
        public double CountdownDays => _countdownDays;

        /// <summary>状態を初期化前へ戻す（ランダム攻撃OFF／レベル遷移時に呼ぶ）。</summary>
        public void Reset()
        {
            _initialized = false;
            _countdownDays = 0.0;
            _lastDisasterCount = 0;
        }

        /// <summary>
        /// 1シミュレーションティックごとに呼ぶ。true=今tickで攻撃発火。
        /// gameDaysDelta: 前回からのゲーム内経過日数。disasterCount: 現在の m_disasterCount。
        /// probability: 現在の m_randomDisastersProbability。freqMultiplier: 設定(0.25..3.0)。rng: [0,1)。
        /// </summary>
        public bool Advance(double gameDaysDelta, int disasterCount, float probability, double freqMultiplier, double rng)
        {
            if (!_initialized)
            {
                _lastDisasterCount = disasterCount;
                _countdownDays = NextInterval(probability, freqMultiplier, rng);
                _initialized = true;
                return false;
            }

            if (disasterCount > _lastDisasterCount)
            {
                // 他の自然災害が発生 → カウントダウンをリセット（合間発生）。
                _lastDisasterCount = disasterCount;
                _countdownDays = NextInterval(probability, freqMultiplier, rng);
                return false;
            }
            if (disasterCount < _lastDisasterCount)
            {
                _lastDisasterCount = disasterCount; // 災害消滅：追従のみ、リセットしない
            }

            if (gameDaysDelta > 0.0)
            {
                _countdownDays -= gameDaysDelta;
            }
            if (_countdownDays <= 0.0)
            {
                _countdownDays = NextInterval(probability, freqMultiplier, rng);
                return true;
            }
            return false;
        }

        /// <summary>probability と頻度乗数から次の間隔(日)を算出。rng[0,1)で[0.5×,1.5×]にばらつかせクランプ。</summary>
        public static double NextInterval(float probability, double freqMultiplier, double rng)
        {
            double m = freqMultiplier > Epsilon ? freqMultiplier : 1.0;
            double pf = ProbabilityFactor(probability);
            double mean = BaseIntervalDays / (m * pf);
            double interval = mean * (0.5 + Clamp01(rng));
            if (interval < MinIntervalDays) return MinIntervalDays;
            if (interval > MaxIntervalDays) return MaxIntervalDays;
            return interval;
        }

        /// <summary>probability を RefProbability で正規化し [ProbFactorMin, ProbFactorMax] にクランプ。</summary>
        public static double ProbabilityFactor(float probability)
        {
            double p = probability > 0f ? probability : 0.0;
            double f = p / RefProbability;
            if (f < ProbFactorMin) return ProbFactorMin;
            if (f > ProbFactorMax) return ProbFactorMax;
            return f;
        }

        private static double Clamp01(double v)
        {
            if (v < 0.0) return 0.0;
            if (v > 1.0) return 1.0;
            return v;
        }
    }
}
