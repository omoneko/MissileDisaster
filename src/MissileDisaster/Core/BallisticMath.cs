namespace MissileDisaster.Core
{
    /// <summary>
    /// ミサイル飛翔（放物線）の純粋数学。UnityEngine 非依存（float のみ）で単体テスト可能。
    /// ゲーム側は x/z を Lerp、y を Lerp + ArcHeightAt で合成して Vector3 を作る。
    /// </summary>
    public static class BallisticMath
    {
        public static float Clamp01(float t)
        {
            if (t < 0f) return 0f;
            if (t > 1f) return 1f;
            return t;
        }

        public static float Lerp(float a, float b, float t)
        {
            t = Clamp01(t);
            return a + (b - a) * t;
        }

        /// <summary>放物線の高さ成分。t=0,1 で 0、t=0.5 で arcHeight。</summary>
        public static float ArcHeightAt(float t, float arcHeight)
        {
            t = Clamp01(t);
            return arcHeight * 4f * t * (1f - t);
        }

        /// <summary>進行度 t を「地表投影距離 groundDistance を speed で進む」ぶんだけ加算。</summary>
        public static float AdvanceT(float t, float groundDistance, float speed, float dt)
        {
            if (groundDistance <= 0.0001f) return 1f; // 距離0は即着弾扱い
            return t + (speed * dt) / groundDistance;
        }
    }
}
