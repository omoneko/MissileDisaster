using System;
using System.Reflection;
using UnityEngine;

namespace MissileDisaster.Game.UI
{
    /// <summary>
    /// Registers the launch button with Unified UI, when the player has it. Main thread only.
    ///
    /// <para>
    /// Bound by reflection rather than by referencing UnifiedUILib, so the mod has no dependency
    /// on it: without UUI installed nothing here resolves, <see cref="IsAvailable"/> returns
    /// false and the button goes into the vanilla disasters row as before. The same pattern the
    /// mod already uses to talk to Alien Invasion without either mod requiring the other.
    /// </para>
    ///
    /// The API was read out of UnifiedUILib's IL, not guessed:
    /// <code>
    /// UUICustomButton UUIHelpers.RegisterCustomButton(
    ///     string name, string groupName, string tooltip, Texture2D icon,
    ///     Action&lt;bool&gt; onToggle, Action&lt;ToolBase&gt; onToolChanged = null,
    ///     UUIHotKeys hotkeys = null)
    /// </code>
    /// and the returned button carries <c>IsPressed</c> and <c>Release()</c>.
    /// </summary>
    public static class UnifiedUiButton
    {
        private const string HelpersTypeName = "UnifiedUI.Helpers.UUIHelpers";
        private const string HotKeysTypeName = "UnifiedUI.Helpers.UUIHotKeys";

        /// <summary>The group Unified UI files the button under. "Tools" is where the launchers and painters live.</summary>
        private const string GroupName = "Tools";

        private static object _button;          // UUICustomButton
        private static PropertyInfo _isPressed;
        private static MethodInfo _release;
        private static bool _looked;
        private static Type _helpers;

        /// <summary>Whether Unified UI is installed and its API is reachable.</summary>
        public static bool IsAvailable
        {
            get { return Helpers() != null; }
        }

        /// <summary>Whether the button has actually been registered this session.</summary>
        public static bool Registered
        {
            get { return _button != null; }
        }

        private static Type Helpers()
        {
            if (_looked) return _helpers;
            _looked = true;
            try
            {
                Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < loaded.Length; i++)
                {
                    Type t = loaded[i].GetType(HelpersTypeName, false);
                    if (t != null) { _helpers = t; break; }
                }
            }
            catch (Exception e)
            {
                ModConfig.LogError("UnifiedUiButton: looking for UUI failed: " + e);
            }
            return _helpers;
        }

        /// <summary>
        /// Adds the launch button to Unified UI. onToggle is called with true when the player
        /// presses it and false when they release it. Returns false if UUI is not there or the
        /// call did not take, in which case the caller should fall back to its own button.
        /// </summary>
        public static bool Register(string name, string tooltip, Texture2D icon,
            Action<bool> onToggle)
        {
            Type helpers = Helpers();
            if (helpers == null || icon == null || onToggle == null) return false;

            try
            {
                Type hotKeys = helpers.Assembly.GetType(HotKeysTypeName, false);
                if (hotKeys == null) return false;

                // The exact overload, by its parameter types: UUI offers several and picking by
                // name alone would be ambiguous.
                MethodInfo register = helpers.GetMethod("RegisterCustomButton",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new Type[]
                    {
                        typeof(string), typeof(string), typeof(string), typeof(Texture2D),
                        typeof(Action<bool>), typeof(Action<ToolBase>), hotKeys
                    },
                    null);
                if (register == null)
                {
                    ModConfig.LogError("UnifiedUiButton: UUI is present but RegisterCustomButton "
                        + "does not have the signature this was built against - not registering");
                    return false;
                }

                _button = register.Invoke(null, new object[]
                {
                    name, GroupName, tooltip, icon, onToggle, null, null
                });
                if (_button == null) return false;

                Type buttonType = _button.GetType();
                _isPressed = buttonType.GetProperty("IsPressed");
                _release = buttonType.GetMethod("Release", Type.EmptyTypes);
                ModConfig.LogAlways("UnifiedUiButton: registered with Unified UI");
                return true;
            }
            catch (Exception e)
            {
                ModConfig.LogError("UnifiedUiButton.Register error: " + e);
                _button = null;
                return false;
            }
        }

        /// <summary>
        /// Pushes the button in or lets it out. Needed because the launch panel can be closed by
        /// other means - its own close box, or another tool taking over - and a button left lit
        /// after its panel has gone is worse than no button.
        /// </summary>
        public static void SetPressed(bool pressed)
        {
            if (_button == null || _isPressed == null) return;
            try
            {
                object current = _isPressed.GetValue(_button, null);
                if (current is bool && (bool)current == pressed) return;
                _isPressed.SetValue(_button, pressed, null);
            }
            catch (Exception e)
            {
                ModConfig.LogError("UnifiedUiButton.SetPressed error: " + e);
            }
        }

        /// <summary>Removes the button. Called on level unload, so it does not survive into the next city.</summary>
        public static void Release()
        {
            try
            {
                if (_button != null && _release != null) _release.Invoke(_button, null);
            }
            catch (Exception e)
            {
                ModConfig.LogError("UnifiedUiButton.Release error: " + e);
            }
            finally
            {
                _button = null;
                _isPressed = null;
                _release = null;
            }
        }
    }
}
