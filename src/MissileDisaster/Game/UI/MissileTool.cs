using ColossalFramework;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.UI
{
    /// <summary>
    /// The tool for clicking where the missile should land, with the same feel as the vanilla
    /// disasters: aim, then left click to confirm.
    /// A ToolBase lifecycle runs on the main/render thread, so calling Launch directly from here
    /// is safe.
    /// </summary>
    public class MissileTool : ToolBase
    {
        // The selected warhead, nuclear yield in kilotons and burst height, all set from
        // MissilePanel. They are static, so they survive the tool being reopened.
        public static WarheadType CurrentWarhead = WarheadType.Conventional;
        public static int CurrentYieldKilotons = NuclearYields.StandardKilotons;
        public static int CurrentConventionalKilograms = ConventionalYields.ReferenceKilograms;
        public static BurstType CurrentBurst = BurstType.Groundburst;

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
                // The yield factor comes from kilotons for a nuclear warhead and from kilograms
                // of TNT otherwise; the blast radius goes as its cube root.
                float yield = CurrentWarhead == WarheadType.Nuclear
                    ? NuclearYields.Multiplier(CurrentYieldKilotons)
                    : ConventionalYields.Multiplier(CurrentConventionalKilograms);
                MissileManager.Launch(m_cachedPosition, CurrentWarhead, yield, CurrentBurst);
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
