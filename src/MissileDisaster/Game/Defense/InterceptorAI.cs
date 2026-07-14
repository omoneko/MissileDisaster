using MissileDisaster.Core;

namespace MissileDisaster.Game.Defense
{
    /// <summary>
    /// 迎撃施設・支援施設の建物 AI。存在・電力・維持・コストは基底 PlayerBuildingAI に委譲する。
    /// - 迎撃施設: 担当する迎撃層 Kind を持つ（後続 S2 で InterceptorRegistry が走査して迎撃判定に使う）。
    /// - 支援施設(レーダーサイト): IsRadar=true。稼働中は迎撃確率に SupportMultiplier を掛ける（S2 で反映）。
    /// PlayerBuildingAI 派生: 車両スポーンやアニメーションを持たない「ただ在るだけ」の建物として扱う。
    /// </summary>
    public class InterceptorAI : PlayerBuildingAI
    {
        public InterceptorKind Kind = InterceptorKind.Pac;

        /// <summary>支援(レーダー)施設なら true。true のとき迎撃層は持たず、確率補正のみ提供する。</summary>
        public bool IsRadar;

        /// <summary>稼働中に迎撃確率へ掛ける倍率（レーダー用。既定 1=無効）。</summary>
        public float SupportMultiplier = 1f;
    }
}
