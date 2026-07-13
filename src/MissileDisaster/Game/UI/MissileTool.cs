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
        // Phase 1 は通常弾頭固定。後続 Phase で選択 UI から差し替える。
        public WarheadType SelectedWarhead = WarheadType.Conventional;

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
            if (m_toolController.IsInsideUI) return;
            if (e.type != EventType.MouseDown || e.button != 0 || !m_placementValid) return;
            try
            {
                MissileManager.Launch(m_cachedPosition, SelectedWarhead);
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
    }
}
