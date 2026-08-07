using ColossalFramework.UI;
using MissileDisaster.Core;
using UnityEngine;

namespace MissileDisaster.Game.UI
{
    /// <summary>
    /// The permanent panel - a direct child of UIView - for choosing the warhead, the nuclear
    /// yield in kilotons and the burst height. The choices are written to MissileTool's static
    /// fields, and starting the aiming opens MissileTool, after which a click on the map
    /// launches. The yield can be picked from a catalogue of ten weapons or typed in directly.
    /// As with AlienInvasion.InvasionUI, it is created on level load and destroyed on unload, so
    /// no static state is left behind.
    /// UIComponents must be created on the main thread.
    /// </summary>
    public static class MissilePanel
    {
        private const string PanelName = "MissileDisasterControlPanel";
        private const string SpriteNormal = "ButtonMenu";
        private const string SpriteSelected = "ButtonMenuFocused";

        private static readonly WarheadType[] Warheads =
        {
            WarheadType.Conventional, WarheadType.Cluster, WarheadType.WhitePhosphorus,
            WarheadType.Thermobaric, WarheadType.Nuclear,
        };
        private static readonly string[] WarheadLabels =
            { "Conventional", "Cluster", "White Phosphorus", "Thermobaric", "Nuclear" };

        private static readonly BurstType[] Bursts = { BurstType.Airburst, BurstType.Groundburst };
        private static readonly string[] BurstLabels = { "Air Burst", "Ground Burst" };

        private static UIPanel _panel;
        private static UIButton[] _warheadButtons;
        private static UIButton[] _burstButtons;
        private static UITextField _ktField;
        private static UITextField _kgField;

        /// <summary>Creates the panel. Called from the main thread on level load.</summary>
        public static void Create()
        {
            try
            {
                UIView view = UIView.GetAView();
                if (view == null) { ModConfig.LogError("MissilePanel.Create: UIView is null"); return; }
                if (view.FindUIComponent<UIPanel>(PanelName) != null) return; // guard against creating it twice

                _panel = view.AddUIComponent(typeof(UIPanel)) as UIPanel;
                if (_panel == null) { ModConfig.LogError("MissilePanel.Create: failed to create the UIPanel"); return; }
                _panel.name = PanelName;
                _panel.backgroundSprite = "MenuPanel2";
                _panel.width = ModConfig.PanelWidth;
                _panel.relativePosition = new Vector3(ModConfig.PanelPosX, ModConfig.PanelPosY);

                float y = BuildContents();
                _panel.height = y + 8f;

                RefreshHighlight();
                _panel.Hide(); // hidden by default; the button in the disasters tab opens it
                ModConfig.Log("created MissilePanel");
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("MissilePanel.Create error: " + e);
            }
        }

        /// <summary>Safe to call every frame; creates the panel if it does not exist yet, which retries past a UIView that was not ready.</summary>
        public static void EnsureCreated()
        {
            if (_panel == null) Create();
        }

        /// <summary>Moves the panel back to its default on-screen position and shows it, to recover one dragged off screen or otherwise lost.</summary>
        public static void ResetPosition()
        {
            EnsureCreated();
            if (_panel != null)
            {
                _panel.relativePosition = new Vector3(ModConfig.PanelPosX, ModConfig.PanelPosY);
                Show();
            }
        }

        /// <summary>Called from the button in the disasters tab: hides the panel if it is showing and shows it if it is not.</summary>
        public static void Toggle()
        {
            EnsureCreated();
            if (_panel == null) return;
            if (_panel.isVisible) Hide(); else Show();
        }

        /// <summary>Shows the panel and brings it to the front.</summary>
        public static void Show()
        {
            EnsureCreated();
            if (_panel != null) { _panel.Show(); _panel.BringToFront(); }
        }

        /// <summary>Called from the missile icon in the disasters tab: shows the panel and opens the aiming tool straight away.</summary>
        public static void ShowAndStartTargeting()
        {
            Show();
            StartAiming(); // ToolsModifierControl.SetTool<MissileTool>()
        }

        /// <summary>Hides the panel; the button in the disasters tab opens it again.</summary>
        public static void Hide()
        {
            if (_panel != null) _panel.Hide();
        }

        /// <summary>Called on level unload. Destroys the panel and drops the references, leaving no static state behind.</summary>
        public static void Destroy()
        {
            try
            {
                if (_panel != null)
                {
                    Object.Destroy(_panel.gameObject);
                    _panel = null;
                }
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("MissilePanel.Destroy error: " + e);
            }
            finally
            {
                _warheadButtons = null;
                _burstButtons = null;
                _ktField = null;
                _kgField = null;
            }
        }

        private static float BuildContents()
        {
            float pad = 8f;
            float w = ModConfig.PanelWidth - pad * 2f;
            float y = pad;

            // The title, which doubles as the drag handle
            UILabel title = _panel.AddUIComponent<UILabel>();
            title.text = "Missile Launch Control";
            title.textScale = 0.9f;
            title.relativePosition = new Vector3(pad, y);
            var drag = _panel.AddUIComponent<UIDragHandle>();
            drag.target = _panel;
            drag.width = ModConfig.PanelWidth;
            drag.height = 24f;
            drag.relativePosition = new Vector3(0f, 0f);

            // The close button, top right. It is added after the drag handle, so it sits in
            // front and receives the click. Closing is not final: the button in the disasters
            // tab opens the panel again.
            UIButton closeBtn = MakeButton("✕", ModConfig.PanelWidth - 26f, 3f, 22f);
            closeBtn.textScale = 0.9f;
            closeBtn.tooltip = "Close (reopen from the Missile button in the Disasters panel)";
            closeBtn.eventClick += (c, p) => Hide();
            y += 26f;

            // Warhead selection
            y = AddSectionLabel("Warhead", pad, y);
            _warheadButtons = new UIButton[Warheads.Length];
            for (int i = 0; i < Warheads.Length; i++)
            {
                WarheadType type = Warheads[i]; // bound to a local for the closure
                UIButton b = MakeButton(WarheadLabels[i], pad, y, w);
                b.eventClick += (c, p) => { MissileTool.CurrentWarhead = type; RefreshHighlight(); };
                _warheadButtons[i] = b;
                y += ModConfig.PanelButtonHeight + ModConfig.PanelButtonGap;
            }

            // Nuclear yield: pick from the catalogue or type the kilotons in
            y += 4f;
            y = AddSectionLabel("Nuclear Yield (nuclear only, kt)", pad, y);

            UIDropDown dd = MakeWeaponDropdown(pad, y, w);
            y += ModConfig.PanelButtonHeight + ModConfig.PanelButtonGap;

            _ktField = MakeKtField(pad, y, w);
            y += 26f + ModConfig.PanelButtonGap;

            // Conventional yield, typed in as kilograms of TNT and applied to non-nuclear warheads
            y += 4f;
            y = AddSectionLabel("Conventional Yield (non-nuclear, kg TNT)", pad, y);
            _kgField = MakeKgField(pad, y, w);
            y += 26f + ModConfig.PanelButtonGap;

            // Burst height: air or ground
            y += 4f;
            y = AddSectionLabel("Burst Height", pad, y);
            _burstButtons = new UIButton[Bursts.Length];
            float halfW = (w - ModConfig.PanelButtonGap) * 0.5f;
            for (int i = 0; i < Bursts.Length; i++)
            {
                BurstType bt = Bursts[i];
                float bx = pad + i * (halfW + ModConfig.PanelButtonGap);
                UIButton b = MakeButton(BurstLabels[i], bx, y, halfW);
                b.eventClick += (c, p) => { MissileTool.CurrentBurst = bt; RefreshHighlight(); };
                _burstButtons[i] = b;
            }
            y += ModConfig.PanelButtonHeight + ModConfig.PanelButtonGap;

            // Start aiming, which opens the tool
            y += 6f;
            UIButton launch = MakeButton("Start Targeting (click to launch)", pad, y, w);
            launch.color = new Color32(255, 190, 120, 255);
            launch.eventClick += (c, p) => StartAiming();
            y += ModConfig.PanelButtonHeight + ModConfig.PanelButtonGap;

            return y;
        }

        private static UIDropDown MakeWeaponDropdown(float x, float y, float width)
        {
            UIDropDown dd = _panel.AddUIComponent<UIDropDown>();
            dd.size = new Vector2(width, ModConfig.PanelButtonHeight);
            dd.relativePosition = new Vector3(x, y);
            dd.normalBgSprite = "ButtonMenu";
            dd.hoveredBgSprite = "ButtonMenuHovered";
            dd.disabledBgSprite = "ButtonMenuDisabled";
            dd.listBackground = "GenericPanelLight";
            dd.itemHeight = 22;
            dd.itemHover = "ListItemHover";
            dd.itemHighlight = "ListItemHighlight";
            dd.listWidth = (int)width;
            dd.listHeight = 220;
            dd.listPosition = UIDropDown.PopupListPosition.Below;
            dd.textScale = 0.75f;
            dd.textFieldPadding = new RectOffset(8, 8, 6, 0);
            dd.itemPadding = new RectOffset(8, 0, 3, 0);
            dd.popupColor = new Color32(45, 52, 61, 255);
            dd.popupTextColor = new Color32(230, 230, 230, 255);
            dd.foregroundSpriteMode = UIForegroundSpriteMode.Stretch;
            dd.verticalAlignment = UIVerticalAlignment.Middle;
            dd.horizontalAlignment = UIHorizontalAlignment.Left;

            NuclearWeapon[] catalog = NuclearWeapons.Catalog;
            var items = new string[catalog.Length];
            for (int i = 0; i < catalog.Length; i++)
            {
                items[i] = catalog[i].Name + " (" + catalog[i].Kilotons + "kt)";
            }
            dd.items = items;
            dd.selectedIndex = -1; // nothing selected by default; the typed-in yield's default applies

            // The trigger button, the arrow at the right-hand end
            UIButton trigger = dd.AddUIComponent<UIButton>();
            trigger.text = "▼";
            trigger.textScale = 0.7f;
            trigger.size = new Vector2(24f, ModConfig.PanelButtonHeight);
            trigger.relativePosition = new Vector3(width - 24f, 0f);
            trigger.normalBgSprite = "ButtonMenu";
            trigger.hoveredBgSprite = "ButtonMenuHovered";
            trigger.pressedBgSprite = "ButtonMenuPressed";
            dd.triggerButton = trigger;

            dd.eventSelectedIndexChanged += (c, index) =>
            {
                if (index < 0 || index >= catalog.Length) return;
                MissileTool.CurrentYieldKilotons = catalog[index].Kilotons;
                if (_ktField != null) _ktField.text = catalog[index].Kilotons.ToString();
            };
            return dd;
        }

        private static UITextField MakeNumericField(float x, float y, float width, int initial, string tooltip)
        {
            UITextField tf = _panel.AddUIComponent<UITextField>();
            tf.size = new Vector2(width, 26f);
            tf.relativePosition = new Vector3(x, y);
            tf.builtinKeyNavigation = true;
            tf.readOnly = false;
            tf.numericalOnly = true;
            tf.allowFloats = false;
            tf.maxLength = 8;
            tf.selectionSprite = "EmptySprite";
            tf.normalBgSprite = "TextFieldPanel";
            tf.hoveredBgSprite = "TextFieldPanelHovered";
            tf.focusedBgSprite = "TextFieldPanel";
            tf.textScale = 0.85f;
            tf.textColor = Color.white;
            tf.padding = new RectOffset(8, 8, 6, 6);
            tf.horizontalAlignment = UIHorizontalAlignment.Left;
            tf.verticalAlignment = UIVerticalAlignment.Middle;
            tf.text = initial.ToString();
            tf.tooltip = tooltip;
            return tf;
        }

        private static UITextField MakeKtField(float x, float y, float width)
        {
            UITextField tf = MakeNumericField(x, y, width, MissileTool.CurrentYieldKilotons, "Enter yield in kt (press Enter)");
            tf.eventTextSubmitted += (c, s) => ApplyKtText(s);
            tf.eventLostFocus += (c, p) => { if (_ktField != null) ApplyKtText(_ktField.text); };
            return tf;
        }

        private static UITextField MakeKgField(float x, float y, float width)
        {
            UITextField tf = MakeNumericField(x, y, width, MissileTool.CurrentConventionalKilograms, "Enter charge in kg TNT (press Enter)");
            tf.eventTextSubmitted += (c, s) => ApplyKgText(s);
            tf.eventLostFocus += (c, p) => { if (_kgField != null) ApplyKgText(_kgField.text); };
            return tf;
        }

        private static void ApplyKtText(string s)
        {
            int kt;
            if (int.TryParse(s, out kt) && kt > 0)
            {
                MissileTool.CurrentYieldKilotons = kt;
            }
            else if (_ktField != null)
            {
                _ktField.text = MissileTool.CurrentYieldKilotons.ToString(); // invalid input reverts to the current value
            }
        }

        private static void ApplyKgText(string s)
        {
            int kg;
            if (int.TryParse(s, out kg) && kg > 0)
            {
                MissileTool.CurrentConventionalKilograms = kg;
            }
            else if (_kgField != null)
            {
                _kgField.text = MissileTool.CurrentConventionalKilograms.ToString(); // invalid input reverts to the current value
            }
        }

        private static float AddSectionLabel(string text, float x, float y)
        {
            UILabel label = _panel.AddUIComponent<UILabel>();
            label.text = text;
            label.textScale = 0.75f;
            label.textColor = new Color32(200, 200, 200, 255);
            label.relativePosition = new Vector3(x, y);
            return y + 18f;
        }

        private static UIButton MakeButton(string text, float x, float y, float width)
        {
            UIButton b = _panel.AddUIComponent<UIButton>();
            b.text = text;
            b.textScale = 0.8f;
            b.size = new Vector2(width, ModConfig.PanelButtonHeight);
            b.relativePosition = new Vector3(x, y);
            b.normalBgSprite = SpriteNormal;
            b.hoveredBgSprite = "ButtonMenuHovered";
            b.pressedBgSprite = "ButtonMenuPressed";
            return b;
        }

        /// <summary>Highlights the selected warhead and burst height buttons.</summary>
        private static void RefreshHighlight()
        {
            if (_warheadButtons != null)
            {
                for (int i = 0; i < _warheadButtons.Length; i++)
                {
                    if (_warheadButtons[i] == null) continue;
                    _warheadButtons[i].normalBgSprite =
                        Warheads[i] == MissileTool.CurrentWarhead ? SpriteSelected : SpriteNormal;
                }
            }
            if (_burstButtons != null)
            {
                for (int i = 0; i < _burstButtons.Length; i++)
                {
                    if (_burstButtons[i] == null) continue;
                    _burstButtons[i].normalBgSprite =
                        Bursts[i] == MissileTool.CurrentBurst ? SpriteSelected : SpriteNormal;
                }
            }
        }

        private static void StartAiming()
        {
            try
            {
                ToolsModifierControl.SetTool<MissileTool>();
            }
            catch (System.Exception e)
            {
                ModConfig.LogError("MissilePanel.StartAiming error: " + e);
            }
        }
    }
}
