using ColossalFramework;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.UI
{
    /// <summary>
    /// 着弾点をクリック指定するツール。バニラ災害と同じ「狙って左クリックで確定」。
    /// ToolBase のライフサイクルはメイン/レンダースレッドなので Launch を直接呼んでよい。
    /// </summary>
    public class MissileTool : ToolBase
    {
        // 選択中の弾頭種別。正式な選択 UI は後続フェーズ。現状はツール使用中に数字キー1-5で切替できる暫定手段
        // （種別ごとの着弾差を実機確認するため）。static なのでツール再起動を跨いで選択を保持する。
        public static WarheadType CurrentWarhead = WarheadType.Conventional;

        private Vector3 m_cachedPosition;
        private bool m_placementValid;
        private Ray m_mouseRay;
        private float m_mouseRayLength;
        private bool m_mouseRayValid;

        protected override void OnToolLateUpdate()
        {
            m_mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            m_mouseRayLength = Camera.main.farClipPlane;
            m_mouseRayValid = !m_toolController.IsInsideUI && Cursor.visible;
        }

        public override void SimulationStep()
        {
            if (m_mouseRayValid)
            {
                RaycastInput input = new RaycastInput(m_mouseRay, m_mouseRayLength);
                RaycastOutput output;
                if (RayCast(input, out output))
                {
                    output.m_hitPos.y = Singleton<TerrainManager>.instance.SampleRawHeightSmoothWithWater(output.m_hitPos, false, 0f);
                    m_cachedPosition = output.m_hitPos;
                    m_placementValid = true;
                    return;
                }
            }
            m_placementValid = false;
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            if (!m_placementValid) return;
            Color color = new Color(1f, 0.4f, 0.1f, 0.6f);
            Singleton<RenderManager>.instance.OverlayEffect.DrawCircle(
                cameraInfo, color, m_cachedPosition, 100f,
                m_cachedPosition.y - 100f, m_cachedPosition.y + 100f, false, true);
        }

        protected override void OnToolGUI(Event e)
        {
            // 弾頭種別の暫定選択（数字キー1-5）。正式UIは後続フェーズ。
            if (e.type == EventType.KeyDown && TrySelectWarhead(e.keyCode))
            {
                ModConfig.Log("Selected warhead: " + CurrentWarhead);
                return;
            }

            // 選択中の弾頭を画面左上に表示（実機確認用の簡易ラベル）。
            if (e.type == EventType.Repaint)
            {
                GUI.Label(new Rect(12f, 12f, 480f, 24f),
                    "[MissileDisaster] 弾頭[1-5]: " + CurrentWarhead + "  (F9で照準→クリックで発射)");
            }

            if (m_toolController.IsInsideUI) return;
            if (e.type != EventType.MouseDown || e.button != 0 || !m_placementValid) return;
            try
            {
                MissileManager.Launch(m_cachedPosition, CurrentWarhead);
            }
            catch (System.Exception ex)
            {
                ModConfig.LogError("MissileTool.OnToolGUI error: " + ex);
            }
            finally
            {
                ToolsModifierControl.SetTool<DefaultTool>();
            }
        }

        /// <summary>数字キー1-5を弾頭種別に対応付ける。該当キーなら true。</summary>
        private static bool TrySelectWarhead(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Alpha1: case KeyCode.Keypad1: CurrentWarhead = WarheadType.Conventional; return true;
                case KeyCode.Alpha2: case KeyCode.Keypad2: CurrentWarhead = WarheadType.Cluster; return true;
                case KeyCode.Alpha3: case KeyCode.Keypad3: CurrentWarhead = WarheadType.WhitePhosphorus; return true;
                case KeyCode.Alpha4: case KeyCode.Keypad4: CurrentWarhead = WarheadType.Thermobaric; return true;
                case KeyCode.Alpha5: case KeyCode.Keypad5: CurrentWarhead = WarheadType.Nuclear; return true;
                default: return false;
            }
        }
    }
}
