using ICities;

namespace MissileDisaster.Game.Loading
{
    /// <summary>
    /// Registers MissileTool with the ToolController on every level load.
    /// SetTool&lt;T&gt;() only works once the tool is registered - see UI.ToolRegistration - and
    /// without this the hotkey silently does nothing.
    /// </summary>
    public class MissileLoadingExtension : LoadingExtensionBase
    {
        public override void OnLevelLoaded(LoadMode mode)
        {
            base.OnLevelLoaded(mode);
            try
            {
                ModSettings.Ensure();
                MissileDisaster.Game.MissileManager.Reset();
                Defense.InterceptorRegistry.Reset();
                UI.ToolRegistration.Register<UI.MissileTool>();
                UI.MissilePanel.Create();
                UI.MissileDisasterButton.CreateButton(); // the launch button in the disasters tab
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("MissileLoadingExtension.OnLevelLoaded error: " + e);
            }
        }

        public override void OnLevelUnloading()
        {
            base.OnLevelUnloading();
            try
            {
                MissileDisaster.Game.MissileManager.Reset();
                Defense.InterceptorRegistry.Reset();
                Contamination.ContaminationManager.Reset(); // drop the in-memory ledger; OnLoadData repopulates it on load
                UI.MissileDisasterButton.DestroyButton();
                UI.MissilePanel.Destroy();
                UI.StrikeNotice.Reset(); // the next city gets its own warning
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("MissileLoadingExtension.OnLevelUnloading error: " + e);
            }
        }
    }
}
