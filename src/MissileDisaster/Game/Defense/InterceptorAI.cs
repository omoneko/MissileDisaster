using MissileDisaster.Core;

namespace MissileDisaster.Game.Defense
{
    /// <summary>
    /// 迎撃施設の建物 AI。存在・電力・維持は基底 PlayerBuildingAI に委譲する（S1）。
    /// 担当する迎撃層 Kind を保持し、後続 S2 で InterceptorRegistry の走査対象を識別するために使う。
    /// PlayerBuildingAI 派生: 車両スポーンやアニメーションを持たない「ただ在るだけ」の建物として扱う。
    /// </summary>
    public class InterceptorAI : PlayerBuildingAI
    {
        public InterceptorKind Kind = InterceptorKind.Pac;
    }
}
