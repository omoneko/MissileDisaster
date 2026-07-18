using ICities;

namespace MissileDisaster.Game.Loading
{
    /// <summary>
    /// レベルロード毎に MissileTool を ToolController へ登録する。
    /// SetTool&lt;T&gt;() が機能するには事前登録が必須（UI.ToolRegistration 参照）。
    /// これを行わないと F9 が空振りする。
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
                Contamination.ContaminationManager.Reset(); // メモリ台帳を破棄（ロード時は OnLoadData が再投入）
                UI.MissilePanel.Destroy();
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("MissileLoadingExtension.OnLevelUnloading error: " + e);
            }
        }
    }
}
