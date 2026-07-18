using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game
{
    /// <summary>
    /// ランダム攻撃モード：発火タイミングは StrikeScheduler（sim スレッド）が決め、本クラスは
    /// メインスレッドで実際の発射を行う。着弾パターン（Single/MIRV/Random）を設定で分岐する。
    /// MIRV は複数弾を同一フレームで発射する＝全弾の飛翔時間が一定のため着弾も同時になる。
    /// 目標は優先照準（StrikeTargeting：原発/迎撃施設＞交通拠点＞ランドマーク＞その他）で抽選し、
    /// MIRV は各弾が独立に抽選されるため複数の重要施設へ同時着弾しうる。すべてメインスレッド。
    /// </summary>
    public static class RandomStrike
    {
        private const int MirvMin = 3;          // MIRV 発数の下限
        private const int MirvMax = 6;          // MIRV 発数の上限（Random.Range 上端は排他なので +1 して使う）
        private const float MirvChance = 0.30f; // Random パターンで MIRV になる確率（残り70%はSingle）
        private const float FallbackRange = 4500f;

        /// <summary>設定の AttackPattern に従って攻撃を実行（メインスレッド専用）。</summary>
        public static void FireStrike()
        {
            try
            {
                StrikeTargeting targeting = new StrikeTargeting();
                targeting.Scan(); // 建物走査は1回だけ。MIRV でも使い回す。

                int count = ResolveWarheadCount();
                for (int i = 0; i < count; i++)
                {
                    FireOne(targeting);
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("RandomStrike.FireStrike error: " + e);
            }
        }

        /// <summary>着弾パターンから今回の発射数を決める。Single=1, MIRV=3〜6, Random=70%Single/30%MIRV。</summary>
        private static int ResolveWarheadCount()
        {
            int pattern = ModSettings.AttackPatternValue;
            bool mirv = pattern == 1 || (pattern == 2 && Random.value < MirvChance);
            return mirv ? Random.Range(MirvMin, MirvMax + 1) : 1;
        }

        /// <summary>1発を発射（優先照準で目標を抽選、無ければランダム座標）。メインスレッド専用。</summary>
        private static void FireOne(StrikeTargeting targeting)
        {
            Vector3 target;
            if (targeting == null || !targeting.TryPick(out target))
            {
                float x = Random.Range(-FallbackRange, FallbackRange);
                float z = Random.Range(-FallbackRange, FallbackRange);
                target = new Vector3(x, 0f, z);
                target.y = TerrainManager.instance.SampleRawHeightSmoothWithWater(target, false, 0f);
            }

            WarheadType type = PickWarhead();
            float yield = type == WarheadType.Nuclear
                ? NuclearYields.Multiplier(NuclearYields.StandardKilotons)
                : ConventionalYields.Multiplier(ConventionalYields.ReferenceKilograms);

            MissileManager.Launch(target, type, yield, BurstType.Groundburst);
        }

        private static WarheadType PickWarhead()
        {
            int w = ModSettings.RandomWarhead != null ? ModSettings.RandomWarhead.value : 0;
            if (w >= 1 && w <= 5) return (WarheadType)(w - 1); // 1..5 → Conventional..Nuclear
            return (WarheadType)Random.Range(0, 5);            // 0 = ランダム
        }
    }
}
